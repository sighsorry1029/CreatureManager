# EpicMMO level label compatibility

2026-09-28. Baseline: CreatureManager `0e0cbf7` (1.2.1).

## Contract and implementation

- `1 - General` / `Adjust EpicMMO LevelBar Position`: On by default, bound
  through the existing synchronized config helper and admin locking policy.
  The server selects the behavior; each installed client adjusts its UI.
- The optional plugin is identified by `WackyMole.EpicMMOSystem` and its public
  BepInEx config entry `2.Creature level control` / `Enabled_creature_level`.
  Discovery occurs on the first HUD update after normal plugin loading.
  No hard assembly reference, dependency update, external config write, new RPC
  or save format is introduced. EpicMMO level control Off is respected.
- `CreatureModifierManager.UpdateEnemyHuds` retains its existing traversal.
  Its Harmony postfix runs after the EpicMMO owner. Existing boss-HUD temporary
  state and finalizer restoration remain intact. Only regular non-player,
  non-mount, non-boss HUDs are adjusted.
- `CreatureEpicMmoHud` caches `Name(Clone)` under the existing name transform.
  This covers both EpicMMO's normal creation and its UpdateHuds fallback.
  On applies `(70, -15)` even when EpicMMO's configured coordinates differ.
  Off restores the last pre-adjustment position, including the fallback's
  original `(37, -30)`, rather than silently changing EpicMMO's own behavior.
- Stable labels and stable misses do not repeat hierarchy searches. A new child,
  destroyed cached label or reparented label triggers discovery again. The
  existing traversal marks live entries; departures are removed in `finally`.
  HUD destruction, world teardown and plugin shutdown restore and clear state.
  An external change made after CM's last adjustment is not overwritten on Off.
- This is a separate optional UI boundary, not part of modifier rolling or
  server combat rules. It uses public Unity/BepInEx APIs and the existing HUD
  integration. Game targets remain the original private `EnemyHud.UpdateHuds`
  and `OnDestroy` methods. Existing publicizer/runtime access handling for
  CreatureManager's HUD traversal is unchanged.

## Verified inputs and checks

- Original EpicMMOSystem DLL: **1.9.70**, SHA-256
  `EA34358692649548EF8D7F84EBBD0D4776C7171B94D54CE7E4D56E3ADBCD9EF1`.
  Read from the supplied `vvvv` profile; not modified or publicized.
- Its normal `MonsterColorTexts.Postfix` reads the synced `MobLevelPosition`;
  `StarVisibilityMMO.Postfix` can recreate the same child with fixed `(37, -30)`.
  Both use the Harmony owner `WackyMole.EpicMMOSystem`.
- Debug build with `DeployToGame=true`: zero warnings/errors; merged DLL copied
  to local Valheim plugins and SHA-256 matched.
- YAML checks and GameCompatibilityCheck passed against original Windows x64
  Valheim 1.0.16 client b25527674 and dedicated server b25527701 assemblies.
  The adapter adds 39 checks of compiled production control flow, live settings,
  restoration, exclusion, replacement, cleanup and cached hierarchy lookup.
  A stable 1,000-update fixture performs no additional Find or position writes.
- The fixture substitutes native Unity properties and object comparison, and
  inspects the supplied EpicMMO binary as metadata. It does not execute a Unity
  scene, install the Harmony detours, or demonstrate network synchronization.
  No frame-time or FPS improvement is claimed.

To include the optional binary contract check, set
`CREATUREMANAGER_CHECK_EPIC_MMO` to the original EpicMMOSystem.dll path and run
the existing `tools/GameCompatibilityCheck/run_mono.py` with the final plugin,
original game root, and BepInEx core directory.

Remaining game checks: client/host/dedicated join with server On and Off;
live admin toggle; normal and recreated labels; bosses/boss-style Enforcers,
players and mounts; reconnect/world changes; and other HUD mods/UI scales.
