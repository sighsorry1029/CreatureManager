using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;

namespace CreatureManager;

// PoV owns raw difficulty scaling. CM must identify writes that already include it.
// Resolve this optional contract after plugin loading, once on both client and server.
internal static class CreaturePovCompatibility
{
    internal const string PluginGuid = "treextr.pathofvalheiman";
    private static bool _checked;
    private static bool _otherLevelProvider;
    private static Harmony? _levelHarmony;
    private static Harmony? _lootHarmony;
    private static Harmony? _bossHarmony;
    private static Harmony? _hudHarmony;
    private static Func<bool>? _hudForeignLevels;
    private static Func<UnityEngine.GameObject, bool>? _hudForeignStars;
    private static Action<Character, float>? _setScaledMaxHealth;
    private static Func<Character, bool>? _removePendingHealth;
    private const string BossPendingKey = "CreatureManager_PovBossModifiersPending";
    private static Func<bool>? _bossOn, _rarityWorking;
    private static Func<Character, bool>? _killedBefore, _leftAlone;
    private static ConfigEntry<bool>? _bossAfterFirstKill;
    private static ConfigEntry<int>? _bossModsPerStar;
    private static Action<Character, int>? _fillBossModifiers;
    private static AccessTools.FieldRef<Character, ZNetView>? _characterView;
    private static bool _bossFailureReported;

    internal static bool HasHealthBridge => _setScaledMaxHealth != null;

    internal static void Initialize()
    {
        if (_checked) return;
        _checked = true;
        if (!Chainloader.PluginInfos.TryGetValue(PluginGuid, out var info)) return;
        Assembly assembly;
        try
        {
            CreatureManagerPlugin.Log.LogInfo($"Checking Path of Valheiman {info.Metadata.Version} interoperability contracts.");
            assembly = info.Instance.GetType().Assembly;
        }
        catch (Exception ex)
        {
            CreatureManagerPlugin.Log.LogWarning($"Path of Valheiman plugin could not be inspected: {ex.Message}");
            return;
        }
        Install(assembly);
    }

    private static void Install(Assembly assembly)
    {
        Enable("final HP", () => InstallHealth(assembly), ClearHealth);
        Enable("live level ownership", () => InstallLevels(assembly), () => RemoveHarmony(ref _levelHarmony));
        Enable("star-loot ownership", () => InstallLoot(assembly), RemoveLoot);
        Enable("Full boss modifiers", () => InstallBosses(assembly), RemoveBosses);
        Enable("star display", () => InstallHud(assembly), RemoveHud);
    }

    private static void Enable(string feature, Action install, Action cleanup)
    {
        try
        {
            install();
            CreatureManagerPlugin.Log.LogInfo($"Path of Valheiman {feature} compatibility enabled.");
        }
        catch (Exception ex)
        {
            try
            {
                cleanup();
                CreatureManagerPlugin.Log.LogWarning($"Path of Valheiman {feature} compatibility disabled: {ex.Message}");
            }
            catch (Exception rollback)
            {
                CreatureManagerPlugin.Log.LogWarning($"Path of Valheiman {feature} compatibility failed ({ex.Message}); cleanup also failed ({rollback.Message}). Restart after resolving the patch conflict.");
            }
        }
    }

