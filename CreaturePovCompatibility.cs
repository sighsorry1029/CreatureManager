using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using HarmonyLib;

namespace CreatureManager;

// PoV owns raw difficulty scaling. CM must identify writes that already include it.
// Resolve this optional contract after plugin loading, once on both client and server.
internal static class CreaturePovCompatibility
{
    private const string PluginGuid = "treextr.pathofvalheiman";
    private static bool _checked;
    private static bool _otherLevelProvider;
    private static Harmony? _harmony;
    private static Action<Character, float>? _setScaledMaxHealth;
    private static Func<Character, bool>? _removePendingHealth;

    internal static bool HasHealthBridge => _setScaledMaxHealth != null;

    internal static void Initialize()
    {
        if (_checked) return;
        _checked = true;
        if (!Chainloader.PluginInfos.TryGetValue(PluginGuid, out var info)) return;
        try
        {
            // Private state/patch contracts below were verified against this release.
            // Do not silently apply them to a different implementation.
            if (info.Metadata.Version != new System.Version(5, 0, 7))
                throw new NotSupportedException($"unverified PoV version {info.Metadata.Version}");
            Install(info.Instance.GetType().Assembly);
            CreatureManagerPlugin.Log.LogInfo("Path of Valheiman 5.0.7 compatibility enabled: final HP writes, live level ownership, and star-loot ownership.");
        }
        catch (Exception ex)
        {
            try
            {
                RemovePatches();
                CreatureManagerPlugin.Log.LogWarning($"Path of Valheiman compatibility was not enabled; existing behavior is preserved: {ex.Message}");
            }
            catch (Exception cleanup)
            {
                CreatureManagerPlugin.Log.LogWarning($"Path of Valheiman compatibility initialization failed ({ex.Message}), and patch cleanup failed ({cleanup.Message}). Compatibility state is uncertain; restart after resolving the patch conflict.");
            }
        }
    }

    private static void Install(Assembly assembly)
    {
        Type difficulty = assembly.GetType("PassiveTree.EnemyDifficulty", true)!;
        MethodInfo setter = AccessTools.DeclaredMethod(difficulty, "SetScaledMaxHealth", new[] { typeof(Character), typeof(float) });
        FieldInfo pending = AccessTools.DeclaredField(difficulty, "justScaled");
        if (setter == null || !setter.IsStatic || setter.ReturnType != typeof(void) || pending == null || !pending.IsStatic ||
            !pending.FieldType.IsGenericType || pending.FieldType.GetGenericTypeDefinition() != typeof(ConditionalWeakTable<,>) ||
            pending.FieldType.GetGenericArguments()[0] != typeof(Character))
            throw new MissingMemberException("PoV final-health contract changed");
        object table = pending.GetValue(null)!;
        var remove = (Func<Character, bool>)Delegate.CreateDelegate(typeof(Func<Character, bool>), table,
            pending.FieldType.GetMethod("Remove", new[] { typeof(Character) })!);
        var set = (Action<Character, float>)Delegate.CreateDelegate(typeof(Action<Character, float>), setter);

        Type stars = assembly.GetType("PassiveTree.CreatureStars", true)!;
        MethodInfo foreign = AccessTools.DeclaredPropertyGetter(stars, "ForeignLevels");
        Type state = stars.GetNestedType("LootState", BindingFlags.NonPublic)!;
        Type loot = stars.GetNestedType("CharacterDrop_GenerateDropList_Stars", BindingFlags.NonPublic)!;
        MethodInfo prefix = AccessTools.DeclaredMethod(loot, "Prefix", new[] { typeof(CharacterDrop), state.MakeByRefType() });
        if (foreign == null || !foreign.IsStatic || foreign.ReturnType != typeof(bool) || prefix == null ||
            !prefix.IsStatic || prefix.ReturnType != typeof(void) || !prefix.GetParameters()[1].IsOut)
            throw new MissingMemberException("PoV level/loot contract changed");
        ValidateLootEntry(PatchProcessor.GetOriginalInstructions(prefix));

        // Match PoV's other-provider recognition once. CM's own setting is read live;
        // no registry enumeration, reflection, or cached config result on combat paths.
        _otherLevelProvider = Chainloader.PluginInfos.Values.Any(plugin =>
        {
            string name = (plugin?.Metadata?.GUID + " " + plugin?.Metadata?.Name).ToLowerInvariant();
            return name.Contains("starlevelsystem") || name.Contains("creaturelevelcontrol") || name.Contains("creature level and loot");
        });
        _harmony = new Harmony("sighsorry.CreatureManager.PoV");
        _harmony.Patch(foreign, prefix: new HarmonyMethod(typeof(CreaturePovCompatibility), nameof(ForeignLevelsPrefix)));
        _harmony.Patch(prefix, transpiler: new HarmonyMethod(typeof(CreaturePovCompatibility), nameof(LootPrefixTranspiler)));
        // PoV may have already compiled its caller before our late initialization.
        // Rebuild the wrapper with the same registered patches, avoiding a stale inline.
        _harmony.CreateProcessor(AccessTools.DeclaredMethod(typeof(CharacterDrop), nameof(CharacterDrop.GenerateDropList))).Patch();
        _removePendingHealth = remove;
        _setScaledMaxHealth = set;
    }

