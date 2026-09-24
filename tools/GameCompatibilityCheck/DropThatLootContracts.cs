using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using HarmonyLib;
using MethodReference = Mono.Cecil.MethodReference;
using MonoMod.Utils;
using UnityEngine;

// Optional tests against the supplied Drop That binary. It is not a mod build dependency.
internal static class DropThatLootContracts
{
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const string SystemNamespace = "DropThat.Drop.CharacterDropSystem.";

    internal static void Run(Assembly plugin, Assembly dropThat)
    {
        MethodInfo Member(string name, string method) => dropThat.GetType(SystemNamespace + name, true)!.GetMethod(method, Static)!;
        MethodInfo filterPatch = Member("Patches.Patch_CharacterDrop_ConfigureDroplist", "ConfigureDrops");
        MethodInfo trackPatch = Member("Patches.Patch_TrackDrops", "TrackDrop");
        MethodInfo cmPatch = plugin.GetType("CreatureManager.CreatureManagerCharacterLootPatch", true)!.GetMethod("Transpiler", Static)!;
        MethodInfo target = typeof(CharacterDrop).GetMethod(nameof(CharacterDrop.GenerateDropList))!;
        foreach (MethodInfo[] order in new[] { new[] { filterPatch, trackPatch }, new[] { trackPatch, filterPatch } })
        {
            List<CodeInstruction> codes = PatchProcessor.GetOriginalInstructions(target);
            foreach (MethodInfo patch in order.Concat(new[] { cmPatch })) codes = Apply(patch, codes);
            foreach (string hook in new[] { "FilterDrops", "KeepTrack", "ScaleChance", "ScaleAmount" })
                Require(Calls(codes, hook) == 1, "composed GenerateDropList retains " + hook);
            Require(codes.FindIndex(c => c.operand is MethodInfo m && m.Name == "FilterDrops") <
                    codes.FindIndex(c => c.operand is MethodInfo m && m.Name == "ScaleChance"), "conditions run before chance calculation");
        }

        foreach (var path in new[]
        {
            (Target: AccessTools.Method(typeof(Ragdoll), "SaveLootList"), Patch: Member("Patches.Patch_TrackDrops", "TranspileStoreConfigReferences"), Hook: "StoreConfigReferences"),
            (Target: AccessTools.Method(typeof(Ragdoll), "SpawnLoot"), Patch: Member("Patches.Patch_TrackDrops", "TranspileLoadConfigReferences"), Hook: "LoadConfigReferences"),
            (Target: AccessTools.Method(typeof(CharacterDrop), "DropItems"), Patch: Member("Patches.Patch_CharacterDrop_ConfigureDroppedItems", "HookSpawnedItem"), Hook: "StackDropsAndReturnNewIndex")
        })
        {
            var generator = new DynamicMethod("DropThatPathFixture", typeof(void), Type.EmptyTypes).GetILGenerator();
            List<CodeInstruction> codes = Apply(path.Patch, PatchProcessor.GetOriginalInstructions(path.Target, generator), generator);
            Require(Calls(codes, path.Hook) == 1, "original game path retains " + path.Hook);
        }

        CheckFilterAndLimits(dropThat);
        System.Console.WriteLine($"Drop That {dropThat.GetName().Version}: combined transpilers, reference-preserving conditions, original limit postprocessing, and stack/ragdoll patch sites passed. Unity object lookup/position replaced only in managed fixtures; no world or network execution.");
    }

    private static List<CodeInstruction> Apply(MethodInfo patch, List<CodeInstruction> codes, ILGenerator? generator = null) =>
        ((IEnumerable<CodeInstruction>)patch.Invoke(null, patch.GetParameters().Length == 1
            ? new object[] { codes } : new object[] { codes, generator! })!).ToList();

    private static int Calls(List<CodeInstruction> codes, string name) => codes.Count(c => c.operand is MethodInfo m && m.Name == name);

