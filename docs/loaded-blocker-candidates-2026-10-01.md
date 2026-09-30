# Loaded spawn-blocker candidates — 2026-10-01

## Scope and decision

- Baseline: clean `main`, `7cfce3a6c8c3345d3ad18de5d5c3a7b6f91b33ef`, CreatureManager 1.2.3.
- Reference only: BossRules 1.1.3, `95e32dabfc934edfc509607147519cd64d478e8b`,
  especially `BossRulesManager` and its Character lifecycle registration.
- Changed only nearby ordinary-spawn query acceleration and its lifecycle/tests.
  BossRules, server Karma discovery, pending Enforcer checks, configurations,
  resources, dependencies, loot, RPCs and saved state are unchanged.
- The original plan to replace the entire loaded-character scan was narrowed:
  a candidate list is not authoritative evidence that a blocker is absent.
  `Character.m_boss` is a public field; mods can change it after initialization.
  Enforcer flags can arrive later, Characters can lack a ZDO, and the original
  `ZSyncTransform.OwnerSync` updates ZDO positions in LateUpdate, after a live
  transform may have crossed a sector boundary. A sector-only fallback would
  not preserve all of the old loaded-character checks.

## Implementation and cost

`CreatureSpawnBlocker` owns one HashSet of loaded candidates. Awake, the existing
Start/spawn lifecycle and local Enforcer marking observe candidates independently
of server authority or Karma/level settings. When both blocking options are Off,
observation and queries return early. Character destruction removes a candidate;
scene/plugin teardown clears references. Death is checked at query time rather
than permanently discarding a candidate that could revive.

Queries check candidates' current category, health and transform first. A matching
candidate immediately blocks the attempt. On a miss, the original full Character
scan and received-but-unloaded ZDO fallback remain; discoveries become candidates
for later queries. The existing synchronous result cache remains, with invalidation
on candidate observation/removal and the existing spawn-context completion.

This adds registration/cleanup work and memory proportional to observed candidates,
but avoids scanning ordinary creatures when a registered blocker matches. A miss
still costs the original scan plus the small candidate pass. There is no new timer,
per-frame scan, cross-attempt negative cache, server message or dependency. Outdoor
simulation distance, dungeon anchor boundaries and boss/Enforcer precedence remain
unchanged; this is not a 3x3 range change or an absence-query optimization.

## Implementation verification (before the version bump)

- Baseline and final Debug solution builds with `DeployToGame=true`: zero warnings
  and errors; ILRepack completes before the final plugin DLL is copied.
- Baseline original-client Mono contracts passed. Final original Valheim 1.0.16
  client build 25527674 and dedicated server build 25527701 contracts passed:
  7,755 game/Unity references, 105 Harmony targets, seven transpiler applications,
  and managed behavior checks. Existing snapshots were reused without extraction.
- Added contracts cover loaded/unloaded option combinations, late Enforcer flags,
  direct boss changes, stale ZDO position/death data, no ZDO, live enable/disable,
  movement/realm changes, death/revival, unload/destroy/reset and scoped invalidation.
  Lifecycle hook connections are inspected in the compiled DLL.
- With 1,000 ordinary loaded creatures plus one registered boss, one spawn query
  performs one live classification and zero full-list/sector queries. This excludes
  initial registration cost and measures fixture work counts, not CPU time or FPS.
- The fixture executes compiled production bodies while substituting native scene,
  transport and Unity object identity boundaries. An initial fixture failure reached
  native Unity hash/equality initialization; the fixture now uses reference identity
  for its synthetic Character objects. No production workaround was added.
- Final Debug/installed plugin SHA-256:
  `3B5CF840202EB0ACE0D4100CB7EA9547F199B2BAFAB5681C8B0FD70D4BA00CFD`.
  Installed game assembly matches the preserved original client assembly.
- Read-only independent code review and `git diff --check` passed.

No Unity gameplay, host/dedicated multiplayer or actual CPU/GC profiling was run.
Remaining checks: ordinary spawning with/without nearby blockers, late client
reception, ownership transfer, dungeon transitions, death/revival and world reentry.
Optional-mod binary composition was not rerun; spawn Harmony targets/order and
transpilers were not changed. That implementation step did not change the version,
create a Release package or push.

## Requested release: 1.2.4

The subsequent release request includes the candidate optimization above, its
regression tests and changelog. The discussed fixed 3x3/5x5 ranges and source-zone
blocking policy were not implemented; the existing range and candidate-position
policy remain unchanged.

- Debug build with `DeployToGame=true` and normal Release build: zero warnings/errors.
- YAML DTO/parser checks and the final Release DLL's original 1.0.16 client/server
  Mono checks passed, with the same 7,755 references, 105 targets and seven transpilers.
- Assembly version 1.2.4.0, plugin version 1.2.4 and manifest version 1.2.4 agree.
  ServerSync and YamlDotNet are merged; BepInEx dependency remains 5.4.2351.
- `Thunderstore/CreatureManager_v1.2.4.zip`: 17 files, 3,833,491 bytes. Every entry
  matches its source, including the Release DLL, changelog and 11 bundled textures.
- Final Debug/installed plugin SHA-256:
  `3E4886C1C437F7973CA38C1FEF5CC3F266AB0DF4B29246EA1E86625B8A271628`.
- Release DLL SHA-256:
  `5F0B64BDDFB8194DFD49002201EBB85D943201BB046661B88616B9C89C31272E`.
- Release ZIP SHA-256:
  `56EB6B39A9797417AEDCF8A64D5302EBD4FB7896B49C0E25113F40E6BA9C5B1E`.

Actual game/multiplayer execution and performance measurement remain unverified.
