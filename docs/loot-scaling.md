# Star loot scaling — implementation and validation

2026-09-24. Defaults: `CalculateChance`, creature 50%, boss 50%. Existing saved selections and percentages are not migrated or overwritten. `Vanilla` leaves the existing calculation intact. DropNSpawn disables CM's calculation at runtime; Drop That alone does not. Neither external plugin is a required build/runtime dependency.

## Boundaries

- `CharacterDrop.GenerateDropList` retains its original drop entries, pseudo-drop handling, resource scaling, one-per-player override and per-entry cap. Two guarded star multiplications are replaced together, or neither is changed when matching fails.
- Drop That's filter and tracking transpilers run before CM's transpiler. The original entry objects retain their condition/modifier metadata. CM calculates inside the original method; Drop That's postfix limits run afterward.
- Only non-player characters with a valid, locally owned ZNetView and eligible non-trophy item drops are scaled. `levelMultiplier=false`, non-item drops and one-per-player rewards are excluded. Boss classification follows `Character.IsBoss()`.
- Explicit Enforcer bonus loot calls `CharacterDrop.DropItems` directly, outside this patch. No additional spawn, RPC, ZDO field, event subscription or per-frame work is introduced. Existing ragdoll loot is not recalculated.
- Drop That tables which already define complete rewards per level can opt out per entry with `ScaleByLevel=false`, or globally with CM's `Vanilla` setting.

## Automated checks

The test executable uses original game DLLs and the game's Mono in a separate process. It does not launch Unity, load a world, install native detours, or modify the supplied DLLs. Config fixtures use a temporary directory.

```powershell
dotnet build CreatureManager.csproj -c Debug -p:DeployToGame=true
dotnet build tools/GameCompatibilityCheck/GameCompatibilityCheck.csproj -c Debug
python tools/GameCompatibilityCheck/run_mono.py '<game directory>' bin/Debug/CreatureManager.dll '<BepInEx/core directory>' '<optional Valheim.DropThat.dll>'
```

Without the final argument, the checks cover CM arithmetic/rounding/caps, owner/category exclusions, independent boss/creature percentages, late DropNSpawn detection, Drop That permission, both mods together, and atomic transpiler fallback.

With the supplied Drop That **3.1.6.0** DLL (SHA-256 `F20E41B20D8B8C065F803A779A1759752604E26084FFB345CC0360B220ABFEB7`), the checks also:

- Compose the actual Drop That filtering and tracking transpilers in both relative orders, followed by CM; all four hooks must remain exactly once.
- Apply Drop That's actual stack and ragdoll metadata transpilers to original game methods and verify their hook locations. This checks IL composition, not item spawning or serialization at runtime.
- Execute copied binary filter/limit bodies with only Unity-backed null, position and cache lookup replaced by managed fixtures. Allowed/denied entries using the same prefab retain separate identity and conditions. Configured global/per-entry limits run on the resulting quantities.

Validation on 2026-09-24 passed against the installed Valheim 1.0.15 client and the preserved 1.0.15 dedicated server (`dedicated-server-b25390671-windows-x64-20260918T185703Z-depot-restored/original`): 7,462 game/Unity IL references, 98 Harmony targets, six CM/embedded-library transpiler applications, loot contracts, and the optional Drop That checks, with zero failures on each role. The Debug build had zero warnings/errors. The final DLL and deployed `BepInEx/plugins/CreatureManager.dll` shared SHA-256 `E01B7BC1275321CBAA0316ACBB6CCBC5A0EBD9A5206AC742860A49097E9C4AF8`.

### Existing Drop That limitation

The supplied 3.1.6 binary's `CharacterDropSessionManager.LimitDropAmounts` initializes its selected limit to `-1`. Its per-entry `AmountLimit` condition also requires that selected limit to exceed the per-entry limit. Consequently, a per-entry limit alone is ignored in the checked fixture when the global `DropLimit` has not been triggered. Example: quantity 12, `AmountLimit=7`, global limit disabled leaves 12; global limit 10 reduces it to 7. This is existing Drop That behavior, recorded by the optional test, and is not repaired by this CM patch. Do not describe per-entry limits as independently guaranteed by CM.

## Release 1.1.15 validation

The 2026-09-24 Release build includes the loot changes and subsequent structural simplifications. `dotnet build CreatureManager.sln -c Release` completed with zero warnings/errors. The Release YAML checks and original Valheim 1.0.15 client/server Mono checks passed, including Drop That 3.1.6: 7,456 game/Unity references, 98 Harmony targets, six transpiler applications, and all managed contracts. No world or multiplayer session was run.

`Thunderstore/CreatureManager_v1.1.15.zip` contains the merged DLL, README, changelog, manifest, icon and English translation; all six entries match their source files. Assembly version `1.1.15.0` and manifest version `1.1.15` agree. Release DLL SHA-256: `D77DC5CC354845E6C24950C608E5BCE54AECCC9D71F0F2A5D820DF07BA2C9D08`. ZIP SHA-256: `EFFCCB10E7A5133720EFEE8EF94F97ABE112A22F822F50F5F9DAD45CC3AE6A0A`.

## Execution still required

Use the same gameplay mods on all peers. Verify single player/listen host, a remote owning client and a dedicated server, including owner transfer around death. Cover 0/1/2/high-star creatures and bosses, resource multipliers, duplicate-prefab conditional entries, `ScaleByLevel=false`, trophies, one-per-player rewards, explicit Enforcer rewards, direct and ragdoll deaths, stacked items and their `cheated`/world-level metadata. Confirm no duplicate or missing loot across ragdoll cleanup, reconnect and world reload. Actual Unity/world/network execution is separate from the managed checks above.
