using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using Mono.Cecil;
using MonoMod.Utils;

// Real BepInEx config parsing and compiled cap logic. Only Unity's random-number call is
// substituted to enumerate every possible removal path; no scene or network is started.
internal static class ModifierLimitContracts
{
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static int[] Choices = Array.Empty<int>();
    private static int ChoiceIndex;

    internal static void Run(Assembly plugin)
    {
        Type entry = plugin.GetType("CreatureManager.CreatureManagerPlugin", true)!;
        Type levels = plugin.GetType("CreatureManager.CreatureLevelManager", true)!;
        Type modifiers = plugin.GetType("CreatureManager.CreatureModifierManager", true)!;
        CheckConfig(entry, levels);
        CheckSelection(modifiers);

        // The cap belongs to automatic selection, never the shared write/restore paths.
        MethodInfo cap = modifiers.GetMethod("LimitRolledModifiers", Static)!;
        using var module = ModuleDefinition.ReadModule(plugin.Location);
        string[] callers = module.GetType(modifiers.FullName).Methods
            .Where(m => m.HasBody && m.Body.Instructions.Any(i => i.Operand is MethodReference called &&
                called.DeclaringType.FullName == modifiers.FullName && called.Name == cap.Name))
            .Select(m => m.Name).ToArray();
        Require(callers.SequenceEqual(new[] { "TryRollModifiers" }), "only automatic rolls are capped");
        var roll = PatchProcessor.GetOriginalInstructions(modifiers.GetMethod("TryRollModifiers", Static)!).ToList();
        int capCall = roll.FindIndex(i => Equals(i.operand, cap));
        int rollCall = roll.FindIndex(i => i.operand is MethodInfo m && m.Name == "RollConfiguredMask");
        int storeCall = roll.FindLastIndex(i => i.operand is MethodInfo m && m.Name == "StoreInitialModifierState");
        Require(rollCall < capCall && capCall < storeCall, "cap follows group rolls and precedes initial storage");
        Require((int)modifiers.GetField("MaxActiveModifiers", Static)!.GetRawConstantValue()! == 4, "fixed HUD/forced capacity unchanged");
        System.Console.WriteLine("Modifier limit contracts passed: legacy On/Off/numeric config, five UI choices, save/reload/serialized and boxed values, category/Enforcer precedence, Off gate, exhaustive unbiased subsets including high bits, no extra RNG under cap, automatic-only call path. No Unity scene or multiplayer execution.");
    }

    private static void CheckConfig(Type entry, Type levels)
    {
        string[] fieldNames = { "EnableGlobalModifiers", "EnableBossModifiers", "EnableEnforcerModifiers" };
        string[] keys = { "Global Modifiers", "Boss Modifiers", "Enforcer Modifiers" };
        FieldInfo[] fields = fieldNames.Select(n => entry.GetField(n, Static)!).ToArray();
        object?[] saved = fields.Select(f => f.GetValue(null)).ToArray();
        Type limit = fields[0].FieldType.GetGenericArguments()[0];
        object Value(string name) => Enum.Parse(limit, name);
        MethodInfo bind = typeof(ConfigFile).GetMethods().Single(m => m.Name == "Bind" && m.IsGenericMethodDefinition &&
            m.GetParameters().Length == 4 && m.GetParameters()[3].ParameterType == typeof(ConfigDescription)).MakeGenericMethod(limit);
        MethodInfo maximum = levels.GetMethod("GetMaximumRolledModifiers", Static, null, new[] { typeof(bool), typeof(bool) }, null)!;
        MethodInfo enabled = levels.GetMethod("AreModifiersEnabled", Static, null, new[] { typeof(bool), typeof(bool) }, null)!;
        string path = Path.Combine(Path.GetTempPath(), "CreatureManager-modifier-limits-" + Guid.NewGuid() + ".cfg");
        File.WriteAllText(path, "[2 - Levels]\nGlobal Modifiers = On\nBoss Modifiers = Off\nEnforcer Modifiers = 1\n");
        var config = new ConfigFile(path, false) { SaveOnConfigSet = false };
        var entries = new ConfigEntryBase[3];
        try
        {
            for (int i = 0; i < fields.Length; i++)
            {
                var description = (ConfigDescription)entry.GetMethod("ModifierLimitDescription", Static)!
                    .Invoke(null, new object[] { "fixture", i })!;
                entries[i] = (ConfigEntryBase)bind.Invoke(config, new[] { "2 - Levels", keys[i], Value("Max4"), description })!;
                fields[i].SetValue(null, entries[i]);
                var acceptable = description.AcceptableValues!;
                string[] choices = ((IEnumerable)acceptable.GetType().GetProperty("AcceptableValues")!.GetValue(acceptable)!)
                    .Cast<object>().Select(v => v.ToString()!).ToArray();
                Require(choices.SequenceEqual(new[] { "Max4", "Max3", "Max2", "Max1", "Off" }), "five canonical UI choices");
                Require(acceptable.Clamp(Value("On")).Equals(Value("Max4")), "legacy On normalizes");
                Require(acceptable.Clamp(Enum.ToObject(limit, 999)).Equals(Value("Max4")), "undefined enum uses default");
            }

            Require(entries[0].GetSerializedValue() == "Max4" && entries[1].GetSerializedValue() == "Off" &&
                    entries[2].GetSerializedValue() == "Max4", "initial legacy strings and numeric On");
            config.Save();
            string written = File.ReadAllText(path);
            Require(written.Contains("Global Modifiers = Max4") && written.Contains("Boss Modifiers = Off"), "canonical saved values");

            for (int category = 0; category < 3; category++)
            foreach (var item in new[] { ("Max4", 4), ("Max3", 3), ("Max2", 2), ("Max1", 1), ("Off", 0) })
            {
                foreach (ConfigEntryBase setting in entries) setting.SetSerializedValue("Off");
                entries[category].SetSerializedValue(item.Item1);
                foreach (bool boss in new[] { false, true })
                foreach (bool enforcer in new[] { false, true })
                {
                    int selected = enforcer ? 2 : boss ? 1 : 0;
                    int expected = selected == category ? item.Item2 : 0;
                    object[] args = { boss, enforcer };
                    Require((int)maximum.Invoke(null, args)! == expected, "category cap and Enforcer precedence");
                    Require((bool)enabled.Invoke(null, args)! == (expected > 0), "Off effect gate; all positive caps enabled");
                }
                // ConfigEntry's boxed/serialized paths are also used by synchronization.
                entries[category].BoxedValue = Value("On");
                Require(entries[category].GetSerializedValue() == "Max4", "boxed legacy On normalizes");
                entries[category].SetSerializedValue(item.Item1);
                Require(entries[category].GetSerializedValue() == item.Item1, "canonical serialized round trip");
            }

            File.WriteAllText(path, "[2 - Levels]\nGlobal Modifiers = on\nBoss Modifiers = Max1\nEnforcer Modifiers = Off\n");
            config.Reload();
            Require(entries[0].GetSerializedValue() == "Max4" && entries[1].GetSerializedValue() == "Max1" &&
                    entries[2].GetSerializedValue() == "Off", "live file reload supports legacy and new values");
            foreach (FieldInfo field in fields) field.SetValue(null, null);
            Require((int)maximum.Invoke(null, new object[] { false, false })! == 4, "uninitialized default remains four");
        }
        finally
        {
            for (int i = 0; i < fields.Length; i++) fields[i].SetValue(null, saved[i]);
            File.Delete(path);
        }
    }

