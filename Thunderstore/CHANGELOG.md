# Changelog

## 1.2.2

- Add the server-synchronized `Adjust EpicMMO LevelBar Position` switch under `1 - General`, On by default. Clients with WackyEpicMMOSystem position regular creature level labels at `(70, -15)`, including labels recreated by EpicMMO's fallback path.
- Apply the display override independently of CreatureManager's level and modifier settings without editing EpicMMO's configuration. The displayed position takes priority over EpicMMO's configured coordinates; Off restores the position recorded before adjustment. Boss, player and mount HUDs are unchanged, and dedicated servers do not adjust UI.
- Cache label lookups, release departed HUD entries, and restore positions on live disable, HUD destruction, world teardown and plugin shutdown. Add isolated regression checks and original EpicMMOSystem 1.9.70 binary contract inspection.

## 1.2.1

- Reuse temporary Karma blocker buffers and scoped spawn-query state, and share prefab classifications within each nearby search to reduce repeated allocations and lookups. Preserve live boss/Enforcer checks, spawn exclusions, and pending summon rules.
- Add dungeon-only Blink collision and destination checks. Stop before solid obstacles when a safe position remains, or cancel unsafe Blinks, with clearance checks for creature size and nearby walkable ground for grounded creatures. Outdoor Blink behavior remains unchanged.
- Increase the default Dungeon Enforcer Spawn Delay from 5 to **7 seconds** and replace the English/Korean countdown notice with an in-world warning. Existing saved delay values are preserved.
- Ship all 11 sample PNG textures under `BepInEx/config/CreatureManager/textures/` with `cm_` filename prefixes, update the creature sample, and remove embedded sample PNGs and the `Generate Sample Textures` option.
- Install the package's `BepInEx` folder along with the DLL, including on servers supplying textures to clients. Existing YAML is not rewritten: update old texture references such as `boar2` to `cm_boar2`, or retain their matching old PNG files. Use separate filenames for personal replacements so package updates do not overwrite them.

## 1.2.0

- Add independent, server-synchronized switches to block nearby ordinary spawns while a boss or Enforcer is active, both On by default. Cover SpawnSystem, SpawnArea, and CreatureSpawner within the game's near-loaded sectors while preserving raids, boss prefabs, direct summons, and Enforcer encounters. These switches work independently of Karma and level settings.
- Improve server-side boss discovery so existing and remotely simulated bosses can enforce the configured Karma-gain and Enforcer-summon blocks, including KarmaLevelOnly and Vanilla level mode. Preserve the regional/interior boundaries, existing Karma level bonuses, and Blamer's separate behavior.
- Replace the Global, Boss, and Enforcer modifier switches with independent Max4/Max3/Max2/Max1/Off choices. Max4 remains the default and old On values read as Max4. Limits affect new automatic rolls only; existing, inherited, restored, and explicitly forced modifiers are not trimmed. Off continues to disable effects and icons without deleting saved modifiers.
- Add Dungeon Enforcer Spawn Delay (s), defaulting to **5 seconds** with a 0-30 range. Warn players before dungeon Enforcers and their minions arrive together at a reserved position. Cancel invalid reservations without charging Karma or starting cooldown; outdoor encounters and a zero delay remain immediate. Existing saved settings are preserved.
- Add Reflection Damage Cap, defaulting to **25 health per activation from one creature**; 0 means unlimited. Apply the cap after validating and consuming the original damage evidence, and show it in the localized modifier description.
- Include three DeepNorth encounters in newly generated karma.yml: Barka outdoors, ElakingMole with Elaking in TheHole01, and JotunWarrior with BlobMork in MorkBorg. Each uses a +2 level bonus, required/consumed Karma of 40/30, and guaranteed bonus loot. Existing YAML files are not overwritten.
- Simplify texture-transfer bookkeeping and faction snapshot publication while preserving transfer retry/cleanup behavior, configuration validation, and public faction lookup contracts.
- Extend managed regression checks for boss discovery, modifier limits, reflection caps, delayed summons, nearby spawn blocking, texture transfers, and faction reloads. Verify original Valheim 1.0.16 client/server references and DropNSpawn/Drop That patch composition; actual game and multiplayer execution remain separately required.

## 1.1.17

