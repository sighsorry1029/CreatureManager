# Changelog

## 1.0.4

- Add the server-synchronized `Blink Skips First Attack` option, enabled by default, so a creature's first successful hostile attack does not teleport or use Blink's extended AI range; cooldown still begins only after an actual Blink.
- Keep Blink activation and first-attack tracking owner-authoritative, and exclude non-enemy utility actions from consuming the protected opening attack.
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