    private static bool ForeignLevelsPrefix(ref bool __result)
    {
        __result = _otherLevelProvider || CreatureManagerPlugin.EnableLevelSystem == null ||
                   CreatureManagerPlugin.EnableLevelSystem.Value == CreatureManagerPlugin.LevelSystemMode.Full;
        return false;
    }

    private static bool YieldStarLoot() =>
        Chainloader.PluginInfos.ContainsKey("sighsorry.DropNSpawn") || CreatureLoot.IsEnabled();

    private static int ValidateLootEntry(List<CodeInstruction> codes)
    {
        int start = codes.FindIndex(code => code.opcode != OpCodes.Nop);
        if (start < 0 || start + 3 >= codes.Count || codes[start].opcode != OpCodes.Ldarg_1 ||
            codes[start + 1].opcode != OpCodes.Ldnull || codes[start + 2].opcode != OpCodes.Stind_Ref ||
            codes.Take(start + 3).Any(code => code.labels.Count != 0 || code.blocks.Count != 0))
            throw new NotSupportedException("PoV loot prefix no longer initializes its state at entry");
        return start + 3;
    }

    private static IEnumerable<CodeInstruction> LootPrefixTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        List<CodeInstruction> codes = instructions.ToList();
        int index = ValidateLootEntry(codes);
        Label original = generator.DefineLabel();
        codes[index].labels.Add(original);
        // Leave PoV's Postfix/Finalizer a null state: no row flags were changed and no
        // count multiplier is pending. Its separate rarity/extra-loot patches remain.
        codes.InsertRange(index, new[]
        {
            new CodeInstruction(OpCodes.Call, AccessTools.DeclaredMethod(typeof(CreaturePovCompatibility), nameof(YieldStarLoot))),
            new CodeInstruction(OpCodes.Brfalse, original),
            new CodeInstruction(OpCodes.Ret)
        });
        return codes;
    }

    internal static void SetFinalMaxHealth(Character character, float health)
    {
        // Clear before SetMaxHealth too: vanilla can call SetHealth when clamping down.
        _removePendingHealth?.Invoke(character);
        if (_setScaledMaxHealth != null) _setScaledMaxHealth(character, health);
        else character.SetMaxHealth(health);
    }

    internal static void SetFinalHealth(Character character, float health)
    {
        // PoV's one-frame follow-up conversion is only for raw HP, not this final value.
        _removePendingHealth?.Invoke(character);
        character.SetHealth(health);
    }

    private static void RemovePatches()
    {
        _setScaledMaxHealth = null;
        _removePendingHealth = null;
        _otherLevelProvider = false;
        if (_harmony != null)
        {
            _harmony.UnpatchSelf();
            _harmony.CreateProcessor(AccessTools.DeclaredMethod(typeof(CharacterDrop), nameof(CharacterDrop.GenerateDropList))).Patch();
        }
        _harmony = null;
    }

    internal static void Reset()
    {
        try { RemovePatches(); }
        finally { _checked = false; }
    }
}