    private static void CheckSelection(Type modifiers)
    {
        MethodInfo cap = modifiers.GetMethod("LimitRolledModifiers", Static)!;
        Type maskType = cap.GetParameters()[0].ParameterType;
        using var copy = new DynamicMethodDefinition(cap);
        typeof(DynamicMethodDefinition).GetProperty(nameof(DynamicMethodDefinition.OriginalMethod))!.SetValue(copy, null);
        copy.OwnerType = typeof(ModifierLimitContracts);
        foreach (var instruction in copy.Definition.Body.Instructions)
        {
            if (instruction.Operand is MethodReference method && method.DeclaringType.FullName == "UnityEngine.Random")
                instruction.Operand = copy.Module.ImportReference(typeof(ModifierLimitContracts).GetMethod(nameof(RandomRange), Static)!);
        }
        MethodInfo run = copy.Generate();
        // One member per actual group, including the bit above signed Int32's range.
        long[] bits = { Bit("Undodgeable"), Bit("Unflinching"), Bit("AdrenalineDrain"), Bit("Blamer") };
        long Bit(string name) => Convert.ToInt64(Enum.Parse(maskType, name));
        for (int selection = 0; selection < 16; selection++)
        {
            long input = 0;
            for (int i = 0; i < bits.Length; i++) if ((selection & (1 << i)) != 0) input |= bits[i];
            int count = bits.Count(b => (input & b) != 0);
            for (int max = 0; max <= 4; max++)
            {
                var outcomes = new Dictionary<long, int>();
                foreach (int[] path in Paths(count, max))
                {
                    Choices = path;
                    ChoiceIndex = 0;
                    long result = Convert.ToInt64(run.Invoke(null, new[] { Enum.ToObject(maskType, input), (object)max }));
                    Require((result & ~input) == 0 && bits.Count(b => (result & b) != 0) == Math.Min(count, max), "cap keeps only selected effects");
                    Require(ChoiceIndex == path.Length, "expected RNG calls; none below cap or Off");
                    if (count <= max) Require(result == input, "default/under-cap result unchanged");
                    outcomes[result] = outcomes.TryGetValue(result, out int previous) ? previous + 1 : 1;
                }
                int expectedOutcomes = Enumerable.Range(0, 16).Count(s => (s & ~selection) == 0 &&
                    Enumerable.Range(0, 4).Count(i => (s & (1 << i)) != 0) == Math.Min(count, max));
                Require(outcomes.Count == expectedOutcomes && outcomes.Values.Distinct().Count() == 1,
                    "all valid subsets equally likely; no category order bias");
            }
        }
    }

    private static IEnumerable<int[]> Paths(int count, int max)
    {
        if (max == 0 || count <= max) { yield return Array.Empty<int>(); yield break; }
        for (int choice = 0; choice < count; choice++)
        foreach (int[] rest in Paths(count - 1, max)) yield return new[] { choice }.Concat(rest).ToArray();
    }

    private static int RandomRange(int min, int max)
    {
        Require(min == 0 && ChoiceIndex < Choices.Length && Choices[ChoiceIndex] < max, "random draw bounds");
        return Choices[ChoiceIndex++];
    }

    private static void Require(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("Modifier limit contract failed: " + label);
    }
}