- Add `Vanilla` to `Enable Level System`: keep levels assigned by the game or other mods while retaining CreatureManager stat, distance, scale, and modifier rules. Skip CM level rolls and Karma/Enforcer level bonuses; preserve the default `On`, existing `On`/`Off` values, explicit spawn-command levels, and completed creature state.
- Keep modifiers independently controlled by `Global Modifiers`, `Boss Modifiers`, and `Enforcer Modifiers`; selecting `Vanilla` does not disable them. Track level-one creatures so later external level changes refresh health and damage scaling without compounding stats or healing existing damage.
- Use the prefab's original level sizes in `Vanilla` mode when no matching `scalePerLevel` rule exists. Explicit zero and positive values keep their existing meaning, and Boss defaults remain unchanged. New YAML files comment out Global `scalePerLevel: 0.1`; this also leaves Global star-size growth disabled by default in `On`. Existing YAML files and stored modifier sizes are not rewritten.
- Keep Global `damagePerLevel` at 0.25 and explain in YAML comments, config help, and the README that vanilla uses 0.5. `Vanilla` level assignment continues to use the configured stat values rather than switching all balance settings to vanilla.
- Add managed regression checks for mode serialization, level assignment and adoption, ownership, health preservation, prefab sizing, explicit scale overrides, and generated YAML defaults. Actual game and multiplayer execution remain separately required.

## 1.1.16

- Protect `FrozenKing_p2` from CreatureManager level/stat scaling, direct health/regen overrides, and all combat modifiers so its seven-Aspect encounter can complete through the game's normal phase-three transition. Phase 1, phase 3, and the Aspects retain their existing rules.
- Preserve incoming encounter damage and block modifier inheritance/restoration on phase two, including Reaping stat restoration and Deathward lethal prevention. `cm:spawn` permits this prefab only at level 1 without modifiers. Existing altered encounters are not repaired or migrated.
- Add managed regression checks for phase identification, spawn policies, health overrides, modifier application, seven unscaled hits, and command ownership. Actual boss fights and multiplayer execution remain separately required.
- Require BepInExPack Valheim 5.4.2351.

## 1.1.15

- Add synchronized `Character Loot System` settings with `CalculateChance` as the default and separate creature/boss percentages, both defaulting to 50% per star. Eligible drops retain their base chance and gain linear amounts with probabilistic rounding instead of exponential star scaling. Existing saved settings are preserved; select `Vanilla` to keep the existing calculation.
- Automatically defer loot scaling to DropNSpawn when installed. Keep CalculateChance available alongside Drop That while preserving drop-entry identity and conditions; entries with `ScaleByLevel=false`, trophies, one-per-player rewards, and explicit Enforcer bonus loot are excluded from CreatureManager's calculation.
- Share AI boolean parsing between YAML validation and application while preserving accepted aliases and the separate strict character/boss boolean rules.
- Unify common creature/boss HUD rendering without changing their layouts, and remove unused Sprite state from Compendium text entries.
- Add loot and AI parser regression checks, including managed Drop That patch-composition checks against original Valheim 1.0.15 client and dedicated-server assemblies. Actual world and multiplayer execution remain separately required.

## 1.1.14

- Preserve the game's current saved creature level when reloading already processed creatures, preventing stale initial CreatureManager levels from undoing FeedLikeGrandma feeding upgrades after zone unload/reload.
- Preserve intentional level decreases and existing modifier, health/damage multiplier, and visual restoration without rerolling initial levels.
- Add regression checks for saved-level restoration, stale initial assignments, missing/invalid saved levels, and unchanged persisted state, alongside the client and dedicated-server compatibility checks.

## 1.1.13

- Add compatibility with Valheim 1.0.7 across console commands, creature appearance hashes, ZDO data access, zone queries, spirit damage, and guaranteed Enforcer loot while preserving existing configuration, snapshot, authority, ownership, and duplicate-drop contracts.
- Update the embedded ServerSync library for Valheim 1.0.7, including authoritative admin checks and safe buffering of the new player-history and admin-list messages during initial configuration synchronization.
- Build against the installed original game assemblies with isolated compile-time access copies, preventing stale publicized references from hiding game API changes without modifying or packaging Valheim DLLs.
- Remove the obsolete `Humanoid.OnStopMoving` compatibility transpiler now that Valheim performs the correct current-attack null check, and add client/dedicated-server compatibility checks for game references, Harmony targets, transpilers, private access paths, and serialized state contracts.
- Require BepInExPack Valheim 5.4.2350.

## 1.1.12

