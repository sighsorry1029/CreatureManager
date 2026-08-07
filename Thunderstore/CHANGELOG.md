# Changelog

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
