using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace CreatureManager;

internal static class CreatureManagerRandomHairRuntime
{
    private const string SetHairEquippedMethodName = "SetHairEquipped";
    private const string SetBeardEquippedMethodName = "SetBeardEquipped";
    private const string GetHairItemMethodName = "GetHairItem";
    private const string UpdateLodgroupMethodName = "UpdateLodgroup";
    private const string UpdateBaseModelMethodName = "UpdateBaseModel";
    private static readonly int RandomHairHashKey = "CreatureManager.RandomHair".GetStableHashCode();
    private static readonly int RandomHairColorManagedKey = "CreatureManager.RandomHairColorManaged".GetStableHashCode();
    private static readonly HashSet<int> EligibleVisuals = new();
    private static readonly HashSet<int> AppliedVisuals = new();
    private static readonly HashSet<int> FailedVisuals = new();
    private static readonly Dictionary<int, Vector3> ConfiguredHairColors = new();
    private static readonly Dictionary<int, Vector3> AppliedHairColors = new();
    private static readonly HashSet<int> FailedRagdollAccessoryVisuals = new();
    private static readonly HashSet<int> FailedRagdollColorVisuals = new();
    private static readonly HashSet<int> FailedRagdollModelVisuals = new();

    private delegate bool SetHairEquippedDelegate(VisEquipment instance, int hash);
    private delegate bool SetBeardEquippedDelegate(VisEquipment instance, int hash);
    private delegate int GetHairItemDelegate(
        VisEquipment instance,
        ItemDrop.ItemData.HelmetHairType type,
        int itemHash,
        ItemDrop.ItemData.AccessoryType accessory);
    private delegate void UpdateLodgroupDelegate(VisEquipment instance);
    private delegate void UpdateBaseModelDelegate(VisEquipment instance);

    private static bool _delegatesResolved;
    private static bool _visualRuntimeFailed;
    private static bool _ragdollAppearanceDelegateWarningLogged;
    private static SetHairEquippedDelegate? _setHairEquipped;
    private static SetBeardEquippedDelegate? _setBeardEquipped;
    private static GetHairItemDelegate? _getHairItem;
    private static UpdateLodgroupDelegate? _updateLodgroup;
    private static UpdateBaseModelDelegate? _updateBaseModel;

    internal static void Initialize(Humanoid humanoid)
    {
        if (humanoid == null)
        {
            return;
        }

        VisEquipment? visEquipment = humanoid.GetComponent<VisEquipment>();
        bool configured = CreatureDomainManager.TryGetRandomHairPrefabs(
            humanoid,
            out IReadOnlyList<GameObject> configuredPrefabs);
        List<GameObject> candidates = configured
            ? configuredPrefabs.Where(prefab => prefab != null).ToList()
            : new List<GameObject>();
        bool hasConfiguredHairColor = CreatureDomainManager.TryGetConfiguredAppearanceHairColor(
            humanoid,
            out Vector3 configuredHairColor);

        if (visEquipment != null)
        {
            int visualId = visEquipment.GetInstanceID();
            FailedVisuals.Remove(visualId);
            if (candidates.Count > 0)
            {
                EligibleVisuals.Add(visualId);
            }
            else
            {
                EligibleVisuals.Remove(visualId);
                ClearAppliedVisual(visEquipment);
            }

            ConfigureAppearanceHairColor(
                humanoid,
                visEquipment,
                candidates.Count > 0,
                hasConfiguredHairColor,
                configuredHairColor);
        }

        ZNetView? nview = humanoid.m_nview;
        if (nview == null || !nview.IsValid() || !nview.IsOwner())
        {
            if (candidates.Count > 0 && visEquipment != null)
            {
                UpdateVisual(visEquipment);
            }

            return;
        }

        ZDO? zdo = nview.GetZDO();
        if (zdo == null)
        {
            return;
        }

        if (candidates.Count == 0)
        {
            if (zdo.GetInt(RandomHairHashKey, 0) != 0)
            {
                zdo.Set(RandomHairHashKey, 0);
                RestoreStandardHair(humanoid, visEquipment);
            }

            return;
        }

        int storedHash = zdo.GetInt(RandomHairHashKey, 0);
        GameObject? selected = candidates.FirstOrDefault(
            prefab => prefab.name.GetStableHashCode() == storedHash);
        if (selected == null)
        {
            int seed = zdo.GetInt(ZDOVars.s_seed, 0);
            selected = candidates[SelectDeterministicIndex(seed, candidates.Count)];
            storedHash = selected.name.GetStableHashCode();
            zdo.Set(RandomHairHashKey, storedHash);
        }

        if (visEquipment != null && visEquipment.m_isPlayer)
        {
            // Player-style VisEquipment reads the normal hair ZDO field before rendering.
            // GetHairItem is patched below so the selected custom hair bypasses helmet hiding.
            visEquipment.SetHairItem(selected.name);
        }

        if (visEquipment != null)
        {
            UpdateVisual(visEquipment);
        }
    }

