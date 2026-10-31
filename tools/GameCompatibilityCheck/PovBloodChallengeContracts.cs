using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

internal static partial class PovCompatibilityContracts
{
    // The additive state machine itself is production code. Only the engine and
    // policy inputs (selected weights, Karma result and synchronized settings) are fixtures.
    private static bool BloodFixtureActive, BloodHasRule = true, BloodKarmaReady = true, BloodRejectReplayOwner;
    private static int BloodBaseLevel = 1, BloodKarmaBonus, BloodDraws, BloodEnsures, BloodLevelWrites;
    private static long BloodUid = 900;

    private static MethodInfo? BloodBoundary(MethodInfo method)
    {
        if (!BloodFixtureActive) return null;
        string type = method.DeclaringType!.FullName!, name = method.Name;
        string? replacement = type == Levels.FullName ? name switch
        {
            "TrySelectLevelWeights" => nameof(BloodWeights),
            "TryResolveKarmaBonus" => nameof(BloodKarma),
            "TrySelectHealthMultiplier" => nameof(BloodNoHealthRule),
            "TrySelectDamageMultiplier" => nameof(BloodNoDamageRule),
            "StoreSpeedMultiplier" or "RestoreStoredHealthMultiplier" => nameof(NoState),
            "GetPrefabName" => nameof(BloodPrefab),
            "ShouldRollLevel" => nameof(TrueCharacter),
            "IsReadyForModifierApplication" => nameof(BloodReadyLevel),
            _ => null
        } : type == "UnityEngine.Component" && name == "get_gameObject" ? nameof(BloodGameObject)
          : type == "ZDO" && name == "Set" && method.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(string), typeof(string) }) ? nameof(BloodSetString)
          : type == "PassiveTree.EnemyDifficulty" && name == "Ensure" ? nameof(BloodEnsure)
          : type == "PassiveTree.EnemyDifficulty" && name == "FixedHealth" ? nameof(IsFrozen)
          : type == "CreatureManager.CreatureManagerSpawnLifecycle" && name == "IsCommandSpawn" ? nameof(FalseCharacter)
          : type == "ZNetView" && name == "IsOwner" ? nameof(BloodIsOwner)
          : null;
        return replacement == null ? null : AccessTools.Method(typeof(PovCompatibilityContracts), replacement);
    }

    private static bool BloodWeights(Character c, out List<float> weights)
    {
        BloodDraws++;
        weights = Enumerable.Repeat(0f, Math.Max(1, BloodBaseLevel)).ToList();
        weights[weights.Count - 1] = 100;
        return BloodHasRule;
    }
    private static bool BloodKarma(Character c, ZDO zdo, out int bonus) { bonus = BloodKarmaBonus; return BloodKarmaReady; }
    private static bool BloodNoHealthRule(Character c, out float multiplier) { multiplier = 1; return false; }
    private static bool BloodNoDamageRule(Character c, out float multiplier, out bool replaces) { multiplier = 1; replaces = false; return false; }
    private static GameObject BloodGameObject(Component component) => null!;
    private static string BloodPrefab(GameObject gameObject) => "Eikthyr";
    private static bool BloodReadyLevel(Character c) => ConfigReady && Data.GetBool(LevelKey("ProcessingCompleteKey"));
    private static void BloodSetString(ZDO zdo, string key, string value) => ZDOExtraData.Set(zdo.m_uid, key.GetStableHashCode(), value);
    private static void BloodEnsure(Character c) { BloodEnsures++; }
    private static bool BloodIsOwner(ZNetView view) => Owner && (!BloodRejectReplayOwner ||
        ((System.Collections.ICollection)Bridge.GetField("BloodReplays", All)!.GetValue(null)!).Count == 0);
    private static void BloodForeignPostfix() { }
    private static void GrantBloodBoss(Character c, int count)
    {
        Require(Owner, "blood grant is authoritative");
        BossGrants++; BossGrantCount = count;
        SetInt(Data, "pt.crmod", 1);
        if (ThrowBossFill) throw new InvalidOperationException("blood fixture failure after writing modifiers");
    }

    private static void CheckBloodChallenges(Harmony harness, Assembly pov, Type bossType, ConfigEntryBase levelMode)
    {
        BloodFixtureActive = true;
        Copies.Clear();
        var native = new Harmony("CM.tests.PoV.bloodNativeBoundaries");
        MethodInfo give = bossType.GetMethod("Give", All)!;
        Type phaseType = pov.GetType("PassiveTree.BossPhases", true)!;
        MethodInfo phase = phaseType.GetMethod("Give", All)!;
        MethodInfo apply = null!;
        string state = BloodKey("BloodStateKey"), runes = BloodKey("BloodRunesKey"), baseline = BloodKey("BloodBaseKey");
        try
        {
            // Install has already checked the supplied untouched methods. The only
            // added test patches substitute native/world boundaries after production
            // transpilers; refresh this offline fixture's trusted patch snapshot.
            // Runtime guard rejection is checked separately with an unknown patch.
            foreach (MethodInfo method in new[] { give, phase })
                native.Patch(method, transpiler: new HarmonyMethod(typeof(PovCompatibilityContracts), nameof(PovBoundaries))
                    { after = new[] { "sighsorry.CreatureManager.PoV.Blood" } });
            foreach (MethodInfo method in Bridge.GetMethods(All).Where(method => method.Name.Contains("Blood") &&
                         !method.Name.StartsWith("Install") && !method.Name.StartsWith("Validate") &&
                         !method.Name.EndsWith("Transpiler") && method.GetMethodBody() != null))
                harness.Patch(method, transpiler: new HarmonyMethod(typeof(PovCompatibilityContracts), nameof(PovBoundaries)));
            foreach (string method in new[] { "QueuePovBloodChallenge", "AdoptPovAssignedLevel", "TryGetOwnedZdo" })
                harness.Patch(Levels.GetMethod(method, All)!, transpiler: new HarmonyMethod(typeof(PovCompatibilityContracts), nameof(PovBoundaries)));
            Bridge.GetField("_bloodPatchContract", All)!.SetValue(null, Bridge.GetMethod("BloodPatchContract", All)!.Invoke(null, null));

            Require(Bridge.GetField("_bloodHarmony", All)!.GetValue(null) != null, "blood bridge installed by normal optional startup");
            var original = PatchProcessor.GetOriginalInstructions(give);
            Bridge.GetMethod("ValidateBloodGive", All)!.Invoke(null, new object[] { original });
            var changed = PatchProcessor.GetOriginalInstructions(give);
            int levelCall = changed.FindIndex(code => code.operand is MethodInfo method && method.DeclaringType == typeof(Character) && method.Name == "SetLevel");
            changed[levelCall - 1].opcode = OpCodes.Sub;
            Rejected("ValidateBloodGive", changed);
            changed = PatchProcessor.GetOriginalInstructions(give);
            changed.First(code => Equals(code.operand, "pt.boss.stars")).operand = "different.stars";
            Rejected("ValidateBloodGive", changed);
            changed = PatchProcessor.GetOriginalInstructions(give);
            changed.First(code => code.opcode == OpCodes.Mul).opcode = OpCodes.Add;
            Rejected("ValidateBloodGive", changed);
            changed = PatchProcessor.GetOriginalInstructions(phase);
            changed.RemoveAll(code => code.operand is MethodInfo method && method.DeclaringType == typeof(Character) && method.Name == "SetLevel");
            Rejected("ValidateBloodPhase", changed);

            apply = Copy(Levels.GetMethod("TryApplyLevel", All)!);
            void Tick() => apply.Invoke(null, new object[] { Actor });
            void Give(int amount) => give.Invoke(null, new object[] { Actor, amount });
            void Reset(int chosenLevel = 1, int modifiersPerStar = 1)
            {
                Data = New<ZDO>(); Data.m_uid = new ZDOID(780, (uint)++BloodUid);
                typeof(ZNetView).GetField("m_zdo", All)!.SetValue(View, Data);
                typeof(Character).GetField("m_nview", All)!.SetValue(Actor, View);
                typeof(ZNetView).GetField("m_initZDO", All)!.SetValue(null, null);
                Actor.m_nViewOverride = null;
                levelMode.SetSerializedValue("Full");
                BossEnabled.Value = BossFirstKill.Value = true; BossMods.Value = modifiersPerStar;
                Owner = ConfigReady = Boss = WasKilled = RarityWorking = BloodKarmaReady = BloodHasRule = true;
                FrozenPhase = LeftAlone = PreserveRolls = ThrowBossFill = BloodRejectReplayOwner = false;
                CurrentLevel = 1; BloodBaseLevel = chosenLevel; BloodKarmaBonus = 0;
                Maximum = Health = 400; BossGrants = BossGrantCount = BloodDraws = BloodEnsures = BloodLevelWrites = 0;
                SetFloat(Data, "PT_EnemyHealthFactor", 4f);
                SetBool(Data, BloodKey("BossPendingKey"), true);
                ((System.Collections.IDictionary)Levels.GetField("PendingLevelCharacters", All)!.GetValue(null)!).Clear();
            }

            Reset(4); Give(2);
            Require(Data.GetInt(state) == 1 && Data.GetInt(runes) == 2 && CurrentLevel == 1 && BossGrants == 0,
                "runes queue without provisional stars, health, modifiers or CM roll");
            Tick();
            Require(CurrentLevel == 6 && Data.GetInt(baseline) == 4 && Data.GetInt(state) == 3 && BossGrantCount == 5,
                "CM three stars plus two runes is five stars with separated normal/rune modifier budget");
            Require(Data.GetInt("pt.boss.stars") == 5 && Data.GetInt(LevelKey("DesiredLevelKey")) == 6 && BloodDraws == 1 && BossGrants == 1,
                "PoV and CM stored levels agree after a single baseline roll and one Fill");
            int levelWrites = BloodLevelWrites;
            Give(2); Tick(); Tick();
            Require(CurrentLevel == 6 && BloodDraws == 1 && BossGrants == 1 && BloodLevelWrites == levelWrites,
                "duplicate Give and initial lifecycle callbacks neither reroll nor add nor grant twice");

            Reset(6, 0); Give(1); Tick();
            Require(CurrentLevel == 7 && BossGrantCount == 1, "five CM stars plus one rune exceeds five final stars but grants only one rune modifier at m=0");
            Reset(6, 0); SetInt(Data, "pt.crmod", 31); Give(1); Tick();
            Require(CurrentLevel == 7 && BossGrantCount == 5, "Fill target preserves five existing PoV modifiers when new budget is only one");
            Reset(4, 2); WasKilled = false; Give(2); Tick();
            Require(CurrentLevel == 6 && BossGrantCount == 4, "first-kill rule blocks only base PoV modifiers, not actual rune contribution");
            Reset(3); BloodKarmaBonus = 2; Give(2); Tick();
            Require(CurrentLevel == 7 && Data.GetInt(baseline) == 5, "Karma belongs to CM baseline before exactly one rune addition");
            Reset(); BloodHasRule = false; CurrentLevel = 3; Give(2); Tick();
            Require(CurrentLevel == 5 && Data.GetInt(baseline) == 3, "Full without a matching rule preserves genuine pre-rune level as baseline");
            Reset(); BloodHasRule = false; CurrentLevel = 3; BloodKarmaBonus = 1; Give(2); Tick();
            Require(CurrentLevel == 4 && Data.GetInt(baseline) == 2, "no-rule Karma uses CM's ordinary level-one baseline before rune addition");

            Reset(4); ConfigReady = false; Give(2); Tick();
            Require(CurrentLevel == 1 && Data.GetInt(state) == 1 && BloodDraws == 0, "configuration wait retains consumed rune receipt without drawing");
            ConfigReady = true; BloodKarmaReady = false; Tick();
            Require(CurrentLevel == 1 && Data.GetInt(state) == 1 && BloodDraws == 0, "unresolved authoritative Karma defers both baseline and effects");
            BloodKarmaReady = true; Owner = false; Tick();
            Require(Data.GetInt(state) == 1 && BloodDraws == 0, "non-owner cannot advance the pending rune");
            Character oldActor = Actor;
            Actor = New<Character>(); typeof(Character).GetField("m_nview", All)!.SetValue(Actor, View);
            Owner = true; Health = 300; Tick();
            Require(CurrentLevel == 6 && BossGrants == 1 && Maximum - Health == 100, "new owner/Character resumes persisted receipt while preserving damage taken during wait");
            Actor = oldActor; Tick();
            Require(CurrentLevel == 6 && BossGrants == 1 && BloodDraws == 1, "reconstructed completed object does not repeat addition");

            Reset(4); ConfigReady = false; levelMode.SetSerializedValue("Off"); Give(2); Tick();
            Require(Data.GetInt(state) == 1 && CurrentLevel == 1, "unsynchronized local Off cannot discard the server's pending rune");
            ConfigReady = true; levelMode.SetSerializedValue("Full"); Tick();
            Require(CurrentLevel == 6 && Data.GetInt(state) == 3, "server Full after synchronization adds the preserved rune to CM baseline");

            Reset(9); CurrentLevel = 4;
            SetBool(Data, LevelKey("AppliedKey"), true); SetBool(Data, LevelKey("ProcessingCompleteKey"), true);
            Give(2); Tick();
            Require(CurrentLevel == 6 && BloodDraws == 0 && BossGrants == 1, "already initialized boss uses its actual CM baseline and still completes queued rune");
            foreach (string mode in new[] { "ExceptLevel", "Off" })
            {
                Reset(9); Give(2); levelMode.SetSerializedValue(mode); Tick();
                Require(CurrentLevel == 3 && Data.GetInt(state) == 3 && BloodDraws == 0, "pending Full to " + mode + " honors already consumed rune with original PoV policy");
                Reset(9); levelMode.SetSerializedValue(mode); Give(2);
                Require(CurrentLevel == 3 && Data.GetInt(state) == 0, mode + " does not capture a new rune or enable additive policy");
            }

            Reset(10); Owner = false; Give(2);
            Require(CurrentLevel == 1 && Data.GetInt(state) == 0 && BossGrants == 0, "remote original call cannot create authoritative rune receipt");
            Reset(10); RarityWorking = false; Give(2); Tick();
            Require(CurrentLevel == 12 && BossGrants == 0, "disabled rarity does not turn off additive stars or invent modifiers");

            Reset(9);
            object carry = Activator.CreateInstance(phase.GetParameters()[1].ParameterType)!;
            carry.GetType().GetField("Stars")!.SetValue(carry, 5);
            phase.Invoke(null, new[] { Actor, carry });
            Tick();
            Require(CurrentLevel == 6 && BloodDraws == 0 && Data.GetInt(state) == 0 && BossGrants == 0,
                "next phase adopts the already-combined five stars without rolling, adding runes or duplicating Fill");
            Reset(9); FrozenPhase = true; phase.Invoke(null, new[] { Actor, carry });
            Require(CurrentLevel == 1 && !Data.GetBool(LevelKey("AppliedKey")) && BloodLevelWrites == 0,
                "FrozenKing fixed-health phase does not pretend that the skipped SetLevel assigned carried stars");
            Reset(9); Bridge.GetField("_otherLevelProvider", All)!.SetValue(null, true);
            try
            {
                phase.Invoke(null, new[] { Actor, carry });
                Require(CurrentLevel == 6 && !Data.GetBool(LevelKey("AppliedKey")), "external level provider retains ownership of phase level adoption");
            }
            finally { Bridge.GetField("_otherLevelProvider", All)!.SetValue(null, false); }

            Reset(4); Give(2); ThrowBossFill = true;
            Type entry = Levels.Assembly.GetType("CreatureManager.CreatureManagerPlugin", true)!;
            PropertyInfo logProperty = entry.GetProperty("Log", All)!;
            object savedLog = logProperty.GetValue(null)!;
            var expectedLog = BepInEx.Logging.Logger.CreateLogSource("CM.tests.expectedBloodFailure");
            int warnings = 0; expectedLog.LogEvent += (_, e) => { if (e.Level == BepInEx.Logging.LogLevel.Warning) warnings++; };
            try
            {
                logProperty.SetValue(null, expectedLog); Tick(); Tick();
                Require(Data.GetInt(state) == 4 && CurrentLevel == 6 && BossGrants == 1 && warnings == 1,
                    "partial foreign Fill failure preserves completed star assignment and is never retried");
                Require(((System.Collections.ICollection)Bridge.GetField("BloodReplays", All)!.GetValue(null)!).Count == 0 &&
                        ((System.Collections.ICollection)Bridge.GetField("BloodPhases", All)!.GetValue(null)!).Count == 0,
                    "exception unwinds target-specific replay and phase scopes");

                // Reset only the fixture's failure latch between independent scenarios.
                Reset(4); Bridge.GetField("_bloodAccepting", All)!.SetValue(null, true);
                Give(2); BloodRejectReplayOwner = true; Tick();
                Require(Data.GetInt(state) == 4 && CurrentLevel == 1 && BossGrants == 0 && warnings == 2,
                    "original Give owner guard returning without SetLevel is failed, never falsely completed");

                Reset(4); Bridge.GetField("_bloodAccepting", All)!.SetValue(null, true); Give(2);
                native.Patch(give, postfix: new HarmonyMethod(typeof(PovCompatibilityContracts), nameof(BloodForeignPostfix)));
                Tick(); Tick();
                Require(Data.GetInt(state) == 4 && CurrentLevel == 6 && BossGrants == 0 && warnings == 3,
                    "late unknown assignment patch stops foreign replay while preserving consumed rune stars exactly once");
                Require(Bridge.GetField("_levelHarmony", All)!.GetValue(null) != null && Bridge.GetField("_fillBossModifiers", All)!.GetValue(null) != null,
                    "blood contract rejection leaves independent CM/PoV integrations installed");
                native.Unpatch(give, AccessTools.Method(typeof(PovCompatibilityContracts), nameof(BloodForeignPostfix)));

                Reset(4); Bridge.GetField("_bloodAccepting", All)!.SetValue(null, true);
                native.Unpatch(give, Bridge.GetMethod("BloodGiveTranspiler", All)!);
                Give(2);
                Require(Data.GetInt(state) == 0 && CurrentLevel == 3 && warnings == 4,
                    "partial loss of production blood transpiler is rejected before a new rune is captured");
            }
            finally { ThrowBossFill = false; logProperty.SetValue(null, savedLog); }
            Reset();
            System.Console.WriteLine("PoV blood contracts passed: real Give interception/replay, CM rule plus Karma plus runes, independent modifier budgets, no-rule/preinitialized cases, duplicate suppression, configuration/Karma/authority wait, retained ZDO with reconstructed Character, phase carry/fixed-HP/provider ownership, Full/ExceptLevel/Off, incomplete sink, changed IL/late/partial patches and partial foreign failure cleanup. No Unity world or network was executed.");
        }
        finally
        {
            BloodFixtureActive = false; Copies.Clear(); native.UnpatchSelf();
            Owner = ConfigReady = Boss = WasKilled = RarityWorking = true;
            FrozenPhase = LeftAlone = PreserveRolls = ThrowBossFill = BloodRejectReplayOwner = false;
            levelMode.SetSerializedValue("Full"); BossMods.Value = 1;
        }
    }

    private static string BloodKey(string key) => (string)Bridge.GetField(key, All)!.GetRawConstantValue()!;
    private static string LevelKey(string key) => (string)Levels.GetField(key, All)!.GetRawConstantValue()!;
}
