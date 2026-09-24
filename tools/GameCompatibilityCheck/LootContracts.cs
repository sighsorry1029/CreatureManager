using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

internal static class LootContracts
{
    private const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private delegate bool Scaling(CharacterDrop.Drop drop, Character character, out int level, out int percent);
    private static float Roll;
    private static int RollCount;
    private static bool Player, Owner, Valid, Boss;
    private static int Level;
    private static ItemDrop? Item;
    private static ZNetView? View;

    internal static void Run(Assembly plugin, string? dropThatPath = null)
    {
        Type loot = plugin.GetType("CreatureManager.CreatureLoot", true)!;
        Type entry = plugin.GetType("CreatureManager.CreatureManagerPlugin", true)!;
        FieldInfo modeField = entry.GetField("LootSystem", Static)!;
        FieldInfo creatureField = entry.GetField("AdditionalLootChancePerStarCreature", Static)!;
        FieldInfo bossField = entry.GetField("AdditionalLootChancePerStarBoss", Static)!;
        object?[] saved = { modeField.GetValue(null), creatureField.GetValue(null), bossField.GetValue(null) };
        string[] guids = { "sighsorry.DropNSpawn", "asharppen.valheim.drop_that" };
        Require(guids.All(guid => !Chainloader.PluginInfos.ContainsKey(guid)), "isolated plugin registry");
        var config = new ConfigFile(Path.Combine(Path.GetTempPath(), "CreatureManager-loot-fixture-" + Guid.NewGuid() + ".cfg"), false)
        {
            SaveOnConfigSet = false
        };
        Type modeType = modeField.FieldType.GetGenericArguments()[0];
        MethodInfo bind = typeof(ConfigFile).GetMethods().Single(m => m.Name == "Bind" && m.IsGenericMethodDefinition &&
            m.GetParameters().Length == 4 && m.GetParameters()[3].ParameterType == typeof(ConfigDescription));
        var mode = (ConfigEntryBase)bind.MakeGenericMethod(modeType).Invoke(config,
            new[] { "fixture", "mode", Enum.ToObject(modeType, 0), (object)new ConfigDescription("") })!;
        modeField.SetValue(null, mode);
        creatureField.SetValue(null, config.Bind("fixture", "creatures", 25));
        bossField.SetValue(null, config.Bind("fixture", "bosses", 75));
        try
        {
            var enabled = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), loot.GetMethod("IsEnabled", Static)!);
            Require(!enabled(), "Vanilla does not activate scaling");
            var untouchedDrop = new CharacterDrop.Drop { m_levelMultiplier = true, m_chance = 0.2f };
            object?[] chanceArgs = { 0.2f, 4f, untouchedDrop, null };
            object?[] amountArgs = { 3, 4, untouchedDrop, null };
            Require((float)loot.GetMethod("ScaleChance", Static)!.Invoke(null, chanceArgs)! == 0.8f, "Vanilla chance retained");
            Require((int)loot.GetMethod("ScaleAmount", Static)!.Invoke(null, amountArgs)! == 12, "Vanilla amount retained");
            mode.BoxedValue = Enum.ToObject(modeType, 1);
            Require(enabled(), "standalone CalculateChance activates");
            // Added after the initial probe: verifies the DNS-after-CM load order.
            Chainloader.PluginInfos.Add(guids[0], null!);
            Require(!enabled(), "loaded DNS disables scaling");
            Require((int)mode.BoxedValue == 1, "compatibility guard does not rewrite config");
            Require((float)loot.GetMethod("ScaleChance", Static)!.Invoke(null, chanceArgs)! == 0.8f, "external chance retained");
            Require((int)loot.GetMethod("ScaleAmount", Static)!.Invoke(null, amountArgs)! == 12, "external amount retained");
            Chainloader.PluginInfos.Remove(guids[0]);
            Require(enabled(), "no stale compatibility cache");

            CheckAmounts(loot);
            CheckEligibility(loot);
            CheckTranspiler(plugin, entry, loot);