    internal static void RefreshLoadedHumanoids()
    {
        Character[] characters = Character.GetAllCharacters().ToArray();
        foreach (Character character in characters)
        {
            if (character is Humanoid humanoid && humanoid != null)
            {
                try
                {
                    Initialize(humanoid);
                }
                catch (Exception ex)
                {
                    CreatureManagerPlugin.Log.LogWarning(
                        $"Failed to initialize random hair on loaded humanoid '{humanoid.gameObject.name}': {ex.Message}");
                }
            }
        }
    }

    private static void ConfigureAppearanceHairColor(
        Humanoid humanoid,
        VisEquipment visEquipment,
        bool hasRandomHair,
        bool hasConfiguredColor,
        Vector3 configuredColor)
    {
        int visualId = visEquipment.GetInstanceID();
        bool previouslyConfigured = ConfiguredHairColors.ContainsKey(visualId);
        if (hasRandomHair && hasConfiguredColor)
        {
            ConfiguredHairColors[visualId] = configuredColor;
        }
        else
        {
            ConfiguredHairColors.Remove(visualId);
        }

        ZNetView? nview = humanoid.m_nview;
        ZDO? zdo = nview != null && nview.IsValid() ? nview.GetZDO() : null;
        bool ownsZdo = nview != null && nview.IsValid() && nview.IsOwner() && zdo != null;
        if (hasConfiguredColor)
        {
            visEquipment.m_hairColor = configuredColor;
            if (ownsZdo)
            {
                zdo!.Set(ZDOVars.s_hairColor, configuredColor);
                zdo.Set(RandomHairColorManagedKey, 1);
            }

            return;
        }

        bool restoreManagedColor = ownsZdo && zdo!.GetInt(RandomHairColorManagedKey, 0) != 0;
        if (!previouslyConfigured && !AppliedHairColors.ContainsKey(visualId) && !restoreManagedColor)
        {
            return;
        }

        Vector3 inheritedColor = CreatureDomainManager.GetInheritedAppearanceHairColor(humanoid);
        visEquipment.m_hairColor = inheritedColor;
        if (restoreManagedColor)
        {
            zdo!.Set(ZDOVars.s_hairColor, inheritedColor);
            zdo.Set(RandomHairColorManagedKey, 0);
        }

        ApplyHairMaterialColor(visEquipment, inheritedColor, force: true);
        AppliedHairColors.Remove(visualId);
    }

    internal static void UpdateVisual(VisEquipment visEquipment)
    {
        if (visEquipment == null || _visualRuntimeFailed)
        {
            return;
        }

        int visualId = visEquipment.GetInstanceID();
        if (FailedVisuals.Contains(visualId) ||
            (!EligibleVisuals.Contains(visualId) && !AppliedVisuals.Contains(visualId)))
        {
            return;
        }

        int hairHash = GetStoredHairHash(visEquipment);
        if (hairHash == 0)
        {
            ClearAppliedVisual(visEquipment);
            return;
        }

        if (ApplyHairVisual(visEquipment, hairHash))
        {
            AppliedVisuals.Add(visualId);
        }
    }

