# CreatureManager

CreatureManager is a server-synced Valheim framework for creating, tuning, and scaling creatures without taking ownership of drops or spawn tables.

It is built around a simple workflow: discover loaded content, copy only what you need, and keep the resulting configuration small.

## Highlights

- Clone or edit vanilla and modded creature prefabs.
- Configure selected `Character` and `Humanoid` fields, equipment, appearance, scale, and textures.
- Clone and tune monster attack prefabs, including damage, animation, projectile, AI range, and attack status effects.
- Clone projectile prefabs and override `Projectile.m_spawnOnHit` or `SpawnAbility.m_spawnPrefab` without duplicating their remaining component state.
- Build reusable `BaseAI` and `MonsterAI` presets, or borrow AI directly from another creature prefab.
- Add and edit factions without mirroring unrelated game systems.
- Define weighted levels for global, boss, biome, group, and individual prefab rules.
- Scale health, damage, modifier chance, and visuals by level or world distance.
- Roll 32 combat modifiers across Offense, Defense, Affliction, and Special groups.
- Add regional Karma progression and configurable Enforcer encounters.
- Inspect loaded creatures, attacks, AI, loadouts, projectiles, textures, and level visuals through generated references.
- Hot-reload YAML and local PNG textures while keeping synchronized gameplay definitions server-authoritative.

## Why CreatureManager

**Focused ownership**

CreatureManager handles creature identity, combat, AI, visuals, levels, and regional pressure. Drops and spawn tables remain available to dedicated tools such as DropNSpawn.

**Reference-driven configuration**

Generated reference files include content from Valheim and loaded mods, grouped by best-effort source. You do not need to guess prefab, attack, projectile, equipment, or texture names.

**Small overrides**

Configuration files contain only intentional changes. Full scaffolds are available when discovery is more useful than compact references, but they are never required as active configuration.

**Predictable schemas**

Only the documented canonical YAML fields are accepted. Schema comments and complete examples live in the generated configuration files so they stay next to the values they describe.

## Installation

1. Install BepInEx for Valheim.
2. Place `CreatureManager.dll` in `BepInEx/plugins`.
3. Start Valheim once to generate configuration and reference files.
4. For multiplayer, install the same CreatureManager version on the server and every client.

ServerSync requires the configured CreatureManager version during connection and synchronizes gameplay configuration from the server. Client-only display options remain local.

## Building from Source

Debug builds neither deploy to Valheim nor create release archives. The project discovers common Steam install locations automatically; if discovery fails, copy `environment.props.example` to the ignored `environment.props` file and update its machine-local paths.

```powershell
dotnet build CreatureManager.sln -c Debug
```

Set `DeployToValheim=true` only when the built DLL should be copied to the configured `CopyOutputDLLPath`:

```powershell
dotnet build CreatureManager.sln -c Debug -p:DeployToValheim=true
```

Release builds create archives in `artifacts/Thunderstore` and `artifacts/Nexus`. The merged DLL assembly version supplies the package version and updates the tracked `Thunderstore/manifest.json` automatically. This root file is the single README source and is copied only into the temporary Thunderstore staging directory.

```powershell
dotnet build CreatureManager.sln -c Release
```

Pass `-p:BuildPackages=false` when a Release DLL is needed without archives.

## Getting Started

1. Open `BepInEx/config/CreatureManager` after the first launch.
2. Find the target prefab in a generated reference file.
3. Copy the relevant entry or name into the matching active YAML file.
4. Change only the fields you need.
5. Save the file. Most YAML and texture changes reload automatically.

The generated YAML headers are the schema reference. This README intentionally does not duplicate their field-by-field examples.

## Configuration Files

| File | Purpose |
|---|---|
| `creatures.yml` | Clone and edit creatures, humanoid loadouts, appearance, scale, textures, and AI assignment. |
| `attacks.yml` | Clone and edit monster attack item prefabs. |
| `creatures.sample.yml` | Inactive creature examples generated on the first launch. Copy selected entries into an active creature file, or rename it to `creatures_sample.yml` to activate it. |
| `attacks.sample.yml` | Inactive attack examples generated on the first launch. Copy selected entries into an active attack file, or rename it to `attacks_sample.yml` to activate it. |
| `projectile.yml` | Clone projectile prefabs and override supported on-hit or spawn-ability prefab references. |
| `ai.yml` | Define reusable AI presets or overrides. |
| `factions.yml` | Define and edit factions. This domain is intentionally single-file. |
| `levels.yml` | Configure level weights, stat scaling, distance scaling, visuals, and modifiers. |
| `karma.yml` | Configure regional Karma and Enforcer encounters. |
| `localization/<Language>.yml` | Define server-authoritative localized text using Valheim language names such as `English.yml` and `Korean.yml`. |
| `textures/` | Store local PNG textures used by creature texture overrides. With the client-local `Generate Sample Textures` option on by default, bundled samples are generated only when missing, so existing replacements are preserved. |

