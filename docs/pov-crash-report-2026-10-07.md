# CreatureManager / Path of Valheiman: reentrant level changes and hard crashes

Date: 2026-10-07

## Summary for the Path of Valheiman developer

We found a concrete interaction between CreatureManager (CM) 1.2.5 and Path of Valheiman (PoV) 4.13.0 that can repeatedly call `Character.SetLevel` until the stack is exhausted. CM restores its requested level while PoV reapplies additional stars. Neither side reaches the end of the outer call.

The original behavior was reproduced in a bounded, isolated test. Native dumps from the reported game crashes also show Mono executing its stack-overflow recovery path before a secondary access violation. This makes the level recursion a strong explanation for the reported crashes. The dumps do not retain the original managed call stack, so they do not directly identify `Character.SetLevel` as the initial overflowing function.

A CM-side fix is included in version 1.2.6 and passed the automated checks described below. Version 1.2.5 does not contain this correction. A post-fix reproduction in the actual game is still required. This is not a claim that all interactions or balance settings between the two mods have been validated.

## Observed environment and reproduction

| Component | Version / setting |
| --- | --- |
| Valheim | 1.0.17, Windows x64 |
| Unity | 6000.0.75 |
| BepInExPack Valheim | 5.4.2351 |
| CreatureManager | 1.2.5; installed DLL matches the released DLL by SHA-256 |
| Path of Valheiman | 4.13.0 |
| PoV Link | 0.3.0 |
| CM level mode | `Full` |
| CM biome preset | `Normal` |
| PoV creature rarity | Enabled |
| PoV maximum stars | 8 |
| PoV chance of each star above two | 0.5 |

The maintainer reproduced a hard crash after teleporting to DeepNorth in a local-host session with CM installed. The preceding run without CM reached DeepNorth without the same crash. Comparing the two logs' loaded-plugin lists shows exactly one added plugin: CM. This comparison does not establish that every setting or saved world state was identical.

DropNSpawn, Drop That, Spawn That, and StarLevelSystem were disabled in the supplied crashing run. Their directories were present, but their DLLs were renamed with `.old`, and they were absent from the loaded-plugin list.

Other users reported crashes during ordinary exploration and near bosses, including after disabling CM modifiers and nearby-spawn suppression. Those other runs have not been independently verified. The supplied profile itself had CM modifiers and both nearby-spawn blockers enabled.

## The conflicting call sequence

Levels below refer to `Character` levels: level 3 means two visible stars.

1. CM records a desired level, then calls `SetManagedLevel(character, 3)`.
2. PoV's `RarityRoll.Character_SetLevel_Sync.Postfix` calls `RarityRoll.Sync`.
3. For an eligible ordinary creature with a qualifying stored seed, PoV adds stars and calls `Character.SetLevel(5)`.
4. CM's `Character.SetLevel` postfix sees level 5 instead of its desired level 3.
5. In CM 1.2.5, `TryAdoptExternalLevelOverride` refuses the change while `ManagedSetLevelDepth > 0`. Its caller, `RestoreConfiguredLevel`, responds by calling `SetManagedLevel(character, 3)` again.
6. PoV sees the same stored base and seed and requests level 5 again.

```text
CM SetManagedLevel(3)
  -> PoV Sync -> SetLevel(5)
    -> CM RestoreConfiguredLevel -> SetManagedLevel(3)
      -> PoV Sync -> SetLevel(5)
        -> ...
```

PoV's `ours` flag prevents overwriting the stored base-star value during its own change. It does not prevent `Sync` from running again. Consequently, the promotion remains deterministic on each return to CM's requested level.

In the inspected PoV DLL, `CreatureStars.ForeignLevels` recognizes StarLevelSystem and Creature Level and Loot Control, but does not recognize CM's GUID, `sighsorry.CreatureManager`. Its existing foreign-level fallback therefore does not resolve this combination.

Relevant CM 1.2.5 source at commit `47e45a6e8c3b389a51e42fc7387d1a4fb90af473`:

- `CreatureLevelManager.TryApplyLevelState`: records the desired level before calling the game.
- `CreatureLevelManager.RestoreConfiguredLevel`, `TryAdoptExternalLevelOverride`, and `SetManagedLevel`: the recursive restoration path.
- `CreatureManagerCharacterSetLevelPatch.Postfix` in `CreatureHarmonyPatches.cs`: invokes the restoration.

The PoV symbols above were inspected in the installed 4.13.0 DLL. No modified PoV binary was used or produced.

## Why the reported symptoms fit

- Disabling CM combat modifiers or nearby-spawn suppression does not disable CM's level assignment and restoration.
- DeepNorth has a higher chance of meeting the additional-star prerequisite under CM's Normal biome preset. This explains why entering a newly loaded region can expose the interaction; it does not imply a DeepNorth-only defect or a measured crash probability.
- PoV's relevant ordinary-creature eligibility path excludes bosses. A crash shortly after summoning a boss does not prove that the boss itself caused this particular loop; nearby ordinary creature initialization may also be involved.
- Ordinary natural spawning is a better reproduction path than a console spawn command, because CM gives explicit commands a separate level-adoption policy.