    internal static bool TryBypassHelmetHairFilter(
        VisEquipment visEquipment,
        int itemHash,
        ItemDrop.ItemData.AccessoryType accessory,
        out int result)
    {
        result = 0;
        int visualId = visEquipment != null ? visEquipment.GetInstanceID() : 0;
        if (visEquipment == null ||
            accessory != ItemDrop.ItemData.AccessoryType.Hair ||
            itemHash == 0 ||
            FailedVisuals.Contains(visualId) ||
            !EligibleVisuals.Contains(visualId) ||
            GetStoredHairHash(visEquipment) != itemHash)
        {
            return false;
        }

        result = itemHash;
        return true;
    }

    internal static void ApplyToRagdoll(Humanoid source, Ragdoll ragdoll)
    {
        if (source == null || ragdoll == null)
        {
            return;
        }

        VisEquipment? visEquipment = ragdoll.GetComponent<VisEquipment>();
        if (visEquipment == null)
        {
            return;
        }

        if (!CreatureDomainManager.TryGetConfiguredRagdollAppearance(
                visEquipment,
                out CreatureDomainManager.CreatureAppearanceRuntimeState appearance))
        {
            CreatureDomainManager.TryGetConfiguredAppearance(source, out appearance);
        }

        if (appearance != null)
        {
            ApplyConfiguredAppearanceToRagdoll(visEquipment, appearance);
        }

        CreatureDomainManager.ApplyConfiguredRagdollTextures(visEquipment);

        ZDO? sourceZdo = source.m_nview?.GetZDO();
        int hairHash = sourceZdo?.GetInt(RandomHairHashKey, 0) ?? 0;
        if (hairHash == 0)
        {
            return;
        }

        int visualId = visEquipment.GetInstanceID();
        EligibleVisuals.Add(visualId);
        bool hasConfiguredHairColor = CreatureDomainManager.TryGetConfiguredAppearanceHairColor(
            source,
            out Vector3 configuredHairColor);
        if (hasConfiguredHairColor)
        {
            ConfiguredHairColors[visualId] = configuredHairColor;
            visEquipment.m_hairColor = configuredHairColor;
        }
        else
        {
            ConfiguredHairColors.Remove(visualId);
        }

        ZNetView? ragdollView = ragdoll.GetComponent<ZNetView>();
        ZDO? ragdollZdo = ragdollView?.GetZDO();
        if (ragdollView != null && ragdollView.IsValid() && ragdollView.IsOwner() && ragdollZdo != null)
        {
            ragdollZdo.Set(RandomHairHashKey, hairHash);
            if (visEquipment.m_isPlayer)
            {
                // Keep vanilla's player-style hair field aligned so its normal visual update does not
                // replace the configured random hair immediately before our postfix restores it.
                ragdollZdo.Set(ZDOVars.s_hairItem, hairHash);
            }
            if (hasConfiguredHairColor)
            {
                ragdollZdo.Set(ZDOVars.s_hairColor, configuredHairColor);
                ragdollZdo.Set(RandomHairColorManagedKey, 1);
            }
        }

        if (ApplyHairVisual(visEquipment, hairHash))
        {
            AppliedVisuals.Add(visualId);
        }
    }

    private static void ApplyConfiguredAppearanceToRagdoll(
        VisEquipment visEquipment,
        CreatureDomainManager.CreatureAppearanceRuntimeState appearance)
    {
        ZNetView? nview = visEquipment.m_nview;
        ZDO? zdo = nview != null && nview.IsValid() ? nview.GetZDO() : null;
        bool ownsZdo = nview != null && nview.IsValid() && nview.IsOwner() && zdo != null;

        if (appearance.Hair != null)
        {
            visEquipment.m_hairItem = appearance.Hair;
            if (ownsZdo)
            {
                zdo!.Set(
                    ZDOVars.s_hairItem,
                    appearance.Hair.Length > 0 ? appearance.Hair.GetStableHashCode() : 0);
            }
        }

        if (appearance.Beard != null)
        {
            visEquipment.m_beardItem = appearance.Beard;
            if (ownsZdo)
            {
                zdo!.Set(
                    ZDOVars.s_beardItem,
                    appearance.Beard.Length > 0 ? appearance.Beard.GetStableHashCode() : 0);
            }
        }

        if (appearance.HairColor.HasValue)
        {
            visEquipment.m_hairColor = appearance.HairColor.Value;
            if (ownsZdo)
            {
                zdo!.Set(ZDOVars.s_hairColor, appearance.HairColor.Value);
            }
        }

        if (appearance.SkinColor.HasValue)
        {
            visEquipment.m_skinColor = appearance.SkinColor.Value;
            if (ownsZdo)
            {
                zdo!.Set(ZDOVars.s_skinColor, appearance.SkinColor.Value);
            }
        }

        if (appearance.ModelIndex is int modelIndex &&
            IsSupportedModelIndex(visEquipment, modelIndex))
        {
            visEquipment.m_modelIndex = modelIndex;
            if (ownsZdo)
            {
                zdo!.Set(ZDOVars.s_modelIndex, modelIndex);
            }
        }

        ApplyConfiguredRagdollAppearanceVisual(visEquipment, appearance);
    }

