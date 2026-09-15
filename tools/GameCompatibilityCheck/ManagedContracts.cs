using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using HarmonyLib;
using UnityEngine;

internal static class ManagedContracts
{
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    internal static void Run(Assembly plugin)
    {
        // Only managed fields are touched on these fixtures. No native Unity object is created.
        Type appearance = plugin.GetType("CreatureManager.CreatureAppearanceRuntime", true)!;
        VisEquipment vis = (VisEquipment)FormatterServices.GetUninitializedObject(typeof(VisEquipment));
        var hair = (AccessTools.FieldRef<VisEquipment, int>)appearance.GetField("HairItemHash", Static)!.GetValue(null)!;
        var beard = (AccessTools.FieldRef<VisEquipment, int>)appearance.GetField("BeardItemHash", Static)!.GetValue(null)!;
        Require(typeof(VisEquipment).GetField("m_hairItem", Instance)!.IsPrivate && typeof(VisEquipment).GetField("m_beardItem", Instance)!.IsPrivate, "Runtime reference retains original private access");
        hair(vis) = 123;
        beard(vis) = -456;
        Require((int)typeof(VisEquipment).GetField("m_hairItem", Instance)!.GetValue(vis)! == 123, "Original private hair field write");
        Require((int)typeof(VisEquipment).GetField("m_beardItem", Instance)!.GetValue(vis)! == -456, "Original private beard field write");
        MethodInfo hash = appearance.GetMethod("GetAccessoryHash", Static)!;
        Require((int)hash.Invoke(null, new object?[] { null })! == 0, "Null accessory sentinel");
        Require((int)hash.Invoke(null, new object[] { "" })! == 0, "Empty accessory sentinel");
        Require((int)hash.Invoke(null, new object[] { "Hair1" })! == "Hair1".GetStableHashCode(), "Accessory stable hash");
        appearance.GetMethod("EnsureDelegates", Static)!.Invoke(null, null);
        foreach (string field in new[] { "_setHairEquipped", "_setBeardEquipped", "_getHairItem", "_updateLodgroup", "_updateBaseModel" })
            Require(appearance.GetField(field, Static)!.GetValue(null) != null, "Appearance delegate " + field);

        Type modifiers = plugin.GetType("CreatureManager.CreatureModifierManager", true)!;
        Require(modifiers.GetField("AddSpiritDamage", Static)!.GetValue(null) is Action<Character, float, short>, "Spirit delegate on original DLL");
        CheckSnapshot(modifiers);
        CheckLevelPersistence(plugin);

        // This calls the production direct private-field access in assembly_guiutils, using only managed data.
        Type localizationManager = plugin.GetType("CreatureManager.CreatureServerLocalization", true)!;
        Localization localization = (Localization)FormatterServices.GetUninitializedObject(typeof(Localization));
        var translations = new Dictionary<string, string> { ["fixture"] = "vanilla" };
        typeof(Localization).GetField("m_translations", Instance)!.SetValue(localization, translations);
        MethodInfo apply = localizationManager.GetMethod("ApplyTranslation", Static)!;
        MethodInfo restore = localizationManager.GetMethod("RestoreTranslationIfOwned", Static)!;
        apply.Invoke(null, new object[] { localization, "fixture", "server" });
        Require(translations["fixture"] == "server", "Server translation applied");
        restore.Invoke(null, new object[] { localization, "fixture" });
        Require(translations["fixture"] == "vanilla", "Owned translation restored");
        apply.Invoke(null, new object[] { localization, "fixture", "server" });
        translations["fixture"] = "another mod";
        restore.Invoke(null, new object[] { localization, "fixture" });
        Require(translations["fixture"] == "another mod", "Foreign translation preserved");
        apply.Invoke(null, new object[] { localization, "new_fixture", "server" });
        restore.Invoke(null, new object[] { localization, "new_fixture" });
        Require(!translations.ContainsKey("new_fixture"), "New token removed on restore");

        Type commands = plugin.GetType("CreatureManager.CreatureConsoleCommands", true)!;
        commands.GetMethod("Register", Static)!.Invoke(null, null);
        foreach (string field in new[] { "SpawnCommand", "KarmaCommand" })
        {
            var command = (Terminal.ConsoleCommand)commands.GetField(field, Static)!.GetValue(null)!;
            Require(command.IsCheat && command.OnlyAdmin && command.OnlyServer && command.IsNetwork && command.RemoteCommand && !command.HideBehindDevCommands, "Console authority policy: " + field);
            var args = new Terminal.ConsoleEventArgs(command.Command + " fixture", null!, command);
            Require(ReferenceEquals(args.Commmand, command) && args.Length == 2, "Console command context: " + field);
        }

        MethodInfo? languageSetup = AccessTools.DeclaredMethod(typeof(Localization), "SetupLanguage", new[] { typeof(string) });
        Require(languageSetup != null && AccessTools.DeclaredMethod(typeof(FejdStartup), "SetupGui", Type.EmptyTypes) != null, "Dynamic localization patches");
        Require(AccessTools.Field(typeof(HitData), "m_ranged") != null && AccessTools.Field(typeof(MonsterAI), "m_targetCreature") != null, "Ranged/AI reflection paths");
        Require(typeof(BaseAI).GetField("m_pathAgentType")?.FieldType.IsEnum == true, "AI YAML enum reflection path");
        Require(typeof(Player).GetMethod("IsCrouching", Instance)?.ReturnType == typeof(bool), "Sneak HUD reflection path");
        Type configType = plugin.GetType("ServerSync.ConfigSync", true)!;
        object config = Activator.CreateInstance(configType, new object[] { "compatibility-fixture" })!;
        Type valueType = plugin.GetType("ServerSync.CustomSyncedValue`1", true)!.MakeGenericType(typeof(string));
        object synced = Activator.CreateInstance(valueType, new[] { config, "fixture", "initial", (object)0 })!;
        valueType.GetProperty("Value")!.SetValue(synced, "updated");
        Require((string)valueType.GetProperty("Value")!.GetValue(synced)! == "updated", "ServerSync initial value/update without literal-field exception");
        foreach (var input in new[] { new Vector2s(-32768, 32767), new Vector2s(-1, -1), new Vector2s(0, 0), new Vector2s(170, -170) })
        {
            Vector2i internalZone = input.ToVector2i();
            Require(new Vector2s(internalZone) == input, "Zone conversion round trip");
        }
        var distance = new SimulationDistance(1, 0, classic: true);
        Require(distance.IsClassic && distance.NearSimulationDistance == 1 && distance.TotalSimulationDistance == 1, "3x3 query policy");
        System.Console.WriteLine("Managed contracts passed: accessory access/delegates, snapshot v2, localization ownership, console authority, zone conversion, reflection paths.");
    }