    private static void CheckFilterAndLimits(Assembly assembly)
    {
        Type session = assembly.GetType(SystemNamespace + "Managers.CharacterDropSessionManager", true)!;
        Type templateType = assembly.GetType(SystemNamespace + "Models.CharacterDropDropTemplate", true)!;
        Type infoType = assembly.GetType(SystemNamespace + "Models.DropConfigInfo", true)!;
        Type contextType = assembly.GetType(SystemNamespace + "Models.DropContext", true)!;
        Type cacheType = assembly.GetType(SystemNamespace + "Caches.TempDropListCache", true)!;
        Type configType = assembly.GetType("DropThat.Configuration.GeneralConfig", true)!;
        PropertyInfo configProperty = assembly.GetType("DropThat.Configuration.GeneralConfigManager", true)!.GetProperty("Config", Static)!;
        object? priorConfig = configProperty.GetValue(null);
        object config = Activator.CreateInstance(configType)!;
        configProperty.SetValue(null, config);
        try
        {
            CharacterDrop source = Uninitialized<CharacterDrop>();
            GameObject prefab = Uninitialized<GameObject>();
            // Equal prefab names/objects must not merge entry-specific conditions or limits.
            var allowed = new CharacterDrop.Drop { m_prefab = prefab, m_amountMin = 3, m_amountMax = 3, m_levelMultiplier = true };
            var denied = new CharacterDrop.Drop { m_prefab = prefab, m_levelMultiplier = true };
            var vanilla = new CharacterDrop.Drop { m_prefab = prefab, m_levelMultiplier = false };
            source.m_drops = new List<CharacterDrop.Drop> { allowed, denied, vanilla };
            object table = session.GetProperty("DropInstanceTable", Static)!.GetValue(null)!;
            object allowTemplate = Activator.CreateInstance(templateType)!;
            object denyTemplate = Activator.CreateInstance(templateType)!;
            Type conditionType = MakeConditionType(assembly, contextType);
            object allowCondition = Activator.CreateInstance(conditionType)!;
            object denyCondition = Activator.CreateInstance(conditionType)!;
            conditionType.GetField("Allowed")!.SetValue(allowCondition, true);
            ((IList)templateType.GetProperty("Conditions")!.GetValue(allowTemplate)!).Add(allowCondition);
            ((IList)templateType.GetProperty("Conditions")!.GetValue(denyTemplate)!).Add(denyCondition);
            object Info(object template)
            {
                object info = Activator.CreateInstance(infoType)!;
                infoType.GetProperty("DropTemplate")!.SetValue(info, template);
                return info;
            }
            object allowInfo = Info(allowTemplate);
            table.GetType().GetMethod("Add")!.Invoke(table, new[] { allowed, allowInfo });
            table.GetType().GetMethod("Add")!.Invoke(table, new[] { denied, Info(denyTemplate) });

            // Copy actual binary bodies. Only Unity-backed null/position/cache lookup is
            // replaced; Drop That's condition dispatch and amount-limit logic execute intact.
            using var filter = new DynamicMethodDefinition(session.GetMethod("FilterDrops", Static)!);
            foreach (var instruction in filter.Definition.Body.Instructions)
            {
                if (!(instruction.Operand is MethodReference method)) continue;
                if (method.Name == "IsNull" && method.DeclaringType.FullName == "ThatCore.Extensions.UnityObjectExtensions")
                    instruction.Operand = filter.Module.ImportReference(typeof(DropThatLootContracts).GetMethod(nameof(IsNull), Static)!);
                else if (method.Name == ".ctor" && method.DeclaringType.FullName == contextType.FullName)
                {
                    instruction.OpCode = Mono.Cecil.Cil.OpCodes.Call;
                    instruction.Operand = filter.Module.ImportReference(typeof(DropThatLootContracts).GetMethod(nameof(Context), Static)!.MakeGenericMethod(contextType));
                }
            }
            var filterDrops = (Func<CharacterDrop, List<CharacterDrop.Drop>>)filter.Generate().CreateDelegate(typeof(Func<CharacterDrop, List<CharacterDrop.Drop>>));
            List<CharacterDrop.Drop> result = filterDrops(source);
            Require(result.Count == 2 && ReferenceEquals(result[0], allowed) && ReferenceEquals(result[1], vanilla), "conditions and original drop identity");
            Require(source.m_drops.Count == 3 && allowed.m_amountMin == 3 && allowed.m_levelMultiplier && !vanilla.m_levelMultiplier, "source amounts and ScaleByLevel untouched");
            conditionType.GetField("Allowed")!.SetValue(allowCondition, false);
            Require(filterDrops(source).SequenceEqual(new[] { vanilla }), "changing condition excludes configured item");

            using var limits = new DynamicMethodDefinition(session.GetMethod("LimitDropAmounts", Static)!);
            MethodInfo lookup = cacheType.GetMethod("GetDrop", Static, null, new[] { typeof(object), typeof(int) }, null)!;
            foreach (var instruction in limits.Definition.Body.Instructions)
                if (instruction.Operand is MethodReference method && method.DeclaringType.FullName == cacheType.FullName && method.Name == "GetDrop")
                    instruction.Operand = limits.Module.ImportReference(lookup);
            MethodInfo set = cacheType.GetMethod("SetDrop", Static, null, new[] { typeof(object), infoType, typeof(int?) }, null)!;
            set.Invoke(null, new object[] { source, allowInfo, 0 });
            var limitDrops = (Action<CharacterDrop, List<KeyValuePair<GameObject, int>>>)limits.Generate().CreateDelegate(typeof(Action<CharacterDrop, List<KeyValuePair<GameObject, int>>>));
            object dropLimit = configType.GetField("DropLimit")!.GetValue(config)!;
            PropertyInfo limitValue = dropLimit.GetType().GetProperty("Value")!;
            List<KeyValuePair<GameObject, int>> Drops() => new() { new(prefab, 12), new(prefab, 25), new(prefab, 3) };
            limitValue.SetValue(dropLimit, 10);
            templateType.GetProperty("AmountLimit")!.SetValue(allowTemplate, 7);
            var amounts = Drops();
            limitDrops(source, amounts);
            Require(amounts.Select(x => x.Value).SequenceEqual(new[] { 7, 10, 3 }), "Drop That postprocessing limits scaled amounts by entry");

            // Record the installed library's existing policy; CM must not silently repair it.
            limitValue.SetValue(dropLimit, -1);
            amounts = Drops();
            limitDrops(source, amounts);
            if (amounts[0].Value == 12)
                System.Console.WriteLine("NOTE: supplied Drop That ignores AmountLimit here without a triggered global DropLimit; this existing library behavior is not changed by CM.");
        }
        finally { configProperty.SetValue(null, priorConfig); }
    }

