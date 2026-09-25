# FrozenKing phase-two encounter protection

This change prevents CreatureManager from altering new `FrozenKing_p2` encounters.
It does not reset existing levels/health, rewrite stored modifier data, reconstruct
Aspect kills, or introduce a migration or save-format version. Previously stalled
encounters are not repaired.

## Evidence and scope

The inspected game is Windows x64 Valheim **1.0.15**, client build **25390630**.
Original files were read from
`C:\Users\blizz\.codex\references\valheim\snapshots\client-b25390630-windows-x64-20260918T131715Z`.
The installed original `assembly_valheim.dll` matches the snapshot SHA-256:
`59F53FB55D99D22A33E8ED094EEC8D21E9F133543BCE92BC3D80DCE44033ADB1`.
The existing ILSpy `derived/ilspy-9.1.0.7988-r1/assemblies/assembly_valheim/csharp`
extraction was used for Character, HitData, Aoe, and effect creation.

Targeted UnityPy 1.25.3 reads of the original SoftRef bundles confirmed:

| Resource | Relevant contract |
| --- | --- |
| `FrozenKing_p2` Humanoid | Base health 7000; NonPlayer resistance Normal; ordinary combat damage immune/ignored. |
| Seven `Aspect_*` prefabs | Death effects include `aspect_aoe_explosion`. |
| `aspect_aoe_explosion` Aoe | NonPlayer damage 1000; all other damage and per-level damage zero; distance scaling disabled. |
| `FrozenKing_p2` death effects | Spawn `FrozenKing_p3` through the game's effect path. |

Bundle inputs under `original/valheim_Data/StreamingAssets/SoftRef/Bundles`:

- `86c3d76e` (script names), SHA-256 `88ce37837ab56ec10ff26358e7df2d4057e87326f3db6e59f2a263cc2c9325d5`.
- `c4210710` (prefabs), SHA-256 `a41629783ab3fbabb7da4ddd0ee91c0ee4936b6cdd0b378dcf33642e1482a967`.

The targeted reads verified these hashes against the preserved manifest first.
No original DLL/bundle was modified or publicized for analysis. This is not a
whole-game resource review or a change to the mod's supported-version range.

`HitData.DamageTypes.Modify` multiplies NonPlayer damage, unlike the ordinary
`HitData.ApplyModifier` scaling path. CM's damage processing uses the former for
level bonuses and Armored/Reaping/Enraged. Merely preventing additional stars
would leave other ways to interrupt the seven-hit mechanic.

## Implementation boundaries

- `CreatureLevelManager.IsFrozenKingPhaseTwo` identifies only the exact prefab.
  Runtime calls compare the ZDO prefab hash; prefab editing/pre-Awake falls back
  to the existing clone-name normalization. The original protected `m_nview`
  field is read through a cached Harmony `FieldRef`. No Unity object cache,
  repeated reflection lookup, per-frame name allocation, or new component is added.
- Initial level processing exits before sync waits, Karma queries, and HP writes.
  The existing spawn-policy representation blocks level/stat/scale rules and
  modifiers. SetLevel adoption/health-restoration hooks leave the phase alone.
- `CreatureDomainManager.ApplyHealthTuple` also skips direct YAML health/regen
  edits, which otherwise happen before instance-level rules.
- Modifier inheritance/restore reject this target. Runtime Reaping stat restore,
  active modifier eligibility (including ZDO-only server checks), and incoming
  damage processing preserve the encounter. Deathward uses the existing common
  eligibility check. `cm:spawn` permits only level 1 with no modifiers and retains
  the existing owner checks.
- Harmony targets, priorities, `__state`, exception cleanup, game RPC ownership,
  death/phase-three spawning, settings, and snapshot version 2 are unchanged.
  Phase 1, phase 3, all seven Aspects, and differently named custom clones keep
  their existing policies. Other mods/direct game commands remain outside this guard.

## Implementation verification (before the version bump)

- Baseline and final `dotnet build CreatureManager.sln -c Debug -p:DeployToGame=true`
  succeeded with no warnings/errors. Final merged DLL was deployed to the normal
  game `BepInEx/plugins` directory; source/destination SHA-256 matched:
  `62BBBC511DC4901D64931AED9DB4204FC1B327E011F5A041AFC4224EC4D25FAA`.
- YAML DTO/parser checks passed. The final GameCompatibilityCheck passed against
  both original client 1.0.15 and dedicated server 1.0.15 build **25390671**:
  7463 game/Unity IL references, 98 Harmony targets, and 6 transpiler applications.
  Server inputs are the preserved
  `dedicated-server-b25390671-windows-x64-20260918T185703Z-depot-restored/original`.
- `FrozenKingContracts` executes copied production method bodies in the game's
  Mono with only Unity/world boundaries substituted. It checks exact identity,
  pre-Awake fallback, all spawn-source policies, direct health overrides,
  inheritance/snapshot rejection, passive/Reaping application, unchanged damage
  for seven hits including Deathward, and command owner checks. Stored state
  remains untouched. These are managed fixtures, not an actual fight simulation.
- Existing loot, saved-level, localization, snapshot, and authority checks passed.
  The optional external Drop That binary check was omitted because its previously
  supplied `adminclient` profile path no longer exists; its initial attempted run
  failed at loading that optional file, before this patch's changes.

## Release 1.1.16 verification

The subsequent release step raised the plugin/assembly/manifest version to 1.1.16
and the package's BepInExPack dependency to 5.4.2351. Debug deployment and the
normal Release build/package target passed with no warnings/errors. The Release
DLL passed the same client/server managed compatibility checks and the Release
YAML parser checks passed. ZIP validation confirmed exactly the six expected
files, intact CRCs, matching source/DLL bytes, and the correct version/dependency.

- Deployed Debug DLL SHA-256: `D835CA2BA874838307E9B3A3C539619CAACFC4C601767E28FC60FE7FAB077FF7` (source and game copy match).
- `bin/Release/CreatureManager.dll` SHA-256: `374A7D9524593C14DBA60003D144C7A5E90ABEDEC7D31290D1FCC034D7BAEE8A`.
- `Thunderstore/CreatureManager_v1.1.16.zip` SHA-256: `1D1FFF7010E4043867FFDCF209389963D37867C1C6899290E2A26290DCAA657A`.

## Remaining in-game verification

Still required in the game, after restarting with the updated DLL on all participants:

1. Start a new fight with aggressive Boss/prefab level/HP rules, Karma, and guaranteed
   Regenerating/Reaping/Armored/Deathward settings. Confirm only phase two stays at
   level 1 and the prefab's original health, without these modifiers.
2. Kill all seven Aspects; verify seven expected HP reductions, no intervening CM
   healing, and one normal transition into phase three.
3. Repeat on a listen host and a dedicated server, including an ownership handoff
   and save/reconnect during a newly started phase two. Verify remaining health is
   preserved and phase three is neither missed nor duplicated.

No Unity scene, native Harmony detour installation, actual boss fight, multiplayer
session, or live ownership transfer was executed by the managed checks.
