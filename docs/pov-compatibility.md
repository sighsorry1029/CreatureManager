# CreatureManager / Path of Valheiman interoperability

Gameplay integration released in CreatureManager **1.3.7**; the combined HUD and automatic modifier overlap filter are included in **1.3.8**, 2026-10-19. The earlier 1.3.6 release record remains available in
[the 5.0.7 review](pov-5.0.7-compatibility.md).

CreatureManager **1.3.9** replaces Toxic Death with Toxic Aura and extends the poison
overlap filter to PoV **Reeking** as well as **Rotten**. The YAML upgrade guidance is
in the [default configuration reference](defaults/README.md); the earlier verification
records below retain their original version scope.

## PoV 6.0.7 update — CreatureManager 1.3.12, 2026-11-10

The supplied original PoV 6.0.7 DLL has SHA-256
`D6F7D6AE1D4772650E8657AF030022ED2873FE430B48FADF5A57C076FA4069E1`.
Its modifier enum is now Int64, with additional effects above bit 31. CM accepts
the recognized Int32 and Int64 contracts rather than a release/hash allowlist.
The older reader is zero-extended; the new reader uses PoV's complete stored mask,
including `pt.crmod2`. Fresh/late boss checks and blood-rune modifier budgets must
not mistake upper-bit-only effects for an empty creature or trim them. Selection
still belongs to PoV's `Fill`, including its exclusions and group policies.
The existing 14 overlap pairs and dungeon Blink exception are unchanged. New
same-name effects such as PoV Deathward are not automatically treated as CM's
self-revival effect.

PoV now infers that a same-frame max-health read followed by a foreign max-health
write is already scaled. CM also reads max health merely to preserve missing HP
before writing a **raw** target. A one-use, thread-local character scope identifies
that CM write at PoV's verified predicate call; other writers retain PoV's
heuristic. The explicit final-health API and pending-current-health cleanup remain
separate. A rejected raw integration withholds CM's raw health rewrite and does
not record it as applied; it does not disable a valid final-health integration.
The scope unwinds on exceptions and does not suppress a subsequent foreign
read-derived write in the same call's postfix. No PoV difficulty formula is copied.

`BossStars.Give` now clamps its input to PoV's configured star cap. Only CM's
captured **Full** rune replay bypasses that local clamp, preserving the documented
CM base stars plus consumed runes policy and the same rune modifier budget.
Ordinary calls and Off/ExceptLevel keep PoV's cap. A pending switch out of Full
also keeps that cap, including cap zero's intentional no-level/no-effect result.
PoV's long phase-modifier storage and fixed-health phase behavior are preserved.
No altar inventory handling, config values, or existing saved creatures are rewritten.

The combined HUD retains four columns and its existing status placement. It reads
all 64 bits and resolves either the older borrowed atlas or the new verified
embedded `enemymod` PNGs. CM lazily creates and owns one texture/sprite pair per
PNG; the bounded cache is reused across HUD/world recreation and released on
adapter removal. Borrowed PoV atlas sprites are never destroyed. Partial decode
or creation failures release CM's objects and retain native HUD fallback. PNGs
use CM's fixed bilinear rendering; this does not promise identical mipmapping to
PoV's own UI settings. Runtime DLL replacement is unsupported.

Validation used Valheim 1.0.21 original client 26249040 and dedicated-server
26266951 assemblies and Mono runtimes. The baseline Debug build and standalone
checks passed. The old PoV harness stopped at the new health heuristic's native
Unity boundary; that was a test-fixture limitation, not evidence of a game crash.
The updated Debug build and checker build have zero warnings/errors. The final
DLL passed standalone, PoV 6.0.7 client and PoV 6.0.7 server managed checks with
zero failures, plus YAML sync/parser checks. The Debug plugin was deployed to
the local Steam game's plugins directory with matching SHA-256.