## Native crash evidence

The matching Windows Application Error event occurred at 10:43:38 local time on 2026-10-07. It recorded `0xc0000005` in `ntdll.dll`. The corresponding dump, `valheim.exe.27276.dmp`, was written at 10:43:58. An earlier crash at 10:31:57 had the same failure path.

Using the exact matching official Unity Mono symbols, the relevant path was identified as:

```text
Mono restore_stack trampoline
  -> _resetstkoflw
  -> __acrt_SetThreadStackGuarantee
  -> delayed system-DLL loading
  -> Steam overlay's DLL-loading hook
  -> GetModuleHandleW
  -> ntdll access violation
```

The return address into Mono's generated `restore_stack` trampoline was also checked against its emitter. This is evidence of stack-overflow recovery, even though Windows records the final error as an access violation. The final fault is consistent with a secondary failure during recovery, which also explains why a normal managed exception need not appear in BepInEx's log.

Steam overlay's presence in this stack does not establish that it initiated the failure. The original overflowing managed frames are unavailable in these minidumps.

Background references: [Unity Mono's Windows exception and stack recovery code](https://github.com/Unity-Technologies/mono/blob/unity-main/mono/mini/mini-windows.c), [Microsoft `_resetstkoflw` documentation](https://learn.microsoft.com/en-us/cpp/c-runtime-library/reference/resetstkoflw). The current upstream source is explanatory material; the installed binary and its matching symbols supplied the dump evidence.

## CM-side correction

The 1.2.6 correction:

- Tracks an active CM-owned level assignment per creature, rather than with one global nesting counter.
- Defers CM's `SetLevel` postfix side effects for that creature until the CM caller resumes after the game's call and all external postfixes return.
- Removes the recursive restoration to the old requested level.
- Records the final actual `GetLevel()` without another `SetLevel` call. Existing callers then calculate level-dependent stats and visuals using that level.
- Reads the actual current level for ordinary external changes too, so a stale outer `level` argument cannot overwrite a completed nested change.
- Releases the active-assignment state in `finally` and preserves the existing absolute missing-health handling.
- Keeps CM completion separate from external spawn-context adoption. A level change to a different creature within the callback remains independently processable.

The fix does not change config keys, default values, saved-state keys, or optional dependencies. It does not disable PoV features or add a hard incompatibility declaration. PoV's final level can differ from CM's initial roll; the fix prevents the two mods from repeatedly enforcing competing values.

Use CM 1.2.6 or later to test this correction; reinstalling CM 1.2.5 does not include it. Update the server and all clients together, as CM's existing version synchronization requires matching versions.

## Validation and remaining checks

Completed:

- The unmodified baseline Debug build and existing compatibility checks passed.
- The initial isolated reproduction used extracted CM and PoV level-handling methods. With PoV seed 1, its actual extra-star calculation produced two extra stars, resulting in `3 -> 5 -> 3 -> 5...`. The test safely stopped at 25 calls. Reversing postfix order did not remove the loop.
- A permanent regression test was added to CM's `LevelModeContracts`. It executes the compiled CM methods and postfix while substituting the foreign postfix and Unity/world boundaries. It exceeded the bounded recursion limit with the pre-fix DLL and passed with the patched DLL.
- Regression coverage includes foreign level increases, decreases and no change; both postfix orders; final stored level and health/damage calculations; missing-health preservation; stale outer arguments; another creature changed during the callback; external spawn contexts; forced CM levels and their source tags; exception cleanup; and `ExceptLevel` / `Off` controls.
- The patched Debug build completed without warnings or errors. The compatibility suite passed with zero failures against the installed original Valheim managed assemblies under the game's Mono runtime, without a Unity scene or native Harmony detours.
- Version 1.2.6 Debug and Release builds completed without warnings or errors. The final merged Release DLL also passed the compatibility suite with zero failures, including the reentrant-level regression. The Release ZIP's 17 files matched their source hashes, and its manifest and DLL versions agreed. These are build and automated-check results, not confirmation of game-session behavior or publication on a mod site.

Still required:

- Repeat the same DeepNorth teleport / natural-spawn reproduction with the patched CM DLL and the original modpack settings.
- Observe ordinary exploration, dungeon entry, and encounters where new creatures load or spawn.
- Verify multiplayer owner-side behavior in an actual host/client or dedicated-server session.
- Check the intended combined health, damage, rarity and modifier balance separately. Passing the recursion regression is not full compatibility certification.

## Possible complementary PoV change

Please consider recognizing `sighsorry.CreatureManager` in the foreign-level handling. If the intended policy is that CM owns star assignment, PoV could read the resulting stars for rarity while declining its own extra-star assignment.

This needs a mode-aware policy decision: CM's `ExceptLevel` mode deliberately leaves level assignment to the game or another mod, while `Full` assigns levels. Treating the mere presence of CM as ownership in every mode could unnecessarily disable PoV behavior. The CM recursion fix stands independently of that decision.

For temporary diagnosis on the original versions, changing CM's `Enable Level System` to `ExceptLevel`, or more decisively `Off`, isolates this level-assignment path. These are diagnostic controls, not proof that every forced-spawn or other interaction is safe.