    private static Type MakeConditionType(Assembly assembly, Type contextType)
    {
        Type contract = assembly.GetType(SystemNamespace + "Conditions.IDropCondition", true)!;
        var builder = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName("DropThatConditionFixture"), AssemblyBuilderAccess.Run)
            .DefineDynamicModule("Fixture").DefineType("FixtureCondition", TypeAttributes.Public, typeof(object), new[] { contract });
        FieldBuilder allowed = builder.DefineField("Allowed", typeof(bool), FieldAttributes.Public);
        MethodBuilder valid = builder.DefineMethod("IsValid", MethodAttributes.Public | MethodAttributes.Virtual, typeof(bool), new[] { contextType });
        ILGenerator il = valid.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, allowed);
        il.Emit(OpCodes.Ret);
        MethodBuilder pointless = builder.DefineMethod("IsPointless", MethodAttributes.Public | MethodAttributes.Virtual, typeof(bool), Type.EmptyTypes);
        il = pointless.GetILGenerator();
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Ret);
        return builder.CreateType()!;
    }

    private static bool IsNull(UnityEngine.Object value) => ReferenceEquals(value, null);
    private static T Context<T>(CharacterDrop source) => Uninitialized<T>();
    private static T Uninitialized<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
    private static void Require(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("Drop That contract failed: " + label);
    }
}