Release 1.3.12 was built with zero warnings/errors. Its final merged DLL also
passed standalone and PoV 6.0.7 client/server managed checks with zero failures,
plus the Release YAML sync/parser checks. The 17-file release ZIP was checked
for CRC integrity, exact source-file contents, matching DLL/manifest version and
the BepInEx 5.4.2360 dependency. The isolated Mono runs emit native Unity internal-call
warnings because they do not start the Unity engine; their passing managed checks
do not establish graphics or game-session compatibility.

Added cases exercise the real read heuristic, raw/final HP and deficit preservation,
nested foreign writes, rejected writes and exception cleanup; 32/64-bit readers,
upper-bit-only boss effects and long phase carry; five runes with caps zero/two
across Full/Off/ExceptLevel and pending mode changes; all 46 PNG resources,
cross-HUD cache reuse and partial-failure/idempotent cleanup. Native graphics,
world/ownership inputs, PoV random effect application and transport are fixtures.
The earlier 5.3.6 original DLL is no longer available locally; its full runtime
suite was not rerun, although the Int32 reader is covered by a synthetic fixture.
Logs and input/output hashes are under `artifacts/PoV607Patch-20261110` locally.

No Unity world or actual host/dedicated multiplayer session was run. Use the same
patched CM on server and clients, then check new and saved bosses, owner transfer,
blood summons/phase changes, injured and Reaping creatures, and the combined HUD
through world exit/rejoin. These are remaining game checks, not passed tests.

## Activation and scope

CM no longer requires an exact PoV release number. After plugin loading it checks
the signatures, state types and relevant original IL of the methods it uses:

- Final HP: the scaled maximum-health setter, thread-local nesting/exception
  cleanup, and per-character pending current-health conversion.
- Raw HP: the optional read-derived heuristic, thread-local read state and caller
  guard; CM's exception is limited to its own raw write.
- Live level ownership: the recognized level providers in `ForeignLevels`.
- Star loot: entry initializes an empty `__state`, and Postfix/Finalizer return
  without side effects for that empty state, preserving the original exception.
- Full boss modifiers: the fresh/restored ZNetView boundary, stored modifier key,
  configuration types and modifier fill entry.
- Full blood runes: the owner gate, stored star keys, level assignment, rune
  modifier budget, health call order and phase carry assignment. The active
  Harmony patch set is rechecked before capturing/replaying a rune or phase.
- Combined HUD: PoV's active modifier reader, ordered icon catalog, sprite accessor,
  `RarityHud.Icons` and its caller, plus `AilmentHudBridge.Draw`'s update call and
  the status view's hierarchy. CM takes over only when its own row is ready.

Each integration owns its cleanup. A changed contract produces a warning and
disables that integration; the boss integration additionally needs the live
level-ownership integration. IL checks and reflection resolution run during startup;
the blood bridge rechecks its cached patch registration at work boundaries, not
on each attack or every creature's frame. This checks the boundaries CM relies on; it cannot
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

For an ordinary boss grant without a blood rune, there is no second star roll,
forced full heal or call to `BossStars.Give`.
Before invoking PoV, CM skips bosses with any stored modifier flags (including
the upper `pt.crmod2` word when present), including
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

## Blood Challenge runes in Full

CreatureManager 1.3.11 gives blood runes an additive policy in
`Full`: **final stars = CM's normal stars, including applicable Karma, + rune count**.
PoV normally assigns its rune count as the final star count. Without this bridge,
CM's later initial level roll can replace that assignment. The bridge coordinates
these two decisions before completing the creature's level-dependent effects.

The existing altar limit of five runes is an additional-star limit. For example,
CM five stars plus one rune produces six stars. It is not clamped to five stars.
Ordinary CM spawns continue to use their existing rules. `ExceptLevel` and `Off`
retain PoV's own rune policy rather than receiving a new CM base roll.