    private static void InstallHealth(Assembly assembly)
    {
        Type difficulty = assembly.GetType("PassiveTree.EnemyDifficulty", true)!;
        MethodInfo setter = AccessTools.DeclaredMethod(difficulty, "SetScaledMaxHealth", new[] { typeof(Character), typeof(float) });
        FieldInfo pending = AccessTools.DeclaredField(difficulty, "justScaled");
        if (setter == null || !setter.IsStatic || setter.ReturnType != typeof(void) || pending == null || !pending.IsStatic ||
            !pending.FieldType.IsGenericType || pending.FieldType.GetGenericTypeDefinition() != typeof(ConditionalWeakTable<,>) ||
            pending.FieldType.GetGenericArguments()[0] != typeof(Character))
            throw new MissingMemberException("PoV final-health contract changed");
        FieldInfo scope = AccessTools.DeclaredField(difficulty, "alreadyScaledMaxHealth");
        if (scope == null || !scope.IsStatic || scope.FieldType != typeof(int) ||
            !scope.IsDefined(typeof(ThreadStaticAttribute), false) ||
            setter.GetMethodBody()?.ExceptionHandlingClauses.Any(clause => clause.Flags == ExceptionHandlingClauseOptions.Finally) != true)
            throw new NotSupportedException("PoV scaled-health scope no longer has thread-local exception cleanup");
        RequireMember(setter, scope, OpCodes.Stsfld);
        ValidateHealthScope(PatchProcessor.GetOriginalInstructions(setter), scope);
        RequireMember(setter, AccessTools.DeclaredMethod(typeof(Character), nameof(Character.SetMaxHealth)));
        MethodInfo maxPrefix = StaticMethod(difficulty.GetNestedType("MaxHealth", BindingFlags.NonPublic)!, "Prefix", typeof(void),
            typeof(Character), typeof(float).MakeByRefType(), difficulty.GetNestedType("MaxHealthState")!.MakeByRefType());
        RequireMember(maxPrefix, scope, OpCodes.Ldsfld);
        MethodInfo follow = StaticMethod(difficulty.GetNestedType("FollowMaxHealth", BindingFlags.NonPublic)!, "Prefix", typeof(void),
            typeof(Character), typeof(float).MakeByRefType());
        RequireMember(follow, pending, OpCodes.Ldsfld);
        RequireMember(follow, pending.FieldType.GetMethod("TryGetValue"));
        RequireMember(follow, pending.FieldType.GetMethod("Remove"));
        object table = pending.GetValue(null)!;
        var remove = (Func<Character, bool>)Delegate.CreateDelegate(typeof(Func<Character, bool>), table,
            pending.FieldType.GetMethod("Remove", new[] { typeof(Character) })!);
        var set = (Action<Character, float>)Delegate.CreateDelegate(typeof(Action<Character, float>), setter);
        _removePendingHealth = remove;
        _setScaledMaxHealth = set;
    }

    private static void InstallLevels(Assembly assembly)
    {
        Type stars = assembly.GetType("PassiveTree.CreatureStars", true)!;
        MethodInfo foreign = AccessTools.DeclaredPropertyGetter(stars, "ForeignLevels");
        if (foreign == null || !foreign.IsStatic || foreign.ReturnType != typeof(bool))
            throw new MissingMemberException("PoV foreign-level getter changed");
        // We replace this getter, including its cache. Reject a different provider policy
        // instead of silently dropping newly recognized providers in a future PoV.
        ValidateLevelProviders(PatchProcessor.GetOriginalInstructions(foreign));

        // Match PoV's other-provider recognition once. CM's own setting is read live;
        // no registry enumeration, reflection, or cached config result on combat paths.
        _otherLevelProvider = Chainloader.PluginInfos.Values.Any(plugin =>
        {
            string name = (plugin?.Metadata?.GUID + " " + plugin?.Metadata?.Name).ToLowerInvariant();
            return name.Contains("starlevelsystem") || name.Contains("creaturelevelcontrol") || name.Contains("creature level and loot");
        });
        _levelHarmony = new Harmony("sighsorry.CreatureManager.PoV.Levels");
        _levelHarmony.Patch(foreign, prefix: new HarmonyMethod(typeof(CreaturePovCompatibility), nameof(ForeignLevelsPrefix)));
    }

    private static void InstallLoot(Assembly assembly)
    {
        Type stars = assembly.GetType("PassiveTree.CreatureStars", true)!;
        Type state = stars.GetNestedType("LootState", BindingFlags.NonPublic)!;
        Type loot = stars.GetNestedType("CharacterDrop_GenerateDropList_Stars", BindingFlags.NonPublic)!;
        MethodInfo prefix = StaticMethod(loot, "Prefix", typeof(void), typeof(CharacterDrop), state.MakeByRefType());
        if (!prefix.GetParameters()[1].IsOut) throw new MissingMemberException("PoV loot state is no longer an out parameter");
        ValidateLootEntry(PatchProcessor.GetOriginalInstructions(prefix));
        ValidateNullStateExit(PatchProcessor.GetOriginalInstructions(StaticMethod(loot, "Postfix", typeof(void), state,
            typeof(List<KeyValuePair<UnityEngine.GameObject, int>>).MakeByRefType())), false);
        ValidateNullStateExit(PatchProcessor.GetOriginalInstructions(StaticMethod(loot, "Finalizer", typeof(Exception), typeof(Exception), state)), true);
        _lootHarmony = new Harmony("sighsorry.CreatureManager.PoV.Loot");
        _lootHarmony.Patch(prefix, transpiler: new HarmonyMethod(typeof(CreaturePovCompatibility), nameof(LootPrefixTranspiler)));
        // PoV may have already compiled its caller before our late initialization.
        // Rebuild the wrapper with the same registered patches, avoiding a stale inline.
        RebuildLootCaller(_lootHarmony);
    }

