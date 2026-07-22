# Changelog

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