The base decision, rune contribution and completion state belong to the creature,
not to a client-local counter. An owner change or a saved creature loading again
must not reroll the base or apply the same rune contribution twice. Supported boss
phases carry the completed result; the next phase does not add the runes again.
PoV's fixed-health phase exceptions remain in effect.

Deferred application preserves missing health rather than granting a late full
heal. A partial foreign failure is recorded as failed and is not retried. If a
patch changes after runes were captured, CM preserves their consumed star
contribution without replaying unverified PoV effects. New captures stop until
restart; unrelated CM and PoV integrations continue.

Stars and PoV modifier counts have separate budgets. The normal CM boss-modifier
share still follows PoV's switches, first-kill policy and configured modifiers per
star. The rune share follows PoV's existing rune rule: at least one modifier per
actual rune while its rarity system is working. Thus CM five stars plus one rune
with `Boss modifiers per star = 0` contributes one rune's modifier share, not six.
PoV's available effects and exclusions still limit what can actually be selected.
CM's own modifier selection follows the completed PoV selection so its existing
overlap filter sees those effects.

The bridge checks the relevant PoV methods and their behavior contract rather
than a whole-DLL version/hash allowlist. Its hooks are optional and do not change
PoV's DLL, configuration, altar inventory handling, or normal world saving.
Existing bosses that lost rune stars before this patch are not retroactively
reconstructed from ambiguous old PoV state. New altar summons are the meaningful
comparison case; explicit spawn commands have their own preserved-level policy.

### Blood-rune verification, 2026-10-31

The pre-release local Debug build passed with zero warnings/errors. Its merged DLL was
copied to the Steam game's `BepInEx/plugins/CreatureManager.dll`; source and
installed SHA-256 matched. That implementation check preceded the 1.3.11 release
build. The manifest BepInEx dependency is now `5.4.2360`.

The complete compatibility harness passed with zero failures for both supplied
original PoV **5.3.3** and **5.3.6** DLLs, using Valheim **1.0.21**'s original game
assembly and bundled Mono. The baseline without PoV also passed with zero
failures. The blood cases cover additive stars beyond five,
Karma/no-rule bases, independent modifier budgets, duplicate calls, pending
configuration/ownership, preserved ZDO state on a reconstructed character,
mode changes, phase carry/fixed health, partial failures and rejected changed
IL or missing/late Harmony patches. See
[`PovBloodChallengeContracts.cs`](../tools/GameCompatibilityCheck/PovBloodChallengeContracts.cs).

This is isolated managed execution: Unity scene/native calls, ownership,
configuration readiness, rule inputs and PoV random effect selection use fixtures.
Original `Give` and phase bodies run with test-only native-boundary substitutions;
CM's level algorithm uses an IL copy with fixture inputs. The production contract
is validated before admitting those substitutions in the test. Native-call
diagnostics from standalone Mono do not establish actual game compatibility.
No ZDO serialization/reload, Unity world, host or dedicated-server encounter was
run. Test logs, exact
input/build hashes and the deployment comparison are retained locally under
`artifacts/PoVBloodChallenge-20261031`.

Use this same patched CM DLL on server and clients, then check a new altar summon,
save/rejoin, owner transfer and a boss phase transition in the actual modpack.
Only the local Steam plugin was updated by this Debug build; Gale profiles and
remote servers were not changed.

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
| Rotten | Toxic Aura |
| Reeking | Toxic Aura |

This is a gameplay policy to avoid overlapping concepts, not a claim that paired
effects have identical formulas. In particular, Mirror reflects ranged hits and
Reflection reacts to melee hits; Rotten leaves a poison pool, while Reeking and
Toxic Aura apply poison around a living creature. Both poison effects exclude
new CM Toxic Aura rolls. CM Blink remains eligible indoors because PoV 5.0.10's
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
sprites are retried. Older atlas sprites are borrowed from PoV; new embedded PNGs
are decoded into a CM-owned cache released on adapter removal. Per-HUD state is weakly keyed and
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
