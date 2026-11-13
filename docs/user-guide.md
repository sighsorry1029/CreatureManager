# CreatureManager User Guide

Detailed configuration and usage notes. For the feature overview, images, and GIFs, see the [README](../README.md).

**CreatureManager 1.3.14.** For the previous two-file layout, see the [1.3.13 guide](https://github.com/sighsorry1029/CreatureManager/blob/4cd9b55/docs/user-guide.md).

## Contents

- [Installation and settings](#installation-and-settings)
- [Configuration files and samples](#configuration-files)
- [Generated references](#generated-references)
- [Reload behavior](#reload-behavior)
- [Creatures, attacks, and AI](#creatures-attacks-and-ai)
- [Levels and progression](#levels-and-progression)
- [Combat modifiers](#combat-modifiers)
- [Star loot scaling](#star-loot-scaling)
- [Karma and Enforcers](#karma-and-enforcers)
- [Textures and ragdolls](#textures-and-ragdolls)
- [Common workflows](#common-workflows)
- [Console commands](#console-commands)
- [Compatibility](#compatibility)

## Installation and Settings

Requires BepInExPack Valheim 5.4.2360. Install the same CreatureManager build on the server and every client. Copy the entire package's `BepInEx` folder, not just the DLL, so bundled sample textures are available too.

After the first launch, use `BepInEx/config/sighsorry.CreatureManager.cfg` for feature switches and `BepInEx/config/CreatureManager/` for YAML, localization, and textures. In multiplayer, the server supplies synchronized settings. `Lock Configuration` defaults to On; server administrators control locked settings.

The [README settings table](../README.md#settings) lists the main switches. The sections below explain their interactions. Generated config descriptions and YAML headers are the field reference.

## Configuration Files

| File | Purpose |
|---|---|
| `creatures.yml` | Clone and edit creatures, humanoid loadouts, appearance, scale, textures, and AI assignment. |
| `attacks.yml` | Clone and edit monster attack item prefabs. |
| `creatures.sample.yml` | Inactive creature examples generated on the first launch. Copy selected entries into an active creature file, or rename it to `creatures_sample.yml` to activate it. |
| `attacks.sample.yml` | Inactive attack examples generated on the first launch. Copy selected entries into an active attack file, or rename it to `attacks_sample.yml` to activate it. |
| `projectile.yml` | Clone projectile prefabs and override fixed or weighted-random on-hit spawns and spawn-ability prefab references. |
| `ai.yml` | Override same-name creature AI directly or define uniquely named reusable AI presets. |
| `factions.yml` | Define and edit factions. This domain is intentionally single-file. |
| [`levels.yml`](https://github.com/sighsorry1029/CreatureManager/blob/main/docs/defaults/levels.yml) | Configure level weights, stat scaling, distance scaling, and visuals. |
| [`modifier.yml`](defaults/modifier.yml) | Configure modifier chances, powers, distance scaling, and Enforcer leader rules. |
| [`karma.yml`](https://github.com/sighsorry1029/CreatureManager/blob/main/docs/defaults/karma.yml) | Configure regional Karma and Enforcer encounters. |
| `localization/<Language>.yml` | Define server-authoritative localized text using Valheim language names such as `English.yml` and `Korean.yml`. |
| `textures/` | Store top-level PNG files used by creature texture overrides. The server synchronizes only files referenced by active creature definitions; clients keep verified SHA-256 content in `cache/synced-textures`. Release packages install the bundled `cm_*.png` samples here as ordinary files; the DLL does not generate them. |

Browse all eight [default YAML files](https://github.com/sighsorry1029/CreatureManager/tree/main/docs/defaults), generated from the same code used on first launch. These links track current source; check the version notice before copying. Existing user files are not automatically updated: merge only the entries you need rather than replacing your configuration wholesale.

### Samples and File Names

The creature, attack, projectile, AI, and level domains load both `.yml` and `.yaml` base or split files, such as `creatures.yml`, `creatures.yaml`, and `creatures_*.yaml`. The generated `creatures.sample.yml` and `attacks.sample.yml` names do not match those active patterns, so the examples remain inactive. Copy selected entries into the active files, or install MonsterLabZ and rename them to `creatures_sample.yml` and `attacks_sample.yml` to activate the complete sample set. Bonebeard, Vincent, and Root Witch require MonsterLabZ. Vitrfell uses the packaged `cm_boar2.png` texture. Use the exact filenames `factions.yml`, `karma.yml` and `modifier.yml` for those domains.

## Generated References

| File | Contents |
|---|---|
| `creatures.reference.yml` | Compact loaded creature information. |
| `creatures.full.yml` | Full creature scaffold generated on demand. |
| `attacks.reference.yml` | Loaded monster attack prefab candidates. |
| `ai.reference.yml` | Loaded creature AI values and reusable source prefab names. |
| `creatureLoadout.reference.txt` | Likely creature and NPC equipment prefabs. |
| `projectile.reference.yml` | Projectile, AOE, and spawn-ability candidates, their supported override fields, and reference-only `usedByAttacks` metadata. |
| `textures.reference.txt` | Loaded `Texture2D` names grouped by source. |
| `levelVisual.reference.yml` | Vanilla `LevelEffects` profiles generated on demand. |

References refresh automatically after Valheim's prefab databases are ready and only rewrite when their generated content changes. Treat them as discovery output rather than active configuration.

## Reload Behavior

Saving an active YAML file updates synchronized templates and global rules without a restart. Already loaded creatures keep their creature fields, AI, equipped attack data, and completed level and modifier state; existing Enforcers keep their identity, level, loot, and modifiers. Creature GameObjects instantiated after the reload use the current templates; this includes persistent creatures loaded again after a zone unload. Projectiles and ragdolls are separate runtime objects, so ones created after the reload also use current templates even when their source creature already existed. Faction relationships, Karma economy and encounter checks, server localization, and shared texture visuals are live global or visual exceptions. Changing `clonedFrom` for an already registered clone requires a restart.

Keep a backup before broad edits. Save one logical change at a time and check the BepInEx log for parse/apply errors. A reference file is generated output, not an active override.

## Creatures, Attacks, and AI

Creature definitions can modify an existing prefab or clone one with `clonedFrom`. An AI definition named after a loaded `MonsterAI` or `AnimalAI` creature applies directly to that prefab; a unique AI name remains a reusable preset and can use `copyFrom` for its baseline. A creature-level `ai:` assignment is only needed to select a reusable preset or borrow another creature prefab's AI, and takes priority over same-name direct application. Clones can also use cloned attacks, custom factions, humanoid equipment, and local texture overrides.

The supported surface is intentionally narrower than a full prefab database dump. It concentrates on fields useful for monsters and customizable NPCs while leaving drop-table editing, spawn tables, player prefabs, and large effect graphs to other domains or mods.

### Combat Target Switching

The synchronized `7 - AI` settings add optional combat target switching, independently of levels and modifiers:

| Setting | Default |
| --- | --- |
| `Player Target Switching` | `Off` |
| `Player Target Switch Interval (s)` | `10` |
| `Player Target Switch Chance (%)` | `25` |
| `Player Target Hold Duration (s)` | `5` (range `0`–`30`) |

When enabled, an alerted, untamed MonsterAI creature (including bosses and Enforcers) can choose a sensed, hostile player other than its current target, with a path check before switching. One player is sufficient when an animal is currently tanking. Attempts wait the configured interval and use normal target-search ticks. Valid selections are retained for the configured hold duration, released on the next search tick; attacks are not interrupted and normal pursuit abandonment remains active. Death, lost sensing or a blocked path can end a hold early. A hold duration of `0` disables holding; switching attempts still follow the configured interval. An active hold takes precedence over the switch interval, so it is not interrupted by another switching attempt. Failed rolls, missing candidates and unreachable selections consume the interval. Changing any of these settings clears pending attempts and holds and restarts the attempt interval; Off returns to normal selection on the next search.

Tamed creatures and FrozenKing phase 2 are excluded. This feature does not identify ranged attackers or override ability-specific targeting (such as independently targeted meteors). Decisions run only on the creature's network owner, including in dedicated-server games.

## Levels and Progression

Level rules can target broad defaults or narrow content: global rules, bosses, biomes, named groups, and individual prefabs. Built-in biome presets provide a starting point, while explicit YAML rules replace only the values you choose.

### Level Modes

The synchronized `[2 - Levels]` option `Enable Level System` accepts:

| Value | Automatic level assignment | Stat, distance, scale, and modifier rules |
| --- | --- | --- |
| `Off` | Keep the level assigned by the game or another mod | Disabled |
| `ExceptLevel` | Keep the level assigned by the game or another mod | Enabled, using that actual level |
| `Full` (default) | CreatureManager level weights, biome presets, and Karma/Enforcer level bonuses | Enabled |

### Biome Presets

`Biome Level Preset` is used only in `Full` mode:

| Preset | Level weights supplied by the preset |
| --- | --- |
| `Easy` (default), `Normal`, `Hard`, `VeryHard` | Built-in biome distributions; explicit YAML biome/group/prefab weights take priority. |
| `None` | No built-in distribution. Matching YAML `level` weights and Karma/Enforcer level bonuses still apply. |

Newly generated `levels.yml` files end with commented biome distributions for all four built-in presets, generated from the same weights used at runtime, plus an explanation of `None`. Copy or uncomment only the biome blocks you want to override, merging with existing blocks to avoid duplicate keys. Existing YAML files are preserved.

### YAML Weights and Fallbacks

`Full + None` lets YAML choose which creatures receive CM level rolls. New defaults leave `Global.level` commented out (**since 1.3.4; 1.3.3 generated an active `[80, 20]` rule**). If enabled, including in an existing file, `Global.level: [80, 20]` supplies a base roll of 80% zero-star / 20% one-star wherever no more specific YAML level rule or built-in biome preset applies. Global is the last fallback, not an override of prefab/group/biome rules. To keep game/other-mod levels except selected targets, use `Full + None`, leave broad level weights disabled, and add weights to the desired prefab/group/biome. Leave other fields in place to retain their independent stat and modifier rules. A creature with no matching weights keeps its assigned level **only if no Karma/Enforcer level bonus applies**. Weights specify the base roll, not a final star cap; even `level: [100]` can receive bonus levels. Disable Karma level bonuses separately when a strict normal-spawn distribution is wanted.

### Boss Level Fallback

In `Full`, `Bosses Follow Biome Level Preset = On` (default) lets regular bosses fall back to top-level biome YAML `level` weights, then the built-in preset. For example, `Swamp: { level: [100] }` also supplies a zero-star base roll for bosses spawned in Swamp, unless a Boss/group/prefab level rule takes priority. Explicit biome weights still work with preset `None`. Turn the option Off to exclude both biome fallbacks from bosses; explicit Boss/group/prefab rules, including `Boss: { Swamp: { level: [100] } }`, remain valid. Only level weights are shared: boss health, damage, speed, size, and modifiers do not inherit common biome YAML, and regular bosses never inherit Global.

**Upgrade note for 1.3.13:** this option previously allowed only built-in preset weights for regular bosses; shared biome YAML was ignored. With the default option On, existing biome YAML can now change newly spawned bosses' base levels too. Existing YAML files and completed creatures are not rewritten or rerolled. The default remains `Full + Easy`; Karma bonuses remain separate.

### Keeping Game-Assigned Levels

Existing `Off` config values remain valid. The former `On` selection (including numeric `1`) is read as `Full`, and `Vanilla` (including numeric `2`) is read as `ExceptLevel`; both are saved under their new names. With `ExceptLevel`, `Biome Level Preset`, `Bosses Follow Biome Level Preset`, and YAML `level` weights do not assign levels. Biome/group/prefab health, damage, per-level and distance multipliers, visual scale, and modifiers still follow the normal rule and spawn-source restrictions; the three modifier switches remain independent. Karma tracking and Enforcer encounters follow `Karma System Mode`, but their level bonuses are skipped. Loot settings remain independent. Explicit command levels (including `cm:spawn`) still work. Switching modes does not reroll or reset completed level/stat/modifier state on existing creatures; test automatic level assignment with new spawns.

`ExceptLevel` only excludes CreatureManager's automatic level assignment. In a new default `levels.yml`, change `Global.damagePerLevel` from **0.25** to **0.5** to match vanilla per-star damage growth for regular creatures. Default health growth already matches vanilla, and omitted Global `scalePerLevel` preserves the prefab's original level sizes in this mode. Other matching rules and modifiers still apply; Karma, Enforcer encounters, and loot remain independently configured. Boss rules are separate and still apply if another mod or an explicit command gives a boss extra levels.

### Size Scaling

In both `Full` and `ExceptLevel`, if no matching rule supplies `scalePerLevel`, creatures use their prefab's original level sizes, including in dungeons and on saddle-able creatures (**extended to Full in 1.3.5**). This applies with any biome preset. Omission means no value is inherited from any applicable Global/Boss, biome, group, or prefab rule. Level one and levels without a prefab size entry leave the current size alone; no growth is extrapolated beyond the prefab's table. An explicit `0` keeps the original size without star growth; a positive value uses CM's existing size formula and dungeon/saddle restrictions. Color-state cycling remains enabled. Prefabs without level-size setups receive no automatic CM size adjustment when the rule is omitted.

New default YAML files leave Global `scalePerLevel: 0.1` as a commented example and keep Boss `scalePerLevel: 0.1` active. Uncomment the Global example to enable CM star growth, or set it to `0` to disable star growth where no more specific rule overrides it. Published 1.3.4 treated an omitted scale rule as no star-size increase in `Full`; the new native-size fallback changes that behavior without rewriting YAML. Existing explicit values remain overrides. This does not migrate stored modifier sizes or guarantee that removing a rule immediately restores every loaded creature's previous size; original level-size behavior is applied when its visuals are processed again.

### Damage Scaling

Levels can control weighted star distribution, health, damage, movement speed, visual scale, distance scaling, and modifier selection. `damagePerLevel` replaces vanilla's 50% monster damage growth per level: for example, `damage: 1` and `damagePerLevel: 0.2` make a level 6 creature deal 2x level-1 damage to character targets before distance, multiplayer, and modifier scaling. Vanilla level visual states rotate when a creature exceeds the visual states supplied by its prefab.

### Movement Speed

`speed` and `speedPerLevel` multiply walking, running, flying, swimming, acceleration, and turning by `speed * (1 + (level - 1) * speedPerLevel)`. For example, `speed: 1.2` and `speedPerLevel: 0.1` give 1.2x / 1.32x / 1.44x at zero / one / two stars. Each field inherits independently through the usual level-rule priority; with no matching value, defaults are 1 and 0. `speed` must be finite and greater than zero (values below 1 slow movement); `speedPerLevel` must be finite and nonnegative, and explicit 0 overrides inherited star growth. The generated YAML leaves both as commented examples. These rules work in `Full` and `ExceptLevel`, including with modifiers disabled, and stop applying in `Off`. They multiply with Swift and other movement adjustments; they do not change attack animation speed, attack intervals, or `distanceScaling`. Unlike the absolute `creatures.yml` character speed tuple, these are additional multipliers.

The owner stores the resolved speed multiplier alongside each creature's level stats. Completed creatures retain that value on configuration reload and owner changes; older creatures without a speed value remain at 1x. Supported later level changes recalculate it without rerolling modifiers, while growth/restore transitions inherit stored stats. No speed rules are searched every frame, and original speed fields are not overwritten or repeatedly multiplied.

### Spawn Lifecycle and Exceptions

CreatureManager is spawn-aware. Natural hostile spawns receive the complete matching rule, while breeding, eggs, grow-up transitions, tamed restores, Blood Magic summons, and explicit console spawns preserve or restrict values according to their lifecycle instead of blindly rerolling every creature initialization.

`FrozenKing_p2` is protected because its health drives the seven-Aspect encounter. CreatureManager skips level/stat scaling, the creature `health` override (including regeneration time), and all modifiers for this exact prefab, and leaves incoming encounter damage unchanged. `cm:spawn` accepts it only at level 1 without modifiers; modifier inheritance and snapshot restore reject it. Phase 1, phase 3, and the Aspects retain their normal rules. This prevents interference in new encounters; it does not repair previously altered health or recover a stalled fight. Other mods and direct game commands can still alter the encounter.

## Combat Modifiers

`Global Modifiers`, `Boss Modifiers`, and `Enforcer Modifiers` in `2 - Levels` independently accept `Max4`, `Max3`, `Max2`, `Max1`, or `Off` (default: `Max4`). MaxN limits new automatic rolls; it does not trim previously stored, inherited, or explicitly forced modifiers. Off disables effects and icons without deleting stored values.

Configure modifier chances and powers in `modifier.yml`. The [README showcase](../README.md#combat-modifiers) and in-game Compendium describe all 32 effects.

### Modifier rules and upgrades

A fresh install puts all three base modifier tables in `modifier.yml`. For natural spawns, an individual prefab takes priority over its group, the biome and Global. Regular bosses use Boss, group and prefab rules. Enforcer leaders first use ordinary rules with their applicable distance adjustment, then Enforcer, biome.enforcer, group.enforcer and prefab.enforcer. A nested biome is more specific than its parent. Minions use their ordinary creature rules.

```yaml
Troll:
  enforcer:
    modifiers:
      staggering: 15, 0.6
  BlackForest:
    enforcer:
      modifiers:
        staggering: 30  # Retains the 0.6 power.
```

Set `modifiers: []` to clear lower layers at that scope. An empty map or omitted field inherits. Place `modifierDistanceScaling` on an ordinary target. Leader chance overrides are applied after distance scaling. Define groups locally in this file or use group names from `levels.yml` and its split files. If two level files use the same name for different members, declare the intended group in `modifier.yml`.

Only `modifier.yml` supplies automatic modifier rules. When it is missing, new and existing installations create the full default tables. An existing empty or comment-only file stays empty and supplies no automatic rolls. This also applies to files created by earlier development builds.

The loader skips `modifiers` and `modifierDistanceScaling` in all active level files, and `modifiers` on Enforcer defaults and individual summons in `karma.yml`. On the host or dedicated server, the first such entry in each file produces a migration notice. To retain custom chances, powers, disabled effects (`0`), clears (`[]`) and distance scaling, move those values into `modifier.yml`. For a selected leader prefab use `prefab.enforcer` or `prefab.biome.enforcer`. A biome selector uses the leader's position at the roll. Outdoor and dungeon candidates for the same prefab in the same biome share that rule. Use matching builds on all peers. Hot reload affects future rolls; existing stored rolls are preserved.

### Tuple Syntax

Every modifier tuple requires `chance` first. Later values form an optional trailing suffix: stop after the last value you want to override. Omitted values inherit through the normal rule chain and otherwise use runtime defaults. Explicit `0` is a supplied value subject to normal field validation, while empty interior slots such as `10, , 5` are invalid.

### Toxic Aura

Toxic Aura replaces Toxic Death. Use `toxicAura: chance, maxHealthPoison`, for example `toxicAura: 10, 0.1`. In `modifier.yml`, `toxicDeath` is accepted as an alias: chance and power are preserved, while old radius/effect values are ignored. In the same modifier block, explicit `toxicAura` wins regardless of order and the two entries are never merged. New `toxicAura` entries accept only chance and optional power. A compatibility notice is logged once per session; existing files are not rewritten or populated automatically. The radius is the larger XZ extent of the live body collider plus 3m, so creature scaling is included. Poison is reapplied once per second through vanilla poison/resistance rules, without additional attacker damage multipliers; it does not stack as direct damage per second. Only the creature's network owner applies damage. Each client maintains a local particle-only visual extracted from `blob_aoe`, with no `Aoe`, `ZNetView` or repeated effect RPCs. Death, taming, disabled effects and scene teardown stop it; there is no death burst or lingering pool.

**Upgrading from Toxic Death:** the new default poison strength is `0.1`, and a value of `0.3` copied into `modifier.yml` gives the recurring aura that stronger setting. Review both creature and Enforcer entries when moving old blocks. To use the new default strength, change the second tuple value to `0.1` while keeping your chance, for example `toxicDeath: 10, 0.1, 4, blob_aoe`, or replace it with `toxicAura: 10, 0.1`. Existing saved Toxic Death flags also become Toxic Aura; old per-creature power/radius/effect fields are not migrated, and a missing new aura power uses `0.1`.

### Blink

Blink always uses the built-in `fx_Adrenaline1` effect. Configure `blink: chance, cooldown, maxRange` in `modifier.yml`. An existing fourth tuple value (including `none`) is accepted but ignored; stored creature effect names are also ignored. Existing YAML files are not rewritten. This prevents custom effect prefabs from creating unintended network or gameplay objects; Blink's chance, cooldown and range remain configurable.

### Karma, Omen, and Reaping Kill Credit

Enforcers and their summoned minions grant zero kill Karma. If one carries Omen, a credited player kill can still trigger an Enforcer summon near the killer. By default, Omen can trigger during the regional cooldown. If your server already has `Block Omen Enforcer During Cooldown = On`, set it to `Off` to allow this. The dead creature is excluded from the regional cap. With a cap of one, killing an Omen minion while its Enforcer is still active in that region cannot summon another.

Karma/Omen DoT rewards use the damage pool that actually killed the creature, not unrelated active effects or a recent-hit timeout. Karma accepts confirmed player/tamed/PlayerSpawned contributors; Omen still requires a real player. Within each pool the first confirmed player takes precedence over a tame/summon. Simultaneous fire/spirit ticks also prefer a real player, then use the fire candidate if both are equally eligible. This selects a reward representative, not the largest damage contributor. The server still validates that source, ownership and duplicate deaths; a disconnected/unverifiable selected source is not replaced with a nearby player.

Reaping is independent of this contributor policy: all creature and player deaths within 24m count, including tamed creatures, PlayerSpawned summons, mixed DoT, monster combat and environmental deaths. A death can benefit each nearby surviving Reaping creature once, not the dead creature itself. Despawns, unloads and deletion without a confirmed death do not count. Owner, distance, duplicate-death and growth-limit checks remain in place; a confirmed player respawn allows that player's next death to count again. Summon-heavy fights and repeated player deaths can reach the existing growth caps faster.

## Star Loot Scaling

The synchronized `[6 - Loot]` settings in `BepInEx/config/sighsorry.CreatureManager.cfg` work independently of `Enable Level System`:

| Setting | Default | Behavior |
|---|---|---|
| `Character Loot System` | `CalculateChance` | Replaces exponential star scaling for eligible item drops. Select `Vanilla` to preserve existing scaling. |
| `Chance For Additional Loot Per Star For Creatures` | `50` | Additional successful-drop amount percentage per star, from 0 to 100. |
| `Chance For Additional Loot Per Star For Bosses` | `50` | The same percentage for characters the game identifies as bosses. |

`CalculateChance` keeps an eligible item's base drop chance and scales its successful amount by `1 + stars * percentage / 100`. Only the fractional remainder is rolled: with a base amount of 1 and 50%, one star gives 1 or 2 items, two stars give 2, and three stars give 2 or 3. This changes loot balance: vanilla increases both drop chance and amount by stars. Zero percent keeps the base chance and amount. The game's resource modifiers and 100-item per-entry cap still apply.

Only non-trophy items whose drop entry enables `levelMultiplier` are eligible. One-per-player rewards, non-item drops, player drops, and explicit Enforcer bonus loot retain their existing behavior. Ordinary Enforcer drops use the game's boss classification. Calculation happens on the creature's network owner; the existing death, ragdoll and item-spawn paths remain responsible for delivery. Settings affect newly generated loot, not loot already stored in ragdolls.

The default applies to new or missing settings. An existing saved `Vanilla` selection or custom percentage is preserved; change it explicitly to opt into the new defaults.

When DropNSpawn is loaded, CreatureManager automatically leaves loot scaling to DropNSpawn and logs a notice when `CalculateChance` is first used. Drop That alone does not disable CreatureManager's calculation: its configured items, conditions and base quantities remain in use, and CreatureManager replaces star scaling only on eligible entries with `ScaleByLevel=true`. Entries with `ScaleByLevel=false` are excluded. Drop That's quantity-limit postprocessing, stacking and ragdoll metadata paths remain in place. If your Drop That tables already set the complete reward for each level, set `ScaleByLevel=false` on those entries or select `Vanilla` in CreatureManager.

Implementation boundaries and game-version checks are recorded in [loot scaling](loot-scaling.md). PoV behavior is covered in [compatibility](#compatibility).

CreatureManager does not rewrite the selected setting or the other mod's configuration. Install the same gameplay mods on the server and all clients so ownership changes do not change which drop system is active.

## Karma and Enforcers

Karma is a regional pressure system based on a sliding 3x3 neighborhood of vanilla zones. Outdoor and dungeon neighborhoods track independent Karma, levels, decay, and Enforcer cooldowns while sharing the configured thresholds and gain rules. Kills of untamed, non-player creatures by a player, a tamed creature, or another player-side creature raise Karma in the current neighborhood; runtime-summoned creatures, including Enforcers and their minions, do not. Higher thresholds can add levels to future natural spawns, and idle regions decay over time.

Enforcers turn high Karma into encounters. Biome and dungeon tables in `karma.yml` select the Enforcer, minions, level bonus, bonus loot, and location restrictions. Set the leader's modifiers in `modifier.yml`. Nearby overlapping player regions are evaluated as one connected check region to avoid duplicate rolls for the same group.

New default `karma.yml` files include three DeepNorth encounters: Barka alone outdoors, ElakingMole with one Elaking in `TheHole01`, and JotunWarrior with one BlobMork in `MorkBorg`. Each uses `[40, 30, 2]` for required Karma, consumed Karma, and additional level bonus, inherits the Enforcer modifier rules, and grants its Enforcer's trophy plus two Silver Necklaces as bonus loot. The level bonus is not a final star cap. Dungeon entries match the location prefab names, not the dungeon generator names. Existing `karma.yml` files are preserved; add a DeepNorth section manually to enable these encounters in an existing configuration.

`3 - Karma` → `Karma System Mode` selects `KarmaLevelAndEnforcer` (default), `KarmaLevelOnly`, `EnforcerOnly`, or `Off`. EnforcerOnly still tracks Karma for encounters but does not add Karma levels to normal spawns. Off stops Karma processing without deleting stored values. Current Karma appears near the minimap, and Enforcer events use localized center-screen messages.

### Dungeon Placement

Dungeon Enforcer location matching covers the registered interior zone, including rooms outside the entrance's location radius. Placement stays in that same zone. Loaded-scene placement accounts for the prefab capsule's bottom when positioning it on the floor; navigation still targets the floor, and a warned reservation keeps its selected position. These rules do not add support for relocated or multi-zone custom interiors.

### Dungeon Diagnostics

For troubleshooting, enable **3 - Karma → Dungeon Enforcer Diagnostics** on the server or host (default **Off**, local and not synchronized). `[DungeonEnforcerDiag]` reports counts by reason every 30 seconds, with at most three short prefab/location and coordinate samples per reason. Counts describe candidate checks and outcomes, not spawn probabilities. Toggle changes, world reset and a successful `karma.yml` reload clear the observations. No report is emitted for an empty window.

`UnloadedAnchorFallback` means the server used an existing spawner ZDO because dungeon geometry was not loaded locally; it does **not** confirm free space around that point. `FloorMissing`, `CapsuleBlocked`, `PathAndLineBlocked`, `InvalidDungeonZone`, `MinionPositionMissing` and `ReservationCanceled` help distinguish placement failures. Dedicated-server client-assisted geometry validation is not implemented by this change: the existing anchor fallback, server authority, encounter costs and minion policy remain unchanged.

## Textures and Ragdolls

Inspect `textures.reference.txt`, place a PNG directly in the server's `textures` directory when needed, and reference it from the creature's texture slots. Rooted paths, subdirectories, traversal names, links, and non-PNG local files are rejected. The synchronized manifest is content-addressed, so clients request only missing hashes and keep the previous complete texture generation if a transfer or validation fails. CreatureManager creates a target-specific ragdoll prefab for configured scale, textures, or appearance instead of changing the shared source ragdoll. Texture entries are also attempted on matching ragdoll renderer names/paths; a ragdoll without a matching renderer logs a warning and leaves that slot unchanged. Existing `EffectData` scale flags run unchanged during creation, after which CreatureManager restores the exact configured scale on its target-specific ragdoll. Runtime texture property blocks keep configured body textures authoritative when `appearance.modelIndex` updates the ragdoll model.

Install the package's `BepInEx` folder too, including on dedicated servers that supply referenced PNGs to clients. DLL-only Debug updates do not install sample PNGs. Give personal texture replacements a separate filename, because package updates can replace bundled files. Existing configuration and sample YAML files are not rewritten: when adopting the renamed PNGs, update old texture references such as `boar2` to `cm_boar2`, or retain the matching old PNGs.

## Common Workflows

### Tune an Existing Creature

Find its prefab in `creatures.reference.yml`, copy the identity into `creatures.yml`, and add only the fields you want to change. Use `ai.yml` for reusable behavior and `attacks.yml` for attack changes.

### Build a Custom Creature

Use `clonedFrom` and the generated headers as a starting point, or adapt `creatures.sample.yml` and `attacks.sample.yml`. See [sample activation and requirements](#samples-and-file-names); do not rename the entire sample set unless you want all of it enabled.

Generated references compact repeated prefab entries into weighted lists while preserving first-occurrence order. `usedByAttacks` is informational and ignored in active configuration. Component blocks already identify their kind; no `type` field is generated or accepted.

### Tune Progression or Encounters

Use `levels.yml` for level and stat rules and `modifier.yml` for modifier rules. Use `karma.yml` for regional gain/decay and outdoor or dungeon Enforcer tables. Start with broad defaults and add only the narrower overrides you need.

### Reskin a Creature

Find source textures in `textures.reference.txt`, or place your own PNG directly under the server's `textures/` directory and reference it in the creature's texture slots. See [textures and ragdolls](#textures-and-ragdolls) for naming and reload limits.

## Console Commands

| Command | Purpose |
|---|---|
| `cm:reference creature` | Regenerate the compact creature reference. |
| `cm:reference attack` | Regenerate the attack reference. |
| `cm:reference ai` | Regenerate the AI reference. |
| `cm:reference loadout` | Regenerate the creature loadout list. |
| `cm:reference projectile` | Regenerate `projectile.reference.yml`. |
| `cm:reference texture` | Regenerate the texture list. |
| `cm:reference levelvisual` | Generate the vanilla level visual reference. |
| `cm:full creature` | Generate the full creature scaffold. |
| `cm:spawn <prefab> [modifiers] [level]` | Spawn a test creature with up to four forced modifiers and an optional exact level. |
| `cm:karma` | Show the current outdoor or dungeon neighborhood Karma. |
| `cm:karma <value>` | Set the current outdoor or dungeon neighborhood Karma to a non-negative value. |

The spawn command supports prefab and comma-separated modifier completion. It is intended for testing and does not perform a normal level or modifier roll. `cm:spawn` and `cm:karma` are available to the local listen-server host and connected dedicated-server administrators; a dedicated-server console has no player position to target.

## Compatibility

Do not combine CreatureManager with Creature Level & Loot Control, StarLevelSystem, MonsterDB, or MonsterModifiers: these are declared incompatible. Other integrations below are optional; their mods are not required by CreatureManager.

### Path of Valheiman

Compatible builds coordinate final health, level ownership, star loot, boss modifiers, and HUD layout. CreatureManager checks the relevant contracts rather than accepting one exact version; a rejected contract disables its affected integration with a warning.

In Full mode, PoV's boss modifiers use the final CM star count and PoV's boss settings. Blood Challenge runes add their own stars, and automatic CM modifier rolls avoid selected overlapping PoV effects. The shared HUD places PoV icons before CM icons in rows of four. These settings do not silently disable CM's own modifiers.

See [PoV interoperability](pov-compatibility.md) for boss budgets, rune handling, overlap exclusions, HUD behavior, limits, and verification. Existing saved bosses are not rerolled.

### DropNSpawn and Drop That

See [star loot scaling](#star-loot-scaling) for which mod owns the calculation and how to opt out per drop.

DropNSpawn's optional periodic dungeon spawner can consult `CreatureManager.CreatureManagerSpawnApi.CanSpawn(GameObject prefab, Vector3 position)` to honor the existing boss/Enforcer ordinary-spawn blocking settings. Call this API on Unity's main thread immediately before creation using the registered prefab and final world position. It evaluates current settings/state; it does not reserve a spawn or apply Enforcer/Karma summon behavior. Use the updated builds of both mods for this integration.

### WackyEpicMMOSystem

When using WackyEpicMMOSystem, `1 - General` → `Adjust EpicMMO LevelBar Position` is a server-synchronized switch, **On** by default. It places regular creature level labels at `(70, -15)`, including recreated labels, without editing EpicMMO's configuration. This displayed position takes priority over EpicMMO's configured coordinates. **Off** restores the position recorded before adjustment. Boss, player, and mount HUDs are unchanged. The switch applies live and works independently of CreatureManager's level and modifier settings; clients without EpicMMO and dedicated servers do not adjust UI.

See the [EpicMMO HUD integration notes](epicmmo-hud-compatibility.md) for the implementation and tested boundaries.
