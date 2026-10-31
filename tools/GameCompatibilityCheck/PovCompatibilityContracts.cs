using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using Mono.Cecil;
using MonoMod.Utils;
using UnityEngine;

// Optional tests against the supplied, unmodified PoV DLL. Managed detours are installed
// only in this offline process. Unity, scene, RNG, ownership and FX boundaries are fixtures.
internal static partial class PovCompatibilityContracts
{
    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private static readonly Dictionary<MethodInfo, MethodInfo> Copies = new();
    private static Type Bridge = null!, Levels = null!, Modifiers = null!, Difficulty = null!;
    private static Character Actor = null!;
    private static ZDO Data = null!;
    private static float Maximum, Health;
    private static int Writes;
    private static bool ThrowWrite, ThrowLoot;
    private static CharacterDrop Dropper = null!;
    private static readonly List<CharacterDrop.Drop> Rows = new();
    private static MethodInfo Generate = null!;
    private static Func<bool> ForeignLevels = null!;
    private static int CurrentLevel = 1;
    private static bool Owner = true, ConfigReady = true, LevelReady, Boss = true, FrozenPhase, WasKilled = true, LeftAlone, RarityWorking = true;
    private static int BossGrants, BossGrantCount;
    private static ZNetView View = null!;
    private static bool ThrowBossFill, PreserveRolls;
    private static float HudTime;
    private static bool Interior = true;
    private static float GroupRoll = .5f;
    private static int GroupDraws;
    private static ConfigEntry<bool> BossEnabled = null!, BossFirstKill = null!;
    private static ConfigEntry<int> BossMods = null!;

