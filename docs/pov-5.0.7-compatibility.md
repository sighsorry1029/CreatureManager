# CreatureManager / Path of Valheiman 5.0.7 compatibility

Released in CreatureManager **1.3.6**. Implemented and checked on 2026-10-18 against source based on
`fd734455889e2f7e060ff11707681a01e1bd5d49` and Valheim 1.0.20.
This is a focused interoperability fix, not a diagnosis of the reported infinite
boss-health problem. The reporter's complete mod list remains unknown.

## Verified input and boundaries

- PoV BepInPlugin: `treextr.pathofvalheiman`, **5.0.7**. Its assembly version is
  `1.0.0.0` and is not the release identifier.
- Supplied `PathOfValheiman.dll` SHA-256:
  `AD0AD431504CCE541474B7DD0546EE6D91485F8CABEFB2D0C932D355A5E4F887`.
- Original Valheim Windows x64 client build 25909357 and dedicated-server build
  25909431. Original game and PoV DLLs were analyzed without publicizing them.
  CM's existing build still uses publicized compile-time copies in `obj` and
  its existing final-DLL access attributes. That build setup was not changed.
- PoV's private pending-health state and patch entry are validated once after
  plugin loading. The bridge is restricted to plugin version 5.0.7. Other
  versions keep their existing behavior and produce a compatibility warning.
  A future PoV version requires rechecking these private contracts.

## Changes

| Path | Problem | Implemented behavior |
| --- | --- | --- |
| Reaping gain and stored-HP restoration | CM stores a base maximum that already includes PoV difficulty, then sends base + bonus through PoV's raw `SetMaxHealth` multiplier again. | Use PoV's existing `EnemyDifficulty.SetScaledMaxHealth` for these two final-maximum writes. CM caps, kill eligibility, owner checks and stored keys are unchanged. |
| Explicit current-HP restoration | PoV retains a per-character, same-frame raw-to-scaled conversion after setting maximum HP. CM's absolute final HP could be converted a second time, particularly when heavily injured. | Remove only that character's pending conversion before CM's final writes: managed-level deficit restoration, stored deficit restoration, ordinary stat reapplication, Reaping healing, Deathward recovery and rollback. |
| Ordinary CM health multiplier | CM must still supply raw maximum HP for PoV to scale once, but then must restore missing HP against the resulting maximum. | Keep the ordinary `SetMaxHealth` path; with the bridge active, read the resulting maximum before restoring CM's absolute missing-HP amount. Without PoV, retain the existing calculation. |
| PoV level-provider detection | `CreatureStars.ForeignLevels` caches the first CM setting and can become stale after server config synchronization or a live change. | Read CM's current mode: `Full` owns level assignment; `ExceptLevel` and `Off` do not. Preserve PoV's recognition of other level providers, evaluated once after plugin loading. No plugin scan or reflection on each combat call. |
| Vanilla-table star loot | PoV temporarily clears `m_levelMultiplier`, then applies its own count factor. This prevents CM's per-entry calculation from running and can override DNS's chosen policy. | PoV's star-loot Prefix initializes `__state` to null and yields when CM CalculateChance is active or DNS is loaded. Thus its Postfix/Finalizer have nothing to multiply or restore; row flags remain available to the selected loot system. |

The drop rule follows the current settings at generation time:

| Configuration | PoV's vanilla-table star replacement |
| --- | --- |
| DropNSpawn loaded, either CM loot mode | Yields to DNS. CM already yields to DNS too. |
| No DNS, CM `CalculateChance` | Yields to CM; CM's existing per-row exclusions still apply. |
| No DNS, CM `Vanilla` | Retains PoV's original policy. In particular, PoV itself yields when another mod owns levels, including CM `Full`. |

This does not alter PoV's separate rarity bonus / extra-drop code or synthesize
additional items. No config keys, defaults, YAML schema, ZDO/RPC identifiers or
dependency requirements changed. The release increments CM to 1.3.6; PoV is not
modified on disk. No stored-data migration is added.
Existing corrupt or infinite HP is not globally repaired.