    private static void CheckLevelPersistence(Assembly plugin)
    {
        Type levels = plugin.GetType("CreatureManager.CreatureLevelManager", true)!;
        MethodInfo restore = levels.GetMethod("SynchronizeStoredRuntimeState", Static)!;
        Func<ZDO, int, int> selectLevel = ExtractLevelSelection(restore);

        var cases = new[]
        {
            (Label: "fed level survives stale initial level", Saved: (int?)3, Initial: (int?)1, Expected: 3),
            (Label: "intentional downgrade survives reload", Saved: (int?)1, Initial: (int?)5, Expected: 1),
            (Label: "saved level without CM assignment", Saved: (int?)4, Initial: (int?)null, Expected: 4),
            (Label: "absent game level uses vanilla default", Saved: (int?)null, Initial: (int?)5, Expected: 1),
            (Label: "invalid saved level is clamped", Saved: (int?)0, Initial: (int?)5, Expected: 1)
        };
        uint nextId = 400;
        foreach (var item in cases)
        {
            // Completion and preserved-roll paths share this production selection. Health,
            // visuals, lifecycle dispatch and ownership are not executed by these fixtures.
            foreach (bool complete in new[] { true, false })
            {
                var id = new ZDOID(987654321L, nextId++);
                ZDO zdo = (ZDO)FormatterServices.GetUninitializedObject(typeof(ZDO));
                zdo.m_uid = id;
                if (item.Saved.HasValue) ZDOExtraData.Set(id, ZDOVars.s_level, item.Saved.Value);
                if (item.Initial.HasValue) ZDOExtraData.Set(id, "CreatureManager_DesiredLevel".GetStableHashCode(), item.Initial.Value);
                ZDOExtraData.Set(id, "CreatureManager_LevelProcessingComplete".GetStableHashCode(), complete ? 1 : 0);
                ZDOExtraData.Set(id, "CreatureManager_ModifiersApplied".GetStableHashCode(), 1);
                ZDOExtraData.Set(id, "CreatureManager_LevelHealthMultiplier".GetStableHashCode(), 2.5f);
                ZDOExtraData.Set(id, "CreatureManager_LevelDamageMultiplier".GetStableHashCode(), 1.75f);

                // Repeated reads must not consume, migrate, or overwrite persisted state.
                for (int reload = 0; reload < 2; reload++)
                {
                    Require(selectLevel(zdo, 9) == item.Expected, "Level reload: " + item.Label);
                    Require(zdo.GetInt(ZDOVars.s_level, -999) == (item.Saved ?? -999), "Reload does not write game level");
                    Require(zdo.GetInt("CreatureManager_DesiredLevel", -999) == (item.Initial ?? -999), "Reload does not rewrite initial assignment");
                    Require(zdo.GetBool("CreatureManager_LevelProcessingComplete", false) == complete &&
                            zdo.GetBool("CreatureManager_ModifiersApplied", false), "Reload preserves processing markers");
                    Require(zdo.GetFloat("CreatureManager_LevelHealthMultiplier", 0f) == 2.5f &&
                            zdo.GetFloat("CreatureManager_LevelDamageMultiplier", 0f) == 1.75f, "Reload preserves stored stat rolls");
                    Require(zdo.DataRevision == 0, "Reload adds no network data revision");
                }
            }
        }
        System.Console.WriteLine("Level persistence contracts passed: 10 saved-state fixtures, 20 compiled level selections; no Unity lifecycle or network session.");
    }

