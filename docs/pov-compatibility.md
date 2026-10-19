# CreatureManager / Path of Valheiman interoperability

Gameplay integration released in CreatureManager **1.3.7**; the combined HUD and automatic modifier overlap filter are included in **1.3.8**, 2026-10-19. The earlier 1.3.6 release record remains available in
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
- Combined HUD: PoV's active modifier reader, ordered icon catalog, sprite accessor,
  `RarityHud.Icons` and its caller, plus `AilmentHudBridge.Draw`'s update call and
  the status view's hierarchy. CM takes over only when its own row is ready.

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

## Automatic modifier overlap filter

Before a new automatic CM modifier roll, the network owner reads PoV's active
modifier mask once and removes the corresponding CM candidates:

| Active PoV modifier | Excluded CM candidate |
| --- | --- |
| Swift | Swift |
| Furious | Attack Speed |
| Armored | Armored |
| Hardy | Regenerating |
| Greedy | Vampiric |
| Steadfast | Unflinching |
| Piercing | Armor Piercing |
| Burning | Fire |
| Chilling | Frost |
| Shocking | Lightning |
| Blink | Blink, outdoors only |
| Mirror | Reflection |
| Rotten | Toxic Death |

This is a gameplay policy to avoid overlapping concepts, not a claim that paired
effects have identical formulas. In particular, Mirror reflects ranged hits and
Reflection reacts to melee hits; Rotten leaves a poison pool while Toxic Death
applies poison on death. CM Blink remains eligible indoors because PoV 5.0.10's
landing check rejects interiors. Other effects, including Vortex, Deathward,
Adaptive and Chameleon, stay eligible.

The reader uses `RarityMods.On(Character)`, including PoV's live rarity switch
and ordinary/boss-specific disabled-effect lists. A stored but inactive PoV
effect does not exclude a CM candidate. Named enum flags and the reader signature
are resolved once at startup, independently of the HUD integration and release
number. An unsupported contract or a reader exception disables only this filter
and retains normal CM rolls. No config keys, save fields or RPCs are added.

CM's existing configuration, owner, applicability and maximum-count checks stay
in force. Pending CM-managed PoV boss grants complete before candidate selection.
Ordinary creatures and Enforcers keep their existing Awake/SetLevel/spawn order;
the filter does not call PoV Sync/Fill again. There is no new per-frame scan.

Excluded candidates contribute no weight to either pass of the existing group
selector. Remaining weights below 100 still leave a chance of no effect; totals
above 100 keep their relative weights. Empty groups consume no random draw.
Normal YAML chances, including 100%, remain subject to this filter. The maximum
modifier count is a ceiling, not a target that must be filled.

Existing saved/assigned rolls and explicit admin-forced modifiers are unchanged.
The policy is a snapshot at the first automatic roll, not lifelong mutual
exclusion: later PoV Heir inheritance, manual changes, level changes or live
settings do not remove, replace or backfill CM effects already assigned. No
periodic reconciliation, effect rollback or migration is introduced.

## Automatic combined modifier HUD

CM controls the icon/star layout when this optional contract is available.
PoV effects precede CM effects in one right-aligned list, with four icons per row.
Four/eight/twelve effects occupy one/two/three rows; partial last rows also align
right. No blank category slots or six-icon cutoff remain. Stars are separate,
on the left of the first row, using CM's one/two stars or star-plus-number style.

PoV's `Modifier display` does not control these icons or stars; no setting is
rewritten. PoV names and the actual effect activation rules remain unchanged.
CM's `Modifier HUD Icon Layout` is retained for standalone/fallback HUDs. The
combined display works with Full, ExceptLevel and Off; Off still does not roll
or display CM modifiers. Mount/player HUDs retain their existing behavior.
Another recognized level provider prevents this automatic takeover.