    internal static void UpdateConfiguredRagdollAppearance(VisEquipment visEquipment)
    {
        if (visEquipment == null ||
            !CreatureDomainManager.TryGetConfiguredRagdollAppearance(
                visEquipment,
                out CreatureDomainManager.CreatureAppearanceRuntimeState appearance))
        {
            return;
        }

        ApplyConfiguredRagdollAppearanceVisual(visEquipment, appearance);
    }

    private static void ApplyConfiguredRagdollAppearanceVisual(
        VisEquipment visEquipment,
        CreatureDomainManager.CreatureAppearanceRuntimeState appearance)
    {
        if (visEquipment.m_isPlayer)
        {
            return;
        }

        EnsureDelegates();
        ApplyConfiguredRagdollModel(visEquipment, appearance);
        ApplyConfiguredRagdollAccessories(visEquipment, appearance);
        ApplyConfiguredRagdollColors(visEquipment, appearance);
    }

    private static void ApplyConfiguredRagdollModel(
        VisEquipment visEquipment,
        CreatureDomainManager.CreatureAppearanceRuntimeState appearance)
    {
        if (appearance.ModelIndex is not int modelIndex ||
            !IsSupportedModelIndex(visEquipment, modelIndex) ||
            visEquipment.m_models == null ||
            visEquipment.m_models.Length == 0 ||
            visEquipment.m_bodyModel == null)
        {
            return;
        }

        visEquipment.m_modelIndex = modelIndex;
        int visualId = visEquipment.GetInstanceID();
        if (FailedRagdollModelVisuals.Contains(visualId))
        {
            return;
        }

        if (_updateBaseModel == null)
        {
            LogRagdollAppearanceDelegateWarning();
            return;
        }

        try
        {
            _updateBaseModel(visEquipment);
        }
        catch (Exception exception)
        {
            FailedRagdollModelVisuals.Add(visualId);
            CreatureManagerPlugin.Log.LogWarning(
                $"Ragdoll model appearance was disabled for '{visEquipment.name}' after the supported VisEquipment model update failed: {exception.Message}");
        }
    }

    private static void ApplyConfiguredRagdollAccessories(
        VisEquipment visEquipment,
        CreatureDomainManager.CreatureAppearanceRuntimeState appearance)
    {
        if (appearance.Hair == null && appearance.Beard == null)
        {
            return;
        }

        int visualId = visEquipment.GetInstanceID();
        if (FailedRagdollAccessoryVisuals.Contains(visualId))
        {
            return;
        }

        if (_setHairEquipped == null || _setBeardEquipped == null || _getHairItem == null)
        {
            LogRagdollAppearanceDelegateWarning();
            return;
        }

        try
        {
            bool changed = false;
            if (appearance.Beard != null)
            {
                int beardHash = appearance.Beard.Length > 0
                    ? appearance.Beard.GetStableHashCode()
                    : 0;
                beardHash = _getHairItem(
                    visEquipment,
                    visEquipment.m_helmetHideBeard,
                    beardHash,
                    ItemDrop.ItemData.AccessoryType.Beard);
                changed |= _setBeardEquipped(visEquipment, beardHash);
            }

            if (appearance.Hair != null && GetStoredHairHash(visEquipment) == 0)
            {
                int hairHash = appearance.Hair.Length > 0
                    ? appearance.Hair.GetStableHashCode()
                    : 0;
                hairHash = _getHairItem(
                    visEquipment,
                    visEquipment.m_helmetHideHair,
                    hairHash,
                    ItemDrop.ItemData.AccessoryType.Hair);
                changed |= _setHairEquipped(visEquipment, hairHash);
            }

            if (changed)
            {
                _updateLodgroup?.Invoke(visEquipment);
            }
        }
        catch (Exception exception)
        {
            FailedRagdollAccessoryVisuals.Add(visualId);
            CreatureManagerPlugin.Log.LogWarning(
                $"Ragdoll hair/beard appearance was disabled for '{visEquipment.name}' after attachment failed: {exception.Message}");
        }
    }