- Reuse unchanged resistance HUD text and prefixed localization tokens to reduce allocations while continuing to reflect live resistance and translation changes.
- Skip server texture-transfer history scans until an entry can expire, preserving duplicate-request suppression and transfer behavior.
- Remove unused Compendium description calculations during icon placement, reuse visual-state cleanup, and simplify Karma configuration staging and repeated player checks.
- Add opt-in `DeployToGame=true` builds that copy only the final merged plugin DLL into the game's BepInEx plugins directory.

## 1.1.11

- Deliver every configured Enforcer `loot` reward once from the owning peer at death instead of mutating the creature's `CharacterDrop` table, preventing DropNSpawn table replacement and custom death handling from discarding guaranteed rewards while retaining its global stack behavior.

## 1.1.10

- Fix natural and Omen-triggered dungeon Enforcers on dedicated servers by using nearby server-owned CreatureSpawner or SpawnArea ZDOs when the remote dungeon scene is unavailable, while retaining full floor, clearance, and path validation in loaded scenes.
- Align Karma realms, administrator spawn placement, and dungeon level/Modifier scale handling with Valheim's `Character.InInterior` boundary so lower interior layers no longer fall through to outdoor Karma and spawn rules.

## 1.1.9

- Fix cached synchronized texture generations being committed successfully but reported as failed, ensuring the texture-ready refresh reapplies server PNGs to creature and ragdoll prefabs.
- Suppress only the transient missing-texture warnings for server-manifest PNGs during the initial remote definition pass; genuine missing files, resource names, decode failures, and post-sync misses remain visible.

## 1.1.8

- Synchronize server-owned, actively referenced creature PNG overrides through validated SHA-256 manifests, bounded chunk transfers, persistent client caching, and transactional last-known-good activation; hot reload reuses live texture objects while unsafe paths and invalid images are rejected.
- Make persisted Enforcer discovery incremental on server startup and simplify level, modifier, clone, baseline, reference, faction, appearance, and localization runtime paths to reduce repeated work and failure scope.
- Narrow the public modifier transport snapshot to modifier-owned state and require the new v2 format, removing v1 snapshot migration along with JSON localization compatibility.
- Streamline generated `levels.yml` and `karma.yml` guidance, leave `Boss.level` omitted by default so regular bosses can follow the enabled biome preset, retry failed server-localization watchers, and verify all 32 modifier icons in the contract checker.

## 1.1.7

- Discover modded creature attacks directly from loaded Humanoid loadouts and inspect animator controllers with a lightweight Animator-only probe, allowing non-networked attack holders into generated references without instantiating complete creature prefabs.
- Add weighted `projectile.randomSpawnOnHit` and `randomSpawnOnHitCount` reference and override support, including inheritance, explicit clear and disable semantics, validation, baseline restoration, and attack usage metadata.
- Base Fire, Frost, Lightning, and Spirit added damage and Juggernaut eligibility only on channels the target does not Ignore or Immune, respecting weak spots and current damage modifiers so ineffective damage cannot become effective elemental damage or knockback.

## 1.1.6

- Separate outdoor and dungeon Karma neighborhoods, including Karma values, level bonuses, decay, Enforcer consumption and cooldowns, blocker checks, Omen summons, HUD status, and administrator commands, while retaining the shared thresholds and gain rules.

## 1.1.5

- Added the versioned `CreatureManagerModifierApi` for lossless modifier-state capture and restoration by transport mods, including stored powers, cooldowns, accumulated runtime state, cache refresh, and validation before replacement.

## 1.1.4

- Rename the Compendium page to the localized `Combat Modifiers` / `전투 특성` title and add a localized opening hint explaining how to inspect creature weaknesses, resistances, and immunities while sneaking.

## 1.1.3

- Prune Karma level bonus requests whose pending client creature was destroyed or unloaded, preventing dungeon-generation bursts from leaving permanent `Still waiting` diagnostics and stale retry bookkeeping.
- Log server summaries containing only transient `zdoMissing` outcomes at debug level, while retaining warnings for other request failures, accepted anomalies, and prolonged client-side waits.

## 1.1.2

