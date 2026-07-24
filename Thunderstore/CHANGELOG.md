# Changelog

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