    private static Func<ZDO, int, int> ExtractLevelSelection(MethodInfo restore)
    {
        // Execute the final DLL's actual selection IL, not a duplicate of the formula.
        // Stop before Character mutation/health restoration, which require Unity native setup.
        // The old implementation's GetLevel fallback is supplied as a fixture integer.
        var method = new DynamicMethod("SavedCreatureLevel", typeof(int), new[] { typeof(ZDO), typeof(int) });
        ILGenerator il = method.GetILGenerator();
        LocalBuilder[] locals = restore.GetMethodBody()!.LocalVariables.Select(v => il.DeclareLocal(v.LocalType)).ToArray();
        Require(locals.Length > 0 && locals[0].LocalType == typeof(int), "Level selection local contract");
        bool assigned = false;
        foreach (CodeInstruction instruction in PatchProcessor.GetOriginalInstructions(restore))
        {
            OpCode op = instruction.opcode;
            if (op == OpCodes.Ldarg_0 && assigned)
            {
                il.Emit(OpCodes.Ldloc_0);
                il.Emit(OpCodes.Ret);
                return (Func<ZDO, int, int>)method.CreateDelegate(typeof(Func<ZDO, int, int>));
            }
            if (op == OpCodes.Ldarg_0) { il.Emit(OpCodes.Ldarg_1); continue; }
            if (op == OpCodes.Ldarg_1) { il.Emit(OpCodes.Ldarg_0); continue; }
            if (op.FlowControl == FlowControl.Branch || op.FlowControl == FlowControl.Cond_Branch || op == OpCodes.Ret)
                throw new InvalidOperationException("Unexpected control flow in level selection: " + instruction);
            if (instruction.operand is MethodInfo called)
            {
                if (called.DeclaringType == typeof(Character) && called.Name == "GetLevel") continue;
                Require((called.DeclaringType == typeof(ZDO) && called.Name == "GetInt") ||
                        (called.DeclaringType == typeof(Math) && called.Name == "Max"), "Read-only selection call: " + called);
                il.Emit(op, called);
            }
            else if (instruction.operand is string text) il.Emit(op, text);
            else if (instruction.operand is FieldInfo field)
            {
                Require(op == OpCodes.Ldsfld && field.DeclaringType == typeof(ZDOVars), "Game hash read only");
                il.Emit(op, field);
            }
            else if (instruction.operand is LocalBuilder local) il.Emit(op, locals[local.LocalIndex]);
            else if (instruction.operand == null) il.Emit(op);
            else throw new InvalidOperationException("Unsupported level selection instruction: " + instruction);
            assigned |= op == OpCodes.Stloc_0 ||
                        ((op == OpCodes.Stloc || op == OpCodes.Stloc_S) && instruction.operand is LocalBuilder target && target.LocalIndex == 0);
        }
        throw new InvalidOperationException("Missing level selection boundary");
    }