    private static void ApplyConfiguredRagdollColors(
        VisEquipment visEquipment,
        CreatureDomainManager.CreatureAppearanceRuntimeState appearance)
    {
        if (!appearance.SkinColor.HasValue && !appearance.HairColor.HasValue)
        {
            return;
        }

        int visualId = visEquipment.GetInstanceID();
        if (FailedRagdollColorVisuals.Contains(visualId))
        {
            return;
        }

        try
        {
            Renderer? bodyModel = visEquipment.m_bodyModel;
            if (bodyModel != null)
            {
                Material[] bodyMaterials = bodyModel.materials;
                if (appearance.SkinColor.HasValue && bodyMaterials.Length > 0)
                {
                    SetSupportedAppearanceColor(bodyMaterials[0], appearance.SkinColor.Value);
                }

                if (appearance.HairColor.HasValue && bodyMaterials.Length > 1)
                {
                    SetSupportedAppearanceColor(bodyMaterials[1], appearance.HairColor.Value);
                }
            }

            if (appearance.HairColor.HasValue)
            {
                ApplySupportedAppearanceColor(visEquipment.m_hairItemInstance, appearance.HairColor.Value);
                ApplySupportedAppearanceColor(visEquipment.m_beardItemInstance, appearance.HairColor.Value);
            }
        }
        catch (Exception exception)
        {
            FailedRagdollColorVisuals.Add(visualId);
            CreatureManagerPlugin.Log.LogWarning(
                $"Ragdoll color appearance was disabled for '{visEquipment.name}' after material property application failed: {exception.Message}");
        }
    }

    private static void ApplySupportedAppearanceColor(GameObject? itemInstance, Vector3 color)
    {
        if (itemInstance == null)
        {
            return;
        }

        foreach (Renderer renderer in itemInstance.GetComponentsInChildren<Renderer>(includeInactive: true))
        {
            foreach (Material material in renderer.materials)
            {
                SetSupportedAppearanceColor(material, color);
            }
        }
    }

