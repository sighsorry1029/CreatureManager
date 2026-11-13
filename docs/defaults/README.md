# Default YAML reference

**CreatureManager 1.3.14.** These eight files match the first-launch templates. The [1.3.13 reference](https://github.com/sighsorry1029/CreatureManager/tree/4cd9b55/docs/defaults) uses the previous two-file layout. A missing `modifier.yml` is created with the full defaults on both fresh installs and upgrades. Existing files are preserved, including an empty `modifier.yml`. For custom values from older files, see the [modifier upgrade guide](../user-guide.md#modifier-rules-and-upgrades).

| File | Contents |
| --- | --- |
| [levels.yml](levels.yml) | Level and stat defaults, mode guidance, and commented biome distributions for Easy, Normal, Hard, and VeryHard. |
| [modifier.yml](modifier.yml) | Combat modifier chances, powers, distance scaling, and leader overrides by biome, group or prefab. |
| [karma.yml](karma.yml) | Karma gain/decay, outdoor and dungeon Enforcer candidates, bonuses, and loot. |
| [factions.yml](factions.yml) | Default faction relationships and custom-faction examples. |
| [creatures.yml](creatures.yml) | Commented creature override schema and examples. |
| [ai.yml](ai.yml) | Commented AI override schema and examples. |
| [attacks.yml](attacks.yml) | Commented attack override schema and examples. |
| [projectile.yml](projectile.yml) | Commented projectile and spawn-ability override examples. |

Additional inactive examples are maintained in [creatures.sample.yml](../../defaults/creatures.sample.yml) and [attacks.sample.yml](../../defaults/attacks.sample.yml). Runtime-generated `*.reference.yml` files depend on the loaded game and mods, so they are not included here.

## Applying selected settings

- Existing files under `BepInEx/config/CreatureManager/` are preserved by the mod. Compare and merge the entries you need; do not replace a customized file wholesale.
- Toxic Aura defaults to `0.1` poison strength. In `modifier.yml`, `toxicDeath` works as an alias, preserving chance and power but ignoring the former radius/effect. An explicit old `0.3` remains `0.3` and may be strong for the recurring aura: review any per-creature and Enforcer values copied into `modifier.yml` and change the second value to `0.1` if desired. Prefer the new form `toxicAura: 10, 0.1`; keep your existing chance instead of copying `10` blindly. In the same block, explicit `toxicAura` takes priority.
- Blink always uses `fx_Adrenaline1`. Its old fourth tuple value, including `none`, is accepted but ignored; new templates use only chance, cooldown, and range.
- The new `levels.yml` leaves `Global.level` commented out. Published 1.3.3 generated an active `Global.level: [80, 20]`; that existing rule stays active until you remove/comment it yourself.
- `Full + None` keeps game/other-mod levels only where no YAML level weights and no Karma/Enforcer bonus apply. Health, damage, scale, and modifier rules remain independent. This is not a switch to fully vanilla combat balance.
- With no matching `scalePerLevel` value, both `Full` and `ExceptLevel` now use native prefab level sizes, including in dungeons. Published 1.3.4 supported this fallback only in `ExceptLevel`. Explicit `0` disables star growth; positive values retain CM's dungeon/saddle restrictions. Level one, missing prefab size entries, and stored modifier sizes are not migrated or reset.
- Preset distribution tables at the bottom of `levels.yml` are comments, not active rules. Copy only the biome blocks you want and merge duplicate keys. In Full mode, explicit biome `level` weights also apply to new regular bosses when `Bosses Follow Biome Level Preset` is On (the default), even with preset None. Boss/group/prefab level rules take priority; shared biome stats/modifiers remain excluded. Existing biome YAML can therefore affect new bosses after this update; completed creatures keep their levels.
- A `main` URL follows current source and can include unreleased changes. For an archived version, use GitHub's commit-specific file URL. Release notes should pin a reference to the corresponding documentation commit or release tag rather than silently changing an older release's defaults.

These reference files are published in the repository; they are not bundled as active configurations in the Release ZIP.

## Regenerating and checking

Run from the repository root with the original installed game, BepInEx, .NET SDK, and 64-bit Python 3.8+ available. Use the Python executable available on your machine if it is not named `python`.

```powershell
dotnet build CreatureManager.sln -c Debug -p:DeployToGame=true
if ($LASTEXITCODE -ne 0) { throw 'CreatureManager build failed.' }
dotnet build tools/GameCompatibilityCheck/GameCompatibilityCheck.csproj -c Debug
if ($LASTEXITCODE -ne 0) { throw 'Compatibility checker build failed.' }

$env:CREATUREMANAGER_DEFAULT_YAML_DIR = Join-Path (Get-Location) 'docs/defaults'
$env:CREATUREMANAGER_WRITE_DEFAULT_YAML = '1'
try {
    python tools/GameCompatibilityCheck/run_mono.py `
        'C:/Program Files (x86)/Steam/steamapps/common/Valheim' `
        bin/Debug/CreatureManager.dll `
        'C:/Program Files (x86)/Steam/steamapps/common/Valheim/BepInEx/core'
    if ($LASTEXITCODE -ne 0) { throw 'Default YAML export/checks failed.' }
} finally {
    Remove-Item Env:CREATUREMANAGER_WRITE_DEFAULT_YAML -ErrorAction SilentlyContinue
    Remove-Item Env:CREATUREMANAGER_DEFAULT_YAML_DIR -ErrorAction SilentlyContinue
}
```

For a read-only comparison, set only `CREATUREMANAGER_DEFAULT_YAML_DIR` and run the same checker without `CREATUREMANAGER_WRITE_DEFAULT_YAML`. Missing or stale YAML fails the check. Generation executes the compiled template methods with manager initialization bypassed; it parses all eight outputs before writing. Line endings are normalized, and unchanged files are not rewritten. No game scene, network session, or user configuration is loaded by the export/check step. The Debug build separately updates the local plugin DLL under the normal build procedure.

When preparing a release, regenerate/check these files, update this version notice and the README notice, and link the release's changelog to the appropriate fixed revision. Do not relabel an old release with newer defaults.