The creature, attack, projectile, AI, and level domains load both `.yml` and `.yaml` base or split files, such as `creatures.yml`, `creatures.yaml`, and `creatures_*.yaml`. The generated `creatures.sample.yml` and `attacks.sample.yml` names do not match those active patterns, so the examples remain inactive. Copy selected entries into the active files, or install MonsterLabZ and rename them to `creatures_sample.yml` and `attacks_sample.yml` to activate the complete sample set. Bonebeard, Vincent, and Root Witch require MonsterLabZ. Vitrfell's `boar2.png` is included in the bundled default texture set and is generated when the client-local `Generate Sample Textures` option is On. Faction and Karma configuration intentionally accept only the single canonical files `factions.yml` and `karma.yml`.

### Server Localization

Files directly under `localization/` are flat `token: text` mappings. Both `.yml` and `.yaml` are accepted, but keep only one file for each Valheim language name. A token key may have one leading `$`; CreatureManager removes it while loading, so `rootwitch` and `$rootwitch` define the same token. Use `$token` in `creatures.yml` or another localized field. English is applied as the fallback before the client's selected language.

```yaml
# localization/English.yml
cm_rootwitch_name: Root Witch

# localization/Korean.yml
cm_rootwitch_name: 뿌리 마녀

# creatures.yml
character:
  name: $cm_rootwitch_name
```

Only the server, listen host, or current single-player authority reads these files. Connected clients ignore their local `CreatureManager/localization` folder and use the synchronized language maps. Existing vanilla or mod tokens can be referenced without redefining them. Redefining an existing token overrides every use of that token, so use a unique `cm_...` token for clone-specific names.

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

## Core Systems

### Creatures, Attacks, and AI

Creature definitions can modify an existing prefab or clone one with `clonedFrom`. Clones can use cloned attacks, reusable AI presets, AI borrowed from loaded creature prefabs, custom factions, humanoid equipment, and local texture overrides.

The supported surface is intentionally narrower than a full prefab database dump. It concentrates on fields useful for monsters and customizable NPCs while leaving drops, spawn tables, player prefabs, and large effect graphs to other domains or mods.

### Levels and Progression

Level rules can target broad defaults or narrow content: global rules, bosses, biomes, named groups, and individual prefabs. Built-in biome presets provide a starting point, while explicit YAML rules replace only the values you choose.

Levels can control weighted star distribution, health, damage, visual scale, distance scaling, and modifier selection. Vanilla level visual states rotate when a creature exceeds the visual states supplied by its prefab.

CreatureManager is spawn-aware. Natural hostile spawns receive the complete matching rule, while breeding, eggs, grow-up transitions, tamed restores, Blood Magic summons, and explicit console spawns preserve or restrict values according to their lifecycle instead of blindly rerolling every creature initialization.

### Combat Modifiers

CreatureManager provides 32 modifiers in four groups. Natural rolls select at most one modifier from each group, for up to four visible modifiers. The values below are the generated `Global` defaults; `levels.yml` and `karma.yml` can override them for Global, Boss, prefab/group, Enforcer, and individual Enforcer rules.

#### Offense

| Icon | Modifier | Effect with generated `Global` defaults |
| :---: | --- | --- |
| <img src="docs/modifier-icons/enraged.png" width="40" height="40" alt="Enraged icon"> | **Enraged** (`enraged`) | Deals 15% more damage. |
| <img src="docs/modifier-icons/fire.png" width="40" height="40" alt="Fire icon"> | **Fire** (`fire`) | Adds Fire damage equal to 20% of the original hit. |
| <img src="docs/modifier-icons/frost.png" width="40" height="40" alt="Frost icon"> | **Frost** (`frost`) | Adds Frost damage equal to 10% of the original hit. |
| <img src="docs/modifier-icons/lightning.png" width="40" height="40" alt="Lightning icon"> | **Lightning** (`lightning`) | Adds Lightning damage equal to 10% of the original hit. |
| <img src="docs/modifier-icons/spirit.png" width="40" height="40" alt="Spirit icon"> | **Spirit** (`spirit`) | Adds Spirit damage equal to 5% of the original hit. Against players, its Spirit damage over time bypasses armor and resistance. |
| <img src="docs/modifier-icons/armorPiercing.png" width="40" height="40" alt="Armor Piercing icon"> | **Armor Piercing** (`armorPiercing`) | Ignores 30% of player body armor. |
| <img src="docs/modifier-icons/staggering.png" width="40" height="40" alt="Staggering icon"> | **Staggering** (`staggering`) | Adds 60% normal-hit and block-stagger buildup. |
| <img src="docs/modifier-icons/undodgeable.png" width="40" height="40" alt="Undodgeable icon"> | **Undodgeable** (`undodgeable`) | Ignores player dodge invulnerability but deals 25% less damage. Blocking and parrying still work. |

