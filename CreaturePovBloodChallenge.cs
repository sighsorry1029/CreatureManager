using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace CreatureManager;

internal static partial class CreaturePovCompatibility
{
    private const string BloodStateKey = "CreatureManager_PovBloodState";
    private const string BloodRunesKey = "CreatureManager_PovBloodRunes";
    private const string BloodBaseKey = "CreatureManager_PovBloodBaseLevel";
    private const string BloodOriginalKey = "CreatureManager_PovBloodOriginalLevel";
    private const string BloodMissingHealthKey = "CreatureManager_PovBloodMissingHealth";
    private const string BloodHealthPendingKey = "CreatureManager_PovBloodHealthPending";
    // 1: queued, 2: effects attempted (never replay after a partial failure),
    // 3: completed, 4: failed. Only the owner advances the state.
    private static Harmony? _bloodHarmony;
    private static MethodInfo? _bloodGiveMethod, _bloodPhaseMethod;
    private static Action<Character, int>? _bloodGive;
    private static Action<Character, int>? _bloodOriginalFill;
    private static bool _bloodAccepting;
    private static string? _bloodPatchContract;
    private static readonly List<BloodReplay> BloodReplays = new();
    private static readonly List<BloodPhaseScope> BloodPhases = new();

    private sealed class BloodReplay
    {
        internal Character Character = null!;
        internal int BaseLevel;
        internal int Runes;
        internal bool Full;
        internal bool LevelAssigned;
    }

    private sealed class BloodPhaseScope
    {
        internal Character Character = null!;
        internal bool LevelAssigned;
    }

    private static void InstallBloodChallenges(Assembly assembly)
    {
        if (_levelHarmony == null || _fillBossModifiers == null || _characterView == null || !HasHealthBridge)
            throw new NotSupportedException("PoV level, boss and final-health contracts are required");
        Type stars = assembly.GetType("PassiveTree.BossStars", true)!;
        Type phases = assembly.GetType("PassiveTree.BossPhases", true)!;
        Type carry = phases.GetNestedType("Carry", BindingFlags.NonPublic)!;
        Type mods = assembly.GetType("PassiveTree.RarityMods", true)!;
        Type difficulty = assembly.GetType("PassiveTree.EnemyDifficulty", true)!;
        MethodInfo give = StaticMethod(stars, "Give", typeof(void), typeof(Character), typeof(int));
        MethodInfo phase = StaticMethod(phases, "Give", typeof(void), typeof(Character), carry);
        ValidateBloodGive(PatchProcessor.GetOriginalInstructions(give));
        RequireMember(give, StaticMethod(mods, "Fill", typeof(void), typeof(Character), typeof(int)));
        RequireMember(give, StaticMethod(difficulty, "Ensure", typeof(void), typeof(Character)));
        RequireLiteral(phase, "pt.boss.phase-stars");
        RequireLiteral(phase, "pt.boss.phase-mods");
        RequireLiteral(phase, "pt.boss.stars");
        RequireMember(phase, StaticMethod(difficulty, "FixedHealth", typeof(bool), typeof(Character)));
        RequireMember(phase, AccessTools.DeclaredMethod(typeof(Character), nameof(Character.SetLevel)));
        ValidateBloodPhase(PatchProcessor.GetOriginalInstructions(phase));
        if (carry.GetField("Stars", BindingFlags.Public | BindingFlags.Instance)?.FieldType != typeof(int))
            throw new NotSupportedException("PoV boss phase carry representation changed");
        if (HasExternalBloodPatches(give) || HasExternalBloodPatches(phase))
            throw new NotSupportedException("another patch already changes PoV blood or phase assignment");
        _bloodGiveMethod = give;
        _bloodPhaseMethod = phase;
        _bloodGive = (Action<Character, int>)Delegate.CreateDelegate(typeof(Action<Character, int>), give);
        _bloodOriginalFill = (Action<Character, int>)Delegate.CreateDelegate(typeof(Action<Character, int>),
            StaticMethod(mods, "Fill", typeof(void), typeof(Character), typeof(int)));
        _bloodHarmony = new Harmony("sighsorry.CreatureManager.PoV.Blood");
        _bloodHarmony.Patch(give,
            prefix: new HarmonyMethod(typeof(CreaturePovCompatibility), nameof(BloodGivePrefix)),
            transpiler: new HarmonyMethod(typeof(CreaturePovCompatibility), nameof(BloodGiveTranspiler)));
        _bloodHarmony.Patch(phase,
            prefix: new HarmonyMethod(typeof(CreaturePovCompatibility), nameof(BloodPhasePrefix)),
            transpiler: new HarmonyMethod(typeof(CreaturePovCompatibility), nameof(BloodPhaseTranspiler)),
            finalizer: new HarmonyMethod(typeof(CreaturePovCompatibility), nameof(BloodPhaseFinalizer)));
        _bloodPatchContract = BloodPatchContract();
        _bloodAccepting = true;
    }