- Make dedicated-server Karma level bonus responses use authoritative server ZDO position and Enforcer state without requiring requester ownership or a loaded creature prefab, while keeping client application owner-authoritative.
- Back off unanswered Karma bonus requests to 30 seconds and aggregate client/server diagnostics, preventing per-creature warning spam while preserving pending level application until an authoritative response arrives.
- Add configurable Enforcer abandonment cleanup: remove the main Enforcer after no living player remains within the fixed 64 m encounter range, retain summoned minions, and restore persisted Enforcer tracking from server ZDOs after restart.
- Cap Regenerating healing at 20 health per second by default, support `0` as unlimited, and expose the effective cap in generated YAML, Compendium text, translations, and synchronized modifier state.
- Allow every modifier tuple to omit a trailing suffix after its required chance value, inheriting omitted fields before using runtime defaults, while rejecting ambiguous empty positions and documenting the rule.

## 1.1.1

- Preserve explicit creature levels supplied by vanilla spawn commands, World Edit Commands, and Server Devcommands on dedicated servers instead of rerolling them through CreatureManager level rules.
- Make configured `damagePerLevel` replace Valheim's vanilla 50% monster damage growth per level against character targets, while leaving vanilla growth intact when `damagePerLevel` is omitted.
- Persist and reapply the selected damage-growth mode across ownership and runtime state restoration, and clarify the generated `levels.yml` and README examples.
- Apply Disruptive stamina and Eitr recovery reductions through direct resource adjustments instead of resource-use calls, avoiding unintended usage side effects while preserving the configured recovery reduction.

## 1.1.0

- Apply enabled `ai.yml` definitions directly to same-name loaded `MonsterAI` or `AnimalAI` creature prefabs; rename an AI to a unique non-prefab name to keep it preset-only, while explicit creature-level `ai:` assignments remain the higher-priority choice.
- Preflight and track same-name AI targets through the existing transactional baseline and clone-determinism safeguards, and avoid claiming every AI field when a definition uses its own prefab as the implicit baseline.
- Clarify in generated AI and reference headers and the README that original prefabs receive same-name overrides without a creature entry, while `clonedFrom` creatures retain the source baseline AI unless a clone-level same-name definition or explicit `ai:` assignment selects configured AI.

## 1.0.9

- Limit Karma-level and Enforcer spawn/death center messages to connected, living players in the affected Karma region instead of broadcasting them to the entire world, including authoritative dedicated-server targeting and listen-host deduplication.
- Keep dungeon Enforcers inside usable dungeon space by rejecting surface and unrelated vertical-layer spawners, snapping candidates to nearby floors, validating prefab-specific full paths, and using a same-room line-of-sight fallback while NavMesh data is cold.
- Validate boss and minion capsule clearance with bounded retries, skip only minions that have no safe position, refresh dungeon spawner caches for Omen summons, and avoid consuming Karma or starting cooldown when no safe boss position exists.

## 1.0.8

- Fix startup with Norsemen and other mods that extend `Character.Faction` at runtime by pairing injected enum names with their actual values instead of passing them back through `Enum.Parse`.
- Resolve external faction names and IDs without taking ownership of their behavior; unmanaged factions now retain their original aggravatable and hostility logic unless they are explicitly defined in `factions.yml`.
- Reject conflicting runtime faction IDs while keeping CreatureManager-managed vanilla and custom faction relationships unchanged.

## 1.0.7

- Make synchronized YAML reloads transactional and last-known-good: reject invalid faction relationships, duplicate or multi-document mappings, broken clone graphs, stale clone references, and missing required prefab components before publishing, then restore prefab, faction, and clone state if application fails.
- Make live edits deterministic: loaded creatures retain their creature, AI, attack, level, and modifier state, while newly instantiated creatures, projectiles, and ragdolls use current templates; live faction, Karma, localization, and shared texture rules remain immediate, and changing or unsafely hot-adding `clonedFrom` requires a restart.
- Preserve stored level health multipliers and missing health through reload, ownership, and `SetLevel` paths; stop blocked Enforcer checks from extending cooldown, and pause Blamer flee/icon behavior at the regional Karma cap without consuming the modifier.
- Bound server state and modifier traffic: limit each Enforcer candidate to 16 minions and 64 loot items, cap `cm:spawn` levels at 100, prune bounded Karma sector state, authorize Reaping only from observed deaths, limit Reflection/Reaping queues, and rate-limit Vortex/Juggernaut network effects.
- Centralize and validate all 32 modifier definitions and icons, add reproducible exact-icon checks, keep Compendium icon links stable, and preserve loadout ordering and duplicate weight entries in generated references.
- Improve partial-startup and shutdown cleanup, and automatically rebuild a failed configuration file watcher.