#### Defense

| Icon | Modifier | Effect with generated `Global` defaults |
| :---: | --- | --- |
| <img src="docs/modifier-icons/armored.png" width="40" height="40" alt="Armored icon"> | **Armored** (`armored`) | Takes 30% less damage. |
| <img src="docs/modifier-icons/deathward.png" width="40" height="40" alt="Deathward icon"> | **Deathward** (`deathward`) | Cancels lethal damage and restores 20% of max health. Has a 10s cooldown and up to 3 activations. |
| <img src="docs/modifier-icons/regenerating.png" width="40" height="40" alt="Regenerating icon"> | **Regenerating** (`regenerating`) | Heals a configurable share of max health per second. Defaults are 1% for Global creatures, 0.2% for bosses, and 0.5% for Enforcers. |
| <img src="docs/modifier-icons/reflection.png" width="40" height="40" alt="Reflection icon"> | **Reflection** (`reflection`) | Has a 50% chance on a direct melee hit to reflect 10% of the health actually lost, bypassing defense and resistance. |
| <img src="docs/modifier-icons/vortex.png" width="40" height="40" alt="Vortex icon"> | **Vortex** (`vortex`) | Has a 50% chance to negate projectile damage, push, stagger, and status effects. |
| <img src="docs/modifier-icons/adaptive.png" width="40" height="40" alt="Adaptive icon"> | **Adaptive** (`adaptive`) | Remembers the dominant hit type for 5s and reduces matching damage by 50%. |
| <img src="docs/modifier-icons/unflinching.png" width="40" height="40" alt="Unflinching icon"> | **Unflinching** (`unflinching`) | Immune to normal-hit and perfect-parry stagger. |
| <img src="docs/modifier-icons/chameleon.png" width="40" height="40" alt="Chameleon icon"> | **Chameleon** (`chameleon`) | While alerted, gains one rotating damage-type immunity and switches it every 10s. |

#### Affliction

| Icon | Modifier | Effect with generated `Global` defaults |
| :---: | --- | --- |
| <img src="docs/modifier-icons/exposed.png" width="40" height="40" alt="Exposed icon"> | **Exposed** (`exposed`) | Has a 50% chance to make the player take 20% more damage for 5s. |
| <img src="docs/modifier-icons/weakened.png" width="40" height="40" alt="Weakened icon"> | **Weakened** (`weakened`) | Has a 50% chance to make the player deal 20% less damage for 5s. |
| <img src="docs/modifier-icons/withered.png" width="40" height="40" alt="Withered icon"> | **Withered** (`withered`) | Has a 50% chance to reduce healing received by 50% for 5s. |
| <img src="docs/modifier-icons/crippling.png" width="40" height="40" alt="Crippling icon"> | **Crippling** (`crippling`) | Has a 50% chance to reduce movement speed and jump force by 50% for 5s. |
| <img src="docs/modifier-icons/disruptive.png" width="40" height="40" alt="Disruptive icon"> | **Disruptive** (`disruptive`) | Has a 50% chance to reduce stamina and Eitr recovery by 50% for 5s. |
| <img src="docs/modifier-icons/adrenalineDrain.png" width="40" height="40" alt="Adrenaline Drain icon"> | **Adrenaline Drain** (`adrenalineDrain`) | Has a 50% chance to remove 50% of current adrenaline and reduce adrenaline gain by 50% for 5s. |
| <img src="docs/modifier-icons/corrosive.png" width="40" height="40" alt="Corrosive icon"> | **Corrosive** (`corrosive`) | Has a 50% chance to increase durability loss to equipped gear by 50% for 5s. |
| <img src="docs/modifier-icons/toxicDeath.png" width="40" height="40" alt="Toxic Death icon"> | **Toxic Death** (`toxicDeath`) | On death, poisons players within 4m for 30% of their max health. |

