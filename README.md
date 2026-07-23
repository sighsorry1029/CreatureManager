# CreatureManager

Add 32 combat modifiers and launch live boss hunt events across dungeons and the open world. Clone and configure creatures, attacks, AI, equipment, appearances, and textures; inspect resistances and scale difficulty by biome, distance, or preset.

## Showcase

### Live Enforcer Hunts

![Regional Karma display](https://i.ibb.co/wFDKytnS/karma.png)

*Killing creatures raises regional Karma, which can strengthen future spawns and trigger Enforcer encounters.*

![Black Forest dungeon Enforcer encounter](https://i.ibb.co/bjRJt0s9/blackforestdungeonenforcer.gif)

*Karma accumulates faster in dungeons, so stay alert.*

![Swamp Enforcer encounter](https://i.ibb.co/WvNj02sD/swampenforcer.gif)

*Outdoor and dungeon Enforcer encounters can be configured separately in `karma.yml`, including guaranteed bonus loot.*

![Mountain Enforcer encounter](https://i.ibb.co/KxDkMY9L/mountainenforcer.gif)

*Enforcers spawn at higher levels, are more likely to carry modifiers, and may bring minions that hunt players down.*

### Creature Cloning and Customization

Existing creatures can be modified or cloned. For advanced examples, install [MonsterLabZ](https://thunderstore.io/c/valheim/p/MonsterLabZFix/MonsterLabZ/) and see `creatures.sample.yml` and `attacks.sample.yml`.

*The following custom bosses were created with CreatureManager and MonsterLabZ.*

![Bonebeard custom creature](https://i.ibb.co/RtnVCBC/bonebeard.gif)

*Bonebeard is a melee boss who infuses every attack with poison.*

![Vincent custom creature](https://i.ibb.co/5XKJvDhx/vincent.gif)

*Vincent the Cunning is a ranged boss who fires a variety of projectiles.*

![Root Witch custom creature](https://i.ibb.co/YB9YQhDH/rootwitch.gif)

*Root Witch is a spellcaster boss who casts a variety of spells.*

### Combat Modifiers

![Creatures displaying multiple modifier icons](https://i.ibb.co/60JDRCGW/mobswithmodifiers.png)

*Creatures roll modifiers independently and display their active effects beneath their nameplates. Levels above 3 appear as a star followed by the level number.*

#### Offense and Defense

![Spirit modifier adding Spirit damage](https://i.ibb.co/chZwZNDR/spirit.gif)

*Spirit adds Spirit damage to the creature's attacks; against players, its damage over time bypasses armor and resistance.*

![Undodgeable modifier bypassing a dodge](https://i.ibb.co/X18bTyr/undodgeable.gif)

*Undodgeable attacks ignore dodge invulnerability, while blocking and parrying remain effective.*

![Deathward modifier preventing a lethal hit](https://i.ibb.co/cKynXmkb/deathward.gif)

*Deathward cancels a lethal hit and restores part of the creature's maximum health.*

![Reflection modifier returning melee damage](https://i.ibb.co/84LqjWMj/reflection.gif)

*Reflection returns part of the creature's actual health loss to a direct melee attacker.*

![Vortex modifier negating a projectile](https://i.ibb.co/k2FtL0g8/vortex.gif)

*Vortex can negate a projectile hit together with its damage and secondary effects.*

![Unflinching modifier preventing stagger](https://i.ibb.co/Ld3psjHY/unflinching.gif)

*Unflinching prevents stagger from normal hits and perfect parries.*

#### Afflictions

![Affliction modifier icons and duration counters](https://i.ibb.co/FbbtDgfN/Afflictionmodifiers.png)

*Timed Afflictions use distinct icons and counters so their remaining duration stays readable in combat.*

#### Special

![Blink modifier teleporting toward a target](https://i.ibb.co/3mVwS3pz/blink.gif)

*Blink teleports the creature near its target to close distance quickly. `Blink Alert Grace Period (s)` delays Blink and its extended attack range for 3 seconds after the creature becomes alerted by default; the timer still expires when no attack can start.*

![Omen modifier triggering an Enforcer check](https://i.ibb.co/G4x2sPfH/omen.gif)

*Omen can request an Enforcer encounter when the affected creature is killed directly by a player or by poison, fire, or spirit damage over time attributed unambiguously to a player.*

![Juggernaut modifier producing heavy knockback](https://i.ibb.co/xKxGBpjK/juggernaut.gif)

*Juggernaut turns successful hits into heavy knockback while resisting attack push.*

![Blamer modifier raising regional Karma](https://i.ibb.co/9mvPYZy1/blamer.gif)

*Blamer flees and raises regional Karma once its health drops below the trigger threshold, then expires when its Karma budget is exhausted.*

### In-game Tools

![Creature resistance inspection](https://i.ibb.co/03cfhV8/inspectresistance.png)

*While sneaking, inspect a creature to view its effective resistances, weaknesses, and immunities beneath the target's nameplate.*

![Defensive modifier entries in the Compendium](https://i.ibb.co/C3VG6r3B/compendium.png)

*The in-game Compendium documents defensive modifiers and their exact behavior.*

![Affliction modifier entries in the Compendium](https://i.ibb.co/21KT36KS/compendium2.png)

*Affliction details are available in the same localized Compendium without leaving the game.*

### Combat Modifiers specific

CreatureManager provides 32 modifiers in four groups. Natural rolls select at most one modifier from each group, for up to four visible modifiers. The values below are the generated `Global` defaults; `levels.yml` and `karma.yml` can override them for Global, Boss, prefab/group, Enforcer, and individual Enforcer rules.

#### Offense

| Icon | Modifier | Effect with generated `Global` defaults |
| :---: | --- | --- |
| <img src="https://i.ibb.co/BVx9sb3K/enraged.png" width="40" height="40" alt="Enraged icon"> | **Enraged** (`enraged`) | Deals 15% more damage. |
| <img src="https://i.ibb.co/0ygZZD0Y/fire.png" width="40" height="40" alt="Fire icon"> | **Fire** (`fire`) | Adds Fire damage equal to 20% of the original hit. |
| <img src="https://i.ibb.co/JR3ckSFN/frost.png" width="40" height="40" alt="Frost icon"> | **Frost** (`frost`) | Adds Frost damage equal to 10% of the original hit. |
| <img src="https://i.ibb.co/9kvRbPqK/lightning.png" width="40" height="40" alt="Lightning icon"> | **Lightning** (`lightning`) | Adds Lightning damage equal to 10% of the original hit. |
| <img src="https://i.ibb.co/nsJX4zm1/spirit.png" width="40" height="40" alt="Spirit icon"> | **Spirit** (`spirit`) | Adds Spirit damage equal to 5% of the original hit. Against players, its Spirit damage over time bypasses armor and resistance. |
| <img src="https://i.ibb.co/WNJrnsHg/armor-Piercing.png" width="40" height="40" alt="Armor Piercing icon"> | **Armor Piercing** (`armorPiercing`) | Ignores 30% of player body armor. |
| <img src="https://i.ibb.co/Jw8T4gkG/staggering.png" width="40" height="40" alt="Staggering icon"> | **Staggering** (`staggering`) | Adds 60% normal-hit and block-stagger buildup. |
| <img src="https://i.ibb.co/rR8dTsFw/undodgeable.png" width="40" height="40" alt="Undodgeable icon"> | **Undodgeable** (`undodgeable`) | Ignores player dodge invulnerability but deals 25% less damage. Blocking and parrying still work. |

#### Defense

| Icon | Modifier | Effect with generated `Global` defaults |
| :---: | --- | --- |
| <img src="https://i.ibb.co/wNHPbYF1/armored.png" width="40" height="40" alt="Armored icon"> | **Armored** (`armored`) | Takes 30% less damage. |
| <img src="https://i.ibb.co/ns25Rvk0/deathward.png" width="40" height="40" alt="Deathward icon"> | **Deathward** (`deathward`) | Cancels lethal damage and restores 20% of max health. Has a 10s cooldown and up to 3 activations. |
| <img src="https://i.ibb.co/x8s20mxk/regenerating.png" width="40" height="40" alt="Regenerating icon"> | **Regenerating** (`regenerating`) | Heals a configurable share of max health per second. Defaults are 1% for Global creatures, 0.2% for bosses, and 0.5% for Enforcers. |
| <img src="https://i.ibb.co/S4q133Xd/reflection.png" width="40" height="40" alt="Reflection icon"> | **Reflection** (`reflection`) | Has a 50% chance on a direct melee hit to reflect 10% of the health actually lost, bypassing defense and resistance. |
| <img src="https://i.ibb.co/1JYL26MD/vortex.png" width="40" height="40" alt="Vortex icon"> | **Vortex** (`vortex`) | Has a 50% chance to negate projectile damage, push, stagger, and status effects. |
| <img src="https://i.ibb.co/KjW1GynX/adaptive.png" width="40" height="40" alt="Adaptive icon"> | **Adaptive** (`adaptive`) | Remembers the dominant hit type for 5s and reduces matching damage by 50%. |
| <img src="https://i.ibb.co/mrcBq1d6/unflinching.png" width="40" height="40" alt="Unflinching icon"> | **Unflinching** (`unflinching`) | Immune to normal-hit and perfect-parry stagger. |
| <img src="https://i.ibb.co/JjW33Mc3/chameleon.png" width="40" height="40" alt="Chameleon icon"> | **Chameleon** (`chameleon`) | While alerted, gains one rotating damage-type immunity and switches it every 10s. |

#### Affliction

| Icon | Modifier | Effect with generated `Global` defaults |
| :---: | --- | --- |
| <img src="https://i.ibb.co/dJBntfKg/exposed.png" width="40" height="40" alt="Exposed icon"> | **Exposed** (`exposed`) | Has a 50% chance to make the player take 20% more damage for 5s. |
| <img src="https://i.ibb.co/F42w0PQ5/weakened.png" width="40" height="40" alt="Weakened icon"> | **Weakened** (`weakened`) | Has a 50% chance to make the player deal 20% less damage for 5s. |
| <img src="https://i.ibb.co/RqB7wdW/withered.png" width="40" height="40" alt="Withered icon"> | **Withered** (`withered`) | Has a 50% chance to reduce healing received by 50% for 5s. |
| <img src="https://i.ibb.co/LzG2TYkb/crippling.png" width="40" height="40" alt="Crippling icon"> | **Crippling** (`crippling`) | Has a 50% chance to reduce movement speed and jump force by 50% for 5s. |
| <img src="https://i.ibb.co/sdY1NFzg/disruptive.png" width="40" height="40" alt="Disruptive icon"> | **Disruptive** (`disruptive`) | Has a 50% chance to reduce stamina and Eitr recovery by 50% for 5s. |
| <img src="https://i.ibb.co/sJJkksLr/adrenaline-Drain.png" width="40" height="40" alt="Adrenaline Drain icon"> | **Adrenaline Drain** (`adrenalineDrain`) | Has a 50% chance to remove 50% of current adrenaline and reduce adrenaline gain by 50% for 5s. |
| <img src="https://i.ibb.co/HTnv5QHG/corrosive.png" width="40" height="40" alt="Corrosive icon"> | **Corrosive** (`corrosive`) | Has a 50% chance to increase durability loss to equipped gear by 50% for 5s. |
| <img src="https://i.ibb.co/FbcNsZ56/toxic-Death.png" width="40" height="40" alt="Toxic Death icon"> | **Toxic Death** (`toxicDeath`) | On death, poisons players within 4m for 30% of their max health. |

#### Special

| Icon | Modifier | Effect with generated `Global` defaults |
| :---: | --- | --- |
| <img src="https://i.ibb.co/3mfDdZgF/swift.png" width="40" height="40" alt="Swift icon"> | **Swift** (`swift`) | Increases movement speed, acceleration, and turning speed by 40%. |
| <img src="https://i.ibb.co/xKKMTzcq/attack-Speed.png" width="40" height="40" alt="Attack Speed icon"> | **Attack Speed** (`attackSpeed`) | Increases animation speed by 30%; attack intervals become 76.92% of normal. |
| <img src="https://i.ibb.co/SXqZd5Pq/vampiric.png" width="40" height="40" alt="Vampiric icon"> | **Vampiric** (`vampiric`) | Heals for 30% of health removed by direct hits; delayed damage over time is excluded. |
| <img src="https://i.ibb.co/Jw9yV49Z/reaping.png" width="40" height="40" alt="Reaping icon"> | **Reaping** (`reaping`) | Nearby kills heal 5% of base max health (up to 20 activations) and grant +10% max health, +1% damage, and +5% size per kill, capped at +200%, +20%, and +50%. No new size is gained in dungeons. |
| <img src="https://i.ibb.co/Gfqc089L/blink.png" width="40" height="40" alt="Blink icon"> | **Blink** (`blink`) | Teleports near its player target within 16m every 6s. By default, Blink and its extended attack range unlock 3s after the creature becomes alerted, even if no attack could start during that time. |
| <img src="https://i.ibb.co/ZpnXrfGq/omen.png" width="40" height="40" alt="Omen icon"> | **Omen** (`omen`) | Has a 50% chance when killed directly by a player or by unambiguously player-attributed poison, fire, or spirit damage over time to force an Enforcer check; cooldown blocking follows the server setting. |
| <img src="https://i.ibb.co/spF4n7Wg/juggernaut.png" width="40" height="40" alt="Juggernaut icon"> | **Juggernaut** (`juggernaut`) | Player hits have at least 150 push force. An actual push starts a 5s cooldown, and the creature is immune to attack push. |
| <img src="https://i.ibb.co/TBCgrbkt/blamer.png" width="40" height="40" alt="Blamer icon"> | **Blamer** (`blamer`) | Below 75% health, flees and adds 0.5 Karma per second up to 45 lifetime Karma. When exhausted, the modifier, icon, and flee behavior end. |

Modifier icons appear on creature and boss HUDs. The Valheim Compendium contains a CreatureManager page with modifier names, icons, and descriptions using the active global values. English and Korean localization are embedded.

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

`localization/English.yml`

```yaml
cm_rootwitch_name: Root Witch
```

`localization/Korean.yml`

```yaml
cm_rootwitch_name: 뿌리 마녀
```

`creatures.yml`

```yaml
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

### Karma and Enforcers

Karma is a regional pressure system based on a sliding 3x3 neighborhood of vanilla zones. Kills of untamed, non-player creatures by a player, a tamed creature, or another player-side creature raise local Karma; runtime-summoned creatures, including Enforcers and their minions, do not. Higher thresholds can add levels to future natural spawns, and idle regions decay over time.

Enforcers turn high Karma into encounters. A biome or dungeon table can choose an Enforcer, minions, modifiers, level bonus, bonus loot, and location restrictions. Nearby overlapping player regions are evaluated as one connected check region to avoid duplicate rolls for the same group.

Enforcer behavior can be used together with Karma levels or independently through BepInEx configuration. Current Karma appears near the minimap, and Enforcer events use localized center-screen messages.

Karma is currently stored in server memory and resets when the server process restarts.

## Common Workflows

**Tune an existing creature**

Find it in `creatures.reference.yml`, copy its identity into `creatures.yml`, and add only the fields being changed.

**Build a customizable humanoid NPC or creature**

Generated references compact repeated prefab entries into this weighted notation while preserving their first-occurrence order. The generated `usedByAttacks` field is informational and is ignored in active configuration. Component blocks already identify their kind, so no `type` field is generated or accepted.

Install MonsterlabZ and refer to samples in creatures.sample.yml and attacks.sample.yml. Change those files name into creatures_sample.yml and attacks_sample.yml, respectively.


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

The spawn command supports prefab and comma-separated modifier completion. It is intended for testing and does not perform a normal level or modifier roll. `cm:spawn` and `cm:karma` are available to the local listen-server host and connected dedicated-server administrators; a dedicated-server console has no player position to target.

## Git
https://github.com/sighsorry1029/CreatureManager