            Chainloader.PluginInfos.Add(guids[1], null!);
            Require(enabled(), "Drop That alone permits CM scaling");
            CheckEligibility(loot);
            Chainloader.PluginInfos.Add(guids[0], null!);
            Require(!enabled(), "DNS retains priority when both drop mods are loaded");
            Chainloader.PluginInfos.Remove(guids[0]);
            if (dropThatPath != null) DropThatLootContracts.Run(plugin, Assembly.LoadFrom(dropThatPath));
        }
        finally
        {
            foreach (string guid in guids) Chainloader.PluginInfos.Remove(guid);
            modeField.SetValue(null, saved[0]);
            creatureField.SetValue(null, saved[1]);
            bossField.SetValue(null, saved[2]);
            loot.GetMethod("ResetRuntimeState", Static)!.Invoke(null, null);
        }

        System.Console.WriteLine("Loot contracts passed: production rounding/limits, eligibility/ownership fixtures, boss/creature settings, late external-mod detection, and atomic IL patch. No Unity scene or network session.");
    }

    private static void CheckAmounts(Type loot)
    {
        var calculate = (Func<int, int, int, int>)CopyManagedMethod(loot.GetMethod("CalculateAmount", Static)!, typeof(Func<int, int, int, int>));
        foreach (var sample in new[] { (1, 1, 50, 1), (1, 3, 50, 2), (3, 3, 50, 6), (1, 6, 100, 6),
                     (7, 50, 0, 7), (0, 50, 100, 0), (-1, 50, 100, -1), (100, 50, 100, 100),
                     (int.MaxValue, int.MaxValue, 100, 100), (99, int.MaxValue, 100, 100), (1, int.MinValue, 50, 1),
                     (3, 3, -10, 3), (3, 3, 150, 9) })
        {
            RollCount = 0;
            Require(calculate(sample.Item1, sample.Item2, sample.Item3) == sample.Item4, "amount/cap: " + sample);
            Require(RollCount == 0, "integer/empty/capped amounts do not consume RNG");
        }

        int total = 0;
        for (int i = 0; i < 100; i++)
        {
            Roll = (i + 0.5f) / 100f;
            RollCount = 0;
            total += calculate(1, 2, 50);
            Require(RollCount == 1, "fractional remainder draws exactly once");
        }
        Require(total == 150, "one-star 50% expectation is 1.5");
        Roll = 0.49f;
        Require(calculate(1, 4, 50) == 3 && calculate(5, 2, 50) == 8, "fraction rounds up");
        Roll = 0.5f;
        Require(calculate(1, 4, 50) == 2 && calculate(5, 2, 50) == 7, "fraction boundary rounds down");
    }

    private static void CheckEligibility(Type loot)
    {
        // Execute the compiled production guard, replacing only calls that require native Unity
        // objects with controlled fixtures. This does not verify real ownership transfer or prefabs.
        var scaling = (Scaling)CopyManagedMethod(loot.GetMethod("TryGetScaling", Static)!, typeof(Scaling));
        Character character = Uninitialized<Character>();
        var drop = new CharacterDrop.Drop { m_prefab = Uninitialized<GameObject>(), m_levelMultiplier = true };
        Item = Uninitialized<ItemDrop>();
        Item.m_itemData = Uninitialized<ItemDrop.ItemData>();
        Item.m_itemData.m_shared = Uninitialized<ItemDrop.ItemData.SharedData>();
        View = Uninitialized<ZNetView>();
        Item.m_itemData.m_shared.m_itemType = ItemDrop.ItemData.ItemType.Material;
        Player = Boss = false;
        Owner = Valid = true;
        Level = 4;
        Require(scaling(drop, character, out int level, out int percent) && level == 4 && percent == 25, "creature percentage");
        Boss = true;
        Require(scaling(drop, character, out _, out percent) && percent == 75, "boss percentage");
        Boss = false;
        foreach (Action change in new Action[] { () => drop.m_levelMultiplier = false, () => drop.m_onePerPlayer = true,
                     () => Player = true, () => Owner = false, () => Valid = false, () => Level = 1,
                     () => Item.m_itemData.m_shared.m_itemType = ItemDrop.ItemData.ItemType.Trophy })
        {
            change();
            Require(!scaling(drop, character, out _, out _), "excluded reward or non-authoritative instance");
            drop.m_levelMultiplier = true;
            drop.m_onePerPlayer = Player = false;
            Owner = Valid = true;
            Level = 4;
            Item.m_itemData.m_shared.m_itemType = ItemDrop.ItemData.ItemType.Material;
        }
        Item = null;
        Require(!scaling(drop, character, out _, out _), "non-item prefab excluded");
        Require(!scaling(drop, null!, out _, out _), "missing character excluded");
    }

    private static void CheckTranspiler(Assembly plugin, Type entry, Type loot)
    {
        MethodInfo target = typeof(CharacterDrop).GetMethod(nameof(CharacterDrop.GenerateDropList))!;
        MethodInfo transpiler = plugin.GetType("CreatureManager.CreatureManagerCharacterLootPatch", true)!.GetMethod("Transpiler", Static)!;
        var original = PatchProcessor.GetOriginalInstructions(target);
        var rewritten = ((IEnumerable<CodeInstruction>)transpiler.Invoke(null, new object[] { original })!).ToList();
        foreach (string name in new[] { "ScaleChance", "ScaleAmount" })
            Require(rewritten.Count(code => code.Calls(loot.GetMethod(name, Static)!)) == 1, "one replacement per multiplier");
        FieldInfo characterField = typeof(CharacterDrop).GetField("m_character", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Require(characterField.IsPrivate, "original CharacterDrop private field metadata");
        Require(rewritten.Count(c => c.LoadsField(characterField)) == original.Count(c => c.LoadsField(characterField)) + 2, "explicit Harmony access to owning character");
        FieldInfo onePerPlayer = typeof(CharacterDrop.Drop).GetField(nameof(CharacterDrop.Drop.m_onePerPlayer))!;
        Require(rewritten.FindIndex(c => c.Calls(loot.GetMethod("ScaleAmount", Static)!)) < rewritten.FindIndex(c => c.LoadsField(onePerPlayer)), "scale before vanilla player override and cap");
        Require(original.Where(c => c.operand is MethodInfo).All(c => rewritten.Any(r => r.opcode == c.opcode && Equals(r.operand, c.operand))), "vanilla resource/pseudo-drop calls retained");

        // An altered game body must preserve both original sites, rather than applying half a patch.
        PropertyInfo logger = entry.GetProperty("Log", Static)!;
        object priorLog = logger.GetValue(null)!;
        using var expectedLog = BepInEx.Logging.Logger.CreateLogSource("Expected loot mismatch fixture");
        int warnings = 0;
        expectedLog.LogEvent += (_, e) => { if (e.Level == BepInEx.Logging.LogLevel.Warning) warnings++; };
        logger.SetValue(null, expectedLog);
        try
        {
            var broken = PatchProcessor.GetOriginalInstructions(target);
            int field = broken.FindIndex(c => c.LoadsField(typeof(CharacterDrop.Drop).GetField(nameof(CharacterDrop.Drop.m_levelMultiplier))!));
            broken[field + 5] = new CodeInstruction(OpCodes.Nop); // Remove the chance multiply.
            var result = ((IEnumerable<CodeInstruction>)transpiler.Invoke(null, new object[] { broken })!).ToList();
            Require(result.SequenceEqual(broken) && warnings == 1, "unrecognized IL leaves complete original calculation");
        }
        finally { logger.SetValue(null, priorLog); }
    }

    private static Delegate CopyManagedMethod(MethodInfo source, Type delegateType)
    {
        var method = new DynamicMethod("LootFixture_" + source.Name, source.ReturnType,
            source.GetParameters().Select(p => p.ParameterType).ToArray(), typeof(LootContracts), true);
        ILGenerator il = method.GetILGenerator();
        LocalBuilder[] locals = source.GetMethodBody()!.LocalVariables.Select(v => il.DeclareLocal(v.LocalType)).ToArray();
        var instructions = PatchProcessor.GetOriginalInstructions(source);
        var labels = new Dictionary<Label, Label>();
        Label Map(Label value) { if (!labels.TryGetValue(value, out Label mapped)) labels[value] = mapped = il.DefineLabel(); return mapped; }
        foreach (CodeInstruction code in instructions)
        {
            Require(code.blocks.Count == 0, "fixture method has no exception blocks");
            foreach (Label label in code.labels) il.MarkLabel(Map(label));
            OpCode op = code.opcode;
            if (code.operand is MethodInfo called)
            {
                string? replacement = called.DeclaringType == typeof(UnityEngine.Random) ? nameof(NextRoll)
                    : called.DeclaringType == typeof(UnityEngine.Object) && called.Name == "op_Equality" ? nameof(SameObject)
                    : called.Name == "TryGetComponent" ? (called.GetGenericArguments()[0] == typeof(ZNetView) ? nameof(GetView) : nameof(GetItem))
                    : called.DeclaringType == typeof(Character) ? called.Name == "IsPlayer" ? nameof(IsPlayer)
                        : called.Name == "IsBoss" ? nameof(IsBoss) : called.Name == "GetLevel" ? nameof(GetLevel) : null
                    : called.DeclaringType == typeof(ZNetView) ? called.Name == "IsValid" ? nameof(IsValid) : called.Name == "IsOwner" ? nameof(IsOwner) : null
                    : null;
                if (replacement != null) { op = OpCodes.Call; called = typeof(LootContracts).GetMethod(replacement, Static)!; }
                il.Emit(op, called);
            }
            else if (code.operand is FieldInfo field) il.Emit(op, field);
            else if (code.operand is LocalBuilder local) il.Emit(op, locals[local.LocalIndex]);
            else if (code.operand is Label label) il.Emit(op, Map(label));
            else if (code.operand is int integer) il.Emit(op, integer);
            else if (code.operand is long wide) il.Emit(op, wide);
            else if (code.operand is float single) il.Emit(op, single);
            else if (code.operand is double real) il.Emit(op, real);
            else if (code.operand is sbyte small) il.Emit(op, small);
            else if (code.operand == null) il.Emit(op);
            else throw new InvalidOperationException("Unexpected fixture IL: " + code);
        }
        return method.CreateDelegate(delegateType);
    }

    private static T Uninitialized<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
    private static float NextRoll() { RollCount++; return Roll; }
    private static bool SameObject(UnityEngine.Object left, UnityEngine.Object right) => ReferenceEquals(left, right);
    private static bool GetView(Component source, out ZNetView view) { view = View!; return !ReferenceEquals(view, null); }
    private static bool GetItem(GameObject source, out ItemDrop item) { item = Item!; return !ReferenceEquals(item, null); }
    private static bool IsPlayer(Character character) => Player;
    private static bool IsBoss(Character character) => Boss;
    private static int GetLevel(Character character) => Level;
    private static bool IsValid(ZNetView view) => Valid;
    private static bool IsOwner(ZNetView view) => Owner;
    private static void Require(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("Loot contract failed: " + label);
    }
}