#### Special

| Icon | Modifier | Effect with generated `Global` defaults |
| :---: | --- | --- |
| <img src="docs/modifier-icons/swift.png" width="40" height="40" alt="Swift icon"> | **Swift** (`swift`) | Increases movement speed, acceleration, and turning speed by 40%. |
| <img src="docs/modifier-icons/attackSpeed.png" width="40" height="40" alt="Attack Speed icon"> | **Attack Speed** (`attackSpeed`) | Increases animation speed by 30%; attack intervals become 76.92% of normal. |
| <img src="docs/modifier-icons/vampiric.png" width="40" height="40" alt="Vampiric icon"> | **Vampiric** (`vampiric`) | Heals for 30% of health removed by direct hits; delayed damage over time is excluded. |
| <img src="docs/modifier-icons/reaping.png" width="40" height="40" alt="Reaping icon"> | **Reaping** (`reaping`) | Nearby kills heal 5% of base max health (up to 20 activations) and grant +10% max health, +1% damage, and +5% size per kill, capped at +200%, +20%, and +50%. No new size is gained in dungeons. |
| <img src="docs/modifier-icons/blink.png" width="40" height="40" alt="Blink icon"> | **Blink** (`blink`) | Teleports near its player target within 24m every 6s. |
| <img src="docs/modifier-icons/omen.png" width="40" height="40" alt="Omen icon"> | **Omen** (`omen`) | Has a 50% chance on a direct player kill to force an Enforcer check regardless of cooldown. |
| <img src="docs/modifier-icons/juggernaut.png" width="40" height="40" alt="Juggernaut icon"> | **Juggernaut** (`juggernaut`) | Player hits have at least 150 push force. An actual push starts a 5s cooldown, and the creature is immune to attack push. |
| <img src="docs/modifier-icons/blamer.png" width="40" height="40" alt="Blamer icon"> | **Blamer** (`blamer`) | Below 75% health, flees and adds 0.5 Karma per second up to 45 lifetime Karma. When exhausted, the modifier, icon, and flee behavior end. |

Modifier icons appear on creature and boss HUDs. The Valheim Compendium contains a CreatureManager page with modifier names, icons, and descriptions using the active global values. English and Korean localization are embedded.

### Karma and Enforcers

Karma is a regional pressure system based on a sliding 3x3 neighborhood of vanilla zones. Kills of untamed, non-player creatures by a player, a tamed creature, or another player-side creature raise local Karma; runtime-summoned creatures, including Enforcers and their minions, do not. Higher thresholds can add levels to future natural spawns, and idle regions decay over time.

Enforcers turn high Karma into encounters. A biome or dungeon table can choose an Enforcer, minions, modifiers, level bonus, bonus loot, and location restrictions. Nearby overlapping player regions are evaluated as one connected check region to avoid duplicate rolls for the same group.

Enforcer behavior can be used together with Karma levels or independently through BepInEx configuration. Current Karma appears near the minimap, and Enforcer events use localized center-screen messages.

Karma is currently stored in server memory and resets when the server process restarts.

## Common Workflows

**Tune an existing creature**

Find it in `creatures.reference.yml`, copy its identity into `creatures.yml`, and add only the fields being changed.

**Build a customizable humanoid NPC**

Clone a suitable humanoid prefab, choose equipment from `creatureLoadout.reference.txt`, and assign an existing or reusable AI definition.

Use `humanoid.randomHair` for attachable NPC hair prefabs that must remain visible with a helmet. It is visual-only: unlike `randomArmor`, it does not consume the helmet equipment slot or grant the source item's armor and equipment effects. The creature's `appearance.hairColor` also tints the selected random-hair visual and its ragdoll; white hair textures provide the most predictable tint. `appearance` is a partial mapping, so omitted fields inherit the source prefab while `hair: ''` or `beard: ''` explicitly clears that ordinary appearance slot. Explicit hair, beard, colors, and model index are copied to a target-specific death ragdoll; normal equipped armor continues through Valheim's own ragdoll setup, while CreatureManager does not force held weapons onto a corpse. Full scaffold generation moves a `randomArmor` list to `randomHair` only when every entry is confirmed as attachable customization hair; mixed hair/helmet lists keep their original one-choice semantics.

**Create a custom combat kit**