PoV still creates, updates and destroys its own ailment/status display. After
PoV applies the current status values, CM positions its visible icons in separate
right-aligned rows of four below the actual modifier/star block. They use the
same 17-unit icon size, independently of PoV's icon-scale setting; accumulation
rings, colours and counters remain intact. PoV status visibility remains effective.
CM resistance text follows visible statuses, including Impale/Corrosion numbers;
absent statuses reserve no empty row. PoV's fallback status display when its
assets cannot load is unchanged. Stars stay at the left edge of the health bar;
there is no separate left-hand status column.

The adapter reads active PoV effects at most every 0.25 seconds per displayed HUD.
It resolves reflection once at startup and reuses icon lists/objects. Destroyed
sprites are retried; PoV owns their assets. Per-HUD state is weakly keyed and
cleared on world teardown, and surviving named CM rows are reused after a reset.
Normal frames neither create icons nor force a default-height/expanded-height
layout cycle. Actual performance has not been measured.

If the contract is unsupported, the HUD integration is disabled independently of
HP/loot/boss compatibility. Before a CM row is ready, PoV can draw its original
row. CM never changes the gameplay `ForeignLevels` getter to choose a display.

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
- Debug and Release 1.3.8 builds completed with zero errors/warnings. The combined
  HUD and overlap filter passed original client standalone and PoV 5.0.10
  client/dedicated-server managed checks against the final merged Release DLL,
  with zero failures. YAML sync checks and all seven documented default templates
  also passed. The Debug DLL matches the locally deployed DLL; the Release ZIP
  passed CRC, version/dependency and all 17 packaged-file content checks.
  These checks do not create a Unity canvas or network session.
- The overlap filter is covered by original PoV active-mask reads with live
  ordinary/boss disable lists, all named effect mappings, the dungeon Blink
  exception, actual CM weighted selection with deterministic RNG, empty groups,
  existing-roll and owner/config/Off gates, forced-path exclusion and pending boss
  call order. Native prefab applicability and interior detection are fixtures.
- Tests exercise normal startup with a different plugin release label, reject
  broken state guards/counter/provider contracts, and check the existing HP/loot
  behavior. Boss tests run installed ZNetView hooks with substituted engine ZDO
  creation, and check fresh vs restored objects without relying on an altar roll,
  altar coexistence, deferred final stars, zero stars/count, first-kill/disabled/FrozenKing exclusions,
  preexisting modifiers, config/owner waits, persistent state on a new Character,
  repeated calls, reentry and partial foreign failure. The production pending
  loop is also checked through owner transfer and CM Off cancellation. HUD tests for the combined adapter check preserved original status calls,
  invalid-contract rejection, all seven PoV display modes, throttled live reads,
  zero/one/four/five/eight/nine/twelve and larger effect counts, independent right alignment, star-only height and unchanged
  gameplay level ownership across Full/ExceptLevel/Off. Cleanup and
  other-provider fallback are checked as well.
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
normal boss roll behavior. For the combined HUD, verify ordinary creatures,
bosses and Enforcers with 0/1/2/3/6+ stars and 0/1/4/5/8/9/12 effects. Switching
PoV Modifier display (including Nothing) must not change the shared icons/stars.
Check live effect removal, status visibility and fixed-size wrapping, Impale/Corrosion numbers,
resistance text, PoV stagger bars, custom HUD anchors, mount transitions and
leave/rejoin. Automated checks do not render a Unity canvas.

For overlap filtering, test new ordinary creatures, altar/direct bosses and
Enforcers on a host and dedicated server with owner transfer. Verify active
Mirror/Rotten exclusions, dungeon Blink eligibility, PoV disabled-effect lists,
saved/forced CM rolls and later Heir changes. Automated checks do not execute
native spawn lifecycles, multiplayer transport or combat effects.

PoV 5.0.10 adds an ExtendedBosses caller exemption and repairs NaN, infinite or
greater-than-100,000,000 maximum HP through prefab health setup and a full heal.
Those PoV policies are unchanged by this patch. This is not a diagnosis or a
guaranteed fix of the earlier infinite boss-HP report with an unknown mod list.
