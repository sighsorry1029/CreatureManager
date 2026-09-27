# Server boss discovery

Baseline: `42eb3909965c6f952bf1eadcf72c79bc2ef0784a` (CreatureManager 1.1.17).
Reference: BossRules `e3d6d38563dcfd90353a568320f47f110d1253ca`, specifically
its queued ZDO observations and saved-ZDO bootstrap, not its despawn/refund policy.

## Problem and scope

Boss blocking previously depended on loaded Characters or previously reported ZDO
IDs. A one-way owner observation could precede server ZDO receipt or be missed
during ownership changes. New On-mode bosses have a separate level-RPC recovery
path; saved, already processed bosses and Vanilla mode do not guarantee that path.
The community Bonemass report was not reproduced in a running game.

## Implementation

- Observe the exact private `ZDOMan.CreateNewZDO(ZDOID, Vector3, int)` overload
  through Harmony `AccessTools`. The original 1.0.16 client/server receive path
  invokes it before `Deserialize`, so queue IDs instead of deciding from prefab 0.
- Resolve queued observations on server maintenance and before blocker decisions.
  Unresolved observations expire after five seconds. This does not make ZDOs that
  have not reached the server available earlier.
- Recover stored bosses with one iterative prefab-search slice per maintenance
  tick, including registered clone bosses. This is a call-count budget, not a
  measured millisecond guarantee.
- During incomplete bootstrap, a negative blocker decision checks the whole
  applicable region through the original public `FindSectorObjects` API. Merged
  player regions retain their complete sector union. Buffers and existing ID sets
  are reused; there is no full-world search on every kill.
- Invalidate discovery on game-data/template refresh, scene/prefab-count changes,
  and feature reactivation. Clear pending state at session teardown. Loaded
  Characters retain their instance classification; unloaded ZDOs use the current
  registered prefab classification. Character unload is not treated as death.
- Keep existing owner validation, RPC names, settings/defaults, region/realm rules,
  Enforcer classification and the excluded-dead-boss exception. No owner takeover,
  new dependency, save migration, level reroll, or balance changes.

Changes to boss-summoned adds, existing Karma level bonuses, Blamer, modifiers and
telegraphed Enforcer encounters are outside this patch.

## Verification

- Baseline and changed Debug plugin builds, including final ILRepack and automatic
  local plugin deployment: zero warnings/errors. Installed DLL SHA-256 compared
  with the final merged build.
- YAML DTO/parser checks passed.
- Original Valheim 1.0.16 client build 25527674 and dedicated-server build 25527701:
  final assembly reference checks, 99 Harmony targets and six transpiler applications
  passed in isolated game Mono. The existing compile-time publicizer arrangement
  was retained; analysis and isolated runtime checks use original assemblies.
- `BossBlockerContracts` executes copied compiled production bodies. Only native
  scene/transport/clock/query boundaries and state storage are substituted; ZDO
  data, discovery flow, region tests and blocking decisions are exercised. Cases
  cover saved/unloaded bosses, On/Vanilla/Off level modes, KarmaLevelOnly, delayed
  prefab data, duplicate observations, remote owner, bootstrap slice budget,
  clone/template invalidation, disable/enable, region unions, dungeon separation,
  last/second boss, dead/missing ZDOs and Enforcer classification/deduplication.

These checks do not install native Harmony detours, start Unity, or run a network
session. Actual client/host/dedicated testing remains necessary for Bonemass adds,
save/restart, ownership transfer, zone unload/reload, late prefab registration and
network timing. Profile dense ZDO reception, bootstrap and mass kills before
claiming any performance improvement. Arbitrary external in-place prefab changes
that do not notify CM or change registration counts are not continuously polled.