    // Keep the original owner/positive-rune guard, storage, Ensure, heal and logging.
    // Only the two effect sinks are replaced, so a changed helper is not reimplemented.
    private static void ValidateBloodGive(List<CodeInstruction> instructions)
    {
        var codes = instructions.Where(code => code.opcode != OpCodes.Nop).ToList();
        int[] levels = Enumerable.Range(0, codes.Count).Where(i => codes[i].operand is MethodInfo method &&
            method.DeclaringType == typeof(Character) && method.Name == nameof(Character.SetLevel)).ToArray();
        int[] fills = Enumerable.Range(0, codes.Count).Where(i => codes[i].operand is MethodInfo method &&
            method.DeclaringType?.FullName == "PassiveTree.RarityMods" && method.Name == "Fill").ToArray();
        int ensure = codes.FindIndex(code => code.operand is MethodInfo method &&
            method.DeclaringType?.FullName == "PassiveTree.EnemyDifficulty" && method.Name == "Ensure");
        int heal = codes.FindIndex(code => code.operand is MethodInfo method &&
            method.DeclaringType == typeof(Character) && method.Name == nameof(Character.SetHealth));
        if (levels.Length != 1 || fills.Length != 1 || levels[0] >= fills[0] || fills[0] >= ensure || ensure >= heal ||
            levels[0] < 3 || codes[levels[0] - 1].opcode != OpCodes.Add || codes[levels[0] - 2].opcode != OpCodes.Ldc_I4_1 ||
            codes[levels[0] - 3].opcode != OpCodes.Ldarg_1 ||
            !codes.Take(levels[0]).Any(code => code.opcode == OpCodes.Ldstr && Equals(code.operand, "pt.boss.stars")) ||
            !codes.Take(levels[0]).Any(code => code.opcode == OpCodes.Ldstr && Equals(code.operand, "pt.boss.rolled")) ||
            !codes.Take(levels[0]).Any(code => code.operand is MethodInfo method &&
                method.DeclaringType == typeof(ZNetView) && method.Name == nameof(ZNetView.IsOwner)))
            throw new NotSupportedException("PoV blood-rune assignment/side-effect order changed");
        int start = levels[0] + 1;
        int max = codes.FindIndex(start, code => code.operand is MethodInfo method && method.DeclaringType == typeof(UnityEngine.Mathf) &&
            method.Name == "Max" && method.ReturnType == typeof(int) && method.GetParameters().All(parameter => parameter.ParameterType == typeof(int)));
        int working = codes.FindIndex(start, code => code.operand is MethodInfo method &&
            method.DeclaringType?.FullName == "PassiveTree.CreatureRarity" && method.Name == "get_Working");
        if (start + 2 >= codes.Count || codes[start].opcode != OpCodes.Ldarg_1 || codes[start + 1].opcode != OpCodes.Ldc_I4_1 ||
            codes[start + 2].operand is not FieldInfo config || config.DeclaringType?.FullName != "PassiveTree.BossStars" ||
            config.Name != "ModsPerStar" || config.FieldType != typeof(BepInEx.Configuration.ConfigEntry<int>) ||
            max <= start || max + 3 >= codes.Count || codes[max + 1].opcode != OpCodes.Mul || working != max + 3 ||
            !codes.Skip(start).Take(max - start).Any(code => code.operand is MethodInfo getter &&
                getter.DeclaringType == typeof(BepInEx.Configuration.ConfigEntry<int>) && getter.Name == "get_Value") ||
            working + 1 >= codes.Count || (codes[working + 1].opcode != OpCodes.Brfalse && codes[working + 1].opcode != OpCodes.Brfalse_S) ||
            codes[working + 1].operand is not Label skipFill || !codes.Skip(fills[0] + 1).Take(ensure - fills[0]).Any(code => code.labels.Contains(skipFill)))
            throw new NotSupportedException("PoV blood-rune modifier budget or rarity guard changed");
    }

