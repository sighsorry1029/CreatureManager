# Blocker query optimization — 2026-09-28

## Scope and baseline

- Branch: `main`; baseline `c7e893c28786b0e814e53e8cab0908e4874f6554`, version 1.2.0.
- Existing `README.md` edits are unrelated and preserved. No version, config,
  resource, dependency, Release package, or publication change.
- References: BossRules `ee1252aa384b7f172e8cfa264b4b4adc9b60a15c` and
  DropNSpawn `f44468b26207b3b615ed16d81f28e9a2eb87d943`, read only. Their target
  registries and short-lived shared queries informed the review; no dependency
  on either mod was added.
- Reviewed paths: nearby spawn blocking, server boss discovery, and pending
  dungeon Enforcer cancellation. This is not a new whole-project audit.

## Changes

### Karma blocker scratch buffers

`GetEnforcerBlockerState` now rents an empty observed-ID set and an ID list.
`CountTrackedBlockerZdos` copies each tracked set into the same list instead of
allocating two `ToList()` snapshots per check. Snapshot enumeration still permits
stale-ID removal and boss-to-Enforcer reclassification. Buffers are cleared in
`finally` and returned to a pool; nested calls rent separate storage. Runtime
reset clears the pool. This affects repeated delay checks and other callers of
the existing blocker query without caching their decisions or reducing checks.

The added state is two reusable collections per concurrent synchronous query,
with retained capacity up to prior use. No new runtime file, API, or interface.
The first acquisition/capacity growth still allocates; this is not a claim that
the entire query or maintenance loop is allocation-free.

### Nearby spawn query storage and prefab lookup reuse

`BeginQuery` / `EndQuery` reuse the existing query object and result dictionary.
End/finalizer clears decisions, the previous-scope reference, event-spawn policy,
and group-rejection flag before reuse. No query result is shared across separate
list/group attempts. Existing spawn-triggered invalidation is unchanged.

Within one `FindBlocker` ZDO search, the boss classification of each prefab hash
is looked up once. The scratch dictionary is cleared in the existing `finally`,
including failed queries. Missing prefabs are cached only during that search;
later registration and in-place metadata changes are observed by the next search.
Loaded instance state still takes precedence over prefab metadata, and Enforcers
retain their separate category. The existing nearby ZDO enumeration and loaded
Character scan remain intact. Reusable empty collections retain capacity, not
world objects or prior decisions.

## Deliberately unchanged

- No persistent boss/Enforcer presence index or timer-based negative cache.
  BossRules' loaded same-boss registry does not cover CM's received-but-unloaded
  ZDO policy, clients owning spawns, or Karma-disabled operation.
- Server bootstrap remains one iterative prefab-search slice per maintenance
  tick, with regional fallback while discovery is incomplete. BossRules instead
  exhausts its iterative searches in a loop; copying that would increase burst
  risk. DropNSpawn's work budget cannot interrupt one native search invocation.
  A different bootstrap algorithm needs actual large-world measurements first.
- Pending reservations still check every 0.5-second maintenance tick, and actual
  spawn execution remains sequential. Boss/player/cap/cooldown/Karma cancellation,
  fixed positions, no charge before successful spawn, and duplicate suppression
  are preserved. No batch-wide snapshot of active blockers was introduced.
- Harmony targets/order/state/finalizers, event-spawn exclusions, live config,
  owner checks, RPCs, save data, loot, and optional-mod policies are unchanged.

## Verification

- Baseline and modified Debug solution builds with `DeployToGame=true`: zero
  warnings/errors, final ILRepack followed by the plugin-only local copy.
- Baseline YAML DTO/parser checks passed; no YAML or config changes were made.
- The initial compatibility-tool build failed because the installed BepInEx core
  folder was missing. The baseline was checked using the existing 5.4.2351 package
  cache via a temporary reference override. The user restored BepInEx; all later
  builds/checks use the installed core, without a tracked build-setting change.
- Original Valheim 1.0.16 client build 25527674 and dedicated server build 25527701
  isolated Mono checks passed: 7,635 game/Unity IL references, 104 Harmony targets,
  seven transpiler applications, and managed contracts. Original game assemblies
  and existing extraction data were reused; compile-time publicizer arrangement
  was not changed. No new non-public game access was introduced.
- Existing boss-discovery and Enforcer-delay contracts passed. Added checks cover
  nested/failed Karma queries, fresh state after removed ZDOs, recycled event/group
  scopes, prefab changes/late registration, and exception cleanup after partial
  classification. The fixture executes compiled production bodies with native
  scene/transport boundaries substituted.
- A fixture with 1,000 unloaded ordinary ZDOs sharing a prefab required one prefab
  classification per search. This measures lookup count in the fixture, not game
  frame time or a percentage FPS improvement.
- DropNSpawn group transpiler composition and Drop That 3.1.6 contracts passed.
- Final merged Debug DLL and installed plugin SHA-256:
  `68AB7E1E33D505AB98D366383F15A0CEF184E5CA68836CB4EF7C1688A63902C0`.

Actual Unity/gameplay, host/dedicated multiplayer, ownership handoff, and in-game
CPU/GC profiling were not run. Remaining checks should cover dense spawning with
and without blockers, saved/unloaded bosses on reconnect, events continuing to
spawn during blocking, and simultaneous pending Enforcers canceled by a newly
arriving boss/player departure. Allocation and lookup reductions are expected to
reduce work; no measured gameplay speedup is claimed.
