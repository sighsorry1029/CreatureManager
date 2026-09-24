using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

namespace CreatureManager;

// Only replaces CharacterDrop's star multipliers. Vanilla still rolls, saves and spawns the loot.
internal static class CreatureLoot
{
    private const string DropNSpawnGuid = "sighsorry.DropNSpawn";
    private static bool _compatibilityNoticeLogged;

    internal static void ResetRuntimeState() => _compatibilityNoticeLogged = false;

    internal static bool IsEnabled()
    {
        if (CreatureManagerPlugin.LootSystem?.Value != CreatureManagerPlugin.CharacterLootSystem.CalculateChance)
        {
            return false;
        }

        // Check at use, after BepInEx has loaded the plugins. DNS declares a soft dependency
        // on CM, so caching this in CM's Awake would miss DNS and adding the reverse dependency
        // would create a cycle. Checking only when generating loot needs no polling or event state.
        if (!Chainloader.PluginInfos.ContainsKey(DropNSpawnGuid))
        {
            return true;
        }

        if (!_compatibilityNoticeLogged)
        {
            _compatibilityNoticeLogged = true;
            CreatureManagerPlugin.Log.LogInfo("Character Loot System: CalculateChance is inactive because DropNSpawn is loaded. CreatureManager leaves loot scaling to DropNSpawn; saved settings are unchanged.");
        }

        return false;
    }

    private static bool TryGetScaling(CharacterDrop.Drop drop, Character character, out int level, out int percent)
    {
        level = 1;
        percent = 0;
        if (!IsEnabled() || !drop.m_levelMultiplier || drop.m_onePerPlayer ||
            character == null || character.IsPlayer() ||
            !character.TryGetComponent(out ZNetView nview) || !nview.IsValid() || !nview.IsOwner() ||
            drop.m_prefab == null || !drop.m_prefab.TryGetComponent(out ItemDrop item))
        {
            return false;
        }

        ItemDrop.ItemData.SharedData? shared = item.m_itemData?.m_shared;
        if (shared == null || shared.m_itemType == ItemDrop.ItemData.ItemType.Trophy)
        {
            return false;
        }

        level = character.GetLevel();
        percent = (character.IsBoss()
            ? CreatureManagerPlugin.AdditionalLootChancePerStarBoss
            : CreatureManagerPlugin.AdditionalLootChancePerStarCreature)?.Value ?? 50;
        return level > 1;
    }

    internal static float ScaleChance(float chance, float vanillaFactor, CharacterDrop.Drop drop, Character character)
    {
        return TryGetScaling(drop, character, out _, out _) ? chance : chance * vanillaFactor;
    }

    internal static int ScaleAmount(int amount, int vanillaFactor, CharacterDrop.Drop drop, Character character)
    {
        return TryGetScaling(drop, character, out int level, out int percent)
            ? CalculateAmount(amount, level, percent)
            : unchecked(amount * vanillaFactor);
    }

    internal static int CalculateAmount(int amount, int level, int percent)
    {
        // Preserve empty rolls; retain vanilla's per-entry cap before doing potentially large
        // arithmetic. Below the cap this integer calculation is exact, even at extreme levels.
        if (amount <= 0 || amount >= 100)
        {
            return Math.Min(amount, 100);
        }

        long stars = Math.Max(0L, (long)level - 1);
        long hundredths = amount * (100L + stars * Math.Max(0, Math.Min(percent, 100)));
        int result = (int)Math.Min(100L, hundredths / 100);
        long remainder = hundredths % 100;
        if (result < 100 && remainder > 0 && UnityEngine.Random.value < remainder / 100f)
        {
            result++;
        }

        return result;
    }
}

[HarmonyPatch(typeof(CharacterDrop), nameof(CharacterDrop.GenerateDropList))]
internal static class CreatureManagerCharacterLootPatch
{
    private static void Prefix()
    {
        // Report the compatibility guard even if another mod disables every levelMultiplier
        // before the original loop, so neither multiplier helper would otherwise be reached.
        _ = CreatureLoot.IsEnabled();
    }