    private static void CheckSnapshot(Type modifiers)
    {
        var keys = (HashSet<int>)modifiers.GetField("ModifierStateKeyHashes", Static)!.GetValue(null)!;
        int applied = "CreatureManager_ModifiersApplied".GetStableHashCode();
        int[] sample = keys.Where(k => k != applied).OrderBy(k => k).Take(6).ToArray();
        var id = new ZDOID(987654321L, 123u);
        // Original 1.0 data collection API; direct setters avoid world/revision machinery in this fixture.
        ZDOExtraData.Set(id, sample[0], 1.25f);
        ZDOExtraData.Set(id, sample[1], new Vector3(1, 2, 3));
        ZDOExtraData.Set(id, sample[2], Quaternion.identity);
        ZDOExtraData.Set(id, applied, 1);
        ZDOExtraData.Set(id, sample[3], 123456789L);
        ZDOExtraData.Set(id, sample[4], "한글 snapshot");
        ZDOExtraData.Set(id, sample[5], new byte[] { 0, 1, 255 });
        ZDOExtraData.Set(id, "foreign.mod".GetStableHashCode(), 999f);
        ZDOExtraData.GetData(id, out var floats, out var vectors, out var quats, out var ints, out var longs, out var strings, out var bytes, out _);
        ZPackage package = new();
        package.Write(2);
        Write(modifiers, package, floats, (p, x) => p.Write(x));
        Write(modifiers, package, vectors, (p, x) => p.Write(x));
        Write(modifiers, package, quats, (p, x) => p.Write(x));
        Write(modifiers, package, ints, (p, x) => p.Write(x));
        Write(modifiers, package, longs, (p, x) => p.Write(x));
        Write(modifiers, package, strings, (p, x) => p.Write(x));
        Write(modifiers, package, bytes, (p, x) => p.Write(x));
        MethodInfo read = modifiers.GetMethod("TryDeserializeModifierState", Static)!;
        object?[] args = { Convert.ToBase64String(package.GetArray()), null, null };
        Require((bool)read.Invoke(null, args)!, "Snapshot v2 accepted: " + args[2]);
        object snapshot = args[1]!;
        var decodedFloats = (List<KeyValuePair<int, float>>)snapshot.GetType().GetProperty("Floats", Instance)!.GetValue(snapshot)!;
        Require(decodedFloats.Count == 1 && decodedFloats[0].Value == 1.25f, "Snapshot allowlist excludes foreign data");
        foreach (string invalid in new[] { "not base64", Convert.ToBase64String(package.GetArray().Concat(new byte[] { 1 }).ToArray()), Convert.ToBase64String(new byte[] { 1, 0, 0, 0 }) })
            Require(!(bool)read.Invoke(null, new object?[] { invalid, null, null })!, "Malformed/old/trailing snapshot rejected");
        // Independently inspect all seven typed sections and counts in their persisted order.
        package.SetPos(0);
        Require(package.ReadInt() == 2, "Snapshot version preserved");
        Require(package.ReadInt() == 1 && package.ReadInt() == sample[0] && package.ReadSingle() == 1.25f, "Float section");
        Require(package.ReadInt() == 1 && package.ReadInt() == sample[1] && package.ReadVector3() == new Vector3(1, 2, 3), "Vector section");
        Require(package.ReadInt() == 1 && package.ReadInt() == sample[2] && package.ReadQuaternion() == Quaternion.identity, "Quaternion section");
        Require(package.ReadInt() == 1 && package.ReadInt() == applied && package.ReadInt() == 1, "Int section");
        Require(package.ReadInt() == 1 && package.ReadInt() == sample[3] && package.ReadLong() == 123456789L, "Long section");
        Require(package.ReadInt() == 1 && package.ReadInt() == sample[4] && package.ReadString() == "한글 snapshot", "String section");
        Require(package.ReadInt() == 1 && package.ReadInt() == sample[5] && package.ReadByteArray().SequenceEqual(new byte[] { 0, 1, 255 }), "Byte-array section");
        Require(package.GetPos() == package.Size(), "No trailing snapshot bytes");
    }

    private static void Write<T>(Type modifiers, ZPackage package, IEnumerable<KeyValuePair<int, T>> entries, Action<ZPackage, T> writer) =>
        modifiers.GetMethod("WriteModifierStateEntries", Static)!.MakeGenericMethod(typeof(T)).Invoke(null, new object[] { package, entries, writer });

    private static void Require(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException("Contract failed: " + label);
    }
}