    private static MethodInfo StaticMethod(Type type, string name, Type returns, params Type[] parameters)
    {
        MethodInfo method = AccessTools.DeclaredMethod(type, name, parameters);
        if (method == null || !method.IsStatic || method.ReturnType != returns)
            throw new MissingMemberException($"PoV contract changed: {type?.FullName}.{name}");
        return method;
    }

    private static ConfigEntry<T>? ConfigEntry<T>(Type type, string name)
    {
        FieldInfo field = AccessTools.DeclaredField(type, name);
        if (field == null || !field.IsStatic || field.FieldType != typeof(ConfigEntry<T>))
            throw new MissingMemberException($"PoV config contract changed: {type.Name}.{name}");
        return (ConfigEntry<T>?)field.GetValue(null);
    }

    private static void InstallBosses(Assembly assembly)
    {
        if (_levelHarmony == null) throw new NotSupportedException("live level ownership bridge is unavailable");
        Type boss = assembly.GetType("PassiveTree.BossStars", true)!;
        Type mods = assembly.GetType("PassiveTree.RarityMods", true)!;
        Type rarity = assembly.GetType("PassiveTree.CreatureRarity", true)!;
        Type api = assembly.GetType("Valheiman.Api.Creatures", true)!;
        Type difficulty = assembly.GetType("PassiveTree.EnemyDifficulty", true)!;
        MethodInfo fill = StaticMethod(mods, "Fill", typeof(void), typeof(Character), typeof(int));
        Type modBits = mods.GetNestedType("Mod")!;
        if (modBits == null || !modBits.IsEnum || Enum.GetUnderlyingType(modBits) != typeof(int))
            throw new NotSupportedException("PoV stored modifier representation changed");
        MethodInfo set = StaticMethod(mods, "Set", typeof(void), typeof(Character), modBits);
        MethodInfo stored = StaticMethod(mods, "Stored", modBits, typeof(Character));
        RequireMember(fill, stored);
        RequireMember(fill, set);
        RequireLiteral(stored, "pt.crmod");
        RequireLiteral(set, "pt.crmod");
        MethodInfo on = StaticMethod(boss, "get_On", typeof(bool));
        MethodInfo working = StaticMethod(rarity, "get_Working", typeof(bool));
        RequireMember(working, AccessTools.DeclaredPropertyGetter(difficulty, "SystemOn"));
        _bossOn = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), on);
        _rarityWorking = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), working);
        _killedBefore = (Func<Character, bool>)Delegate.CreateDelegate(typeof(Func<Character, bool>), StaticMethod(boss, "KilledBefore", typeof(bool), typeof(Character)));
        _leftAlone = (Func<Character, bool>)Delegate.CreateDelegate(typeof(Func<Character, bool>), StaticMethod(api, "IsLeftAlone", typeof(bool), typeof(Character)));
        _bossAfterFirstKill = ConfigEntry<bool>(boss, "AfterFirstKill");
        _bossModsPerStar = ConfigEntry<int>(boss, "ModsPerStar");
        // Character.m_nview is protected in the original game DLL, not a public API.
        _characterView = AccessTools.FieldRefAccess<Character, ZNetView>("m_nview");
        _fillBossModifiers = (Action<Character, int>)Delegate.CreateDelegate(typeof(Action<Character, int>), fill);
        MethodInfo awake = AccessTools.DeclaredMethod(typeof(ZNetView), "Awake");
        RequireMember(awake, AccessTools.DeclaredField(typeof(ZNetView), "m_initZDO"));
        _bossHarmony = new Harmony("sighsorry.CreatureManager.PoV.Bosses");
        _bossHarmony.Patch(awake, prefix: new HarmonyMethod(typeof(CreaturePovCompatibility), nameof(BossViewPrefix)),
            postfix: new HarmonyMethod(typeof(CreaturePovCompatibility), nameof(BossViewPostfix)));
    }

    private static void RequireLiteral(MethodInfo method, string value)
    {
        if (!PatchProcessor.GetOriginalInstructions(method).Any(code => code.opcode == OpCodes.Ldstr && Equals(code.operand, value)))
            throw new NotSupportedException($"PoV stored-state contract changed: {method.DeclaringType?.Name}.{method.Name}");
    }

    private static bool BossModifiersAllowed(Character character)
    {
        try
        {
            return CreatureManagerPlugin.EnableLevelSystem?.Value == CreatureManagerPlugin.LevelSystemMode.Full &&
                   _bossOn!() && _rarityWorking!() && !_leftAlone!(character) &&
                   (_bossAfterFirstKill?.Value != true || _killedBefore!(character)) && (_bossModsPerStar?.Value ?? 0) > 0;
        }
        catch (Exception ex) { DisableBossGrants(ex); return false; }
    }

    private static void BossViewPrefix(ZDO ___m_initZDO, ZDO ___m_zdo, out bool __state)
    {
        // m_initZDO is consumed by Awake when recreating a saved/remote object.
        // Inspect it before Awake, not from Character.Awake after it was cleared.
        __state = _fillBossModifiers != null && ___m_initZDO == null && ___m_zdo == null;
    }

    private static void BossViewPostfix(ZNetView __instance, bool ___m_ghost, bool __state)
    {
        if (!__state || ___m_ghost || !__instance.IsValid() || !__instance.IsOwner()) return;
        Character character = __instance.GetComponent<Character>();
        if (character == null || !character.IsBoss() || character.IsPlayer() ||
            (character.m_nViewOverride != null && character.m_nViewOverride != __instance)) return;
        ReserveBossModifiers(character, __instance.GetZDO());
    }

    private static void ReserveBossModifiers(Character character, ZDO zdo)
    {
        if (zdo == null || zdo.GetInt("pt.crmod") != 0 || CreatureLevelManager.IsFrozenKingPhaseTwo(character)) return;
        // Do not consult unsynchronized settings or character HP during Awake.
        // The existing Character Start/spawn lifecycle queues the final owner decision.
        zdo.Set(BossPendingKey, true);
    }

    // False means the existing CM pending-level queue must retain this character.
    // The ZDO reservation survives a save/owner change; unmarked old bosses are untouched.
    internal static bool TryCompleteBossModifiers(Character character)
    {
        if (_fillBossModifiers == null || character == null) return true;
        ZNetView view = _characterView!(character);
        if (view == null || !view.IsValid()) return true;
        ZDO zdo = view.GetZDO();
        if (zdo == null || !zdo.GetBool(BossPendingKey)) return true;
        if (!view.IsOwner() || !CreatureDomainManager.IsSynchronizedConfigurationReady()) return false;
        bool allowed = character.IsBoss() && !character.IsPlayer() && !character.IsDead() &&
                       !CreatureLevelManager.IsFrozenKingPhaseTwo(character) &&
                       !CreatureLevelManager.ShouldPreserveExistingRolls(character) && BossModifiersAllowed(character);
        if (allowed && !CreatureLevelManager.IsReadyForModifierApplication(character)) return false;
        // Consume before calling PoV: Fill may partially write flags/HP/wards before
        // throwing, and must never be retried or re-entered as a new grant.
        zdo.Set(BossPendingKey, false);
        if (!allowed || zdo.GetInt("pt.crmod") != 0) return true;
        long count = (long)Math.Max(0, character.GetLevel() - 1) * (_bossModsPerStar?.Value ?? 0);
        if (count <= 0) return true;
        try { _fillBossModifiers(character, (int)Math.Min(int.MaxValue, count)); }
        catch (Exception ex) { DisableBossGrants(ex); }
        return true;
    }

    private static void DisableBossGrants(Exception ex)
    {
        _fillBossModifiers = null;
        if (!_bossFailureReported)
        {
            _bossFailureReported = true;
            CreatureManagerPlugin.Log.LogWarning($"PoV boss modifier compatibility disabled until restart; the failed grant will not be retried: {ex.Message}");
        }
    }

    // PoV owns only presentation here. Never change ForeignLevels globally to
    // select a HUD: that getter also controls combat, health, drops and star rolls.
    internal static bool UsesPovStarDisplay => _hudHarmony != null && !_otherLevelProvider;

    private static void InstallHud(Assembly assembly)
    {
        if (_levelHarmony == null) throw new NotSupportedException("live level ownership bridge is unavailable");
        Type hud = assembly.GetType("PassiveTree.RarityHud", true)!;
        Type stars = assembly.GetType("PassiveTree.CreatureStars", true)!;
        MethodInfo icons = StaticMethod(hud, "Icons", typeof(void), AccessTools.Inner(typeof(EnemyHud), "HudData"), typeof(Character));
        MethodInfo foreign = StaticMethod(stars, "get_ForeignLevels", typeof(bool));
        MethodInfo scan = StaticMethod(hud, "ForeignStars", typeof(bool), typeof(UnityEngine.GameObject));
        ValidateHudCalls(PatchProcessor.GetOriginalInstructions(icons), foreign, scan);
        RequireMember(StaticMethod(hud.GetNestedType("EnemyHud_UpdateHuds_Rarity", BindingFlags.NonPublic)!, "Postfix", typeof(void), typeof(EnemyHud)), icons);
        _hudForeignLevels = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), foreign);
        _hudForeignStars = (Func<UnityEngine.GameObject, bool>)Delegate.CreateDelegate(typeof(Func<UnityEngine.GameObject, bool>), scan);
        _hudHarmony = new Harmony("sighsorry.CreatureManager.PoV.Hud");
        _hudHarmony.Patch(icons, transpiler: new HarmonyMethod(typeof(CreaturePovCompatibility), nameof(HudTranspiler)));
        RebuildHudCaller(_hudHarmony);
    }

    private static void ValidateHudCalls(List<CodeInstruction> codes, MethodInfo foreign, MethodInfo scan)
    {
        if (codes.Count(code => code.Calls(foreign)) != 1 || codes.Count(code => code.Calls(scan)) != 1)
            throw new NotSupportedException("PoV HUD no longer has one level-provider and one foreign-star check");
    }

    private static IEnumerable<CodeInstruction> HudTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
    {
        List<CodeInstruction> codes = instructions.ToList();
        Type hud = __originalMethod.DeclaringType!;
        MethodInfo foreign = AccessTools.DeclaredPropertyGetter(hud.Assembly.GetType("PassiveTree.CreatureStars"), "ForeignLevels");
        MethodInfo scan = AccessTools.DeclaredMethod(hud, "ForeignStars");
        ValidateHudCalls(codes, foreign, scan);
        foreach (CodeInstruction code in codes)
        {
            if (code.Calls(foreign)) code.operand = AccessTools.DeclaredMethod(typeof(CreaturePovCompatibility), nameof(HudForeignLevels));
            else if (code.Calls(scan)) code.operand = AccessTools.DeclaredMethod(typeof(CreaturePovCompatibility), nameof(HudForeignStars));
        }
        return codes;
    }

    private static bool HudForeignLevels() => !UsesPovStarDisplay && _hudForeignLevels!();
    private static bool HudForeignStars(UnityEngine.GameObject gui) => !UsesPovStarDisplay && _hudForeignStars!(gui);

    private static void RebuildHudCaller(Harmony harmony) =>
        harmony.CreateProcessor(AccessTools.DeclaredMethod(typeof(EnemyHud), "UpdateHuds")).Patch();

    private static void RemoveHud()
    {
        if (_hudHarmony != null)
        {
            _hudHarmony.UnpatchSelf();
            RebuildHudCaller(_hudHarmony);
            _hudHarmony = null;
        }
        _hudForeignLevels = null;
        _hudForeignStars = null;
    }

    private static void ValidateHealthScope(List<CodeInstruction> instructions, FieldInfo scope)
    {
        List<CodeInstruction> codes = instructions.Where(code => code.opcode != OpCodes.Nop).ToList();
        foreach (OpCode operation in new[] { OpCodes.Add, OpCodes.Sub })
            if (!Enumerable.Range(0, Math.Max(0, codes.Count - 3)).Any(i =>
                    codes[i].opcode == OpCodes.Ldsfld && Equals(codes[i].operand, scope) && codes[i + 1].opcode == OpCodes.Ldc_I4_1 &&
                    codes[i + 2].opcode == operation && codes[i + 3].opcode == OpCodes.Stsfld && Equals(codes[i + 3].operand, scope)))
                throw new NotSupportedException("PoV scaled-health scope no longer balances its nesting counter");
    }

    private static void ValidateLevelProviders(List<CodeInstruction> instructions)
    {
        List<CodeInstruction> codes = instructions.Where(code => code.opcode != OpCodes.Nop).ToList();
        var providers = new HashSet<string>();
        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].operand is not MethodInfo method || method.DeclaringType != typeof(string) || method.Name != nameof(string.Contains)) continue;
            if (i == 0 || codes[i - 1].opcode != OpCodes.Ldstr || method.GetParameters().Length != 1)
                throw new NotSupportedException("PoV foreign-level recognition no longer uses literal provider names");
            providers.Add((string)codes[i - 1].operand);
        }
        if (!providers.SetEquals(new[] { "starlevelsystem", "creaturelevelcontrol", "creature level and loot", "sighsorry.creaturemanager" }))
            throw new NotSupportedException("PoV foreign-level provider recognition changed");
    }

    private static void RequireMember(MethodInfo method, MemberInfo? member, OpCode? opcode = null)
    {
        if (member == null || !PatchProcessor.GetOriginalInstructions(method).Any(code =>
                Equals(code.operand, member) && (!opcode.HasValue || code.opcode == opcode.Value)))
            throw new NotSupportedException($"PoV contract changed: {method.DeclaringType?.Name}.{method.Name} no longer uses {member?.Name}");
    }

    private static void ValidateNullStateExit(List<CodeInstruction> instructions, bool finalizer)
    {
        List<CodeInstruction> codes = instructions.Where(code => code.opcode != OpCodes.Nop).ToList();
        if (codes.Count < 3 || codes[0].opcode != (finalizer ? OpCodes.Ldarg_1 : OpCodes.Ldarg_0) ||
            (codes[1].opcode != OpCodes.Brfalse && codes[1].opcode != OpCodes.Brfalse_S) || codes[1].operand is not Label target)
            throw new NotSupportedException("PoV loot null-state guard changed");
        int exit = codes.FindIndex(code => code.labels.Contains(target));
        if (exit < 0 || (finalizer && (codes[exit].opcode != OpCodes.Ldarg_0 || ++exit >= codes.Count)) || codes[exit].opcode != OpCodes.Ret)
            throw new NotSupportedException("PoV loot null-state path no longer returns without side effects");
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

    private static void ClearHealth()
    {
        _setScaledMaxHealth = null;
        _removePendingHealth = null;
    }

    private static void RebuildLootCaller(Harmony harmony) =>
        harmony.CreateProcessor(AccessTools.DeclaredMethod(typeof(CharacterDrop), nameof(CharacterDrop.GenerateDropList))).Patch();

    private static void RemoveHarmony(ref Harmony? harmony)
    {
        harmony?.UnpatchSelf();
        harmony = null;
    }

    private static void RemoveLoot()
    {
        if (_lootHarmony == null) return;
        _lootHarmony.UnpatchSelf();
        RebuildLootCaller(_lootHarmony);
        _lootHarmony = null;
    }

    private static void RemoveBosses()
    {
        _fillBossModifiers = null;
        _bossOn = _rarityWorking = null;
        _characterView = null;
        _killedBefore = _leftAlone = null;
        _bossAfterFirstKill = null;
        _bossModsPerStar = null;
        _bossFailureReported = false;
        RemoveHarmony(ref _bossHarmony);
    }

    internal static void Reset()
    {
        try
        {
            ClearHealth();
            try { RemoveBosses(); }
            finally
            {
                try { RemoveHud(); }
                finally
                {
                    try { RemoveHarmony(ref _levelHarmony); }
                    finally { RemoveLoot(); }
                }
            }
            _otherLevelProvider = false;
        }
        finally { _checked = false; }
    }
}