    [HarmonyPriority(Priority.Last)]
    [HarmonyAfter("asharppen.valheim.drop_that")]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> codes = instructions.ToList();
        FieldInfo levelMultiplier = AccessTools.Field(typeof(CharacterDrop.Drop), nameof(CharacterDrop.Drop.m_levelMultiplier));
        FieldInfo character = AccessTools.Field(typeof(CharacterDrop), "m_character");
        MethodInfo chance = AccessTools.Method(typeof(CreatureLoot), nameof(CreatureLoot.ScaleChance));
        MethodInfo amount = AccessTools.Method(typeof(CreatureLoot), nameof(CreatureLoot.ScaleAmount));
        List<(int Index, CodeInstruction Drop, MethodInfo Method)> matches = new();
        int multiplierReads = 0;
        for (int i = 1; i + 6 < codes.Count; i++)
        {
            if (!codes[i].LoadsField(levelMultiplier)) continue;
            multiplierReads++;
            // Match the two guarded multiplications, not other chance/resource/pseudo-drop
            // arithmetic. Discover locals instead of depending on their compiler-assigned slots.
            if (LoadLocalIndex(codes[i - 1]) < 0 ||
                (codes[i + 1].opcode != OpCodes.Brfalse && codes[i + 1].opcode != OpCodes.Brfalse_S) ||
                LoadLocalIndex(codes[i + 2]) < 0 || LoadLocalIndex(codes[i + 3]) < 0)
            {
                continue;
            }

            bool isChance = codes[i + 4].opcode == OpCodes.Conv_R4;
            int multiply = i + (isChance ? 5 : 4);
            if (codes[multiply].opcode != OpCodes.Mul ||
                StoreLocalIndex(codes[multiply + 1]) != LoadLocalIndex(codes[i + 2]))
            {
                continue;
            }

            matches.Add((multiply, codes[i - 1], isChance ? chance : amount));
        }

        if (character == null || chance == null || amount == null || multiplierReads != 2 ||
            matches.Count != 2 || matches.Count(match => match.Method == chance) != 1 ||
            matches.Count(match => match.Method == amount) != 1)
        {
            CreatureManagerPlugin.Log.LogWarning("Could not locate both CharacterDrop star multipliers; CreatureManager loot scaling is disabled and the existing drop calculation is preserved.");
            return codes;
        }

        // Validate both sites before changing either. A partial patch could keep exponential
        // amounts with base chances (or the reverse). Drop instances and their metadata stay intact.
        foreach (var match in matches.OrderByDescending(match => match.Index))
        {
            CodeInstruction original = codes[match.Index];
            CodeInstruction loadDrop = new(match.Drop.opcode, match.Drop.operand);
            loadDrop.labels.AddRange(original.labels);
            loadDrop.blocks.AddRange(original.blocks);
            codes.RemoveAt(match.Index);
            codes.InsertRange(match.Index, new[]
            {
                loadDrop,
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, character),
                new CodeInstruction(OpCodes.Call, match.Method)
            });
        }

        return codes;
    }

    private static int LoadLocalIndex(CodeInstruction code)
    {
        if (code.opcode == OpCodes.Ldloc_0) return 0;
        if (code.opcode == OpCodes.Ldloc_1) return 1;
        if (code.opcode == OpCodes.Ldloc_2) return 2;
        if (code.opcode == OpCodes.Ldloc_3) return 3;
        return code.opcode == OpCodes.Ldloc || code.opcode == OpCodes.Ldloc_S
            ? OperandLocalIndex(code.operand) : -1;
    }

    private static int StoreLocalIndex(CodeInstruction code)
    {
        if (code.opcode == OpCodes.Stloc_0) return 0;
        if (code.opcode == OpCodes.Stloc_1) return 1;
        if (code.opcode == OpCodes.Stloc_2) return 2;
        if (code.opcode == OpCodes.Stloc_3) return 3;
        return code.opcode == OpCodes.Stloc || code.opcode == OpCodes.Stloc_S
            ? OperandLocalIndex(code.operand) : -1;
    }

    private static int OperandLocalIndex(object operand) => operand switch
    {
        LocalVariableInfo local => local.LocalIndex,
        byte index => index,
        short index => index,
        int index => index,
        _ => -1
    };
}