Clone attacks in `attacks.yml`, then place those attack prefab names in the creature's humanoid loadout. Use `projectile.reference.yml` to discover candidates and `projectile.yml` to clone them or replace `projectile.spawnOnHit` and `spawnAbility.spawnPrefabs`. Each spawn entry uses `Prefab[:weight]`; an omitted weight is `1`, and weights are relative. A weight may be from 1 to 1000, with at most 4096 expanded slots per list. For example, this selects Skeleton with `10/11` probability and Blob with `1/11` probability:

```yaml
spawnAbility:
  spawnPrefabs: [Skeleton:10, Blob]
```

Generated references compact repeated prefab entries into this weighted notation while preserving their first-occurrence order. The generated `usedByAttacks` field is informational and is ignored in active configuration. Component blocks already identify their kind, so no `type` field is generated or accepted.

**Create world progression**

Use `levels.yml` for biome, group, prefab, boss, distance, and modifier rules. Keep broad defaults near the top and add narrow overrides only where needed.

**Create regional encounters**

Use `karma.yml` to tune Karma gain and decay, then assign outdoor or dungeon Enforcer tables by biome and optional location.

**Reskin a creature**

Inspect `textures.reference.txt`, place a PNG in the `textures` directory when needed, and reference it from the creature's texture slots. CreatureManager creates a target-specific ragdoll prefab for configured scale, textures, or appearance instead of changing the shared source ragdoll. Texture entries are also attempted on matching ragdoll renderer names/paths; a ragdoll without a matching renderer logs a warning and leaves that slot unchanged. Existing `EffectData` scale flags run unchanged during creation, after which CreatureManager restores the exact configured scale on its target-specific ragdoll. Runtime texture property blocks keep configured body textures authoritative when `appearance.modelIndex` updates the ragdoll model.

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
| `cm:karma` | Show the current neighborhood Karma. |
| `cm:karma <value>` | Set the current neighborhood Karma to a non-negative value. |

The spawn command supports prefab and comma-separated modifier completion. It is intended for testing and does not perform a normal level or modifier roll. `cm:spawn` and `cm:karma` are available only to the local listen-server host, not remote administrators or a dedicated-server console.

## Reload Behavior

Saving YAML or replacing PNG files schedules one consolidated reload. Updated definitions affect future spawns and reusable prefab data without repeatedly rebuilding references.

All seven gameplay YAML domains are parsed, validated, and synchronized as one versioned snapshot. If any gameplay domain is invalid, CreatureManager keeps the complete last successfully loaded snapshot and does not synchronize a partial update. An intentionally empty file or top-level `[]` clears that file's user entries; built-in faction defaults, level presets, and Karma defaults still apply. This is separate from `modifiers: []`, which blocks lower modifier fallback.

Localization is synchronized as a separate last-known-good payload so a translation error cannot reject gameplay configuration or trigger a full prefab reapply. A comment-only language file is a valid empty file. Removing a token restores the translation that existed before CreatureManager overrode it, provided another mod has not changed that token afterward.

Existing creatures keep already assigned levels, health, modifiers, and equipment. For future spawns, a reload restores prefab fields that were removed from YAML. Removed CreatureManager clones remain registered until the world closes so existing entities and prefab references stay valid; they are unregistered and destroyed during world teardown. Changing `clonedFrom` for an existing clone name is rejected at runtime; restart Valheim to make that source change.

## Compatibility

- Supports vanilla and loaded mod prefabs discovered through `ZNetScene` and `ObjectDB`.
- Supports Expand World Data custom biome names and cloned location names for level and Enforcer selection.
- Preserves lifecycle values for breeding, egg hatching, grow-up, and pokeball-style tamed restores.
- Detects FeedLikeGrandma when available without requiring it as a dependency.
- Prevents normal level and modifier rerolls for Blood Magic summons and explicit console spawns.
- Uses ServerSync for required-version checks and server-authoritative gameplay configuration.

## Scope

CreatureManager does not manage:

- Player prefab editing or cloning.
- Creature drop tables outside optional Enforcer bonus loot.
- `SpawnSystem`, `CreatureSpawner`, or `SpawnArea` configuration.
- Full effect-list scaffolding.
- Player weapon and recipe configuration.

These boundaries are intentional: CreatureManager is designed to compose with focused spawn, drop, item, and world-data mods instead of replacing them.

## Author

Created by **sighsorry**.

Release notes live in `Thunderstore/CHANGELOG.md` in the source tree and are packaged as `CHANGELOG.md`. Source is distributed under `LICENSE.txt`.
