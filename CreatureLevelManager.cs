using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace CreatureManager;

internal static class CreatureLevelManager
{
    private const string AppliedKey = "CreatureManager_LevelApplied";
    private const string ProcessingCompleteKey = "CreatureManager_LevelProcessingComplete";
    private const string DesiredLevelKey = "CreatureManager_DesiredLevel";
    private const string DesiredLevelSourceKey = "CreatureManager_DesiredLevelSource";
    private const string HealthAppliedKey = "CreatureManager_LevelHealthApplied";
    private const string HealthMultiplierKey = "CreatureManager_LevelHealthMultiplier";
    private const string DamageAppliedKey = "CreatureManager_LevelDamageApplied";
    private const string DamageMultiplierKey = "CreatureManager_LevelDamageMultiplier";
    private const string KarmaBonusRequestRpc = "CreatureManager_LevelKarmaBonusRequest";
    private const string KarmaBonusResponseRpc = "CreatureManager_LevelKarmaBonusResponse";
    private const float KarmaBonusRetryInterval = 0.5f;
    private const float KarmaBonusRequestTimeout = 10f;
    private const float KarmaBonusMaximumRetryInterval = 5f;
    private const string HueProperty = "_Hue";
    private const string SaturationProperty = "_Saturation";
    private const string ValueProperty = "_Value";
    private const string EmissionColorProperty = "_EmissionColor";
    private static readonly object Sync = new();
    private static LevelDefinition[] ActiveDefinitions = Array.Empty<LevelDefinition>();
    private static int ManagedSetLevelDepth;
    private static readonly Stack<(string Reason, Character? Source)> ExplicitLevelContexts = new();
    private static Character? CachedRuleSearchCharacter;
    private static LevelRuleScope CachedRuleSearchScope;
    private static int CachedRuleSearchFrame = -1;
    private static LevelRuleSearch? CachedRuleSearch;
    private static ZRoutedRpc? RegisteredRoutedRpc;
    private static int NextKarmaBonusRequestId;
    private static readonly Dictionary<int, Character> PendingLevelCharacters = new();
    private static readonly Dictionary<ZDOID, PendingKarmaBonusRequest> PendingKarmaBonusRequests = new();
    private static readonly Dictionary<ZDOID, int> ResolvedKarmaBonuses = new();

    private sealed class PendingKarmaBonusRequest
    {
        internal int RequestId;
        internal float StartedAt = -1f;
        internal float NextSendAt;
        internal int TimeoutCount;
    }

    private enum LevelRuleScope
    {
        Full,
        BaselineOnly
    }

    private enum ModifierApplicationMode
    {
        Block,
        Roll,
        Keep
    }

    private readonly struct SpawnPolicy
    {
        internal readonly bool RollLevel;
        internal readonly LevelRuleScope? GeneralScope;
        internal readonly LevelRuleScope? HealthScope;
        internal readonly bool AllowHealthDistanceScaling;
        internal readonly ModifierApplicationMode ModifierMode;
        internal readonly LevelRuleScope ModifierScope;
        internal readonly bool AllowModifierDistanceScaling;

        internal bool KeepExistingRuntime => ModifierMode == ModifierApplicationMode.Keep;

        internal SpawnPolicy(
            bool rollLevel,
            LevelRuleScope? generalScope,
            LevelRuleScope? healthScope,
            bool allowHealthDistanceScaling,
            ModifierApplicationMode modifierMode,
            LevelRuleScope modifierScope,
            bool allowModifierDistanceScaling)
        {
            RollLevel = rollLevel;
            GeneralScope = generalScope;
            HealthScope = healthScope;
            AllowHealthDistanceScaling = allowHealthDistanceScaling;
            ModifierMode = modifierMode;
            ModifierScope = modifierScope;
            AllowModifierDistanceScaling = allowModifierDistanceScaling;
        }
    }

    internal static bool IsLevelSystemEnabled()
    {
        return CreatureManagerPlugin.EnableLevelSystem?.Value != CreatureManagerPlugin.Toggle.Off;
    }

    internal static bool IsExternalTamedSpawn(Character character)
    {
        return character != null &&
               !character.IsPlayer() &&
               character.IsTamed() &&
               !CreatureManagerSpawnLifecycle.IsManagedSpawn(character);
    }

    internal static void Load(List<LevelDefinition> definitions)
    {
        lock (Sync)
        {
            ActiveDefinitions = definitions.ToArray();
        }

        InvalidateRuleSearchCache();
    }

    internal static void ResetRuntimeState()
    {
        ExplicitLevelContexts.Clear();
        ManagedSetLevelDepth = 0;
        NextKarmaBonusRequestId = 0;
        PendingLevelCharacters.Clear();
        PendingKarmaBonusRequests.Clear();
        ResolvedKarmaBonuses.Clear();
        InvalidateRuleSearchCache();
    }

    internal static void RegisterRpcs()
    {
        if (ZRoutedRpc.instance == null || ReferenceEquals(RegisteredRoutedRpc, ZRoutedRpc.instance))
        {
            return;
        }

        ZRoutedRpc.instance.Register<ZPackage>(KarmaBonusRequestRpc, RPC_KarmaBonusRequest);
        ZRoutedRpc.instance.Register<ZPackage>(KarmaBonusResponseRpc, RPC_KarmaBonusResponse);
        RegisteredRoutedRpc = ZRoutedRpc.instance;
    }

    internal static void UpdatePendingApplications()
    {
        RegisterRpcs();
        if (!CreatureDomainManager.IsSynchronizedConfigurationReady())
        {
            return;
        }

        PruneTimedOutKarmaBonusRequests();
        if (!IsLevelSystemEnabled())
        {
            PendingLevelCharacters.Clear();
            PendingKarmaBonusRequests.Clear();
            ResolvedKarmaBonuses.Clear();
            return;
        }

        if (PendingLevelCharacters.Count == 0)
        {
            return;
        }

        foreach (KeyValuePair<int, Character> entry in PendingLevelCharacters.ToArray())
        {
            Character character = entry.Value;
            if (character == null || character.IsPlayer())
            {
                PendingLevelCharacters.Remove(entry.Key);
                continue;
            }

            ZNetView? nview = character.m_nview;
            if (nview == null || !nview.IsValid())
            {
                ForgetPendingLevelCharacter(character, character.GetZDOID());
                continue;
            }

            ZDO zdo = nview.GetZDO();
            if (zdo == null)
            {
                continue;
            }

            if (IsDeadOrWithoutHealth(character, zdo))
            {
                ForgetPendingLevelCharacter(character, zdo.m_uid);
                continue;
            }

            if (zdo.GetBool(ProcessingCompleteKey, false))
            {
                SynchronizeCompletedRuntimeState(character, zdo);
                if (nview.IsOwner())
                {
                    CreatureModifierManager.TryRollModifiers(character);
                }

                ForgetPendingLevelCharacter(character, zdo.m_uid);
                continue;
            }

            if (ShouldKeepExistingRuntime(character))
            {
                ForgetPendingLevelCharacter(character, zdo.m_uid);
                continue;
            }

            if (nview.IsOwner())
            {
                TryApplyLevel(character);
            }
            else
            {
                PendingKarmaBonusRequests.Remove(zdo.m_uid);
                ResolvedKarmaBonuses.Remove(zdo.m_uid);
            }
        }
    }

    internal static void ForgetCharacter(Character character)
    {
        if (character != null)
        {
            ForgetPendingLevelCharacter(character, character.GetZDOID());
        }
    }

    private static void InvalidateRuleSearchCache()
    {
        CachedRuleSearchCharacter = null;
        CachedRuleSearchFrame = -1;
        CachedRuleSearch = null;
    }

    internal static Dictionary<string, ModifierDefinition> GetGlobalModifierDefinitions()
    {
        Dictionary<string, ModifierDefinition> result = new(StringComparer.OrdinalIgnoreCase);
        LevelDefinition[] definitions;
        lock (Sync)
        {
            definitions = ActiveDefinitions;
        }

        foreach (LevelDefinition definition in definitions)
        {
            string target = (definition.Target ?? "").Trim();
            string biome = (definition.Biome ?? "").Trim();
            if (!IsGlobalTarget(target) || biome.Length > 0)
            {
                continue;
            }

            if (definition.ModifiersCleared)
            {
                result.Clear();
                continue;
            }

            if (definition.Modifiers == null)
            {
                continue;
            }

            foreach (KeyValuePair<string, ModifierDefinition> entry in definition.Modifiers)
            {
                if (!result.TryGetValue(entry.Key, out ModifierDefinition current))
                {
                    result[entry.Key] = entry.Value.Clone();
                    continue;
                }

                current.OverlayFrom(entry.Value);
            }
        }

        foreach (string disabled in result
                     .Where(entry => !entry.Value.Chance.HasValue || entry.Value.Chance.Value <= 0f)
                     .Select(entry => entry.Key)
                     .ToArray())
        {
            result.Remove(disabled);
        }

        return result;
    }