    private static void ValidateBloodPhase(List<CodeInstruction> codes)
    {
        int[] assignments = Enumerable.Range(0, codes.Count).Where(i => codes[i].operand is MethodInfo method &&
            method.DeclaringType == typeof(Character) && method.Name == nameof(Character.SetLevel)).ToArray();
        int fixedHealth = codes.FindIndex(code => code.operand is MethodInfo method &&
            method.DeclaringType?.FullName == "PassiveTree.EnemyDifficulty" && method.Name == "FixedHealth");
        if (assignments.Length != 1 || fixedHealth < 0 || fixedHealth >= assignments[0])
            throw new NotSupportedException("PoV fixed-health boss phase assignment changed");
    }

    private static IEnumerable<CodeInstruction> BloodPhaseTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> codes = instructions.ToList();
        ValidateBloodPhase(codes);
        foreach (CodeInstruction code in codes)
            if (code.operand is MethodInfo method && method.DeclaringType == typeof(Character) && method.Name == nameof(Character.SetLevel))
            {
                code.opcode = OpCodes.Call;
                code.operand = AccessTools.DeclaredMethod(typeof(CreaturePovCompatibility), nameof(BloodSetLevel));
            }
        return codes;
    }

    private static IEnumerable<CodeInstruction> BloodGiveTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> codes = instructions.ToList();
        ValidateBloodGive(codes);
        foreach (CodeInstruction code in codes)
        {
            if (code.operand is not MethodInfo method) continue;
            if (method.DeclaringType == typeof(Character) && method.Name == nameof(Character.SetLevel))
            {
                code.opcode = OpCodes.Call;
                code.operand = AccessTools.DeclaredMethod(typeof(CreaturePovCompatibility), nameof(BloodSetLevel));
            }
            else if (method.DeclaringType?.FullName == "PassiveTree.RarityMods" && method.Name == "Fill")
            {
                code.opcode = OpCodes.Call;
                code.operand = AccessTools.DeclaredMethod(typeof(CreaturePovCompatibility), nameof(BloodFill));
            }
        }
        return codes;
    }

    private static bool HasExternalBloodPatches(MethodInfo method)
    {
        Patches? info = Harmony.GetPatchInfo(method);
        return info != null && info.Owners.Any(owner => owner != "sighsorry.CreatureManager.PoV.Blood");
    }

    private static string BloodPatchContract() => string.Join(";", new[] { _bloodGiveMethod, _bloodPhaseMethod }.SelectMany(method =>
    {
        Patches? patches = method == null ? null : Harmony.GetPatchInfo(method);
        return patches == null ? Array.Empty<string>() : new[] { patches.Prefixes, patches.Postfixes, patches.Transpilers, patches.Finalizers }
            .SelectMany((group, kind) => group.Select(patch => $"{method!.Name}:{kind}:{patch.owner}:{patch.index}:{patch.priority}:{string.Join(",", patch.before)}:{string.Join(",", patch.after)}:{patch.PatchMethod}"));
    }));

    private static bool BloodContractCurrent()
    {
        if (_bloodGive == null || _bloodHarmony == null) return false;
        try
        {
            if (BloodPatchContract() == _bloodPatchContract) return true;
            if (_bloodAccepting)
                CreatureManagerPlugin.Log.LogWarning("PoV blood-rune addition stopped: assignment patches changed after validation. Pending rune levels will be preserved without replaying unverified PoV effects.");
        }
        catch (Exception ex)
        {
            if (_bloodAccepting) CreatureManagerPlugin.Log.LogWarning($"PoV blood-rune patch verification failed: {ex.Message}");
        }
        _bloodAccepting = false;
        return false;
    }

    private static ZDO? BloodData(Character character)
    {
        if (character == null || _characterView == null) return null;
        ZNetView view = _characterView(character);
        return view != null && view.IsValid() ? view.GetZDO() : null;
    }

    private static bool BloodOwner(Character character)
    {
        if (character == null || _characterView == null) return false;
        ZNetView view = _characterView(character);
        return view != null && view.IsValid() && view.IsOwner();
    }

    private static bool BloodGivePrefix(Character c, int stars)
    {
        if (ReplayFor(c) != null) return true;
        if (!_bloodAccepting || !BloodContractCurrent() || _otherLevelProvider || stars <= 0 || !BloodOwner(c) ||
            c.IsPlayer() || c.IsDead() || CreatureLevelManager.IsFrozenKingPhaseTwo(c) ||
            (CreatureDomainManager.IsSynchronizedConfigurationReady() && CreatureManagerPlugin.EnableLevelSystem?.Value != CreatureManagerPlugin.LevelSystemMode.Full) ||
            CreatureManagerSpawnLifecycle.IsCommandSpawn(c) || CreatureLevelManager.ShouldPreserveExistingRolls(c)) return true;
        ZDO zdo = BloodData(c)!;
        // Give is a one-shot summon operation. Re-entering it must not award the same
        // consumed altar runes twice. Old pt.boss.stars alone is never a rune receipt.
        if (zdo.GetInt(BloodStateKey) != 0) return false;
        zdo.Set(BloodRunesKey, stars);
        zdo.Set(BloodOriginalKey, Math.Max(1, c.GetLevel()));
        zdo.Set(BloodStateKey, 1);
        CreatureLevelManager.QueuePovBloodChallenge(c);
        return false;
    }

    private static BloodReplay? ReplayFor(Character character) =>
        BloodReplays.LastOrDefault(replay => ReferenceEquals(replay.Character, character));

    internal static bool IsBloodLevelWrite(Character character) => ReplayFor(character) != null ||
        BloodPhases.Any(scope => ReferenceEquals(scope.Character, character));

    internal static bool HasPendingBloodChallenge(Character character) => BloodData(character)?.GetInt(BloodStateKey) == 1;

    internal static int BloodOriginalLevel(Character character) => Math.Max(1, BloodData(character)?.GetInt(BloodOriginalKey, 1) ?? 1);

    internal static void ApplyPendingBloodChallenge(Character character, int baseLevel)
    {
        ZDO? zdo = BloodData(character);
        if (zdo == null || zdo.GetInt(BloodStateKey) != 1 || !BloodOwner(character)) return;
        if (character.IsDead() || character.GetHealth() <= 0f)
        {
            zdo.Set(BloodStateKey, 4);
            return;
        }
        int runes = zdo.GetInt(BloodRunesKey);
        if (runes <= 0) { zdo.Set(BloodStateKey, 4); return; }
        bool full = CreatureManagerPlugin.EnableLevelSystem?.Value == CreatureManagerPlugin.LevelSystemMode.Full;
        baseLevel = full ? Math.Max(1, baseLevel) : 1;
        zdo.Set(BloodBaseKey, baseLevel);
        zdo.Set(BloodMissingHealthKey, Math.Max(0f, character.GetMaxHealth() - character.GetHealth()));
        zdo.Set(BloodHealthPendingKey, true);
        zdo.Set(BloodStateKey, 2);
        zdo.Set(BossPendingKey, false);
        var replay = new BloodReplay { Character = character, BaseLevel = baseLevel, Runes = runes, Full = full };
        BloodReplays.Add(replay);
        try
        {
            if (BloodContractCurrent())
            {
                _bloodGive!(character, runes);
                if (!replay.LevelAssigned || !BloodOwner(character))
                    throw new InvalidOperationException("PoV blood-rune replay did not complete its owned level assignment");
                zdo.Set(BloodStateKey, 3);
            }
            else
            {
                // The item has already been consumed by PoV. Preserve its committed
                // level contribution, but do not call newly changed foreign effects.
                BloodSetLevel(character, runes + 1);
                zdo.Set(BloodStateKey, 4);
            }
        }
        catch (Exception ex)
        {
            zdo.Set(BloodStateKey, 4);
            _bloodAccepting = false;
            CreatureManagerPlugin.Log.LogWarning($"PoV blood-rune effects failed; the partial grant will not be retried: {ex.Message}");
        }
        finally
        {
            BloodReplays.Remove(replay);
            CreatureLevelManager.AdoptPovAssignedLevel(character, "PoV blood rune");
        }
    }

    private static void BloodSetLevel(Character character, int originalLevel)
    {
        BloodReplay? replay = ReplayFor(character);
        if (replay == null)
        {
            character.SetLevel(originalLevel);
            BloodPhaseScope? phase = BloodPhases.LastOrDefault(scope => ReferenceEquals(scope.Character, character));
            if (phase != null) phase.LevelAssigned = character.GetLevel() == originalLevel;
            return;
        }
        int finalLevel = (int)Math.Min(int.MaxValue, (long)replay.BaseLevel + replay.Runes);
        BloodData(character)!.Set("pt.boss.stars", finalLevel - 1);
        character.SetLevel(finalLevel);
        replay.LevelAssigned = character.GetLevel() == finalLevel;
    }

    private static void BloodFill(Character character, int originalCount)
    {
        BloodReplay? replay = ReplayFor(character);
        if (_bloodOriginalFill == null) return;
        if (replay == null) { _bloodOriginalFill(character, originalCount); return; }
        long count = originalCount;
        if (replay.Full && BossModifiersAllowed(character))
            count += (long)Math.Max(0, replay.BaseLevel - 1) * Math.Max(0, _bossModsPerStar?.Value ?? 0);
        // Fill is a target, not an increment; never trim previously stored effects.
        uint bits = unchecked((uint)(BloodData(character)?.GetInt("pt.crmod") ?? 0));
        int existing = 0;
        while (bits != 0) { existing++; bits &= bits - 1; }
        _bloodOriginalFill(character, (int)Math.Min(int.MaxValue, Math.Max(existing, count)));
    }

    internal static void TryCompleteBloodChallengeHealth(Character character)
    {
        ZDO? zdo = BloodData(character);
        if (zdo == null || !zdo.GetBool(BloodHealthPendingKey) || !BloodOwner(character)) return;
        zdo.Set(BloodHealthPendingKey, false);
        if (character.IsDead() || character.GetHealth() <= 0f) return;
        float maximum = character.GetMaxHealth();
        SetFinalHealth(character, Math.Max(Math.Min(1f, maximum), maximum - zdo.GetFloat(BloodMissingHealthKey)));
    }

    private static void BloodPhasePrefix(Character c, out BloodPhaseScope? __state)
    {
        __state = null;
        if (_otherLevelProvider || !BloodContractCurrent() || !BloodOwner(c) || c.IsPlayer() ||
            (CreatureDomainManager.IsSynchronizedConfigurationReady() && CreatureManagerPlugin.EnableLevelSystem?.Value != CreatureManagerPlugin.LevelSystemMode.Full) ||
            CreatureLevelManager.IsFrozenKingPhaseTwo(c)) return;
        __state = new BloodPhaseScope { Character = c };
        BloodPhases.Add(__state);
    }

    private static Exception? BloodPhaseFinalizer(Exception? __exception, Character c, BloodPhaseScope? __state)
    {
        if (__state == null) return __exception;
        try
        {
            if (__state.LevelAssigned && BloodOwner(c))
            {
                int carried = BloodData(c)?.GetInt("pt.boss.phase-stars") ?? 0;
                if (carried > 0 && c.GetLevel() - 1 == carried)
                {
                    BloodData(c)!.Set(BossPendingKey, false);
                    CreatureLevelManager.AdoptPovAssignedLevel(c, __exception == null ? "PoV boss phase" : "PoV partial boss phase");
                }
            }
        }
        finally { BloodPhases.Remove(__state); }
        return __exception;
    }

    private static void RemoveBloodChallenges()
    {
        _bloodAccepting = false;
        RemoveHarmony(ref _bloodHarmony);
        _bloodGive = null;
        _bloodOriginalFill = null;
        _bloodGiveMethod = _bloodPhaseMethod = null;
        _bloodPatchContract = null;
        BloodReplays.Clear();
        BloodPhases.Clear();
    }
}