`CreaturePovCompatibility` owns the optional reflection delegates and its own
Harmony ID. Existing `FejdStartup.Awake` / `ZNetScene.Awake` hooks initialize it
after plugins load, including on dedicated servers. CM shutdown removes its
optional patches and releases delegates. The existing loot wrapper is rebuilt
once after installation/removal, preserving registered patches and priorities,
so a previously compiled caller cannot retain an old inlined Prefix. Failed
initialization is rolled back; rollback failure is reported separately without
propagating through game startup.

## Validation

- Baseline Debug build and original-game compatibility checks passed before edits.
- Final Debug build, DLL merge and configured local plugin deployment passed
  with zero compiler errors/warnings.
- Existing compatibility contracts and default-YAML comparisons passed.
- Final runs: client standalone, client with PoV and dedicated-server with PoV
  each completed with **0 failures**. The YAML sync/parser checker also passed.
  The final Debug DLL and the configured local `BepInEx/plugins/CreatureManager.dll`
  have identical SHA-256:
  `19FAE2AC2C1FB6BD81A239BB3847962A9C0E6CDF72BDE4CCF6F1B0698524BD7F`.
- The 1.3.6 Release build and the same three managed-check configurations also
  passed. All 17 ZIP files matched their source hashes; the assembly and manifest
  versions matched 1.3.6, with the existing BepInEx 5.4.2351 dependency. Release DLL
  SHA-256: `E6C477C027914F3E4313DBEE89617DB69CA79754B0EF592AEC04BAF2CAAE40FF`.
  ZIP SHA-256: `B63D7AA847352A85EE66107F3BDAD3B9168CD538F5A0C2F3F7D3EFC9A4D4935F`.
- Optional PoV tests use the supplied original DLL and actual HarmonyX 2.9
  managed detours in the game's Mono runtime. They warm the original caller
  before late bridge installation, then check live mode changes, CM/DNS/PoV
  loot precedence, unchanged flags after exceptions, unchanged patch owners /
  priorities, and teardown restoration.
- Health tests reproduce an already-scaled 440 maximum becoming 1760 under a
  PoV factor of 4 before the fix. Patched Reaping instead reaches 440 then its
  configured 480 cap, restores that maximum repeatedly, and heals only its
  configured amount. Managed level changes retain severe injuries; ordinary
  raw scaling still applies PoV once. Tests cover per-character pending-state
  isolation, Deathward rollback and exception-safe PoV suppression cleanup.
- Native Unity objects, geometry, FX, RNG and network ownership are fixture
  boundaries. Preparing/restoring managed detours can print missing Unity
  internal-call notices in this offline process; no Unity player is running.
  This is not an actual client, host or dedicated-server play test.

Reproduce the optional check after the normal Debug build:

```powershell
dotnet build tools/GameCompatibilityCheck/GameCompatibilityCheck.csproj -c Debug
$env:CREATUREMANAGER_POV_DLL = '<path to original PoV 5.0.7 DLL>'
$env:CREATUREMANAGER_DEFAULT_YAML_DIR = (Resolve-Path docs/defaults).Path
python tools/GameCompatibilityCheck/run_mono.py '<game directory>' `
    bin/Debug/CreatureManager.dll '<BepInEx core directory>'
```

Use the original client or server game directory for the corresponding reference
check. Omitting `CREATUREMANAGER_POV_DLL` runs the existing standalone suite.

## Remaining game verification and exclusions

In a disposable world, test a wounded Reaping creature and a boss with PoV
difficulty enabled, repeated kills through the Reaping cap, level changes,
save/reload and ownership transfer. Test Deathward after a same-frame health
update, including activation limits. Verify ordinary, trophy/chance-only and
ragdoll loot with CalculateChance, Vanilla and DNS, and compare live server
mode changes between Full and ExceptLevel. Install the matching CM build on
participating clients and server for these tests.

PoV `Warded` flesh/shield allocation, FrozenKing phase policy, combined modifier
balance and PoV-owned chance-only drops are outside this patch. With CM Vanilla
and no DNS, PoV 5.0.7 still applies its original drop policy, including its
existing lack of per-row handling for `m_levelMultiplierNotAmount`. Changing
that policy requires a separate row-aware fix; PoV currently groups amount
scaling by prefab. No conclusion is made about CLLC being installed in the
reported infinite-HP case, or about this patch resolving that report.