    internal static void Run(Assembly cm, Assembly pov)
    {
        Bridge = cm.GetType("CreatureManager.CreaturePovCompatibility", true)!;
        Levels = cm.GetType("CreatureManager.CreatureLevelManager", true)!;
        Modifiers = cm.GetType("CreatureManager.CreatureModifierManager", true)!;
        Difficulty = pov.GetType("PassiveTree.EnemyDifficulty", true)!;
        Type stars = pov.GetType("PassiveTree.CreatureStars", true)!;
        ForeignLevels = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), stars.GetProperty("ForeignLevels")!.GetGetMethod()!);
        Type loot = stars.GetNestedType("CharacterDrop_GenerateDropList_Stars", BindingFlags.NonPublic)!;
        Type entry = cm.GetType("CreatureManager.CreatureManagerPlugin", true)!;
        FieldInfo levelField = entry.GetField("EnableLevelSystem", All)!;
        FieldInfo lootField = entry.GetField("LootSystem", All)!;
        object? savedLevel = levelField.GetValue(null), savedLoot = lootField.GetValue(null);
        var config = new ConfigFile(Path.Combine(Path.GetTempPath(), "CM-PoV-" + Guid.NewGuid() + ".cfg"), false) { SaveOnConfigSet = false };
        ConfigEntryBase levelMode = Bind(config, levelField, "Full"), lootMode = Bind(config, lootField, "Vanilla");
        var harness = new Harmony("CM.tests.PoV.boundaries");
        var foreign = new Harmony("CM.tests.PoV.originalPatches");
        Type bossType = pov.GetType("PassiveTree.BossStars", true)!;
        FieldInfo[] bossConfigFields = { bossType.GetField("Enabled")!, bossType.GetField("AfterFirstKill")!, bossType.GetField("ModsPerStar")! };
        object?[] savedBossConfig = bossConfigFields.Select(field => field.GetValue(null)).ToArray();
        BossEnabled = config.Bind("boss", "enabled", true);
        BossFirstKill = config.Bind("boss", "afterKill", true);
        BossMods = config.Bind("boss", "mods", 1);
        bossConfigFields[0].SetValue(null, BossEnabled); bossConfigFields[1].SetValue(null, BossFirstKill); bossConfigFields[2].SetValue(null, BossMods);
        try
        {
            // Character's static animation hashes normally require the native engine.
            harness.Patch(AccessTools.Method(AccessTools.TypeByName("UnityEngine.Animator"), "StringToHash", new[] { typeof(string) }),
                transpiler: new HarmonyMethod(typeof(PovCompatibilityContracts), nameof(HashBody)));
            Actor = New<Character>();
            Data = New<ZDO>();
            Data.m_uid = new ZDOID(780, 19);
            ZNetView view = New<ZNetView>();
            View = view;
            typeof(Character).GetField("m_nview", All)!.SetValue(Actor, view);
            typeof(ZNetView).GetField("m_zdo", All)!.SetValue(view, Data);
            SetFloat(Data, "PT_EnemyHealthFactor", 4f);
            foreach (string name in new[] { "GetHealth", "GetMaxHealth", "GetMaxHealthBase", "GetLevel", "SetHealth", "SetMaxHealth", "SetLevel", "IsPlayer", "IsBoss", "IsDead" })
                harness.Patch(AccessTools.DeclaredMethod(typeof(Character), name), transpiler: new HarmonyMethod(typeof(PovCompatibilityContracts), nameof(CharacterBody)));
            foreach (string name in new[] { "IsValid", "IsOwner" })
                harness.Patch(AccessTools.DeclaredMethod(typeof(ZNetView), name), transpiler: new HarmonyMethod(typeof(PovCompatibilityContracts), nameof(TrueBody)));
            // Only native/world/policy-input boundaries are substituted. Run the real
            // ZNetView.Awake wrapper and CM capture/completion methods with their
            // real Harmony hooks. The engine's ZDO creation is substituted below.
            foreach (string name in new[] { "BossViewPostfix", "ReserveBossModifiers", "TryCompleteBossModifiers" })
                harness.Patch(Bridge.GetMethod(name, All)!, transpiler: new HarmonyMethod(typeof(PovCompatibilityContracts), nameof(PovBoundaries)));
            harness.Patch(AccessTools.DeclaredMethod(typeof(ZNetView), "Awake"), transpiler: new HarmonyMethod(typeof(PovCompatibilityContracts), nameof(ViewAwakeBody)));
            harness.Patch(bossType.GetMethod("Roll", All)!, transpiler: new HarmonyMethod(typeof(PovCompatibilityContracts), nameof(PovBoundaries)));
            harness.Patch(bossType.GetMethod("KilledBefore", All)!, transpiler: new HarmonyMethod(typeof(PovCompatibilityContracts), nameof(KilledBody)));
            harness.Patch(pov.GetType("Valheiman.Api.Creatures", true)!.GetMethod("IsLeftAlone", All)!, transpiler: new HarmonyMethod(typeof(PovCompatibilityContracts), nameof(LeftAloneBody)));
            harness.Patch(pov.GetType("PassiveTree.CreatureRarity", true)!.GetProperty("Working")!.GetGetMethod()!, transpiler: new HarmonyMethod(typeof(PovCompatibilityContracts), nameof(RarityBody)));
            harness.Patch(pov.GetType("PassiveTree.RarityMods", true)!.GetMethod("Fill", All)!, transpiler: new HarmonyMethod(typeof(PovCompatibilityContracts), nameof(BossFillBody)));
            harness.Patch(pov.GetType("PassiveTree.RarityMods", true)!.GetMethod("On", All)!, transpiler: new HarmonyMethod(typeof(PovCompatibilityContracts), nameof(PovBoundaries)));
            foreach (MethodInfo method in new[] {
                Difficulty.GetMethod("SetScaledMaxHealth", All)!, Difficulty.GetMethod("WriteHealth", All)!,
                Difficulty.GetNestedType("MaxHealth", BindingFlags.NonPublic)!.GetMethod("Prefix", All)!,
                Difficulty.GetNestedType("MaxHealth", BindingFlags.NonPublic)!.GetMethod("Postfix", All)!,
                Difficulty.GetNestedType("FollowMaxHealth", BindingFlags.NonPublic)!.GetMethod("Prefix", All)!,
                loot.GetMethod("Prefix", All)!, loot.GetMethod("Postfix", All)!,
                pov.GetType("PassiveTree.RarityLoot", true)!.GetMethod("ScaleAmount", All)! })
                harness.Patch(method, transpiler: new HarmonyMethod(typeof(PovCompatibilityContracts), nameof(PovBoundaries)));
            harness.Patch(stars.GetNestedType("LootState", BindingFlags.NonPublic)!.GetConstructors(All).Single(),
                transpiler: new HarmonyMethod(typeof(PovCompatibilityContracts), nameof(PovBoundaries)));
            Type max = Difficulty.GetNestedType("MaxHealth", BindingFlags.NonPublic)!;
            foreign.Patch(AccessTools.Method(typeof(Character), "SetMaxHealth"),
                prefix: new HarmonyMethod(max.GetMethod("Prefix", All)!), postfix: new HarmonyMethod(max.GetMethod("Postfix", All)!));
            foreign.Patch(AccessTools.Method(typeof(Character), "SetHealth"),
                prefix: new HarmonyMethod(Difficulty.GetNestedType("FollowMaxHealth", BindingFlags.NonPublic)!.GetMethod("Prefix", All)!));
            Generate = AccessTools.DeclaredMethod(typeof(CharacterDrop), nameof(CharacterDrop.GenerateDropList));
            harness.Patch(Generate, transpiler: new HarmonyMethod(typeof(PovCompatibilityContracts), nameof(LootBody)));
            foreign.Patch(Generate, prefix: new HarmonyMethod(loot.GetMethod("Prefix", All)!),
                postfix: new HarmonyMethod(loot.GetMethod("Postfix", All)!), finalizer: new HarmonyMethod(loot.GetMethod("Finalizer", All)!));
            Dropper = New<CharacterDrop>();
            typeof(CharacterDrop).GetField("m_character", All)!.SetValue(Dropper, Actor);
            foreach (bool chanceOnly in new[] { false, true })
                Rows.Add(new CharacterDrop.Drop { m_prefab = New<GameObject>(), m_levelMultiplier = true, m_levelMultiplierNotAmount = chanceOnly });
            typeof(CharacterDrop).GetField("m_drops", All)!.SetValue(Dropper, Rows);

            // Warm the real caller before CM installs its late optional hooks.
            Require(Roll().All(row => row.Value == 4), "pre-bridge PoV owns count scaling");
            Require(Rows.All(row => row.m_levelMultiplier), "original finalizer restores flags");
            Maximum = 400; Health = 200;
            Actor.SetMaxHealth(440);
            Require(Maximum == 1760, "reproduced double factor on an already-scaled max");
            Require(!ForeignLevels(), "PoV cached the absent-provider result before CM bridge");

            string[] registeredLootPatches = LootPatchRegistrations();
            // Exercise normal startup with a different release label: capability checks,
            // not a version allow-list, must decide whether this same DLL is compatible.
            var povInfo = new PluginInfo();
            typeof(PluginInfo).GetProperty("Metadata")!.SetValue(povInfo,
                new BepInPlugin("treextr.pathofvalheiman", "Path of Valheiman", "99.0.0"));
            Type povPlugin = pov.GetType("PassiveTree.Plugin", true)!;
            typeof(PluginInfo).GetProperty("Instance")!.SetValue(povInfo, FormatterServices.GetUninitializedObject(povPlugin));
            Chainloader.PluginInfos.Add("treextr.pathofvalheiman", povInfo);
            Bridge.GetMethod("Initialize", All)!.Invoke(null, null);
            Require((bool)Bridge.GetProperty("HasHealthBridge", All)!.GetValue(null)!, "compatible structure accepted independently of release number");
            CheckRejectedContracts(loot);
            Require(registeredLootPatches.SequenceEqual(LootPatchRegistrations()), "late wrapper rebuild preserves original patch registrations and priorities");
            foreach (string mode in new[] { "Full", "ExceptLevel", "Off", "Full" })
            {
                levelMode.SetSerializedValue(mode);
                Require(ForeignLevels() == (mode == "Full"), "live level ownership: " + mode);
            }
            Require(Roll().All(row => row.Value == 1), "Full lets CM own levels even with CM loot Vanilla");
            levelMode.SetSerializedValue("ExceptLevel");
            lootMode.SetSerializedValue("CalculateChance");
            Require(Roll().All(row => row.Value == 1) && Rows.All(row => row.m_levelMultiplier), "CM owns star loot; chance-only flags stay intact");
            ThrowLoot = true;
            try { Roll(); throw new InvalidOperationException("expected fixture exception"); }
            catch (TargetInvocationException e) { Require(e.InnerException is InvalidOperationException, "loot exception preserved"); }
            finally { ThrowLoot = false; }
            Require(Rows.All(row => row.m_levelMultiplier), "exception did not corrupt shared drop flags");
            lootMode.SetSerializedValue("Vanilla");
            Require(Roll().All(row => row.Value == 4), "Vanilla retains PoV ownership");
            Chainloader.PluginInfos.Add("sighsorry.DropNSpawn", null!);
            Require(Roll().All(row => row.Value == 1), "DNS owns star loot even when CM is Vanilla");
            Chainloader.PluginInfos.Remove("sighsorry.DropNSpawn");

            CheckHealth();
            CheckHud(pov, levelMode);
            CheckModifierExclusions(pov, levelMode);
            CheckBloodChallenges(harness, pov, bossType, levelMode);
            CheckBossModifiers(bossType, levelMode);
            Bridge.GetMethod("Reset", All)!.Invoke(null, null);
            Require(registeredLootPatches.SequenceEqual(LootPatchRegistrations()), "cleanup preserves other owners' loot patch registrations");
            Require(Roll().All(row => row.Value == 4), "cleanup restores PoV loot without removing its patches");
            Require(!ForeignLevels(), "cleanup restores PoV original cached getter");
            Require(!(bool)Bridge.GetProperty("HasHealthBridge", All)!.GetValue(null)!, "cleanup releases health delegates");
            Require(Bridge.GetField("_activeModifiers", All)!.GetValue(null) == null, "cleanup releases active modifier reader");
            Require(!(bool)cm.GetType("CreatureManager.CreaturePovHud", true)!.GetProperty("Enabled", All)!.GetValue(null)!, "HUD cleanup restores CM preference");
            Chainloader.PluginInfos.Add("fixture.other.levels", new PluginInfo());
            typeof(PluginInfo).GetProperty("Metadata")!.SetValue(Chainloader.PluginInfos["fixture.other.levels"],
                new BepInPlugin("fixture", "Creature Level and Loot", "1.0.0"));
            Bridge.GetMethod("Install", All)!.Invoke(null, new object[] { pov });
            levelMode.SetSerializedValue("Off");
            Require(ForeignLevels(), "other provider detection remains authoritative");
            Require(!(bool)cm.GetType("CreatureManager.CreaturePovHud", true)!.GetProperty("Enabled", All)!.GetValue(null)!, "other level provider prevents HUD takeover");
            System.Console.WriteLine("PoV contracts passed: version-independent startup, rejected incompatible loot contracts, real managed Harmony callers before/after late install, cached/live level ownership, CM/DNS/PoV loot priority and exception cleanup, raw vs final HP, Reaping cap/restore/heal, per-character pending state and shutdown. Unity/world/FX boundaries substituted; no game session.");
        }
        finally
        {
            Bridge.GetMethod("Reset", All)!.Invoke(null, null);
            foreign.UnpatchSelf(); harness.UnpatchSelf();
            levelField.SetValue(null, savedLevel); lootField.SetValue(null, savedLoot);
            Chainloader.PluginInfos.Remove("sighsorry.DropNSpawn");
            Chainloader.PluginInfos.Remove("fixture.other.levels");
            Chainloader.PluginInfos.Remove("treextr.pathofvalheiman");
            for (int i = 0; i < bossConfigFields.Length; i++) bossConfigFields[i].SetValue(null, savedBossConfig[i]);
            Owner = ConfigReady = Boss = WasKilled = RarityWorking = true;
            LevelReady = FrozenPhase = LeftAlone = PreserveRolls = false;
            Interior = true; GroupRoll = .5f; GroupDraws = 0;
            Rows.Clear(); Copies.Clear();
        }
    }

    private static void CheckModifierExclusions(Assembly pov, ConfigEntryBase mode)
    {
        Type foreignMods = pov.GetType("PassiveTree.RarityMods", true)!;
        Type foreignMask = foreignMods.GetNestedType("Mod")!;
        Type cmMask = Modifiers.GetNestedType("ModifierMask", All)!;
        int PovBit(string name) => Convert.ToInt32(Enum.Parse(foreignMask, name));
        long CmBit(string name) => Convert.ToInt64(Enum.Parse(cmMask, name));
        MethodInfo get = Copy(Bridge.GetMethod("GetExcludedModifiers", All)!);
        long Excluded() => Convert.ToInt64(get.Invoke(null, new object[] { Actor }));
        var expected = new[]
        {
            ("Swift", "Swift"), ("Furious", "AttackSpeed"), ("Armored", "Armored"),
            ("Hardy", "Regenerating"), ("Greedy", "Vampiric"), ("Steadfast", "Unflinching"),
            ("Piercing", "ArmorPiercing"), ("Burning", "Fire"), ("Chilling", "Frost"),
            ("Shocking", "Lightning"), ("Blink", "Blink"), ("Mirror", "Reflection"), ("Rotten", "ToxicAura"), ("Reeking", "ToxicAura")
        };
        var config = new ConfigFile(Path.Combine(Path.GetTempPath(), "CM-overlap-" + Guid.NewGuid() + ".cfg"), false) { SaveOnConfigSet = false };
        FieldInfo ordinaryOff = pov.GetType("PassiveTree.CreatureRarity", true)!.GetField("ModsOff")!;
        FieldInfo bossOff = pov.GetType("PassiveTree.BossStars", true)!.GetField("ModsOff")!;
        object? savedOrdinary = ordinaryOff.GetValue(null), savedBoss = bossOff.GetValue(null);
        var ordinary = config.Bind("overlap", "ordinary off", "");
        var bosses = config.Bind("overlap", "boss off", "");
        ordinaryOff.SetValue(null, ordinary); bossOff.SetValue(null, bosses);
        FieldInfo reader = Bridge.GetField("_activeModifiers", All)!;
        var realReader = (Func<Character, int>)reader.GetValue(null)!;
        try
        {
            Boss = Interior = false; RarityWorking = true;
            foreach (string name in Enum.GetNames(foreignMask))
            {
                SetInt(Data, "pt.crmod", PovBit(name));
                var match = expected.SingleOrDefault(pair => pair.Item1 == name);
                Require(Excluded() == (match.Item2 == null ? 0 : CmBit(match.Item2)), "exact policy for active PoV " + name);
            }
            int all = expected.Aggregate(0, (mask, pair) => mask | PovBit(pair.Item1));
            long allCm = expected.Aggregate(0L, (mask, pair) => mask | CmBit(pair.Item2));
            SetInt(Data, "pt.crmod", all);
            Require(Excluded() == allCm, "combined active flags compose without excluding unrelated modifiers");
            Interior = true;
            Require(Excluded() == (allCm & ~CmBit("Blink")), "dungeon retains CM Blink and only Blink");
            Interior = false;
            ordinary.Value = "mirror, rotten";
            Require(Excluded() == (allCm & ~CmBit("Reflection")), "active Reeking still excludes Toxic Aura when Rotten is disabled");
            ordinary.Value = "mirror, rotten, reeking";
            Require(Excluded() == (allCm & ~CmBit("Reflection") & ~CmBit("ToxicAura")), "ordinary live PoV exclusions are respected");
            Boss = true; bosses.Value = "blink, armored";
            Require(Excluded() == (allCm & ~CmBit("Blink") & ~CmBit("Armored")), "boss exclusions override ordinary exclusions");
            RarityWorking = false;
            Require(Excluded() == 0, "disabled PoV rarity does not suppress CM modifiers");
            RarityWorking = true; Boss = false; ordinary.Value = "";
            reader.SetValue(null, null);
            Require(Excluded() == 0, "absent or unsupported PoV retains CM candidates");
            int reads = 0;
            reader.SetValue(null, (Func<Character, int>)(c => { reads++; return realReader(c); }));
            Require(Excluded() == allCm && reads == 1, "one active mask read per selection");

            // Execute the production weighted group selector with only RNG and
            // Unity-dependent prefab applicability substituted.
            MethodInfo select = Copy(Modifiers.GetMethod("RollConfiguredMask", All)!);
            Type chanceType = select.GetParameters()[1].ParameterType;
            object Chances(params (string Key, float Chance)[] entries)
            {
                object result = Activator.CreateInstance(chanceType, true)!;
                foreach (var entry in entries) chanceType.GetMethod("Set", All)!.Invoke(result, new object[] { entry.Key, entry.Chance });
                return result;
            }
            long Roll(object chances, long excluded, float at)
            {
                GroupRoll = at; GroupDraws = 0;
                return Convert.ToInt64(select.Invoke(null, new[] { Actor, chances, Enum.ToObject(cmMask, excluded) }));
            }
            object mixed = Chances(("fire", 100f), ("spirit", 20f), ("reflection", 100f), ("deathward", 100f), ("toxicAura", 100f), ("swift", 100f));
            Require(Roll(mixed, allCm, .1f) == (CmBit("Spirit") | CmBit("Deathward")), "eligible effects still roll across their original groups");
            Require(GroupDraws == 2, "fully excluded groups consume no RNG");
            Require(Roll(mixed, allCm, .5f) == CmBit("Deathward"), "remaining 20 percent is not promoted to a guaranteed success");
            Require(Roll(Chances(("fire", 100f)), allCm, 0f) == 0 && GroupDraws == 0, "even YAML 100 percent cannot select an excluded automatic effect");
            object weighted = Chances(("fire", 100f), ("spirit", 100f), ("armorPiercing", 100f));
            Require(Roll(weighted, CmBit("Fire"), .25f) == CmBit("Spirit") &&
                    Roll(weighted, CmBit("Fire"), .75f) == CmBit("ArmorPiercing"), "excluded weights are removed before normalization");
            Require(Roll(weighted, CmBit("Fire"), 1f) == CmBit("ArmorPiercing"), "upper RNG bound retains only an eligible final candidate");
            Require(Roll(Chances(("blink", 100f)), allCm, 0f) == 0 &&
                    Roll(Chances(("blink", 100f)), allCm & ~CmBit("Blink"), 0f) == CmBit("Blink"), "dungeon exception reaches actual group selection");

            // Existing saves and non-authoritative callers must not even query PoV.
            MethodInfo attempt = Copy(Modifiers.GetMethod("TryRollModifiers", All)!);
            string applied = Key("AppliedKey");
            mode.SetSerializedValue("Full"); LevelReady = ConfigReady = Owner = true;
            SetBool(Data, applied, true); reads = 0;
            attempt.Invoke(null, new object[] { Actor });
            Require(reads == 0 && Data.GetBool(applied), "existing modifier rolls remain unchanged");
            SetBool(Data, applied, false); Owner = false;
            attempt.Invoke(null, new object[] { Actor });
            Owner = true; ConfigReady = false;
            attempt.Invoke(null, new object[] { Actor });
            ConfigReady = true; mode.SetSerializedValue("Off");
            attempt.Invoke(null, new object[] { Actor });
            Require(reads == 0 && !Data.GetBool(applied), "non-owner, unsynchronized and Off paths neither query nor write modifiers");

            var codes = PatchProcessor.GetOriginalInstructions(Modifiers.GetMethod("TryRollModifiers", All)!);
            int complete = codes.FindIndex(code => code.operand is MethodInfo m && m.Name == "TryCompleteBossModifiers");
            int filter = codes.FindIndex(code => code.operand is MethodInfo m && m.Name == "GetExcludedModifiers");
            int roll = codes.FindIndex(code => code.operand is MethodInfo m && m.Name == "RollConfiguredMask");
            Require(complete >= 0 && complete < filter && filter < roll, "all automatic entries complete pending boss grants before reading active effects");
            Require(!PatchProcessor.GetOriginalInstructions(Modifiers.GetMethod("TryApplyForcedModifiers", All)!).Any(code =>
                code.operand is MethodInfo m && (m.Name == "GetExcludedModifiers" || m.Name == "RollConfiguredMask")), "explicit forced modifiers retain priority");
            Rejected("InstallModifierExclusions", typeof(object).Assembly);
            System.Console.WriteLine("PoV overlap contracts passed: all 26 named effects, 13 exclusions, live ordinary/boss disable lists, dungeon Blink, weighted rolls and empty groups, existing/forced rolls, owner/config/Off gates, pending boss order and absent-provider fallback. No game session.");
        }
        finally
        {
            reader.SetValue(null, realReader); ordinaryOff.SetValue(null, savedOrdinary); bossOff.SetValue(null, savedBoss);
            SetInt(Data, "pt.crmod", 0); SetBool(Data, Key("AppliedKey"), false);
            Interior = true; Boss = RarityWorking = Owner = ConfigReady = true; LevelReady = false;
            GroupRoll = .5f; mode.SetSerializedValue("Full");
        }
    }

    private static void CheckBossModifiers(Type bossType, ConfigEntryBase levelMode)
    {
        string pending = (string)Bridge.GetField("BossPendingKey", All)!.GetRawConstantValue()!;
        MethodInfo roll = bossType.GetMethod("Roll", All)!;
        MethodInfo awake = AccessTools.DeclaredMethod(typeof(ZNetView), "Awake");
        MethodInfo finish = Bridge.GetMethod("TryCompleteBossModifiers", All)!;
        Action spawn = () => awake.Invoke(View, null);
        Func<bool> complete = () => (bool)finish.Invoke(null, new object[] { Actor })!;
        Action clear = () =>
        {
            SetInt(Data, pending, 0); SetInt(Data, "pt.boss.rolled", 0); SetInt(Data, "pt.crmod", 0);
            SetBool(Data, (string)Levels.GetField("ProcessingCompleteKey", All)!.GetRawConstantValue()!, false);
            levelMode.SetSerializedValue("Full"); BossEnabled.Value = BossFirstKill.Value = true; BossMods.Value = 1;
            Owner = ConfigReady = Boss = WasKilled = RarityWorking = true;
            LevelReady = FrozenPhase = LeftAlone = PreserveRolls = false;
            BossGrants = BossGrantCount = 0; Maximum = 400; Health = 175; CurrentLevel = 1;
            typeof(ZNetView).GetField("m_zdo", All)!.SetValue(View, null);
            typeof(ZNetView).GetField("m_initZDO", All)!.SetValue(null, null);
            typeof(ZNetView).GetField("m_ghost", All)!.SetValue(View, false);
            Actor.m_nViewOverride = null;
        };
        clear();
        spawn();
        Require(Data.GetBool(pending) && !Data.GetBool("pt.boss.rolled") && BossGrants == 0, "fresh boss reserves without any altar Roll call");
        Require(Maximum == 400 && Health == 175 && CurrentLevel == 1, "capture does not reroll stars or heal");
        CurrentLevel = 4; LevelReady = true;
        Owner = false;
        Require(!complete() && Data.GetBool(pending) && BossGrants == 0, "non-owner preserves pending reservation");
        Owner = true; ConfigReady = false;
        Require(!complete() && Data.GetBool(pending), "unsynchronized config cannot consume reservation");
        ConfigReady = true;
        Require(complete() && BossGrants == 1 && BossGrantCount == 3 && !Data.GetBool(pending), "current owner grants using CM final stars");
        complete(); spawn(); complete();
        Require(BossGrants == 1 && CurrentLevel == 4 && Maximum == 400 && Health == 175, "duplicate callbacks neither grant twice nor change level/HP");

        clear(); typeof(ZNetView).GetField("m_initZDO", All)!.SetValue(null, Data); LevelReady = true; CurrentLevel = 3;
        spawn(); complete();
        Require(BossGrants == 0 && !Data.GetBool(pending), "saved boss without CM/PoV markers is not retrofitted");
        clear(); spawn(); roll.Invoke(null, new object[] { Actor }); CurrentLevel = 3; LevelReady = true;
        Require(complete() && BossGrants == 1 && Data.GetBool("pt.boss.rolled"), "altar Roll still coexists with generic reservation");
        clear(); spawn(); SetInt(Data, "pt.crmod", 1); CurrentLevel = 3; LevelReady = true;
        Require(complete() && BossGrants == 0 && Data.GetInt("pt.crmod") == 1, "existing manual/foreign modifiers cannot be trimmed");
        clear(); spawn(); CurrentLevel = 1; LevelReady = true;
        Require(complete() && BossGrants == 0 && !Data.GetBool(pending), "zero-star boss consumes without granting");
        clear(); BossMods.Value = 0; spawn(); complete();
        Require(!Data.GetBool(pending) && BossGrants == 0, "PoV Normal zero-modifier setting preserved");
        foreach (string policy in new[] { "disabled", "rarity", "first-kill", "left-alone", "frozen", "remote", "ordinary", "ghost", "override", "preserved-restore" })
        {
            clear();
            if (policy == "disabled") BossEnabled.Value = false;
            if (policy == "rarity") RarityWorking = false;
            if (policy == "first-kill") WasKilled = false;
            if (policy == "left-alone") LeftAlone = true;
            if (policy == "frozen") FrozenPhase = true;
            if (policy == "remote") Owner = false;
            if (policy == "ordinary") Boss = false;
            if (policy == "ghost") typeof(ZNetView).GetField("m_ghost", All)!.SetValue(View, true);
            if (policy == "override") Actor.m_nViewOverride = New<ZNetView>();
            if (policy == "preserved-restore") PreserveRolls = true;
            spawn(); complete();
            Require(!Data.GetBool(pending) && BossGrants == 0, "capture exclusion: " + policy);
        }
        foreach (string mode in new[] { "Off", "ExceptLevel" })
        {
            clear(); spawn(); levelMode.SetSerializedValue(mode);
            Require(complete() && !Data.GetBool(pending) && BossGrants == 0, "mode change cancels pending grant: " + mode);
        }
        clear(); spawn(); BossEnabled.Value = false;
        Require(complete() && !Data.GetBool(pending) && BossGrants == 0, "PoV disable cancels pending grant");
        clear(); spawn();
        // A reconstructed Character with the same saved ZDO can finish the pending grant.
        Character previous = Actor;
        Actor = New<Character>();
        typeof(Character).GetField("m_nview", All)!.SetValue(Actor, typeof(Character).GetField("m_nview", All)!.GetValue(previous));
        CurrentLevel = 3; LevelReady = true;
        Require(complete() && BossGrants == 1 && BossGrantCount == 2, "saved pending state survives a new Character instance");
        Actor = previous;
        clear(); CurrentLevel = 3; LevelReady = true; spawn();
        Require(BossGrants == 0 && complete() && BossGrants == 1, "capture defers effects until normal character lifecycle");
        clear(); ConfigReady = false; spawn();
        Require(Data.GetBool(pending) && !complete(), "fresh reservation is retained while config sync is pending");
        ConfigReady = true; CurrentLevel = 3; LevelReady = true;
        Require(complete() && BossGrants == 1, "config synchronization completes the reserved grant");
        clear(); spawn(); CurrentLevel = 3; LevelReady = true;
        var queued = (System.Collections.IDictionary)Levels.GetField("PendingLevelCharacters", All)!.GetValue(null)!;
        queued[InstanceId(Actor)] = Actor;
        SetBool(Data, (string)Levels.GetField("ProcessingCompleteKey", All)!.GetRawConstantValue()!, true);
        MethodInfo tick = Copy(Levels.GetMethod("UpdatePendingApplications", All)!);
        Owner = false; tick.Invoke(null, null);
        Require(queued.Contains(InstanceId(Actor)) && BossGrants == 0, "completed remote boss remains in production pending queue");
        Owner = true; tick.Invoke(null, null);
        Require(!queued.Contains(InstanceId(Actor)) && BossGrants == 1, "production pending queue finishes after ownership transfer");
        clear(); spawn(); queued[InstanceId(Actor)] = Actor;
        levelMode.SetSerializedValue("Off"); Owner = false; tick.Invoke(null, null);
        Require(queued.Contains(InstanceId(Actor)), "Off keeps remote pending reservation until an owner can cancel it");
        Owner = true; tick.Invoke(null, null);
        Require(!queued.Contains(InstanceId(Actor)) && !Data.GetBool(pending) && BossGrants == 0, "Off owner cancels pending reservation without granting");
        clear(); spawn(); queued[InstanceId(Actor)] = Actor; PreserveRolls = true;
        Owner = false; tick.Invoke(null, null);
        Require(queued.Contains(InstanceId(Actor)) && Data.GetBool(pending), "preserved restore waits for owner to consume reservation");
        Owner = true; tick.Invoke(null, null);
        Require(!queued.Contains(InstanceId(Actor)) && !Data.GetBool(pending) && BossGrants == 0, "preserved restore consumes without adding modifiers");
        clear(); spawn(); CurrentLevel = 3; LevelReady = true;
        var entry = Bridge.Assembly.GetType("CreatureManager.CreatureManagerPlugin", true)!;
        var logProperty = entry.GetProperty("Log", All)!;
        object savedLog = logProperty.GetValue(null)!;
        var expectedLog = BepInEx.Logging.Logger.CreateLogSource("CM.tests.expectedPoVBossFailure");
        int warnings = 0;
        expectedLog.LogEvent += (_, e) => { if (e.Level == BepInEx.Logging.LogLevel.Warning) warnings++; };
        try
        {
            logProperty.SetValue(null, expectedLog); ThrowBossFill = true;
            complete(); complete(); spawn();
            Require(BossGrants == 1 && Data.GetInt("pt.crmod") == 1 && !Data.GetBool(pending) && warnings == 1,
                "partial foreign failure preserves flags, consumes reservation and reports once without retrying");
        }
        finally { ThrowBossFill = false; logProperty.SetValue(null, savedLog); }
        System.Console.WriteLine("PoV boss contracts passed: installed ZNetView.Awake hooks for generic fresh vs restored bosses, altar coexistence, deferred final stars, authority/config wait, save/reload, duplicates, policy exclusions, zero stars/count, mode cancellation, preexisting modifiers and unchanged HP/level. PoV random selection/FX and Unity/world boundaries substituted.");
        clear();
    }

    private static void CheckHud(Assembly pov, ConfigEntryBase mode)
    {
        Type hud = Bridge.Assembly.GetType("CreatureManager.CreaturePovHud", true)!;
        MethodInfo draw = pov.GetType("PassiveTree.UI.AilmentHudBridge", true)!.GetMethod("Draw", All)!;
        var original = PatchProcessor.GetOriginalInstructions(draw);
        var registrations = Harmony.GetPatchInfo(draw);
        Require(registrations.Postfixes.Any(p => p.PatchMethod.DeclaringType == hud), "status placement runs after PoV has drawn its live statuses");
        Require(!registrations.Transpilers.Any(p => p.PatchMethod.DeclaringType == hud), "original status drawing/health body is unchanged");
        MethodInfo apply = pov.GetType("PathOfValheiman.AilmentHud.AilmentHudView", true)!.GetMethod("Apply", All)!;
        bool rejected = false;
        try { hud.GetMethod("RequireCall", All)!.Invoke(null, new object[] { typeof(PovCompatibilityContracts).GetMethod(nameof(HudClock), All)!, apply }); }
        catch (TargetInvocationException ex) when (ex.InnerException is NotSupportedException) { rejected = true; }
        Require(rejected, "unknown status lifecycle contract rejected");
        MethodInfo rowCount = hud.GetMethod("RowCount", All)!, height = hud.GetMethod("Height", All)!, position = hud.GetMethod("IconPosition", All)!;
        foreach (int count in new[] { 0, 1, 3, 4, 5, 7, 8, 9, 12, 26, 30 })
        {
            int rows = (int)rowCount.Invoke(null, new object[] { count })!;
            Require(rows == (count == 0 ? 0 : (count - 1) / 4 + 1), "no blank or truncated rows for count " + count);
            float h = (float)height.Invoke(null, new object[] { count, false })!;
            Require(count != 0 || h == 0f, "empty combined HUD has no reserved height");
            var occupied = new HashSet<string>();
            for (int i = 0; i < count; i++)
            {
                Vector2 xy = (Vector2)position.Invoke(null, new object[] { i, count })!;
                Require(xy.x <= 0 && xy.x >= -54 && xy.y <= 0 && -xy.y + 17 <= h, "icon within four-column bounds");
                Require(occupied.Add(xy.x + ":" + xy.y), "unique icon slot");
                if (i == count - 1 || (i + 1) % 4 == 0) Require(xy.x == 0, "each row independently right-aligned");
            }
        }
        Require((float)height.Invoke(null, new object[] { 0, true })! == 20f, "star-only badge reserves just its height");
        // Run the production snapshot reader with only native clock/sprites and
        // the active-effect source substituted; PoV's real display config is live.
        FieldInfo source = hud.GetField("_modifiers", All)!, tile = hud.GetField("_tile", All)!;
        object savedSource = source.GetValue(null)!, savedTile = tile.GetValue(null)!;
        Type foreignHud = pov.GetType("PassiveTree.RarityHud", true)!;
        FieldInfo display = foreignHud.GetField("Mode", All)!;
        object? savedDisplay = display.GetValue(null);
        var config = new ConfigFile(Path.Combine(Path.GetTempPath(), "CM-Hud-" + Guid.NewGuid() + ".cfg"), false) { SaveOnConfigSet = false };
        ConfigEntryBase choice = Bind(config, display, "Icons");
        try
        {
            Type rowType = hud.GetNestedType("Row", All)!;
            MethodInfo read = Copy(hud.GetMethod("Read", All)!);
            Type hudData = typeof(EnemyHud).GetNestedType("HudData", All)!;
            object newHud() { object value = FormatterServices.GetUninitializedObject(hudData); hudData.GetField("m_gui", All)!.SetValue(value, New<GameObject>()); return value; }
            Sprite sprite = New<Sprite>();
            int reads = 0;
            source.SetValue(null, (Func<Character, int>)(_ => { reads++; return int.MaxValue; }));
            tile.SetValue(null, (Func<int, Sprite>)(_ => sprite));
            foreach (string selection in new[] { "Icons", "IconsAndNames", "Names", "ColouredStars", "VanillaStars", "Nothing", "ColouredStarsAndNames" })
            {
                choice.SetSerializedValue(selection);
                hud.GetMethod("ClearRows", All)!.Invoke(null, null);
                object data = newHud();
                HudTime = 1;
                object result = read.Invoke(null, new object[] { Actor, data })!;
                Require(((System.Collections.IList)rowType.GetField("Sprites", All)!.GetValue(result)!).Count > 6,
                    "all modifier icons preserved regardless of PoV display: " + selection);
            }
            hud.GetMethod("ClearRows", All)!.Invoke(null, null);
            source.SetValue(null, (Func<Character, int>)(_ => { reads++; return 0; }));
            object cachedHud = newHud();
            reads = 0; HudTime = 2;
            read.Invoke(null, new object[] { Actor, cachedHud });
            HudTime = 2.1f; read.Invoke(null, new object[] { Actor, cachedHud });
            Require(reads == 1, "no per-frame PoV effect query before refresh deadline");
            HudTime = 2.26f; read.Invoke(null, new object[] { Actor, cachedHud });
            Require(reads == 2, "active effect policy is read again after refresh deadline");
            hudData.GetField("m_isMount", All)!.SetValue(cachedHud, true);
            Require(read.Invoke(null, new object[] { Actor, cachedHud }) == null, "mount HUD remains outside takeover");
        }
        finally
        {
            source.SetValue(null, savedSource); tile.SetValue(null, savedTile); display.SetValue(null, savedDisplay);
            hud.GetMethod("ClearRows", All)!.Invoke(null, null);
        }
        foreach (string selection in new[] { "Full", "ExceptLevel", "Off", "Full" })
        {
            mode.SetSerializedValue(selection);
            Require((bool)hud.GetProperty("Enabled", All)!.GetValue(null)!, "combined HUD independent of level system: " + selection);
            Require(ForeignLevels() == (selection == "Full"), "display does not alter gameplay ownership: " + selection);
        }
        Require((bool)hud.GetMethod("IconsPrefix", All)!.Invoke(null, new object?[] { null })!, "unmanaged HUD preserves PoV fallback");
        System.Console.WriteLine("PoV HUD contracts passed: original DLL adapter installation, unchanged status drawing body, invalid lifecycle rejection, 0/1/4/5/8/9/12/26/30 packed slots, star-only height, all seven PoV display modes, throttled live reads, mount/fallback and unchanged level ownership. Unity rendering not executed.");
    }

    private static void CheckRejectedContracts(Type loot)
    {
        var entry = PatchProcessor.GetOriginalInstructions(loot.GetMethod("Prefix", All)!);
        entry[0].opcode = OpCodes.Nop;
        Rejected("ValidateLootEntry", entry);
        foreach (bool finalizer in new[] { false, true })
        {
            var codes = PatchProcessor.GetOriginalInstructions(loot.GetMethod(finalizer ? "Finalizer" : "Postfix", All)!);
            codes.First(code => code.opcode == OpCodes.Brfalse || code.opcode == OpCodes.Brfalse_S).opcode = OpCodes.Brtrue;
            Rejected("ValidateNullStateExit", codes, finalizer);
        }
        Rejected("InstallLoot", typeof(object).Assembly);
        MethodInfo setter = Difficulty.GetMethod("SetScaledMaxHealth", All)!;
        var scope = PatchProcessor.GetOriginalInstructions(setter);
        scope.First(code => code.opcode == OpCodes.Sub).opcode = OpCodes.Add;
        Rejected("ValidateHealthScope", scope, Difficulty.GetField("alreadyScaledMaxHealth", All)!);
        MethodInfo foreign = ForeignLevels.Method;
        var providers = PatchProcessor.GetOriginalInstructions(foreign);
        providers.First(code => Equals(code.operand, "creaturelevelcontrol")).operand = "new.level.provider";
        Rejected("ValidateLevelProviders", providers);
        Require((bool)Bridge.GetProperty("HasHealthBridge", All)!.GetValue(null)!, "invalid loot contract cannot clear an installed health bridge");
    }

    private static void Rejected(string method, params object[] args)
    {
        try { Bridge.GetMethod(method, All)!.Invoke(null, args); }
        catch (TargetInvocationException ex) when (ex.InnerException is NotSupportedException || ex.InnerException is TypeLoadException)
        { return; }
        throw new InvalidOperationException("PoV contract unexpectedly accepted: " + method);
    }

    private static void CheckHealth()
    {
        Maximum = 400; Health = 50;
        Copy(Levels.GetMethod("SetManagedLevel", All)!).Invoke(null, new object[] { Actor, 1 });
        Require(Maximum == 400 && Health == 50, "managed SetLevel preserves severe injury despite same-frame PoV pending conversion");
        SeedPending(Actor, 100, 400);
        Copy(Levels.GetMethod("RestoreStoredHealthDeficit", All)!).Invoke(null, new object[] { Actor, 350f });
        Require(Maximum == 400 && Health == 50, "stored deficit restores final HP without raw conversion");
        SeedPending(Actor, 100, 400);
        Copy(Modifiers.GetMethod("TryRollbackDeathwardCommit", All)!).Invoke(null, new object?[] { Actor, new HitData(), default(HitData.DamageTypes), 50f, null, 0, 0f });
        Require(Health == 50 && !HasPending(Actor), "Deathward rollback restores absolute HP and consumes only target pending conversion");
        Maximum = 400; Health = 200;
        Copy(Levels.GetMethod("ApplyHealthMultiplier", All)!).Invoke(null, new object[] { Actor, 1.5f });
        Require(Maximum == 600 && Health == 400, "raw CM health factor is scaled once and retains 200 missing HP");
        for (int i = 0; i < 3; i++)
            Copy(Levels.GetMethod("ApplyHealthMultiplier", All)!).Invoke(null, new object[] { Actor, 1.5f });
        Require(Maximum == 600 && Health == 400, "repeated restoration neither heals nor loses HP");
        // Genuine old/raw follow-up conversions still work outside CM's final writes.
        Actor.SetMaxHealth(100); Actor.SetHealth(80);
        Require(Maximum == 400 && Health == 380, "PoV raw follow-up still converts missing HP");
        Maximum = 400; Health = 200;
        SeedPending(Actor, 400, 1600);
        Character other = New<Character>(); SeedPending(other, 100, 400);
        Copy(Bridge.GetMethod("SetFinalHealth", All)!).Invoke(null, new object[] { Actor, 250f });
        Require(Health == 250 && HasPending(other) && !HasPending(Actor), "only the target pending conversion is cleared");

        Type settingsType = Modifiers.GetNestedType("ReapingSettings", BindingFlags.NonPublic)!;
        object settings = Activator.CreateInstance(settingsType, All, null, new object[] { .1f, 2, .1f, .2f, 0f, 0f, 0f, 0f }, null)!;
        var gain = Copy(Modifiers.GetMethod("ApplyReapingGain", All)!);
        var restore = Copy(Modifiers.GetMethod("ApplyStoredReapingHealth", All)!);
        SetFloat(Data, Key("ReapingBaseMaxHealthKey"), 400);
        SetFloat(Data, Key("ReapingBonusHealthKey"), 0);
        SetFloat(Data, Key("ReapingMaxHealthCapKey"), .2f);
        Maximum = 400; Health = 200;
        SeedPending(Actor, 400, 1600);
        gain.Invoke(null, new[] { Actor, Data, settings });
        Require(Maximum == 440 && Health == 240, "first Reaping gain adds 40 HP and heals 40 without rescaling");
        gain.Invoke(null, new[] { Actor, Data, settings });
        Require(Maximum == 480 && Health == 280, "Reaping reaches cap after second gain");
        Require(!(bool)gain.Invoke(null, new[] { Actor, Data, settings })!, "exhausted capped gain is unchanged");
        for (int i = 0; i < 3; i++)
        {
            Maximum = 400;
            restore.Invoke(null, new object[] { Actor, Data });
            Require(Maximum == 480 && Health == 280, "stored Reaping restore is final HP");
        }
        // PoV's thread-local suppression must unwind even if the actual write fails.
        Writes = 0; ThrowWrite = true;
        try { Copy(Bridge.GetMethod("SetFinalMaxHealth", All)!).Invoke(null, new object[] { Actor, 500f }); }
        catch (TargetInvocationException) { }
        finally { ThrowWrite = false; }
        Require(Writes == 1 && (int)Difficulty.GetField("alreadyScaledMaxHealth", All)!.GetValue(null)! == 0, "exception unwinds PoV scope without retrying the write");
    }

    private static ConfigEntryBase Bind(ConfigFile config, FieldInfo field, string value)
    {
        Type type = field.FieldType.GetGenericArguments()[0];
        MethodInfo bind = typeof(ConfigFile).GetMethods().Single(m => m.Name == "Bind" && m.IsGenericMethodDefinition &&
            m.GetParameters().Length == 4 && m.GetParameters()[3].ParameterType == typeof(ConfigDescription));
        var entry = (ConfigEntryBase)bind.MakeGenericMethod(type).Invoke(config, new[] { "fixture", field.Name, Enum.Parse(type, value), (object)new ConfigDescription("") })!;
        field.SetValue(null, entry); return entry;
    }
    private static List<KeyValuePair<GameObject, int>> Roll() => (List<KeyValuePair<GameObject, int>>)Generate.Invoke(Dropper, null)!;
    private static string[] LootPatchRegistrations()
    {
        Patches patches = Harmony.GetPatchInfo(Generate);
        return patches.Prefixes.Select(p => "pre:" + PatchIdentity(p)).Concat(patches.Postfixes.Select(p => "post:" + PatchIdentity(p)))
            .Concat(patches.Finalizers.Select(p => "final:" + PatchIdentity(p))).Concat(patches.Transpilers.Select(p => "il:" + PatchIdentity(p))).OrderBy(p => p).ToArray();
    }
    private static string PatchIdentity(Patch patch) => $"{patch.owner}:{patch.index}:{patch.priority}:{patch.PatchMethod}:{string.Join(",", patch.before)}:{string.Join(",", patch.after)}";
    private static IEnumerable<CodeInstruction> LootBody() => new[] { new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PovCompatibilityContracts), nameof(LootResult))), new CodeInstruction(OpCodes.Ret) };
    private static List<KeyValuePair<GameObject, int>> LootResult()
    {
        if (ThrowLoot) throw new InvalidOperationException("loot fixture failure");
        return Rows.Select(row => new KeyValuePair<GameObject, int>(row.m_prefab, 1)).ToList();
    }
    private static IEnumerable<CodeInstruction> TrueBody(MethodBase __originalMethod) => __originalMethod.Name == "IsOwner"
        ? FieldBody(nameof(Owner)) : new[] { new CodeInstruction(OpCodes.Ldc_I4_1), new CodeInstruction(OpCodes.Ret) };
    private static IEnumerable<CodeInstruction> FieldBody(string name) => new[] { new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(PovCompatibilityContracts), name)), new CodeInstruction(OpCodes.Ret) };
    private static IEnumerable<CodeInstruction> KilledBody() => FieldBody(nameof(WasKilled));
    private static IEnumerable<CodeInstruction> LeftAloneBody() => FieldBody(nameof(LeftAlone));
    private static IEnumerable<CodeInstruction> RarityBody() => FieldBody(nameof(RarityWorking));
    private static IEnumerable<CodeInstruction> BossFillBody() => new[] { new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Ldarg_1), new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PovCompatibilityContracts), nameof(GrantBoss))), new CodeInstruction(OpCodes.Ret) };
    private static void GrantBoss(Character c, int count)
    {
        if (BloodFixtureActive) { GrantBloodBoss(c, count); return; }
        Require(Owner && LevelReady && ConfigReady, "grant must have final level and authority");
        string pending = (string)Bridge.GetField("BossPendingKey", All)!.GetRawConstantValue()!;
        Require(!Data.GetBool(pending), "reservation consumed before foreign side effects");
        Require((bool)Bridge.GetMethod("TryCompleteBossModifiers", All)!.Invoke(null, new object[] { c })!, "reentrant callback sees consumed reservation");
        BossGrants++; BossGrantCount = count;
        SetInt(Data, "pt.crmod", 1);
        if (ThrowBossFill) throw new InvalidOperationException("foreign fixture failure after writing modifiers");
    }
    private static IEnumerable<CodeInstruction> HashBody() => new[] { new CodeInstruction(OpCodes.Ldc_I4_0), new CodeInstruction(OpCodes.Ret) };
    private static IEnumerable<CodeInstruction> ViewAwakeBody() => new[] { new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PovCompatibilityContracts), nameof(InitializeView))), new CodeInstruction(OpCodes.Ret) };
    private static void InitializeView(ZNetView view)
    {
        typeof(ZNetView).GetField("m_zdo", All)!.SetValue(view, Data);
        typeof(ZNetView).GetField("m_initZDO", All)!.SetValue(null, null);
    }
    private static Character ViewCharacter(UnityEngine.Component view) => Actor;
    private static IEnumerable<CodeInstruction> CharacterBody(MethodBase __originalMethod)
    {
        string method = __originalMethod.Name;
        yield return new CodeInstruction(OpCodes.Ldarg_0);
        if (method.StartsWith("Set", StringComparison.Ordinal)) yield return new CodeInstruction(OpCodes.Ldarg_1);
        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PovCompatibilityContracts), method));
        yield return new CodeInstruction(OpCodes.Ret);
    }
    private static MethodInfo? Boundary(MethodInfo method)
    {
        if (BloodBoundary(method) is MethodInfo blood) return blood;
        string type = method.DeclaringType!.FullName!, name = method.Name;
        string? replacement = type == "UnityEngine.Object" ? name switch { "op_Equality" => nameof(Equal), "op_Inequality" => nameof(Different), "GetInstanceID" => nameof(InstanceId), _ => null }
            : type == "UnityEngine.Component" && name == "GetComponent" && method.IsGenericMethod && method.GetGenericArguments()[0] == typeof(Character) ? nameof(ViewCharacter)
            : type == "UnityEngine.Time" && name == "get_frameCount" ? nameof(Frame)
            : type == "UnityEngine.Time" && name == "get_unscaledTime" ? nameof(HudClock)
            : type == "UnityEngine.Random" && name == "get_value" ? nameof(RandomValue)
            : type == "UnityEngine.Random" && name == "Range" && method.ReturnType == typeof(float) ? nameof(GroupRandom)
            : type == "PassiveTree.EnemyDifficulty" && name == "Owner" ? nameof(TrueCharacter)
            : type == "PassiveTree.CreatureStars" ? name switch { "OwnStars" => nameof(OwnStars), "Of" => nameof(TwoStars), "VanillaLootFactor" => nameof(Four), "Trophy" or "Single" => nameof(FalsePrefab), _ => null }
            : type == "ZDO" && name == "Set" && method.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(string), typeof(float) }) ? nameof(SetFloat)
            : type == "ZDO" && name == "Set" && method.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(string), typeof(int) }) ? nameof(SetInt)
            : type == "ZDO" && name == "Set" && method.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(string), typeof(bool) }) ? nameof(SetBool)
            : type == "CreatureManager.CreatureDomainManager" && name == "IsSynchronizedConfigurationReady" ? nameof(ReadyConfig)
            : type == Levels.FullName ? name switch { "IsDungeonCreature" => nameof(IsInterior), "IsFrozenKingPhaseTwo" when method.GetParameters()[0].ParameterType == typeof(Character) => nameof(IsFrozen), "IsReadyForModifierApplication" => nameof(ReadyLevel),
                "AllowsModifierEffects" when method.GetParameters().Length == 1 => nameof(FalseCharacter),
                "RegisterRpcs" or "UpdateTimedOutKarmaBonusRequests" => nameof(NoOp), "SynchronizeStoredRuntimeState" => nameof(NoState),
                "ApplyRuntimeVisuals" or "TryApplyLevel" => nameof(NoCharacter), "ShouldPreserveExistingRolls" => nameof(PreserveCharacter), _ => null }
            : type == Modifiers.FullName ? name switch { "HasModifier" => method.GetParameters()[0].ParameterType == typeof(ZDO) ? nameof(HasReaping) : null,
                "IsModifierApplicable" => nameof(Applicable),
                "ApplyStoredReapingScale" => nameof(NoScale), "BroadcastReapingFeedback" => nameof(NoFeedback), "ShowReapingHealText" => nameof(NoHealText), "TryRollModifiers" => nameof(NoCharacter), _ => null }
            : null;
        return replacement == null ? null : AccessTools.Method(typeof(PovCompatibilityContracts), replacement);
    }
    private static IEnumerable<CodeInstruction> PovBoundaries(IEnumerable<CodeInstruction> instructions)
    {
        foreach (CodeInstruction code in instructions)
        {
            if (code.operand is MethodInfo method)
            {
                if (method.DeclaringType == Difficulty && method.Name == "get_Current") { code.opcode = OpCodes.Ldnull; code.operand = null; }
                else if (Boundary(method) is MethodInfo replacement) { code.opcode = OpCodes.Call; code.operand = replacement; }
            }
            else if (code.operand is ConstructorInfo ctor && ctor.DeclaringType == typeof(HashSet<GameObject>))
            {
                code.opcode = OpCodes.Call; code.operand = AccessTools.Method(typeof(PovCompatibilityContracts), nameof(PrefabSet));
            }
            yield return code;
        }
    }
    private static MethodInfo Copy(MethodInfo source)
    {
        if (Copies.TryGetValue(source, out MethodInfo found)) return found;
        using var copy = new DynamicMethodDefinition(source);
        typeof(DynamicMethodDefinition).GetProperty(nameof(DynamicMethodDefinition.OriginalMethod))!.SetValue(copy, null);
        copy.OwnerType = typeof(PovCompatibilityContracts);
        foreach (var instruction in copy.Definition.Body.Instructions)
        {
            if (instruction.Operand is not MethodReference reference || reference.ResolveReflection() is not MethodInfo method) continue;
            MethodInfo? replacement = Boundary(method);
            if (replacement == null && (method.DeclaringType == Levels || method.DeclaringType == Modifiers || method.DeclaringType == Bridge) && method != source)
                replacement = Copy(method);
            if (replacement == null) continue;
            instruction.OpCode = Mono.Cecil.Cil.OpCodes.Call; instruction.Operand = copy.Module.ImportReference(replacement);
        }
        return Copies[source] = copy.Generate();
    }
    private static void SeedPending(Character c, float raw, float scaled)
    {
        object table = Difficulty.GetField("justScaled", All)!.GetValue(null)!;
        Type valueType = table.GetType().GetGenericArguments()[1];
        object value = Activator.CreateInstance(valueType, true)!;
        valueType.GetField("Raw")!.SetValue(value, raw); valueType.GetField("Scaled")!.SetValue(value, scaled); valueType.GetField("Frame")!.SetValue(value, Frame());
        table.GetType().GetMethod("Remove")!.Invoke(table, new object[] { c });
        table.GetType().GetMethod("Add")!.Invoke(table, new[] { c, value });
    }
    private static bool HasPending(Character c)
    {
        object table = Difficulty.GetField("justScaled", All)!.GetValue(null)!;
        return (bool)table.GetType().GetMethod("TryGetValue")!.Invoke(table, new object?[] { c, null })!;
    }
    private static T New<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
    private static string Key(string key) => (string)Modifiers.GetField(key, All)!.GetValue(null)!;
    private static bool Equal(UnityEngine.Object a, UnityEngine.Object b) => ReferenceEquals(a, b);
    private static bool Different(UnityEngine.Object a, UnityEngine.Object b) => !ReferenceEquals(a, b);
    private static bool TrueCharacter(Character c) => true;
    private static bool IsInterior(Character c) => Interior;
    private static bool Applicable(Character c, object spec) => true;
    private static float GroupRandom(float min, float max) { GroupDraws++; return min + (max - min) * GroupRoll; }
    private static bool FalsePrefab(GameObject prefab) => false;
    private static int TwoStars(Character c) => 2;
    private static float Four(Character c) => 4f;
    private static int Frame() => 42;
    private static float RandomValue() => .5f;
    private static float GetMaxHealth(Character c) => Maximum;
    private static float GetHealth(Character c) => Health;
    private static float GetMaxHealthBase(Character c) => 100;
    private static int GetLevel(Character c) => CurrentLevel;
    private static void SetLevel(Character c, int level)
    {
        CurrentLevel = level;
        if (BloodFixtureActive) { BloodLevelWrites++; SetInt(Data, "level", level); }
        c.SetMaxHealth(100f * level);
    }
    private static bool IsPlayer(Character c) => false;
    private static bool IsBoss(Character c) => Boss;
    private static bool IsDead(Character c) => Health <= 0;
    private static bool IsFrozen(Character c) => FrozenPhase;
    private static bool ReadyConfig() => ConfigReady;
    private static bool ReadyLevel(Character c) => LevelReady;
    private static void NoOp() { }
    private static void NoCharacter(Character c) { }
    private static void NoState(Character c, ZDO data) { }
    private static bool FalseCharacter(Character c) => false;
    private static bool PreserveCharacter(Character c) => PreserveRolls;
    private static int InstanceId(UnityEngine.Object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    private static float HudClock() => HudTime;
    private static bool OwnStars(Character c) => !ForeignLevels(); // rarity enabled, non-player fixture
    private static void SetMaxHealth(Character c, float value) { Writes++; if (ThrowWrite) throw new InvalidOperationException("write failure"); Maximum = value; if (Health > value) c.SetHealth(value); }
    private static void SetHealth(Character c, float value) => Health = value;
    private static void SetFloat(ZDO zdo, string key, float value) => ZDOExtraData.Set(zdo.m_uid, key.GetStableHashCode(), value);
    private static void SetInt(ZDO zdo, string key, int value) => ZDOExtraData.Set(zdo.m_uid, key.GetStableHashCode(), value);
    private static void SetBool(ZDO zdo, string key, bool value) => SetInt(zdo, key, value ? 1 : 0);
    private static bool HasReaping(ZDO zdo, long mask) => true;
    private static HashSet<GameObject> PrefabSet() => new(new PrefabComparer());
    private sealed class PrefabComparer : IEqualityComparer<GameObject>
    {
        public bool Equals(GameObject? a, GameObject? b) => ReferenceEquals(a, b);
        public int GetHashCode(GameObject obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
    private static bool NoScale(Character c, ZDO zdo) => false;
    private static void NoFeedback(Character c, bool scale) { }
    private static void NoHealText(Character c, float heal) { }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException("PoV compatibility: " + message); }
}