    internal static void TryApplyLevel(Character character)
    {
        if (character == null || character.IsPlayer())
        {
            return;
        }

        if (!CreatureDomainManager.IsSynchronizedConfigurationReady())
        {
            PendingLevelCharacters[character.GetInstanceID()] = character;
            return;
        }

        if (!IsLevelSystemEnabled())
        {
            ForgetPendingLevelCharacter(character, character.GetZDOID());
            return;
        }

        if (ShouldKeepExistingRuntime(character))
        {
            return;
        }

        ZNetView? nview = character.m_nview;
        if (nview == null || !nview.IsValid())
        {
            return;
        }

        if (!nview.IsOwner())
        {
            PendingLevelCharacters[character.GetInstanceID()] = character;
            return;
        }

        ZDO zdo = nview.GetZDO();
        if (zdo == null)
        {
            return;
        }

        if (IsDeadOrWithoutHealth(character, zdo))
        {
            ForgetPendingLevelCharacter(character, zdo.m_uid);
            return;
        }

        if (zdo.GetBool(ProcessingCompleteKey, false))
        {
            SynchronizeCompletedRuntimeState(character, zdo);
            CreatureModifierManager.TryRollModifiers(character);
            ForgetPendingLevelCharacter(character, zdo.m_uid);
            return;
        }

        if (!zdo.GetBool(AppliedKey, false))
        {
            if (TryAdoptPreexistingExternalLevel(character, zdo))
            {
                ReapplyLevelDependentRuntimeState(character, zdo);
            }
            else if (ShouldRollLevel(character))
            {
                if (!TryResolveKarmaBonus(character, zdo, out int karmaBonus))
                {
                    PendingLevelCharacters[character.GetInstanceID()] = character;
                    return;
                }

                int level = 1;
                bool hasLevelRule = TrySelectLevelWeights(character, out List<float> weights) &&
                                    TrySelectLevel(weights, GetPrefabName(character.gameObject), out level);
                if (hasLevelRule || karmaBonus > 0)
                {
                    int finalLevel = Math.Max(1, (hasLevelRule ? level : 1) + karmaBonus);
                    zdo.Set(DesiredLevelKey, finalLevel);
                    zdo.Set(AppliedKey, true);
                    SetManagedLevel(character, finalLevel);
                }
            }
        }

        if (!zdo.GetBool(HealthAppliedKey, false) &&
            TrySelectHealthMultiplier(character, out float healthMultiplier))
        {
            ApplyHealthMultiplier(character, healthMultiplier);
            zdo.Set(HealthMultiplierKey, healthMultiplier);
            zdo.Set(HealthAppliedKey, true);
        }

        if (!zdo.GetBool(DamageAppliedKey, false) &&
            TrySelectDamageMultiplier(character, out float damageMultiplier))
        {
            zdo.Set(DamageMultiplierKey, damageMultiplier);
            zdo.Set(DamageAppliedKey, true);
        }

        ApplyRuntimeVisuals(character);
        zdo.Set(ProcessingCompleteKey, true);
        CreatureModifierManager.TryRollModifiers(character);
        ForgetPendingLevelCharacter(character, zdo.m_uid);
    }

    private static bool TryResolveKarmaBonus(Character character, ZDO zdo, out int bonus)
    {
        bonus = 0;
        if (!CreatureKarmaManager.RequiresAuthoritativeLevelBonus(zdo, character.IsBoss()))
        {
            return true;
        }

        if (ZNet.instance == null || ZNet.instance.IsServer())
        {
            bonus = CreatureKarmaManager.GetLevelBonus(character);
            return true;
        }

        if (ResolvedKarmaBonuses.TryGetValue(zdo.m_uid, out bonus))
        {
            ResolvedKarmaBonuses.Remove(zdo.m_uid);
            bonus = Math.Max(0, bonus);
            return true;
        }

        QueueKarmaBonusRequest(zdo);
        return false;
    }

    private static void QueueKarmaBonusRequest(ZDO zdo)
    {
        ZDOID characterId = zdo.m_uid;
        if (characterId == ZDOID.None)
        {
            return;
        }

        if (!PendingKarmaBonusRequests.TryGetValue(characterId, out PendingKarmaBonusRequest request))
        {
            int requestId = unchecked(++NextKarmaBonusRequestId);
            if (requestId <= 0)
            {
                requestId = NextKarmaBonusRequestId = 1;
            }

            request = new PendingKarmaBonusRequest
            {
                RequestId = requestId
            };
            PendingKarmaBonusRequests[characterId] = request;
        }

        float now = Time.unscaledTime;
        if (now < request.NextSendAt)
        {
            return;
        }

        RegisterRpcs();
        if (ZRoutedRpc.instance == null)
        {
            return;
        }

        ZPackage package = new();
        package.Write(request.RequestId);
        package.Write(characterId);
        ZRoutedRpc.instance.InvokeRoutedRPC(
            ZRoutedRpc.instance.GetServerPeerID(),
            KarmaBonusRequestRpc,
            package);
        if (request.StartedAt < 0f)
        {
            request.StartedAt = now;
        }

        float retryInterval = Mathf.Min(
            KarmaBonusMaximumRetryInterval,
            KarmaBonusRetryInterval * Math.Max(1, request.TimeoutCount + 1));
        request.NextSendAt = now + retryInterval;
    }

    private static void PruneTimedOutKarmaBonusRequests()
    {
        float now = Time.unscaledTime;
        foreach (KeyValuePair<ZDOID, PendingKarmaBonusRequest> entry in PendingKarmaBonusRequests.ToArray())
        {
            PendingKarmaBonusRequest request = entry.Value;
            if (request.StartedAt < 0f || now - request.StartedAt < KarmaBonusRequestTimeout)
            {
                continue;
            }

            request.StartedAt = now;
            request.NextSendAt = now;
            request.TimeoutCount++;
            if (request.TimeoutCount == 1 || request.TimeoutCount % 6 == 0)
            {
                CreatureManagerPlugin.Log.LogWarning(
                    $"Still waiting for the server Karma level bonus for creature {entry.Key}; " +
                    "keeping level application pending and retrying at a reduced rate.");
            }
        }
    }

