# CreatureManager / Path of Valheiman interoperability

Released in CreatureManager **1.3.7**, 2026-10-19. The earlier 1.3.6 release record remains available in
[the 5.0.7 review](pov-5.0.7-compatibility.md).

## Activation and scope

CM no longer requires an exact PoV release number. After plugin loading it checks
the signatures, state types and relevant original IL of the methods it uses:

- Final HP: the scaled maximum-health setter, thread-local nesting/exception
  cleanup, and per-character pending current-health conversion.
- Live level ownership: the recognized level providers in `ForeignLevels`.
- Star loot: entry initializes an empty `__state`, and Postfix/Finalizer return
  without side effects for that empty state, preserving the original exception.
- Full boss modifiers: the fresh/restored ZNetView boundary, stored modifier key,
  configuration types and modifier fill entry.
- Star display: the two foreign-level/foreign-star calls inside `RarityHud.Icons`
  and its enemy-HUD caller. Only those display call sites are replaced.

Each integration owns its cleanup. A changed contract produces a warning and
disables that integration; the boss integration additionally needs the live
level-ownership integration. Checks and reflection resolution run during startup,
not on each attack or frame. This checks the boundaries CM relies on; it cannot
prove every future PoV release has unchanged semantics.

The existing raw/final HP distinction, Reaping restoration, live CM mode handling,
and CM/DNS/PoV loot priority are retained. PoV remains an optional dependency.
Its DLL is not modified or redistributed. No YAML/config keys or defaults change.

## Full-mode boss modifiers

A fresh, owned, non-ghost ZNetView with an `IsBoss()` Character reserves a grant.
This covers altar, CreatureSpawner, command and direct prefab instantiation;
it no longer depends on `BossStars.Roll`. The prefix captures `m_initZDO` before
Awake consumes it, distinguishing creation from loading a saved/remote ZDO even
when that old ZDO has no CM or PoV keys. Only the Character's own view is used.
Once CM finishes the boss's level and stats, the current network owner requests:

`max(0, final level - 1) * PoV Boss modifiers per star`

PoV performs its own random modifier selection and boss exclusions. `Boss stars`,
`Difficulty system`, `Creature rarity enabled`, first-kill restrictions and API
`LeaveAlone` are respected. `FrozenKing_p2` is excluded. Normal's default zero
modifiers per star still means no modifiers; use 1 to opt into one per star.
CM modifiers are independent and can be disabled separately.

There is no second star roll, forced full heal or call to `BossStars.Give`.
Before invoking PoV, CM skips bosses with any stored `pt.crmod` flags, including
effects hidden by PoV settings, because `Fill` can remove existing effects.

The additive ZDO bool `CreatureManager_PovBossModifiersPending` records only a
new grant waiting for CM/config readiness or ownership. Existing pending-level
processing handles it; no new global character scan or Update loop is added.
The owner consumes it before invoking PoV, preventing duplicate/reentrant grants.
PoV settings becoming disabled or CM leaving Full cancel a grant when processed
by its owner. Non-owners keep waiting rather than modifying another owner's ZDO.
Destroy/world cleanup uses CM's existing pending queue cleanup. Existing saved
bosses without the marker are unchanged; no migration is performed. Growup and
tamed-restore paths that preserve previous rolls consume the reservation without
adding effects. The prefab must already report `IsBoss()` during network creation;
later promotion of a normal creature to boss by another mod is outside this hook.
Characters using another GameObject's override network view are also excluded.

If PoV throws after partially applying modifiers, CM does not retry that grant.
It reports the error once and stops further boss grants until restart. Other
integrations remain active. PoV's effects may themselves change health, shields,
movement or damage; enabling their modifiers intentionally retains those effects.

## Automatic PoV star display

When its HUD contract is available, PoV automatically owns star presentation.
There is no new CM config entry. Each client uses PoV's existing **5. Controls →
Modifier display** setting, independently of server level/stat policy. CM stops
drawing only its star/number block and retains its modifier icons, Enforcer names,
Blamer indicator and resistance display. CM runs before PoV's HUD postfix.