## 1.0.6

- Remove runtime assembly loading for `UnityEngine.ImageConversionModule`; PNG decoding now resolves `ImageConversion.LoadImage` through reflection from Unity's existing compile-time type reference.
- Restrict optional compatibility type discovery to assemblies already loaded by the game and keep `UnityEngine.ImageConversionModule.dll` out of the release package.

## 1.0.5

- Fix dedicated-server `cm:spawn` and `cm:karma` execution for vanilla admins and Server Devcommands permissions by handling commands after authentication and resolving the invoking player from authoritative peer ZDO state.
- Make periodic Enforcer checks and Omen summons use connected-player ZDO positions, restore boss and Enforcer blocker tracking across headless regions and reloads, and deliver Karma and Enforcer center messages to remote clients.
- Move Blamer Karma grants to a server-validated routed RPC so client-owned creatures can contribute Karma while fleeing, consuming their Blamer budget only when regional Karma actually increases.
- Apply biome presets, `levels.yml` prefab/group overrides, health, damage, scale, and additive Karma levels on the creature's owning peer while retrieving the Karma bonus from the server; modifiers now roll only after level processing completes.
- Harden configuration reload, ownership transfer, delayed RPC, death, and retry paths so synchronized YAML and level state cannot be finalized from stale or incomplete data.

## 1.0.4

- Add the server-synchronized `Blink Alert Grace Period (s)` option (0-10s, default 3s), delaying Blink and its extended attack range after alert while letting the grace period expire even when no attack can start; 0 restores immediate Blink behavior.
- Track Blink alert transitions owner-authoritatively by network time, reapply the grace period only after a creature calms and becomes alerted again, and prevent repeated alert calls or failed attacks from indefinitely suppressing Blink.
- Reduce Blink's default maximum range from 24m to 16m across Global, Boss, Enforcer, examples, and the runtime fallback.
- Improve the 17 px Blink icon with a cyan arrow and violet portal while removing the two decorative sparks, and update the English, Korean, Compendium, and README descriptions.

## 1.0.3

- Rework normal-creature and boss level HUDs with fixed 17 px stars and modifier icons: keep stars at the health-bar lower left, modifiers at the lower right, allow the blocks to overlap, and align their visible edges optically.
- Preserve individual one- and two-star displays, compact higher levels to a star plus count, and add a fallback star when a HUD has no usable vanilla star artwork.
- Show every forced modifier icon up to the four-modifier limit, including multiple modifiers from the same category, in both `FixedCategorySlots` and `RightPacked` layouts.
- Improve 17 px readability of the Armored, Omen, Spirit, Undodgeable, and Unflinching artwork, and keep hover resistance text below the expanded HUD content.
- Keep the Karma minimap label upright with rotating ZenCompass configurations by attaching it to a stable small-map root.
- Reduce steady-state modifier HUD allocations and redundant layout work.
- Clarify in the Biome Level Preset setting that `levels.yml` contains copyable preset biome distributions.

## 1.0.2

- Add dedicated-server remote-admin support for `cm:spawn` and `cm:karma`, using the invoking admin's active player and returning results to their console.
- Fix server-validated Karma credit for client-owned creature deaths, including the race where `DestroyZDO` arrives before the final health sync.
- Fix Blamer flee and icon state on remote-owned creatures and add rate-limited diagnostics for rejected Karma requests.
- Extend Omen to unambiguously player-attributed poison, fire, and spirit damage-over-time kills, target the actual connected killer, exclude the dying creature from blocker checks, and report summon rejection reasons.
- Improve shared direct and delayed death attribution used by Karma, Omen, and Reaping, and update the English and Korean descriptions.

## 1.0.1

- Skip PNG decoding and renderer material texture work on headless servers while preserving ragdoll, scale, and appearance processing.
- Fix the `Humanoid.OnStopMoving` current-attack null guard that could cause a `NullReferenceException`.
- Add cooldown control for Omen-triggered Enforcers and refine Karma and Enforcer defaults.
- Declare incompatibility with CLLC, Star Level System, MonsterDB, and Monster Modifiers.
- Improve generated level configuration guidance and the default `TentaRoot` modifier exclusion.
- Generate only the Thunderstore release archive directly under `Thunderstore`.

## 1.0.0

- Initial release