    private static void RPC_KarmaBonusRequest(long sender, ZPackage package)
    {
        if (ZNet.instance == null ||
            !ZNet.instance.IsServer() ||
            ZRoutedRpc.instance == null)
        {
            return;
        }

        try
        {
            int requestId = package.ReadInt();
            ZDOID characterId = package.ReadZDOID();
            bool ready = false;
            int bonus = 0;
            ZNetPeer peer = ZNet.instance.GetPeer(sender);
            if (requestId > 0 &&
                characterId != ZDOID.None &&
                peer != null &&
                peer.IsReady() &&
                ZDOMan.instance != null &&
                ZNetScene.instance != null)
            {
                ZDO zdo = ZDOMan.instance.GetZDO(characterId);
                GameObject? prefab = zdo != null ? ZNetScene.instance.GetPrefab(zdo.GetPrefab()) : null;
                if (zdo != null &&
                    zdo.GetOwner() == sender &&
                    prefab != null &&
                    prefab.GetComponent<Character>() is Character prefabCharacter &&
                    prefab.GetComponent<Player>() == null)
                {
                    CreatureKarmaManager.TrackPotentialBlockerZdo(zdo, prefabCharacter.IsBoss());
                    bonus = CreatureKarmaManager.GetAuthoritativeLevelBonus(zdo);
                    ready = true;
                }
            }

            ZPackage response = new();
            response.Write(requestId);
            response.Write(characterId);
            response.Write(ready);
            response.Write(Math.Max(0, bonus));
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, KarmaBonusResponseRpc, response);
        }
        catch (Exception ex)
        {
            CreatureManagerPlugin.Log.LogWarning($"Failed to process a Karma level bonus request: {ex.Message}");
        }
    }

    private static void RPC_KarmaBonusResponse(long sender, ZPackage package)
    {
        if (ZRoutedRpc.instance == null || sender != ZRoutedRpc.instance.GetServerPeerID())
        {
            return;
        }

        try
        {
            int requestId = package.ReadInt();
            ZDOID characterId = package.ReadZDOID();
            bool ready = package.ReadBool();
            int bonus = Math.Max(0, package.ReadInt());
            if (!PendingKarmaBonusRequests.TryGetValue(characterId, out PendingKarmaBonusRequest request) ||
                request.RequestId != requestId)
            {
                return;
            }

            if (!TryFindPendingLevelCharacter(characterId, out Character character) ||
                character.m_nview == null ||
                !character.m_nview.IsValid() ||
                !character.m_nview.IsOwner() ||
                character.m_nview.GetZDO()?.GetOwner() != ZRoutedRpc.instance.m_id)
            {
                PendingKarmaBonusRequests.Remove(characterId);
                ResolvedKarmaBonuses.Remove(characterId);
                return;
            }

            if (!ready)
            {
                return;
            }

            PendingKarmaBonusRequests.Remove(characterId);
            ResolvedKarmaBonuses[characterId] = bonus;
            TryApplyLevel(character);
        }
        catch (Exception ex)
        {
            CreatureManagerPlugin.Log.LogWarning($"Failed to process a Karma level bonus response: {ex.Message}");
        }
    }

    private static bool TryFindPendingLevelCharacter(ZDOID characterId, out Character character)
    {
        foreach (Character candidate in PendingLevelCharacters.Values)
        {
            if (candidate != null && candidate.GetZDOID() == characterId)
            {
                character = candidate;
                return true;
            }
        }

        character = null!;
        return false;
    }

    private static void ForgetPendingLevelCharacter(Character character, ZDOID characterId)
    {
        if (character != null)
        {
            PendingLevelCharacters.Remove(character.GetInstanceID());
        }

        if (characterId != ZDOID.None)
        {
            PendingKarmaBonusRequests.Remove(characterId);
            ResolvedKarmaBonuses.Remove(characterId);
        }
    }

    private static void SynchronizeCompletedRuntimeState(Character character, ZDO zdo)
    {
        int level = zdo.GetInt(
            DesiredLevelKey,
            zdo.GetInt(ZDOVars.s_level, Math.Max(1, character.GetLevel())));
        level = Math.Max(1, level);
        if (character.GetLevel() != level)
        {
            character.m_level = level;
        }

        ApplyRuntimeVisuals(character);
    }

    private static bool IsDeadOrWithoutHealth(Character character, ZDO zdo)
    {
        if (character.IsDead() || zdo.GetBool(ZDOVars.s_dead, false))
        {
            return true;
        }

        float health = zdo.GetFloat(ZDOVars.s_health, float.PositiveInfinity);
        return !float.IsNaN(health) && !float.IsInfinity(health) && health <= 0f;
    }

    internal static bool HasManagedLevel(Character character)
    {
        if (!IsLevelSystemEnabled() || character == null || character.IsPlayer())
        {
            return false;
        }

        ZNetView? nview = character.m_nview;
        if (nview == null || !nview.IsValid())
        {
            return false;
        }

        ZDO zdo = nview.GetZDO();
        return zdo != null && zdo.GetBool(AppliedKey, false);
    }

    internal static bool IsReadyForModifierApplication(Character character)
    {
        if (character == null ||
            character.IsPlayer() ||
            !CreatureDomainManager.IsSynchronizedConfigurationReady())
        {
            return false;
        }

        if (ShouldKeepExistingRuntime(character))
        {
            return true;
        }

        ZNetView? nview = character.m_nview;
        if (nview == null || !nview.IsValid())
        {
            return false;
        }

        ZDO zdo = nview.GetZDO();
        return zdo != null && zdo.GetBool(ProcessingCompleteKey, false);
    }

    internal static bool TryApplyForcedLevel(Character character, int level, out string error)
    {
        error = "";
        if (!IsLevelSystemEnabled())
        {
            error = "CreatureManager level system is disabled.";
            return false;
        }

        if (character == null || character.IsPlayer() || level < 1)
        {
            error = "The spawned prefab is not a supported creature or the requested level is invalid.";
            return false;
        }

        ZNetView? nview = character.m_nview;
        if (nview == null || !nview.IsValid() || !nview.IsOwner())
        {
            error = "The spawned creature has no owned network state.";
            return false;
        }

        ZDO zdo = nview.GetZDO();
        if (zdo == null)
        {
            error = "The spawned creature has no ZDO.";
            return false;
        }

        zdo.Set(DesiredLevelKey, level);
        zdo.Set(DesiredLevelSourceKey, "cm:spawn");
        zdo.Set(AppliedKey, true);
        SetManagedLevel(character, level);
        ReapplyLevelDependentRuntimeState(character, zdo);
        ApplyRuntimeVisuals(character);
        return true;
    }

    internal static bool TryApplyRotatedLevelEffects(LevelEffects levelEffects, int level)
    {
        if (!IsLevelSystemEnabled() || levelEffects == null || levelEffects.m_levelSetups == null || levelEffects.m_levelSetups.Count == 0)
        {
            return false;
        }

        Character character = levelEffects.GetComponentInParent<Character>();
        if (character == null || character.IsPlayer())
        {
            return false;
        }

        if (!TryGetScaleRuleScope(character, out _))
        {
            return false;
        }

        try
        {
            CreatureLevelEffectsState state = CreatureLevelEffectsState.Get(levelEffects);
            state.EnsureInitialized(levelEffects);
            ApplyLevelEffectsScale(levelEffects, state, character, level);
            ApplyLevelEffectsVisualState(levelEffects, state, level);
            return true;
        }
        catch (Exception ex)
        {
            CreatureManagerPlugin.Log.LogDebug($"Failed to apply rotated LevelEffects for '{GetPrefabName(character.gameObject)}': {ex.Message}");
            return false;
        }
    }

    internal static void RestoreConfiguredLevel(Character character, int level)
    {
        if (!IsLevelSystemEnabled() || character == null || character.IsPlayer())
        {
            return;
        }

        if (ShouldKeepExistingRuntime(character))
        {
            return;
        }

        ZNetView? nview = character.m_nview;
        if (nview == null || !nview.IsValid() || !nview.IsOwner())
        {
            return;
        }

        ZDO zdo = nview.GetZDO();
        if (zdo == null || !zdo.GetBool(AppliedKey, false))
        {
            return;
        }

        int desiredLevel = zdo.GetInt(DesiredLevelKey, 0);
        if (desiredLevel <= 0 || desiredLevel == level)
        {
            return;
        }

        if (TryAdoptExternalLevelOverride(character, zdo, level))
        {
            return;
        }

        SetManagedLevel(character, desiredLevel);
    }

    internal static void BeginExplicitExternalLevelContext(string reason, Character? source = null)
    {
        ExplicitLevelContexts.Push((reason, source));
    }

    internal static void EndExplicitExternalLevelContext()
    {
        if (ExplicitLevelContexts.Count > 0)
        {
            ExplicitLevelContexts.Pop();
        }
    }

    internal static bool TryAdoptContextualExternalLevel(Character character, int level)
    {
        if (ExplicitLevelContexts.Count <= 0 || ManagedSetLevelDepth > 0)
        {
            return false;
        }

        if (!IsLevelSystemEnabled() || character == null || character.IsPlayer())
        {
            return false;
        }

        ZNetView? nview = character.m_nview;
        if (nview == null || !nview.IsValid() || !nview.IsOwner())
        {
            return false;
        }

        ZDO zdo = nview.GetZDO();
        if (zdo == null)
        {
            return false;
        }

        int externalLevel = Math.Max(1, level);
        (string reason, Character? source) = ExplicitLevelContexts.Peek();
        zdo.Set(DesiredLevelKey, externalLevel);
        zdo.Set(DesiredLevelSourceKey, reason);
        zdo.Set(AppliedKey, true);
        if (source != null)
        {
            CreatureModifierManager.InheritModifiers(source, character);
        }

        if (ShouldKeepExistingRuntime(character))
        {
            InheritLevelRuntimeState(source, character, zdo);
        }
        else
        {
            ReapplyLevelDependentRuntimeState(character, zdo);
        }

        return true;
    }

    private static bool TryAdoptPreexistingExternalLevel(Character character, ZDO zdo)
    {
        if (CreatureManagerSpawnLifecycle.IsManagedSpawn(character) || ShouldRollLevel(character))
        {
            return false;
        }

        int currentLevel = Math.Max(1, character.GetLevel());
        if (currentLevel <= 1)
        {
            return false;
        }

        zdo.Set(DesiredLevelKey, currentLevel);
        zdo.Set(AppliedKey, true);
        return true;
    }

    private static bool TryAdoptExternalLevelOverride(Character character, ZDO zdo, int level)
    {
        if (ManagedSetLevelDepth > 0)
        {
            return false;
        }

        int externalLevel = Math.Max(1, level);
        zdo.Set(DesiredLevelKey, externalLevel);
        zdo.Set(AppliedKey, true);
        ReapplyLevelDependentRuntimeState(character, zdo);
        return true;
    }

    private static void SetManagedLevel(Character character, int level)
    {
        float previousMaxHealth = character.GetMaxHealth();
        float previousHealth = character.GetHealth();
        float missingHealth = Mathf.Max(0f, previousMaxHealth - previousHealth);
        ManagedSetLevelDepth++;
        try
        {
            character.SetLevel(level);
            float updatedMaxHealth = character.GetMaxHealth();
            if (previousHealth > 0f && previousMaxHealth > 0f && updatedMaxHealth > 0f)
            {
                character.SetHealth(Mathf.Max(1f, updatedMaxHealth - missingHealth));
            }
        }
        finally
        {
            ManagedSetLevelDepth--;
        }
    }

    private static void ReapplyLevelDependentRuntimeState(Character character, ZDO zdo)
    {
        if (TrySelectHealthMultiplier(character, out float healthMultiplier))
        {
            ResetVanillaMaxHealth(character);
            ApplyHealthMultiplier(character, healthMultiplier);
            zdo.Set(HealthMultiplierKey, healthMultiplier);
            zdo.Set(HealthAppliedKey, true);
        }
        else if (zdo.GetBool(HealthAppliedKey, false))
        {
            ResetVanillaMaxHealth(character);
            zdo.Set(HealthMultiplierKey, 1f);
            zdo.Set(HealthAppliedKey, false);
        }

        if (TrySelectDamageMultiplier(character, out float damageMultiplier))
        {
            zdo.Set(DamageMultiplierKey, damageMultiplier);
            zdo.Set(DamageAppliedKey, true);
        }
        else if (zdo.GetBool(DamageAppliedKey, false))
        {
            zdo.Set(DamageMultiplierKey, 1f);
            zdo.Set(DamageAppliedKey, false);
        }
    }

    private static void InheritLevelRuntimeState(Character? source, Character target, ZDO targetZdo)
    {
        if (source == null || source == target)
        {
            return;
        }

        ZNetView? sourceView = source.m_nview;
        if (sourceView == null || !sourceView.IsValid())
        {
            return;
        }

        ZDO sourceZdo = sourceView.GetZDO();
        if (sourceZdo == null)
        {
            return;
        }

        if (sourceZdo.GetBool(HealthAppliedKey, false))
        {
            float healthMultiplier = Mathf.Max(0f, sourceZdo.GetFloat(HealthMultiplierKey, 1f));
            if (healthMultiplier > 0f)
            {
                ResetVanillaMaxHealth(target);
                ApplyHealthMultiplier(target, healthMultiplier);
                targetZdo.Set(HealthMultiplierKey, healthMultiplier);
                targetZdo.Set(HealthAppliedKey, true);
            }
        }

        if (sourceZdo.GetBool(DamageAppliedKey, false))
        {
            float damageMultiplier = Mathf.Max(0f, sourceZdo.GetFloat(DamageMultiplierKey, 1f));
            targetZdo.Set(DamageMultiplierKey, damageMultiplier);
            targetZdo.Set(DamageAppliedKey, true);
        }
    }

    private static void ResetVanillaMaxHealth(Character character)
    {
        AccessTools.DeclaredMethod(typeof(Character), "SetupMaxHealth")?.Invoke(character, Array.Empty<object>());
    }

    internal static void ApplyRuntimeVisuals(Character character)
    {
        if (!IsLevelSystemEnabled() ||
            character == null ||
            character.IsPlayer() ||
            !CreatureDomainManager.IsSynchronizedConfigurationReady())
        {
            return;
        }

        if (!TryGetScaleRuleScope(character, out _))
        {
            return;
        }

        bool handledLevelEffects = false;
        foreach (LevelEffects levelEffects in character.GetComponentsInChildren<LevelEffects>(true))
        {
            handledLevelEffects |= TryApplyRotatedLevelEffects(levelEffects, character.GetLevel());
        }

        if (handledLevelEffects)
        {
            CreatureCharacterScaleState.RestoreIfPresent(character);
            return;
        }

        ApplyCharacterScaleFallback(character, character.GetLevel());
    }

    private static void ApplyLevelEffectsScale(LevelEffects levelEffects, CreatureLevelEffectsState state, Character character, int level)
    {
        float multiplier = GetLevelScaleMultiplier(character, level);
        levelEffects.transform.localScale = state.OriginalLocalScale * multiplier;
    }

    private static void ApplyCharacterScaleFallback(Character character, int level)
    {
        CreatureCharacterScaleState state = CreatureCharacterScaleState.Get(character);
        state.EnsureInitialized(character);
        character.transform.localScale = state.OriginalLocalScale * GetLevelScaleMultiplier(character, level);
    }

    private static float GetLevelScaleMultiplier(Character character, int level)
    {
        float scalePerLevel = ShouldApplyLevelScale(character) && TrySelectScalePerLevel(character, out float selectedScalePerLevel)
            ? selectedScalePerLevel
            : 0f;
        return 1f + Math.Max(0, level - 1) * Mathf.Max(0f, scalePerLevel);
    }

    private static bool ShouldApplyLevelScale(Character character)
    {
        if (IsDungeonCreature(character))
        {
            return false;
        }

        if (IsSaddleableCreature(character) &&
            CreatureManagerPlugin.ApplyLevelScaleToSaddleableCreatures?.Value != CreatureManagerPlugin.Toggle.On)
        {
            return false;
        }

        return true;
    }

    private static bool IsSaddleableCreature(Character character)
    {
        Tameable tameable = character.GetComponent<Tameable>();
        if (tameable != null && tameable.m_saddleItem != null)
        {
            return true;
        }

        foreach (Component component in character.GetComponents<Component>())
        {
            if (component != null && component.GetType().Name == "Saddle")
            {
                return true;
            }
        }

        return false;
    }

    internal static bool IsDungeonCreature(Character character)
    {
        Transform transform = character.transform;
        return transform.localPosition.y >= 4500f || transform.position.y >= 4500f;
    }

    private static void ApplyLevelEffectsVisualState(LevelEffects levelEffects, CreatureLevelEffectsState state, int level)
    {
        int stateCount = levelEffects.m_levelSetups.Count + 1;
        int visualState = level <= 1 ? 0 : (level - 1) % stateCount;

        RestoreLevelEffectsBase(levelEffects, state);
        if (visualState == 0)
        {
            return;
        }

        LevelEffects.LevelSetup setup = levelEffects.m_levelSetups[visualState - 1];
        if (setup == null)
        {
            return;
        }

        ApplyLevelEffectsMaterial(levelEffects, state, visualState, setup);
        if (setup.m_enableObject != null)
        {
            setup.m_enableObject.SetActive(true);
        }
    }

    private static void RestoreLevelEffectsBase(LevelEffects levelEffects, CreatureLevelEffectsState state)
    {
        if (levelEffects.m_mainRender != null && state.OriginalMainMaterial != null)
        {
            Material[] materials = levelEffects.m_mainRender.sharedMaterials;
            if (materials.Length > 0)
            {
                materials[0] = state.OriginalMainMaterial;
                levelEffects.m_mainRender.sharedMaterials = materials;
            }
        }

        if (levelEffects.m_baseEnableObject != null)
        {
            levelEffects.m_baseEnableObject.SetActive(state.GetOriginalActive(levelEffects.m_baseEnableObject, true));
        }

        foreach (LevelEffects.LevelSetup setup in levelEffects.m_levelSetups)
        {
            if (setup?.m_enableObject != null)
            {
                setup.m_enableObject.SetActive(false);
            }
        }
    }

    private static void ApplyLevelEffectsMaterial(LevelEffects levelEffects, CreatureLevelEffectsState state, int visualState, LevelEffects.LevelSetup setup)
    {
        Renderer renderer = levelEffects.m_mainRender;
        if (renderer == null || state.OriginalMainMaterial == null)
        {
            return;
        }

        Material material = state.GetOrCreateMaterial(visualState, setup);
        Material[] materials = renderer.sharedMaterials;
        if (materials.Length == 0)
        {
            return;
        }

        materials[0] = material;
        renderer.sharedMaterials = materials;
    }

    internal static void ApplyColorProperties(Material material, LevelEffects.LevelSetup setup)
    {
        if (material.HasProperty(HueProperty))
        {
            material.SetFloat(HueProperty, setup.m_hue);
        }

        if (material.HasProperty(SaturationProperty))
        {
            material.SetFloat(SaturationProperty, setup.m_saturation);
        }

        if (material.HasProperty(ValueProperty))
        {
            material.SetFloat(ValueProperty, setup.m_value);
        }

        if (setup.m_setEmissiveColor && material.HasProperty(EmissionColorProperty))
        {
            material.SetColor(EmissionColorProperty, setup.m_emissiveColor);
        }
    }

    private static bool TrySelectLevelWeights(Character character, out List<float> weights)
    {
        weights = new List<float>();
        if (!TrySelectRule(character, rule => rule.Level is { Count: > 0 }, out LevelDefinition rule))
        {
            return false;
        }

        weights = rule.Level ?? new List<float>();
        return weights.Count > 0;
    }

    private static bool ShouldRollLevel(Character character)
    {
        return GetSpawnPolicy(character).RollLevel;
    }

    private static bool ShouldKeepExistingRuntime(Character character)
    {
        return GetSpawnPolicy(character).KeepExistingRuntime;
    }

    private static SpawnPolicy GetSpawnPolicy(Character character)
    {
        return GetSpawnPolicy(CreatureManagerSpawnLifecycle.GetSpawnSource(character));
    }

    private static SpawnPolicy GetSpawnPolicy(CreatureSpawnSourceKind source)
    {
        return source switch
        {
            CreatureSpawnSourceKind.Command => new SpawnPolicy(
                rollLevel: false,
                generalScope: LevelRuleScope.BaselineOnly,
                healthScope: LevelRuleScope.BaselineOnly,
                allowHealthDistanceScaling: true,
                modifierMode: ModifierApplicationMode.Block,
                modifierScope: LevelRuleScope.Full,
                allowModifierDistanceScaling: false),
            CreatureSpawnSourceKind.PlayerSummon => new SpawnPolicy(
                rollLevel: false,
                generalScope: LevelRuleScope.BaselineOnly,
                healthScope: LevelRuleScope.BaselineOnly,
                allowHealthDistanceScaling: false,
                modifierMode: ModifierApplicationMode.Block,
                modifierScope: LevelRuleScope.Full,
                allowModifierDistanceScaling: false),
            CreatureSpawnSourceKind.Breeding or CreatureSpawnSourceKind.Egg => new SpawnPolicy(
                rollLevel: false,
                generalScope: LevelRuleScope.BaselineOnly,
                healthScope: LevelRuleScope.BaselineOnly,
                allowHealthDistanceScaling: false,
                modifierMode: ModifierApplicationMode.Roll,
                modifierScope: LevelRuleScope.BaselineOnly,
                allowModifierDistanceScaling: false),
            CreatureSpawnSourceKind.Growup or CreatureSpawnSourceKind.TamedRestore => new SpawnPolicy(
                rollLevel: false,
                generalScope: null,
                healthScope: null,
                allowHealthDistanceScaling: false,
                modifierMode: ModifierApplicationMode.Keep,
                modifierScope: LevelRuleScope.Full,
                allowModifierDistanceScaling: false),
            _ => new SpawnPolicy(
                rollLevel: true,
                generalScope: LevelRuleScope.Full,
                healthScope: LevelRuleScope.Full,
                allowHealthDistanceScaling: true,
                modifierMode: ModifierApplicationMode.Roll,
                modifierScope: LevelRuleScope.Full,
                allowModifierDistanceScaling: true)
        };
    }

    private static bool TryGetScaleRuleScope(Character character, out LevelRuleScope scope)
    {
        return TryGetGeneralRuleScope(character, out scope);
    }

    private static bool TryGetHealthRuleScope(Character character, out LevelRuleScope scope, out bool allowDistanceScaling)
    {
        SpawnPolicy policy = GetSpawnPolicy(character);
        scope = policy.HealthScope.GetValueOrDefault(LevelRuleScope.Full);
        allowDistanceScaling = policy.AllowHealthDistanceScaling;
        return policy.HealthScope.HasValue;
    }

    private static bool TryGetDamageRuleScope(Character character, out LevelRuleScope scope, out bool allowDistanceScaling)
    {
        return TryGetHealthRuleScope(character, out scope, out allowDistanceScaling);
    }

    private static bool TryGetGeneralRuleScope(Character character, out LevelRuleScope scope)
    {
        SpawnPolicy policy = GetSpawnPolicy(character);
        scope = policy.GeneralScope.GetValueOrDefault(LevelRuleScope.Full);
        return policy.GeneralScope.HasValue;
    }

    internal static bool ShouldRollModifiers(Character character)
    {
        return AreModifiersEnabled(character) && GetModifierMode(character) == ModifierApplicationMode.Roll;
    }

    internal static bool AllowsModifierEffects(Character character)
    {
        return IsLevelSystemEnabled() &&
               AreModifiersEnabled(character) &&
               GetModifierMode(character) != ModifierApplicationMode.Block;
    }

    internal static bool AllowsModifierEffects(ZDO zdo, bool isBoss, bool isEnforcer)
    {
        return zdo != null &&
               IsLevelSystemEnabled() &&
               AreModifiersEnabled(isBoss, isEnforcer) &&
               GetSpawnPolicy(CreatureManagerSpawnLifecycle.GetSpawnSource(zdo)).ModifierMode !=
               ModifierApplicationMode.Block;
    }

    private static bool AreModifiersEnabled(Character character)
    {
        if (character == null || character.IsPlayer())
        {
            return false;
        }

        return AreModifiersEnabled(character.IsBoss(), CreatureKarmaManager.IsEnforcer(character));
    }

    private static bool AreModifiersEnabled(bool isBoss, bool isEnforcer)
    {
        bool categoryEnabled = isBoss
            ? CreatureManagerPlugin.EnableBossModifiers?.Value != CreatureManagerPlugin.Toggle.Off
            : CreatureManagerPlugin.EnableGlobalModifiers?.Value != CreatureManagerPlugin.Toggle.Off;
        bool enforcerEnabled = CreatureManagerPlugin.EnableEnforcerModifiers?.Value != CreatureManagerPlugin.Toggle.Off;
        return isEnforcer ? enforcerEnabled : categoryEnabled;
    }

    private static ModifierApplicationMode GetModifierMode(Character character)
    {
        return GetSpawnPolicy(character).ModifierMode;
    }

    private static bool TryGetModifierRuleScope(Character character, out LevelRuleScope scope, out bool allowDistanceScaling)
    {
        SpawnPolicy policy = GetSpawnPolicy(character);
        scope = policy.ModifierScope;
        allowDistanceScaling = policy.AllowModifierDistanceScaling;
        return policy.ModifierMode == ModifierApplicationMode.Roll;
    }

    private static bool TrySelectHealthMultiplier(Character character, out float multiplier)
    {
        multiplier = 1f;
        if (!TryGetHealthRuleScope(character, out LevelRuleScope scope, out bool allowDistanceScaling))
        {
            return false;
        }

        bool hasEffect = false;
        float health = 1f;
        if (TrySelectFloatValue(character, rule => rule.Health, out float selectedHealth, scope))
        {
            health = Mathf.Max(0f, selectedHealth);
            hasEffect = true;
        }

        float healthPerLevel = 1f;
        if (TrySelectFloatValue(character, rule => rule.HealthPerLevel, out float selectedHealthPerLevel, scope))
        {
            healthPerLevel = Mathf.Max(0f, selectedHealthPerLevel);
            hasEffect = true;
        }

        float distanceScaling = 1f;
        if (allowDistanceScaling && TrySelectDistanceScalingMultiplier(character, 1, out float selectedDistanceScaling, scope))
        {
            distanceScaling = selectedDistanceScaling;
            hasEffect = true;
        }

        if (!hasEffect)
        {
            return false;
        }

        multiplier = health * GetPerLevelMultiplier(character, healthPerLevel) * distanceScaling / GetVanillaLevelHealthMultiplier(character);
        return multiplier > 0f;
    }

    private static float GetVanillaLevelHealthMultiplier(Character character)
    {
        return Mathf.Max(1, character.GetLevel());
    }

    private static bool TrySelectDamageMultiplier(Character character, out float multiplier)
    {
        multiplier = 1f;
        if (character == null || character.IsPlayer())
        {
            return false;
        }

        if (!TryGetDamageRuleScope(character, out LevelRuleScope scope, out bool allowDistanceScaling))
        {
            return false;
        }

        bool hasEffect = false;
        float damage = 1f;
        if (TrySelectFloatValue(character, rule => rule.Damage, out float selectedDamage, scope))
        {
            damage = Mathf.Max(0f, selectedDamage);
            hasEffect = true;
        }

        float damagePerLevel = 0f;
        if (TrySelectFloatValue(character, rule => rule.DamagePerLevel, out float selectedDamagePerLevel, scope))
        {
            damagePerLevel = Mathf.Max(0f, selectedDamagePerLevel);
            hasEffect = true;
        }

        float distanceScaling = 1f;
        if (allowDistanceScaling && TrySelectDistanceScalingMultiplier(character, 0, out float selectedDistanceScaling, scope))
        {
            distanceScaling = selectedDistanceScaling;
            hasEffect = true;
        }

        if (!hasEffect)
        {
            return false;
        }

        multiplier = damage * GetPerLevelMultiplier(character, damagePerLevel) * distanceScaling;
        return true;
    }

    private static bool TrySelectScalePerLevel(Character character, out float scalePerLevel)
    {
        scalePerLevel = 0f;
        if (!TryGetScaleRuleScope(character, out LevelRuleScope scope))
        {
            return false;
        }

        if (!TrySelectFloatValue(character, rule => rule.ScalePerLevel, out float selectedScalePerLevel, scope))
        {
            return false;
        }

        scalePerLevel = Mathf.Max(0f, selectedScalePerLevel);
        return true;
    }

    internal static bool TryGetDamageMultiplier(Character character, out float multiplier)
    {
        multiplier = 1f;
        if (!IsLevelSystemEnabled() || character == null || character.IsPlayer())
        {
            return false;
        }

        ZNetView? nview = character.m_nview;
        if (nview == null || !nview.IsValid())
        {
            return false;
        }

        ZDO zdo = nview.GetZDO();
        if (zdo == null || !zdo.GetBool(DamageAppliedKey, false))
        {
            return false;
        }

        multiplier = Mathf.Max(0f, zdo.GetFloat(DamageMultiplierKey, 1f));
        return !Mathf.Approximately(multiplier, 1f);
    }

    internal static bool TrySelectModifierChances(Character character, out ModifierChanceDefinition chances)
    {
        chances = new ModifierChanceDefinition();
        if (!IsLevelSystemEnabled() || character == null || character.IsPlayer())
        {
            return false;
        }

        if (!TryGetModifierRuleScope(character, out LevelRuleScope scope, out bool allowDistanceScaling))
        {
            return false;
        }

        LevelRuleSearch search = GetRuleSearch(character, scope);
        float distanceMultiplier = 1f;
        if (allowDistanceScaling)
        {
            TrySelectModifierDistanceScalingMultiplier(search, out distanceMultiplier);
        }

        bool hasChance = false;
        foreach (string modifier in CreatureModifierManager.GetKnownModifierKeys())
        {
            if (!TrySelectModifierChance(search, modifier, out float chance) ||
                !CreatureModifierManager.TrySetModifierChance(chances, modifier, Mathf.Clamp(chance * distanceMultiplier, 0f, 100f)))
            {
                continue;
            }

            hasChance = true;
        }

        return hasChance;
    }

    private static bool TrySelectModifierChance(LevelRuleSearch search, string modifier, out float chance)
    {
        return TrySelectModifierValue(search, modifier, static value => value.Chance, out chance);
    }

    internal static bool TrySelectModifierPowers(Character character, out ModifierPowerDefinition powers)
    {
        powers = new ModifierPowerDefinition();
        if (!IsLevelSystemEnabled() || character == null || character.IsPlayer())
        {
            return false;
        }

        if (!TryGetModifierRuleScope(character, out LevelRuleScope scope, out _))
        {
            return false;
        }

        LevelRuleSearch search = GetRuleSearch(character, scope);
        bool hasPower = false;
        foreach (string modifier in CreatureModifierManager.GetKnownModifierKeys())
        {
            if (!TrySelectModifierPower(search, modifier, out float power) ||
                !CreatureModifierManager.TrySetModifierPower(powers, modifier, CreatureModifierManager.ResolveModifierPower(modifier, power)))
            {
                continue;
            }

            hasPower = true;
        }

        if (TrySelectModifierCooldown(search, "deathward", out float deathwardCooldown))
        {
            powers.DeathwardCooldown = Mathf.Max(0f, deathwardCooldown);
            hasPower = true;
        }

        if (TrySelectModifierInteger(search, "deathward", value => value.MaxActivations, out int deathwardMaxActivations))
        {
            powers.DeathwardMaxActivations = Math.Max(1, deathwardMaxActivations);
            hasPower = true;
        }

        if (TrySelectModifierCooldown(search, "blink", out float blinkCooldown))
        {
            powers.BlinkCooldown = Mathf.Max(0f, blinkCooldown);
            hasPower = true;
        }

        if (TrySelectModifierCooldown(search, "juggernaut", out float knockbackCooldown))
        {
            powers.KnockbackCooldown = Mathf.Max(0f, knockbackCooldown);
            hasPower = true;
        }

        if (TrySelectModifierValue(search, "blamer", value => value.MaxKarmaGain, out float blamerMaxKarmaGain))
        {
            powers.BlamerMaxKarmaGain = Mathf.Max(0f, blamerMaxKarmaGain);
            hasPower = true;
        }

        if (TrySelectModifierValue(search, "blamer", value => value.FleeHealthRatio, out float blamerFleeHealthRatio))
        {
            powers.BlamerFleeHealthRatio = Mathf.Clamp01(blamerFleeHealthRatio);
            hasPower = true;
        }

        if (TrySelectModifierMaxRange(search, "blink", out float blinkMaxRange))
        {
            powers.BlinkMaxRange = Mathf.Max(0f, blinkMaxRange);
            hasPower = true;
        }

        if (TrySelectModifierStartEffect(search, "blink", out string blinkStartEffect))
        {
            powers.BlinkStartEffect = blinkStartEffect;
            hasPower = true;
        }

        if (TrySelectModifierValue(search, "exposed", value => value.ProcChance, out float exposedProcChance))
        {
            powers.ExposedProcChance = Mathf.Clamp01(exposedProcChance);
            hasPower = true;
        }

        if (TrySelectModifierValue(search, "exposed", value => value.Duration, out float exposedDuration))
        {
            powers.ExposedDuration = Mathf.Max(0.1f, exposedDuration);
            hasPower = true;
        }

        if (TrySelectModifierValue(search, "weakened", value => value.ProcChance, out float weakenedProcChance))
        {
            powers.WeakenedProcChance = Mathf.Clamp01(weakenedProcChance);
            hasPower = true;
        }

        if (TrySelectModifierValue(search, "weakened", value => value.Duration, out float weakenedDuration))
        {
            powers.WeakenedDuration = Mathf.Max(0.1f, weakenedDuration);
            hasPower = true;
        }

        if (TrySelectModifierValue(search, "withered", value => value.ProcChance, out float witheredProcChance))
        {
            powers.WitheredProcChance = Mathf.Clamp01(witheredProcChance);
            hasPower = true;
        }

        if (TrySelectModifierValue(search, "withered", value => value.Duration, out float witheredDuration))
        {
            powers.WitheredDuration = Mathf.Max(0.1f, witheredDuration);
            hasPower = true;
        }

        if (TrySelectModifierValue(search, "corrosive", value => value.ProcChance, out float corrosiveProcChance))
        {
            powers.CorrosiveProcChance = Mathf.Clamp01(corrosiveProcChance);
            hasPower = true;
        }

        if (TrySelectModifierValue(search, "corrosive", value => value.Duration, out float corrosiveDuration))
        {
            powers.CorrosiveDuration = Mathf.Max(0.1f, corrosiveDuration);
            hasPower = true;
        }

        if (TrySelectModifierValue(search, "crippling", value => value.SecondaryPower, out float cripplingJump))
        {
            powers.CripplingJump = Mathf.Clamp01(cripplingJump);
            hasPower = true;
        }

        if (TrySelectModifierValue(search, "crippling", value => value.ProcChance, out float cripplingProcChance))
        {
            powers.CripplingProcChance = Mathf.Clamp01(cripplingProcChance);
            hasPower = true;
        }

        if (TrySelectModifierValue(search, "crippling", value => value.Duration, out float cripplingDuration))
        {
            powers.CripplingDuration = Mathf.Max(0.1f, cripplingDuration);
            hasPower = true;
        }

        if (TrySelectModifierValue(search, "disruptive", value => value.SecondaryPower, out float disruptiveEitr))
        {
            powers.DisruptiveEitr = Mathf.Clamp01(disruptiveEitr);
            hasPower = true;
        }

        if (TrySelectModifierValue(search, "disruptive", value => value.ProcChance, out float disruptiveProcChance))
        {
            powers.DisruptiveProcChance = Mathf.Clamp01(disruptiveProcChance);
            hasPower = true;
        }

        if (TrySelectModifierValue(search, "disruptive", value => value.Duration, out float disruptiveDuration))
        {
            powers.DisruptiveDuration = Mathf.Max(0.1f, disruptiveDuration);
            hasPower = true;
        }

        if (TrySelectModifierValue(search, "reflection", value => value.ProcChance, out float reflectionProcChance))
        {
            powers.ReflectionProcChance = Mathf.Clamp01(reflectionProcChance);
            hasPower = true;
        }

        if (TrySelectModifierValue(search, "adrenalineDrain", value => value.SecondaryPower, out float adrenalineDrainGainReduction))
        {
            powers.AdrenalineDrainGainReduction = Mathf.Clamp01(adrenalineDrainGainReduction);
            hasPower = true;
        }

        if (TrySelectModifierValue(search, "adrenalineDrain", value => value.ProcChance, out float adrenalineDrainProcChance))
        {
            powers.AdrenalineDrainProcChance = Mathf.Clamp01(adrenalineDrainProcChance);
            hasPower = true;
        }

        if (TrySelectModifierValue(search, "adrenalineDrain", value => value.Duration, out float adrenalineDrainDuration))
        {
            powers.AdrenalineDrainDuration = Mathf.Max(0.1f, adrenalineDrainDuration);
            hasPower = true;
        }

        if (TrySelectModifierValue(search, "toxicDeath", value => value.Radius, out float toxicDeathRadius))
        {
            powers.ToxicDeathRadius = Mathf.Max(0f, toxicDeathRadius);
            hasPower = true;
        }

        if (TrySelectModifierText(search, "toxicDeath", value => value.TriggerEffect, out string toxicDeathTriggerEffect))
        {
            powers.ToxicDeathTriggerEffect = toxicDeathTriggerEffect;
            hasPower = true;
        }

        if (TrySelectModifierInteger(search, "reaping", value => value.ReapingHealMaxActivations, out int reapingHealMaxActivations))
        {
            powers.ReapingHealMaxActivations = Math.Max(1, reapingHealMaxActivations);
            hasPower = true;
        }

        if (TrySelectModifierValue(search, "reaping", value => value.ReapingMaxHealthPerKill, out float reapingMaxHealthPerKill))
        {
            powers.ReapingMaxHealthPerKill = Mathf.Max(0f, reapingMaxHealthPerKill);
            hasPower = true;
        }

        if (TrySelectModifierValue(search, "reaping", value => value.ReapingMaxHealthCap, out float reapingMaxHealthCap))
        {
            powers.ReapingMaxHealthCap = Mathf.Max(0f, reapingMaxHealthCap);
            hasPower = true;
        }

        if (TrySelectModifierValue(search, "reaping", value => value.ReapingDamagePerKill, out float reapingDamagePerKill))
        {
            powers.ReapingDamagePerKill = Mathf.Max(0f, reapingDamagePerKill);
            hasPower = true;
        }

        if (TrySelectModifierValue(search, "reaping", value => value.ReapingDamageCap, out float reapingDamageCap))
        {
            powers.ReapingDamageCap = Mathf.Max(0f, reapingDamageCap);
            hasPower = true;
        }

        if (TrySelectModifierValue(search, "reaping", value => value.ReapingScalePerKill, out float reapingScalePerKill))
        {
            powers.ReapingScalePerKill = Mathf.Max(0f, reapingScalePerKill);
            hasPower = true;
        }

        if (TrySelectModifierValue(search, "reaping", value => value.ReapingScaleCap, out float reapingScaleCap))
        {
            powers.ReapingScaleCap = Mathf.Max(0f, reapingScaleCap);
            hasPower = true;
        }

        return hasPower;
    }

    private static bool TrySelectModifierPower(LevelRuleSearch search, string modifier, out float power)
    {
        return TrySelectModifierValue(search, modifier, static value => value.Power, out power);
    }

    private static bool TrySelectModifierCooldown(LevelRuleSearch search, string modifier, out float cooldown)
    {
        return TrySelectModifierValue(search, modifier, static value => value.Cooldown, out cooldown);
    }

    private static bool TrySelectModifierMaxRange(LevelRuleSearch search, string modifier, out float maxRange)
    {
        return TrySelectModifierValue(search, modifier, static value => value.MaxRange, out maxRange);
    }

    private static bool TrySelectModifierInteger(
        LevelRuleSearch search,
        string modifier,
        Func<ModifierDefinition, int?> selector,
        out int result)
    {
        result = 0;
        foreach (LevelRuleCandidate candidate in search.Candidates)
        {
            LevelDefinition rule = candidate.Definition;
            if (rule.ModifiersCleared)
            {
                return false;
            }

            ModifierDefinition? definition = TryGetModifier(rule, modifier);
            int? value = definition == null ? null : selector(definition);
            if (!value.HasValue)
            {
                continue;
            }

            result = value.Value;
            return true;
        }

        return false;
    }

    private static bool TrySelectModifierStartEffect(LevelRuleSearch search, string modifier, out string startEffect)
    {
        return TrySelectModifierText(search, modifier, static value => value.StartEffect, out startEffect);
    }

    private static bool TrySelectModifierText(
        LevelRuleSearch search,
        string modifier,
        Func<ModifierDefinition, string?> selector,
        out string value)
    {
        value = "";
        foreach (LevelRuleCandidate candidate in search.Candidates)
        {
            LevelDefinition rule = candidate.Definition;
            if (rule.ModifiersCleared)
            {
                return false;
            }

            ModifierDefinition? selected = TryGetModifier(rule, modifier);
            string? configured = selected != null ? selector(selected) : null;
            if (configured == null)
            {
                continue;
            }

            value = configured;
            return true;
        }

        return false;
    }

    private static bool TrySelectModifierValue(
        LevelRuleSearch search,
        string modifier,
        Func<ModifierDefinition, float?> selector,
        out float value)
    {
        value = 0f;
        foreach (LevelRuleCandidate candidate in search.Candidates)
        {
            LevelDefinition rule = candidate.Definition;
            if (rule.ModifiersCleared)
            {
                return false;
            }

            ModifierDefinition? selected = TryGetModifier(rule, modifier);
            float? configured = selected != null ? selector(selected) : null;
            if (!configured.HasValue)
            {
                continue;
            }

            value = configured.Value;
            return true;
        }

        return false;
    }

    private static ModifierDefinition? TryGetModifier(LevelDefinition rule, string modifier)
    {
        if (rule.Modifiers == null || !rule.Modifiers.TryGetValue(modifier, out ModifierDefinition definition))
        {
            return null;
        }

        return definition;
    }

    private static bool HasHealthEffect(LevelDefinition rule)
    {
        return rule.Health.HasValue || rule.HealthPerLevel.HasValue || HasDistanceScalingValue(rule.DistanceScaling, 1);
    }

    private static bool HasDamageEffect(LevelDefinition rule)
    {
        return rule.Damage.HasValue || rule.DamagePerLevel.HasValue || HasDistanceScalingValue(rule.DistanceScaling, 0);
    }

    private static bool HasDistanceScalingValue(List<float>? scaling, int index)
    {
        return scaling != null && scaling.Count > index && scaling[index] > 0f;
    }

    private static bool TrySelectRule(Character character, Func<LevelDefinition, bool> hasEffect, out LevelDefinition rule, LevelRuleScope scope = LevelRuleScope.Full)
    {
        rule = null!;
        LevelRuleSearch search = GetRuleSearch(character, scope);
        foreach (LevelRuleCandidate candidate in search.Candidates)
        {
            if (!hasEffect(candidate.Definition))
            {
                continue;
            }

            rule = candidate.Definition;
            return true;
        }

        return false;
    }

    private static LevelRuleSearch GetRuleSearch(Character character, LevelRuleScope scope)
    {
        int frame = Time.frameCount;
        if (CachedRuleSearch != null &&
            CachedRuleSearchFrame == frame &&
            CachedRuleSearchScope == scope &&
            CachedRuleSearchCharacter == character)
        {
            return CachedRuleSearch;
        }

        LevelDefinition[] definitions;
        lock (Sync)
        {
            definitions = ActiveDefinitions;
        }

        LevelRuleContext context = LevelRuleContext.From(character);
        List<LevelRuleCandidate> candidates = new(definitions.Length);
        for (int index = 0; index < definitions.Length; index++)
        {
            if (TryMatchRule(definitions[index], context, index, scope, out LevelRuleCandidate candidate))
            {
                candidates.Add(candidate);
            }
        }

        candidates.Sort((left, right) => right.CompareTo(left));
        CachedRuleSearchCharacter = character;
        CachedRuleSearchScope = scope;
        CachedRuleSearchFrame = frame;
        CachedRuleSearch = new LevelRuleSearch(context, candidates);
        return CachedRuleSearch;
    }

    private static bool TrySelectFloatValue(Character character, Func<LevelDefinition, float?> selector, out float value, LevelRuleScope scope = LevelRuleScope.Full)
    {
        value = 0f;
        if (!TrySelectRule(character, rule => selector(rule).HasValue, out LevelDefinition rule, scope))
        {
            return false;
        }

        float? selected = selector(rule);
        if (!selected.HasValue)
        {
            return false;
        }

        value = selected.Value;
        return true;
    }

    private static bool TrySelectDistanceScalingMultiplier(Character character, int valueIndex, out float multiplier, LevelRuleScope scope = LevelRuleScope.Full)
    {
        multiplier = 1f;
        if (!TrySelectRule(character, rule => HasDistanceScalingValue(rule.DistanceScaling, valueIndex), out LevelDefinition rule, scope))
        {
            return false;
        }

        multiplier = GetDistanceScalingMultiplier(GetRuleSearch(character, scope).Context.Distance, rule.DistanceScaling, valueIndex);
        return true;
    }

    private static bool TrySelectModifierDistanceScalingMultiplier(LevelRuleSearch search, out float multiplier)
    {
        multiplier = 1f;
        foreach (LevelRuleCandidate candidate in search.Candidates)
        {
            LevelDefinition rule = candidate.Definition;
            if (!HasModifierDistanceScalingValue(rule.ModifierDistanceScaling))
            {
                continue;
            }

            multiplier = GetModifierDistanceScalingMultiplier(search.Context.Distance, rule.ModifierDistanceScaling);
            return true;
        }

        return false;
    }

    private static bool HasModifierDistanceScalingValue(List<float>? scaling)
    {
        return scaling is { Count: 3 };
    }

    private static bool TryMatchRule(LevelDefinition definition, LevelRuleContext context, int index, LevelRuleScope scope, out LevelRuleCandidate candidate)
    {
        candidate = new LevelRuleCandidate(definition, 0, index);
        string target = (definition.Target ?? "").Trim();
        if (target.Length == 0)
        {
            return false;
        }

        bool regularBoss = context.IsBoss && !context.IsEnforcer;
        if (scope == LevelRuleScope.BaselineOnly &&
            (regularBoss ? !IsBossTarget(target) : !IsGlobalTarget(target)))
        {
            return false;
        }

        string biome = (definition.Biome ?? "").Trim();
        bool hasBiomeCondition = biome.Length > 0;
        if (hasBiomeCondition && !MatchesBiome(biome, context.Biome))
        {
            return false;
        }

        int biomeSpecificity = hasBiomeCondition ? 50 : 0;
        if (IsGlobalTarget(target))
        {
            if (regularBoss)
            {
                return false;
            }

            candidate = new LevelRuleCandidate(definition, biomeSpecificity, index);
            return true;
        }

        if (IsBossTarget(target))
        {
            if (!regularBoss)
            {
                return false;
            }

            candidate = new LevelRuleCandidate(definition, 150 + biomeSpecificity, index);
            return true;
        }

        if (string.Equals(target, context.PrefabName, StringComparison.OrdinalIgnoreCase))
        {
            candidate = new LevelRuleCandidate(definition, 300 + biomeSpecificity, index);
            return true;
        }

        List<string>? prefabs = definition.Prefabs;
        if (prefabs is { Count: > 0 })
        {
            bool matchedPrefab = false;
            foreach (string prefab in prefabs)
            {
                if (!string.Equals(prefab, context.PrefabName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                matchedPrefab = true;
                break;
            }

            if (!matchedPrefab)
            {
                return false;
            }

            candidate = new LevelRuleCandidate(definition, 200 + biomeSpecificity, index);
            return true;
        }

        if ((context.IsEnforcer || !context.IsBoss || BossCanUsePresetBiomeRule(definition)) && MatchesBiome(target, context.Biome))
        {
            candidate = new LevelRuleCandidate(definition, 100, index);
            return true;
        }

        return false;
    }

    private static float GetPerLevelMultiplier(Character character, float perLevel)
    {
        if (perLevel <= 0f)
        {
            return 1f;
        }

        int level = Math.Max(1, character.GetLevel());
        return Mathf.Max(0f, 1f + (level - 1) * perLevel);
    }

    private static bool IsGlobalTarget(string target)
    {
        return string.Equals(target, "global", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBossTarget(string target)
    {
        return string.Equals(target, "boss", StringComparison.OrdinalIgnoreCase);
    }

    private static bool BossCanUsePresetBiomeRule(LevelDefinition definition)
    {
        return definition.IsPreset &&
               CreatureManagerPlugin.BossesFollowBiomeLevelPreset?.Value == CreatureManagerPlugin.Toggle.On;
    }

    private static float GetDistanceScalingMultiplier(float distance, List<float>? scaling, int valueIndex)
    {
        if (scaling == null || scaling.Count <= valueIndex)
        {
            return 1f;
        }

        float perStep = Mathf.Max(0f, scaling[valueIndex]);
        if (perStep <= 0f)
        {
            return 1f;
        }

        float interval = scaling.Count >= 3 && scaling[2] > 0f ? scaling[2] : 1000f;
        int steps = Mathf.FloorToInt(distance / interval);
        if (scaling.Count >= 4 && scaling[3] > 0f)
        {
            steps = Math.Min(steps, Mathf.FloorToInt(scaling[3]));
        }

        return Mathf.Max(0f, 1f + steps * perStep);
    }

    private static float GetModifierDistanceScalingMultiplier(float distance, List<float>? scaling)
    {
        if (scaling is not { Count: 3 })
        {
            return 1f;
        }

        float perStep = Mathf.Max(0f, scaling[0]);
        float interval = scaling[1] > 0f ? scaling[1] : 1000f;
        int steps = Mathf.FloorToInt(distance / interval);
        if (scaling[2] > 0f)
        {
            steps = Math.Min(steps, Mathf.FloorToInt(scaling[2]));
        }

        return Mathf.Max(0f, 1f + steps * perStep);
    }

    private static void ApplyHealthMultiplier(Character character, float multiplier)
    {
        if (multiplier <= 0f)
        {
            return;
        }

        float maxHealth = character.GetMaxHealth();
        float currentHealth = character.GetHealth();
        if (maxHealth <= 0f)
        {
            return;
        }

        float missingHealth = Mathf.Max(0f, maxHealth - currentHealth);
        float targetMaxHealth = maxHealth * multiplier;
        character.SetMaxHealth(targetMaxHealth);
        if (missingHealth > 0f)
        {
            character.SetHealth(Mathf.Max(1f, targetMaxHealth - missingHealth));
        }
        else
        {
            character.Heal(targetMaxHealth, true);
        }
    }

    private static bool TrySelectLevel(List<float> weights, string prefabName, out int level)
    {
        level = 1;
        if (weights.Count == 0)
        {
            CreatureManagerPlugin.Log.LogWarning($"Level rule for '{prefabName}' has no level weights. Use [weightLevel1, weightLevel2, ...].");
            return false;
        }

        float total = weights.Where(weight => weight > 0f).Sum();
        if (total <= 0f)
        {
            CreatureManagerPlugin.Log.LogWarning($"Level rule for '{prefabName}' has no positive level weights.");
            return false;
        }

        float roll = UnityEngine.Random.Range(0f, total);
        for (int index = 0; index < weights.Count; index++)
        {
            float weight = Math.Max(0f, weights[index]);
            if (roll < weight)
            {
                level = index + 1;
                return true;
            }

            roll -= weight;
        }

        level = weights.Count;
        return true;
    }

    internal static string GetBiomeName(Vector3 position)
    {
        return GetBiome(position).ToString();
    }

    private static Heightmap.Biome GetBiome(Vector3 position)
    {
        if (WorldGenerator.instance == null)
        {
            return Heightmap.Biome.None;
        }

        try
        {
            return WorldGenerator.instance.GetBiome(position);
        }
        catch (Exception ex)
        {
            CreatureManagerPlugin.Log.LogDebug($"Failed to resolve biome for level rule at {position}: {ex.Message}");
            return Heightmap.Biome.None;
        }
    }

    private static float GetHorizontalDistance(Vector3 position)
    {
        return Mathf.Sqrt(position.x * position.x + position.z * position.z);
    }

    private static string NormalizeBiomeName(string biomeName)
    {
        return new string((biomeName ?? "")
            .Where(character => !char.IsWhiteSpace(character) && character != '_' && character != '-')
            .Select(char.ToLowerInvariant)
            .ToArray());
    }

    private static bool MatchesBiome(string biomeName, Heightmap.Biome currentBiome)
    {
        string normalized = NormalizeBiomeName(biomeName);
        if (normalized.Length == 0)
        {
            return false;
        }

        if (CreatureKarmaManager.TryResolveBiomeName(biomeName, out Heightmap.Biome parsedBiome))
        {
            return (currentBiome & parsedBiome) != 0;
        }

        if (NormalizeBiomeName(currentBiome.ToString()) == normalized)
        {
            return true;
        }

        return CreatureKarmaManager.TryGetBiomeDisplayName(currentBiome, out string displayName) &&
               NormalizeBiomeName(displayName) == normalized;
    }

    private readonly struct LevelRuleContext
    {
        internal string PrefabName { get; }
        internal Heightmap.Biome Biome { get; }
        internal float Distance { get; }
        internal bool IsBoss { get; }
        internal bool IsEnforcer { get; }

        private LevelRuleContext(string prefabName, Heightmap.Biome biome, float distance, bool isBoss, bool isEnforcer)
        {
            PrefabName = prefabName;
            Biome = biome;
            Distance = distance;
            IsBoss = isBoss;
            IsEnforcer = isEnforcer;
        }

        internal static LevelRuleContext From(Character character)
        {
            return new LevelRuleContext(
                GetPrefabName(character.gameObject),
                GetBiome(character.transform.position),
                GetHorizontalDistance(character.transform.position),
                character.IsBoss(),
                CreatureKarmaManager.IsEnforcer(character));
        }
    }

    private readonly struct LevelRuleCandidate
    {
        internal LevelRuleCandidate(LevelDefinition definition, int specificity, int index)
        {
            Definition = definition;
            Specificity = specificity;
            Index = index;
        }

        internal LevelDefinition Definition { get; }
        private int Specificity { get; }
        private int Index { get; }

        internal int CompareTo(LevelRuleCandidate other)
        {
            int specificity = Specificity.CompareTo(other.Specificity);
            return specificity != 0 ? specificity : Index.CompareTo(other.Index);
        }
    }

    private sealed class LevelRuleSearch
    {
        internal LevelRuleSearch(LevelRuleContext context, List<LevelRuleCandidate> candidates)
        {
            Context = context;
            Candidates = candidates;
        }

        internal LevelRuleContext Context { get; }
        internal List<LevelRuleCandidate> Candidates { get; }
    }

    private static string GetPrefabName(GameObject gameObject)
    {
        string name = gameObject.name;
        int cloneIndex = name.IndexOf("(Clone)", StringComparison.Ordinal);
        if (cloneIndex >= 0)
        {
            name = name.Substring(0, cloneIndex);
        }

        return name.Trim();
    }
}

internal sealed class CreatureLevelEffectsState : MonoBehaviour
{
    private readonly Dictionary<int, Material> MaterialsByVisualState = new();
    private readonly Dictionary<GameObject, bool> OriginalActiveStates = new();
    private bool Initialized;
    internal Vector3 OriginalLocalScale { get; private set; } = Vector3.one;
    internal Material? OriginalMainMaterial { get; private set; }

    internal static CreatureLevelEffectsState Get(LevelEffects levelEffects)
    {
        CreatureLevelEffectsState state = levelEffects.GetComponent<CreatureLevelEffectsState>();
        return state != null ? state : levelEffects.gameObject.AddComponent<CreatureLevelEffectsState>();
    }

    internal void EnsureInitialized(LevelEffects levelEffects)
    {
        if (Initialized)
        {
            return;
        }

        Initialized = true;
        OriginalLocalScale = levelEffects.transform.localScale;
        OriginalMainMaterial = GetFirstMaterial(levelEffects.m_mainRender);
        RecordOriginalActive(levelEffects.m_baseEnableObject);
        foreach (LevelEffects.LevelSetup setup in levelEffects.m_levelSetups)
        {
            RecordOriginalActive(setup?.m_enableObject);
        }
    }

    internal bool GetOriginalActive(GameObject gameObject, bool fallback)
    {
        return OriginalActiveStates.TryGetValue(gameObject, out bool active) ? active : fallback;
    }

    internal Material GetOrCreateMaterial(int visualState, LevelEffects.LevelSetup setup)
    {
        if (MaterialsByVisualState.TryGetValue(visualState, out Material material) && material != null)
        {
            return material;
        }

        material = OriginalMainMaterial != null
            ? new Material(OriginalMainMaterial)
            : new Material(Shader.Find("Standard"));
        CreatureLevelManager.ApplyColorProperties(material, setup);
        MaterialsByVisualState[visualState] = material;
        return material;
    }

    private void RecordOriginalActive(GameObject? gameObject)
    {
        if (gameObject != null && !OriginalActiveStates.ContainsKey(gameObject))
        {
            OriginalActiveStates[gameObject] = gameObject.activeSelf;
        }
    }

    private static Material? GetFirstMaterial(Renderer? renderer)
    {
        if (renderer == null)
        {
            return null;
        }

        Material[] materials = renderer.sharedMaterials;
        return materials.Length > 0 ? materials[0] : null;
    }

    private void OnDestroy()
    {
        foreach (Material material in MaterialsByVisualState.Values)
        {
            if (material != null)
            {
                Destroy(material);
            }
        }

        MaterialsByVisualState.Clear();
        OriginalActiveStates.Clear();
    }
}

internal sealed class CreatureCharacterScaleState : MonoBehaviour
{
    private bool Initialized { get; set; }
    internal Vector3 OriginalLocalScale { get; private set; } = Vector3.one;

    internal static CreatureCharacterScaleState Get(Character character)
    {
        CreatureCharacterScaleState state = character.GetComponent<CreatureCharacterScaleState>();
        return state != null ? state : character.gameObject.AddComponent<CreatureCharacterScaleState>();
    }

    internal static void RestoreIfPresent(Character character)
    {
        CreatureCharacterScaleState state = character.GetComponent<CreatureCharacterScaleState>();
        if (state != null && state.Initialized)
        {
            character.transform.localScale = state.OriginalLocalScale;
        }
    }

    internal void EnsureInitialized(Character character)
    {
        if (Initialized)
        {
            return;
        }

        OriginalLocalScale = character.transform.localScale;
        Initialized = true;
    }
}