Only `RarityHud.Icons`' two foreign-display checks are bypassed. The shared
`CreatureStars.ForeignLevels` getter remains unchanged for health, damage, star
rolls and loot. PoV reads the actual `Character.GetLevel() - 1`. Its low-star
fallback to vanilla stars, five-star row limit/compact number and `Nothing` mode
are retained. `Nothing` may hide higher-star or boss indicators; choose
`ColouredStars` or `VanillaStars` if explicit stars are wanted. This presentation
works independently of PoV's difficulty/rarity switch and CM's level mode.

Missing PoV, a rejected HUD contract, another recognized level provider, or a
custom HUD missing PoV's required name/anchors retains CM's existing display.
No new UI search, cache, timer or per-frame reflection is added. The original
PoV HUD still owns its row cache and setting-change cleanup. Actual UI layout,
colors, template/sprite changes by other HUD mods and icon overlap need game
verification.

## Inputs and validation

- Supplied unmodified PoV plugin **5.0.10** (assembly version remains 1.0.0.0):
  SHA-256 `252B60914EFC9F402A977A8C235E28BCB363D7BB7CEEBDF5F22A228518282E74`.
- Original Valheim 1.0.20 client 25909357 and dedicated-server 25909431 DLLs and
  Mono runtimes. CM's existing build-time publicizer configuration is unchanged;
  runtime checks use the original game DLLs. New access to the protected
  `Character.m_nview` field uses a cached Harmony field accessor.
- Baseline and updated Debug builds, final DLL merge/deployment, and managed
  compatibility checks are performed separately from actual game execution.
- Debug and Release 1.3.7 builds completed with zero errors/warnings. The final
  merged Release DLL passed the standalone client, PoV 5.0.10 client/server,
  and PoV 5.0.9 client managed checks with zero failures. The Debug DLL matches
  the locally deployed DLL. The Release ZIP passed CRC, version/dependency and
  all 17 packaged-file content checks, including the DLL and bundled textures.
- Tests exercise normal startup with a different plugin release label, reject
  broken state guards/counter/provider contracts, and check the existing HP/loot
  behavior. Boss tests run installed ZNetView hooks with substituted engine ZDO
  creation, and check fresh vs restored objects without relying on an altar roll,
  altar coexistence, deferred final stars, zero stars/count, first-kill/disabled/FrozenKing exclusions,
  preexisting modifiers, config/owner waits, persistent state on a new Character,
  repeated calls, reentry and partial foreign failure. The production pending
  loop is also checked through owner transfer and CM Off cancellation. HUD tests
  check the two-call-site replacement, invalid-contract rejection, automatic
  preference, actual star counts and unchanged gameplay level ownership across
  Full/ExceptLevel/Off; cleanup and other-provider fallback are checked as well.
- Unity, scene/transport, PoV random selection and FX are substituted in the
  managed fixtures. Native-call diagnostics from standalone Mono are not game
  execution results. No host, dedicated multiplayer session or boss encounter
  has been run for this change.

Run `tools/GameCompatibilityCheck/run_mono.py` with `CREATUREMANAGER_POV_DLL`
pointing to the original PoV DLL; omit it for standalone CM checks. Use the
original client/server directory for the corresponding runtime.

## Remaining game checks and unchanged PoV behavior

Use matching CM builds on server and clients. Summon new bosses through altars,
CreatureSpawner and direct/command creation at zero and multiple stars,
enable/disable PoV boss modifiers, verify first-kill restrictions,
save/rejoin and transfer ownership while initialization is pending. Check actual
PoV selection, exclusions, shields/FX and wound preservation, plus Reaping and
loot with CM CalculateChance, Vanilla and DNS. CM Off/ExceptLevel retain PoV's
normal boss roll behavior. On a client, switch PoV Modifier display between
Icons, ColouredStars, VanillaStars and Nothing with ordinary creatures, bosses,
Enforcers and CM icons enabled. Check 0/1/2/3/6+ stars, leave/rejoin and custom HUDs.

PoV 5.0.10 adds an ExtendedBosses caller exemption and repairs NaN, infinite or
greater-than-100,000,000 maximum HP through prefab health setup and a full heal.
Those PoV policies are unchanged by this patch. This is not a diagnosis or a
guaranteed fix of the earlier infinite boss-HP report with an unknown mod list.