    private static void SetSupportedAppearanceColor(Material? material, Vector3 color)
    {
        if (material == null)
        {
            return;
        }

        Color materialColor = Utils.Vec3ToColor(color);
        if (material.HasProperty("_SkinColor"))
        {
            material.SetColor("_SkinColor", materialColor);
        }
        else if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", materialColor);
        }
    }

    private static bool IsSupportedModelIndex(VisEquipment visEquipment, int modelIndex)
    {
        if (modelIndex < 0)
        {
            return false;
        }

        return visEquipment.m_models is { Length: > 0 }
            ? modelIndex < visEquipment.m_models.Length
            : modelIndex == 0;
    }

    private static void LogRagdollAppearanceDelegateWarning()
    {
        if (_ragdollAppearanceDelegateWarningLogged)
        {
            return;
        }

        _ragdollAppearanceDelegateWarningLogged = true;
        CreatureManagerPlugin.Log.LogWarning(
            "Some non-player ragdoll appearance fields cannot be rendered because this Valheim version does not expose the expected guarded VisEquipment update methods.");
    }

    internal static void RestoreEligibility(VisEquipment visEquipment)
    {
        if (visEquipment == null || GetStoredHairHash(visEquipment) == 0)
        {
            return;
        }

        Humanoid? humanoid = visEquipment.GetComponent<Humanoid>();
        if (humanoid != null)
        {
            if (!CreatureDomainManager.TryGetRandomHairPrefabs(humanoid, out IReadOnlyList<GameObject> prefabs) ||
                !prefabs.Any(prefab => prefab != null))
            {
                return;
            }
        }
        else if (visEquipment.GetComponent<Ragdoll>() == null)
        {
            return;
        }

        int visualId = visEquipment.GetInstanceID();
        ZDO? zdo = visEquipment.m_nview?.GetZDO();
        if (zdo != null && zdo.GetInt(RandomHairColorManagedKey, 0) != 0)
        {
            Vector3 configuredColor = zdo.GetVec3(ZDOVars.s_hairColor, visEquipment.m_hairColor);
            ConfiguredHairColors[visualId] = configuredColor;
            visEquipment.m_hairColor = configuredColor;
        }

        EligibleVisuals.Add(visualId);
    }

    internal static void Forget(VisEquipment visEquipment)
    {
        if (visEquipment == null)
        {
            return;
        }

        int visualId = visEquipment.GetInstanceID();
        EligibleVisuals.Remove(visualId);
        FailedVisuals.Remove(visualId);
        ClearAppliedVisual(visEquipment);
        ConfiguredHairColors.Remove(visualId);
        AppliedHairColors.Remove(visualId);
        FailedRagdollAccessoryVisuals.Remove(visualId);
        FailedRagdollColorVisuals.Remove(visualId);
        FailedRagdollModelVisuals.Remove(visualId);
    }

    internal static void Reset()
    {
        EligibleVisuals.Clear();
        AppliedVisuals.Clear();
        FailedVisuals.Clear();
        ConfiguredHairColors.Clear();
        AppliedHairColors.Clear();
        FailedRagdollAccessoryVisuals.Clear();
        FailedRagdollColorVisuals.Clear();
        FailedRagdollModelVisuals.Clear();
    }

    private static int GetStoredHairHash(VisEquipment visEquipment)
    {
        ZNetView? nview = visEquipment.m_nview;
        if (nview == null || !nview.IsValid())
        {
            return 0;
        }

        return nview.GetZDO()?.GetInt(RandomHairHashKey, 0) ?? 0;
    }

    private static void ClearAppliedVisual(VisEquipment visEquipment)
    {
        int visualId = visEquipment.GetInstanceID();
        AppliedHairColors.Remove(visualId);
        if (AppliedVisuals.Remove(visualId))
        {
            ApplyHairVisual(visEquipment, 0);
        }
    }

    private static bool ApplyHairVisual(VisEquipment visEquipment, int hairHash)
    {
        EnsureDelegates();
        if (_setHairEquipped == null || _visualRuntimeFailed)
        {
            return false;
        }

        try
        {
            bool changed = _setHairEquipped(visEquipment, hairHash);
            if (changed)
            {
                _updateLodgroup?.Invoke(visEquipment);
            }

            ApplyConfiguredHairMaterialColor(visEquipment, force: changed);
            return true;
        }
        catch (Exception exception)
        {
            int visualId = visEquipment.GetInstanceID();
            FailedVisuals.Add(visualId);
            EligibleVisuals.Remove(visualId);
            AppliedVisuals.Remove(visualId);
            AppliedHairColors.Remove(visualId);
            try
            {
                _setHairEquipped(visEquipment, 0);
                _updateLodgroup?.Invoke(visEquipment);
            }
            catch
            {
                // The visual is already isolated from future random-hair updates for this instance.
            }

            CreatureManagerPlugin.Log.LogWarning(
                $"Random hair visual '{hairHash}' was disabled for '{visEquipment.name}' after attachment failed: {exception.Message}");
            return false;
        }
    }

    private static void ApplyConfiguredHairMaterialColor(VisEquipment visEquipment, bool force)
    {
        if (ConfiguredHairColors.TryGetValue(visEquipment.GetInstanceID(), out Vector3 color))
        {
            ApplyHairMaterialColor(visEquipment, color, force);
        }
    }

    private static void ApplyHairMaterialColor(VisEquipment visEquipment, Vector3 color, bool force)
    {
        int visualId = visEquipment.GetInstanceID();
        if (!force && AppliedHairColors.TryGetValue(visualId, out Vector3 appliedColor) &&
            (appliedColor - color).sqrMagnitude <= 0.000001f)
        {
            return;
        }

        GameObject? hairInstance = visEquipment.m_hairItemInstance;
        if (hairInstance == null)
        {
            AppliedHairColors.Remove(visualId);
            return;
        }

        Color materialColor = Utils.Vec3ToColor(color);
        foreach (Renderer renderer in hairInstance.GetComponentsInChildren<Renderer>(includeInactive: true))
        {
            foreach (Material material in renderer.materials)
            {
                if (material == null)
                {
                    continue;
                }

                if (material.HasProperty("_SkinColor"))
                {
                    material.SetColor("_SkinColor", materialColor);
                }
                else if (material.HasProperty("_Color"))
                {
                    material.SetColor("_Color", materialColor);
                }
            }
        }

        AppliedHairColors[visualId] = color;
    }

    private static void EnsureDelegates()
    {
        if (_delegatesResolved)
        {
            return;
        }

        _delegatesResolved = true;
        try
        {
            MethodInfo? setHairMethod = AccessTools.DeclaredMethod(
                typeof(VisEquipment),
                SetHairEquippedMethodName,
                new[] { typeof(int) });
            MethodInfo? setBeardMethod = AccessTools.DeclaredMethod(
                typeof(VisEquipment),
                SetBeardEquippedMethodName,
                new[] { typeof(int) });
            MethodInfo? getHairItemMethod = AccessTools.DeclaredMethod(
                typeof(VisEquipment),
                GetHairItemMethodName,
                new[]
                {
                    typeof(ItemDrop.ItemData.HelmetHairType),
                    typeof(int),
                    typeof(ItemDrop.ItemData.AccessoryType)
                });
            MethodInfo? updateLodgroupMethod = AccessTools.DeclaredMethod(
                typeof(VisEquipment),
                UpdateLodgroupMethodName,
                Type.EmptyTypes);
            MethodInfo? updateBaseModelMethod = AccessTools.DeclaredMethod(
                typeof(VisEquipment),
                UpdateBaseModelMethodName,
                Type.EmptyTypes);

            if (setHairMethod != null)
            {
                _setHairEquipped = (SetHairEquippedDelegate)Delegate.CreateDelegate(
                    typeof(SetHairEquippedDelegate),
                    setHairMethod);
            }

            if (setBeardMethod != null)
            {
                _setBeardEquipped = (SetBeardEquippedDelegate)Delegate.CreateDelegate(
                    typeof(SetBeardEquippedDelegate),
                    setBeardMethod);
            }

            if (getHairItemMethod != null)
            {
                _getHairItem = (GetHairItemDelegate)Delegate.CreateDelegate(
                    typeof(GetHairItemDelegate),
                    getHairItemMethod);
            }

            if (updateLodgroupMethod != null)
            {
                _updateLodgroup = (UpdateLodgroupDelegate)Delegate.CreateDelegate(
                    typeof(UpdateLodgroupDelegate),
                    updateLodgroupMethod);
            }

            if (updateBaseModelMethod != null)
            {
                _updateBaseModel = (UpdateBaseModelDelegate)Delegate.CreateDelegate(
                    typeof(UpdateBaseModelDelegate),
                    updateBaseModelMethod);
            }

            if (_setHairEquipped == null || _updateLodgroup == null)
            {
                _visualRuntimeFailed = true;
                CreatureManagerPlugin.Log.LogWarning(
                    "Random hair visuals are unavailable because this Valheim version does not expose the expected " +
                    "VisEquipment.SetHairEquipped(int) and UpdateLodgroup() methods.");
            }
        }
        catch (Exception exception)
        {
            _visualRuntimeFailed = true;
            CreatureManagerPlugin.Log.LogWarning(
                $"Random hair visuals are unavailable because VisEquipment delegates could not be created: {exception.Message}");
        }
    }

    private static int SelectDeterministicIndex(int seed, int count)
    {
        uint value = unchecked((uint)seed) + 0x9E3779B9u;
        value = (value ^ (value >> 16)) * 0x7FEB352Du;
        value = (value ^ (value >> 15)) * 0x846CA68Bu;
        value ^= value >> 16;
        return (int)(value % (uint)count);
    }

    private static void RestoreStandardHair(Humanoid humanoid, VisEquipment? visEquipment)
    {
        if (visEquipment != null && visEquipment.m_isPlayer)
        {
            visEquipment.SetHairItem(humanoid.m_hairItem ?? "");
        }
    }
}
