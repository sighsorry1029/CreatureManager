using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace CreatureManager;

internal static class CreatureHarmonyTargetResolver
{
    internal static MethodInfo? FindDeclared(
        Type declaringType,
        string methodName,
        string feature,
        Type[]? argumentTypes = null,
        bool warnIfMissing = true)
    {
        if (argumentTypes == null)
        {
            return FindUniqueDeclared(declaringType, methodName, feature, warnIfMissing);
        }

        MethodInfo? method = AccessTools.DeclaredMethod(declaringType, methodName, argumentTypes);
        if (method == null && warnIfMissing)
        {
            string signature = $"({string.Join(", ", argumentTypes.Select(type => type.Name))})";
            CreatureManagerPlugin.Log.LogWarning(
                $"Could not find optional Harmony target {declaringType.FullName}.{methodName}{signature}; " +
                $"{feature} may be incomplete for this game version.");
        }

        return method;
    }

    internal static MethodInfo? FindUniqueDeclared(
        Type declaringType,
        string methodName,
        string feature,
        bool warnIfMissing = true)
    {
        MethodInfo[] matches = declaringType
            .GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                        BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Where(method => method.Name == methodName)
            .ToArray();
        if (matches.Length == 1)
        {
            return matches[0];
        }

        if (matches.Length == 0)
        {
            if (warnIfMissing)
            {
                CreatureManagerPlugin.Log.LogWarning(
                    $"Could not find optional Harmony target {declaringType.FullName}.{methodName}; " +
                    $"{feature} may be incomplete for this game version.");
            }

            return null;
        }

        string signatures = string.Join(
            ", ",
            matches.Select(method => $"({string.Join(", ", method.GetParameters().Select(parameter => parameter.ParameterType.Name))})"));
        CreatureManagerPlugin.Log.LogWarning(
            $"Found {matches.Length} optional Harmony targets named {declaringType.FullName}.{methodName} " +
            $"with signatures {signatures}; refusing to select an overload implicitly, so {feature} is disabled.");
        return null;
    }
}

[HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.Awake))]
internal static class CreatureManagerFejdStartupAwakePatch
{
    private static void Postfix(FejdStartup __instance)
    {
        CreaturePrefabRegistry.CacheFejdStartup(__instance);
    }
}

[HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake))]
internal static class CreatureManagerZNetSceneAwakePatch
{
    private static void Prefix(ZNetScene __instance)
    {
        CreaturePrefabRegistry.RegisterPendingPrefabs(__instance);
    }

    [HarmonyPriority(Priority.Last)]
    private static void Postfix(ZNetScene __instance)
    {
        CreaturePrefabRegistry.RegisterPendingPrefabs(__instance);
        CreatureDomainManager.NotifyGameDataAvailable();
    }
}

[HarmonyPatch(typeof(ZNet), nameof(ZNet.Awake))]
internal static class CreatureManagerZNetAwakePatch
{
    private static void Postfix()
    {
        CreatureKarmaManager.RegisterRpcs();
    }
}

[HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
internal static class CreatureManagerObjectDbAwakePatch
{
    private static void Prefix(ObjectDB __instance)
    {
        CreaturePrefabRegistry.RegisterPendingPrefabs(__instance);
    }

    [HarmonyPriority(Priority.Last)]
    private static void Postfix(ObjectDB __instance)
    {
        CreaturePrefabRegistry.RegisterPendingPrefabs(__instance);
        CreatureModifierManager.RegisterStatusEffects(__instance);
        CreatureDomainManager.NotifyGameDataAvailable();
    }
}

[HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
internal static class CreatureManagerObjectDbCopyOtherDbPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(ObjectDB __instance)
    {
        CreaturePrefabRegistry.RegisterPendingPrefabs(__instance);
        CreatureModifierManager.RegisterStatusEffects(__instance);
        CreatureDomainManager.NotifyGameDataAvailable();
    }
}

internal static class CreatureGameSettings
{
    internal static void ApplyAll()
    {
        ApplyDifficulty(Game.instance);
        ApplyNameplateRange(EnemyHud.instance);
    }

    internal static void ApplyDifficulty(Game? game)
    {
        if (game == null)
        {
            return;
        }

        game.m_healthScalePerPlayer = Mathf.Clamp(CreatureManagerPlugin.MultiplayerHealthIncreasePerPlayer.Value, 0f, 200f) / 100f;
        game.m_damageScalePerPlayer = Mathf.Clamp(CreatureManagerPlugin.MultiplayerDamageIncreasePerPlayer.Value, 0f, 200f) / 100f;
        game.m_difficultyScaleMaxPlayers = Mathf.Clamp(CreatureManagerPlugin.MultiplayerMaximumPlayerCount.Value, 1, 25);
    }

    internal static void ApplyNameplateRange(EnemyHud? enemyHud)
    {
        if (enemyHud == null)
        {
            return;
        }

        enemyHud.m_maxShowDistance = Mathf.Clamp(CreatureManagerPlugin.NormalCreatureNameplateRange.Value, 10f, 50f);
    }
}

[HarmonyPatch(typeof(Game), nameof(Game.Awake))]
internal static class CreatureManagerGameAwakePatch
{
    private static void Postfix(Game __instance)
    {
        CreatureGameSettings.ApplyDifficulty(__instance);
    }
}

[HarmonyPatch(typeof(EnemyHud), nameof(EnemyHud.Awake))]
internal static class CreatureManagerEnemyHudAwakePatch
{
    private static void Postfix(EnemyHud __instance)
    {
        CreatureGameSettings.ApplyNameplateRange(__instance);
    }
}

[HarmonyPatch(typeof(Minimap), nameof(Minimap.Awake))]
internal static class CreatureManagerMinimapAwakePatch
{
    private static void Postfix(Minimap __instance)
    {
        CreatureKarmaMinimapHud.Create(__instance);
    }
}

[HarmonyPatch(typeof(Minimap), "Update")]
internal static class CreatureManagerMinimapUpdatePatch
{
    private static void Postfix()
    {
        CreatureKarmaMinimapHud.Update();
    }
}

[HarmonyPatch(typeof(Minimap), "OnDestroy")]
internal static class CreatureManagerMinimapOnDestroyPatch
{
    private static void Postfix()
    {
        CreatureKarmaMinimapHud.Clear();
    }
}

[HarmonyPatch(typeof(TextsDialog), "UpdateTextsList")]
internal static class CreatureManagerTextsDialogUpdateTextsListPatch
{
    private static void Postfix(TextsDialog __instance)
    {
        CreatureCompendiumManager.AddModifierEntries(__instance);
    }
}

[HarmonyPatch(typeof(TextsDialog), nameof(TextsDialog.ShowText), new[] { typeof(TextsDialog.TextInfo) })]
internal static class CreatureManagerTextsDialogShowTextPatch
{
    private static void Postfix(TextsDialog __instance, TextsDialog.TextInfo text)
    {
        CreatureCompendiumManager.RefreshPageContentIcons(__instance, text);
    }
}

internal static class CreatureKarmaMinimapHud
{
    private const float RefreshInterval = 1f;
    private static Text? KarmaText;
    private static float NextRefreshTime;

    internal static void Create(Minimap minimap)
    {
        if (minimap == null || KarmaText != null)
        {
            return;
        }

        Transform? parent = null;
        if (minimap.m_smallRoot != null)
        {
            parent = minimap.m_smallRoot.transform;
        }
        else if (minimap.m_mapSmall != null && minimap.m_mapSmall.transform.parent != null)
        {
            parent = minimap.m_mapSmall.transform.parent;
        }
        else if (minimap.m_mapImageSmall != null)
        {
            parent = minimap.m_mapImageSmall.transform;
        }

        if (parent == null)
        {
            return;
        }

        try
        {
            GameObject textObject = new("CreatureManager_KarmaMinimapText", typeof(RectTransform), typeof(Text), typeof(Outline))
            {
                layer = LayerMask.NameToLayer("UI")
            };
            textObject.transform.SetParent(parent, false);

            RectTransform rect = (RectTransform)textObject.transform;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.sizeDelta = new Vector2(220f, 36f);
            rect.anchoredPosition = new Vector2(-8f, 8f);
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            rect.SetAsLastSibling();

            Text text = textObject.GetComponent<Text>();
            text.font = Resources.FindObjectsOfTypeAll<Font>()
                .FirstOrDefault(font => font != null && font.name == "AveriaSerifLibre-Bold")
                ?? Font.CreateDynamicFontFromOSFont("Arial", 14);
            text.fontSize = 14;
            text.alignment = TextAnchor.LowerRight;
            text.color = new Color(1f, 0.78f, 0.24f, 1f);
            text.raycastTarget = false;
            text.text = "";

            Outline outline = textObject.GetComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(1f, -1f);

            textObject.SetActive(false);
            KarmaText = text;
            NextRefreshTime = 0f;
        }
        catch (Exception ex)
        {
            CreatureManagerPlugin.Log.LogWarning($"Failed to create Karma minimap HUD: {ex.Message}");
        }
    }

    internal static void Update()
    {
        if (KarmaText == null || Time.time < NextRefreshTime)
        {
            return;
        }

        NextRefreshTime = Time.time + RefreshInterval;
        Player localPlayer = Player.m_localPlayer;
        if (localPlayer == null)
        {
            SetVisible(false);
            return;
        }

        string text = CreatureKarmaManager.GetMinimapStatus(localPlayer.transform.position);
        if (text.Length == 0)
        {
            SetVisible(false);
            return;
        }

        KarmaText.text = text;
        SetVisible(true);
    }

    internal static void Clear()
    {
        KarmaText = null;
        NextRefreshTime = 0f;
    }

    private static void SetVisible(bool visible)
    {
        if (KarmaText != null && KarmaText.gameObject.activeSelf != visible)
        {
            KarmaText.gameObject.SetActive(visible);
        }
    }
}

internal static class CreatureBossHudOnlyScope
{
    internal static bool Begin(Character? character)
    {
        if (character == null || character.m_boss || !CreatureKarmaManager.IsBossHudOnly(character))
        {
            return false;
        }

        character.m_boss = true;
        return true;
    }

    internal static void End(Character? character, bool changed)
    {
        if (changed && character != null)
        {
            character.m_boss = false;
        }
    }

    internal static List<Character>? Begin(EnemyHud? enemyHud)
    {
        if (enemyHud == null)
        {
            return null;
        }

        List<Character>? changed = null;
        foreach (Character character in enemyHud.m_huds.Keys)
        {
            if (Begin(character))
            {
                (changed ??= new List<Character>()).Add(character);
            }
        }

        return changed;
    }

    internal static void End(List<Character>? changed)
    {
        if (changed == null)
        {
            return;
        }

        foreach (Character character in changed)
        {
            End(character, true);
        }
    }
}

[HarmonyPatch(typeof(VisEquipment), "AttachItem")]
internal static class CreatureManagerVisEquipmentAttachItemPatch
{
    private static void Postfix(VisEquipment __instance, GameObject __result)
    {
        CreatureDomainManager.ScaleAttachedEquipmentVisual(__instance, __result);
    }
}

[HarmonyPatch(typeof(VisEquipment), "AttachArmor")]
internal static class CreatureManagerVisEquipmentAttachArmorPatch
{
    private static void Postfix(VisEquipment __instance, List<GameObject> __result)
    {
        CreatureDomainManager.ScaleAttachedEquipmentVisuals(__instance, __result);
    }
}

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
                Initialize(humanoid);
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

[HarmonyPatch]
internal static class CreatureManagerVisEquipmentUpdateEquipmentVisualsPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        MethodInfo? method = CreatureHarmonyTargetResolver.FindDeclared(
            typeof(VisEquipment),
            "UpdateEquipmentVisuals",
            "ragdoll appearance and random hair visual attachment",
            Type.EmptyTypes);
        if (method != null)
        {
            yield return method;
        }
    }

    [HarmonyPriority(Priority.Last)]
    private static void Postfix(VisEquipment __instance)
    {
        CreatureManagerRandomHairRuntime.UpdateConfiguredRagdollAppearance(__instance);
        CreatureDomainManager.ApplyConfiguredRagdollTextures(__instance);
        CreatureManagerRandomHairRuntime.RestoreEligibility(__instance);
        CreatureManagerRandomHairRuntime.UpdateVisual(__instance);
    }
}

[HarmonyPatch]
internal static class CreatureManagerVisEquipmentGetHairItemPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        MethodInfo? method = CreatureHarmonyTargetResolver.FindDeclared(
            typeof(VisEquipment),
            "GetHairItem",
            "random hair helmet-visibility override",
            new[]
            {
                typeof(ItemDrop.ItemData.HelmetHairType),
                typeof(int),
                typeof(ItemDrop.ItemData.AccessoryType)
            });
        if (method != null)
        {
            yield return method;
        }
    }

    private static bool Prefix(
        VisEquipment __instance,
        int itemHash,
        ItemDrop.ItemData.AccessoryType accessory,
        ref int __result)
    {
        if (!CreatureManagerRandomHairRuntime.TryBypassHelmetHairFilter(
                __instance,
                itemHash,
                accessory,
                out int result))
        {
            return true;
        }

        __result = result;
        return false;
    }
}

[HarmonyPatch(typeof(VisEquipment), "OnEnable")]
internal static class CreatureManagerVisEquipmentOnEnableRandomHairPatch
{
    private static void Postfix(VisEquipment __instance)
    {
        CreatureManagerRandomHairRuntime.UpdateConfiguredRagdollAppearance(__instance);
        CreatureDomainManager.ApplyConfiguredRagdollTextures(__instance);
        CreatureManagerRandomHairRuntime.RestoreEligibility(__instance);
    }
}

[HarmonyPatch(typeof(VisEquipment), "OnDisable")]
internal static class CreatureManagerVisEquipmentOnDisableRandomHairPatch
{
    private static void Prefix(VisEquipment __instance)
    {
        CreatureDomainManager.ForgetRagdollTextureVisual(__instance);
        CreatureManagerRandomHairRuntime.Forget(__instance);
    }
}

[HarmonyPatch(typeof(BaseAI), nameof(BaseAI.Awake))]
internal static class CreatureManagerBaseAiAwakePatch
{
    private static void Postfix(BaseAI __instance)
    {
        CreatureFactionManager.SetupBaseAi(__instance);
    }
}

internal static class CreatureManagerCharacterLifecycle
{
    internal static void ApplyLevelAndModifiers(Character character)
    {
        CreatureLevelManager.TryApplyLevel(character);
        CreatureLevelManager.ApplyRuntimeVisuals(character);
        CreatureModifierManager.TryRollModifiers(character);
    }
}

[HarmonyPatch(typeof(SpawnSystem), "Spawn")]
internal static class CreatureManagerSpawnSystemSpawnPatch
{
    private static void Prefix()
    {
        CreatureManagerSpawnLifecycle.BeginSpawnContext();
    }

    private static Exception? Finalizer(Exception? __exception)
    {
        CreatureManagerSpawnLifecycle.EndSpawnContext();
        return __exception;
    }
}

[HarmonyPatch(typeof(CreatureSpawner), "Spawn")]
internal static class CreatureManagerCreatureSpawnerSpawnPatch
{
    private static void Prefix()
    {
        CreatureManagerSpawnLifecycle.BeginSpawnContext();
    }

    private static Exception? Finalizer(Exception? __exception)
    {
        CreatureManagerSpawnLifecycle.EndSpawnContext();
        return __exception;
    }
}

[HarmonyPatch(typeof(SpawnArea), "SpawnOne")]
internal static class CreatureManagerSpawnAreaSpawnOnePatch
{
    private static void Prefix()
    {
        CreatureManagerSpawnLifecycle.BeginSpawnContext();
    }

    private static Exception? Finalizer(Exception? __exception)
    {
        CreatureManagerSpawnLifecycle.EndSpawnContext();
        return __exception;
    }
}

internal enum CreatureSpawnSourceKind
{
    Unknown = 0,
    Managed = 1,
    Command = 2,
    PlayerSummon = 3,
    Breeding = 4,
    Egg = 5,
    Growup = 6,
    TamedRestore = 7
}

[HarmonyPatch(typeof(Terminal), nameof(Terminal.TryRunCommand))]
internal static class CreatureManagerTerminalTryRunCommandPatch
{
    private static void Prefix(string text, out bool __state)
    {
        __state = CreatureManagerSpawnLifecycle.ShouldTrackCommandSpawn(text);
        if (__state)
        {
            CreatureManagerSpawnLifecycle.BeginCommandSpawnContext();
        }
    }

    private static Exception? Finalizer(Exception? __exception, bool __state)
    {
        if (__state)
        {
            CreatureManagerSpawnLifecycle.EndCommandSpawnContext();
        }

        return __exception;
    }
}

[HarmonyPatch(typeof(Terminal), nameof(Terminal.tabCycle))]
internal static class CreatureManagerTerminalTabCyclePatch
{
    private static void Prefix(Terminal __instance, ref string word, ref List<string> options)
    {
        CreatureConsoleCommands.AdjustSpawnAutocomplete(__instance, ref word, ref options);
    }
}

[HarmonyPatch(typeof(Terminal), nameof(Terminal.updateSearch))]
internal static class CreatureManagerTerminalUpdateSearchPatch
{
    private static void Prefix(Terminal __instance, ref string word, ref List<string> options)
    {
        CreatureConsoleCommands.AdjustSpawnAutocomplete(__instance, ref word, ref options);
    }
}

[HarmonyPatch(typeof(ZNet), nameof(ZNet.RPC_RemoteCommand))]
internal static class CreatureManagerZNetRemoteCommandPatch
{
    private static bool Prefix(ZNet __instance, ZRpc rpc, string command)
    {
        return !CreatureConsoleCommands.TryHandleRemoteAdminCommand(__instance, rpc, command);
    }
}

[HarmonyPatch(typeof(Character), nameof(Character.Awake))]
internal static class CreatureManagerCharacterAwakePatch
{
    private static void Postfix(Character __instance)
    {
        CreatureModifierManager.RegisterCharacterRpcs(__instance);
        CreatureModifierManager.RefreshStoredReapingScale(__instance);
        CreatureManagerSpawnLifecycle.RecordAwake(__instance);
    }
}

[HarmonyPatch(typeof(SpawnAbility), "Spawn")]
internal static class CreatureManagerSpawnAbilitySpawnPatch
{
    private static void Postfix(SpawnAbility __instance, ref IEnumerator __result)
    {
        if (__result != null && CreatureManagerSpawnLifecycle.IsBloodMagicItem(__instance.m_weapon))
        {
            __result = CreatureManagerSpawnLifecycle.WrapSourceContext(__result, CreatureSpawnSourceKind.PlayerSummon);
        }
    }
}

[HarmonyPatch(typeof(Projectile), nameof(Projectile.SpawnOnHit))]
internal static class CreatureManagerProjectileSpawnOnHitPatch
{
    private static void Prefix(Projectile __instance, out bool __state)
    {
        __state = CreatureManagerSpawnLifecycle.IsBloodMagicItem(__instance.m_weapon);
        if (__state)
        {
            CreatureManagerSpawnLifecycle.BeginSourceContext(CreatureSpawnSourceKind.PlayerSummon);
        }
    }

    private static Exception? Finalizer(Exception? __exception, bool __state)
    {
        if (__state)
        {
            CreatureManagerSpawnLifecycle.EndSourceContext();
        }

        return __exception;
    }
}

internal static class CreatureManagerFeedLikeGrandmaPokeballReleasePatch
{
    private static bool _patched;
    private static bool _lateAttempted;

    internal static void ApplyIfAvailable(Harmony harmony, bool lateAttempt = false)
    {
        if (_patched || lateAttempt && _lateAttempted)
        {
            return;
        }

        Type? type = FindType("FeedLikeGrandma.PokeballSystem");
        MethodInfo? method = type == null
            ? null
            : CreatureHarmonyTargetResolver.FindUniqueDeclared(
                type,
                "CompleteRelease",
                "Feed Like Grandma restored-creature tracking",
                warnIfMissing: false);
        if (method == null)
        {
            if (lateAttempt)
            {
                _lateAttempted = true;
            }

            return;
        }

        harmony.Patch(
            method,
            prefix: new HarmonyMethod(typeof(CreatureManagerFeedLikeGrandmaPokeballReleasePatch), nameof(Prefix)),
            finalizer: new HarmonyMethod(typeof(CreatureManagerFeedLikeGrandmaPokeballReleasePatch), nameof(Finalizer)));
        _patched = true;
    }

    internal static void Reset()
    {
        _patched = false;
        _lateAttempted = false;
    }

    private static Type? FindType(string fullName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type? type = assembly.GetType(fullName, throwOnError: false);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }

    private static void Prefix()
    {
        CreatureManagerSpawnLifecycle.BeginSourceContext(CreatureSpawnSourceKind.TamedRestore);
    }

    private static Exception? Finalizer(Exception? __exception)
    {
        CreatureManagerSpawnLifecycle.EndSourceContext();
        return __exception;
    }
}

internal static class CreatureManagerSpawnLifecycle
{
    private const string SpawnSourceKey = "CreatureManager_SpawnSource";
    private static readonly List<Character> SpawnedCharacters = new();
    private static readonly HashSet<int> ManagedSpawnCharacterIds = new();
    private static readonly HashSet<int> CommandSpawnCharacterIds = new();
    private static int SpawnContextDepth;
    private static readonly Stack<CreatureSpawnSourceKind> SourceContexts = new();

    internal static bool ShouldTrackCommandSpawn(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string command = text.TrimStart().Split(new[] { ' ' }, StringSplitOptions.None)[0].ToLowerInvariant();
        return command.StartsWith("spawn", StringComparison.Ordinal);
    }

    internal static void BeginSpawnContext()
    {
        if (SpawnContextDepth == 0)
        {
            SpawnedCharacters.Clear();
        }

        SpawnContextDepth++;
    }

    internal static void BeginCommandSpawnContext()
    {
        BeginSourceContext(CreatureSpawnSourceKind.Command);
    }

    internal static void EndCommandSpawnContext()
    {
        EndSourceContext();
    }

    internal static void BeginSourceContext(CreatureSpawnSourceKind source)
    {
        SourceContexts.Push(source);
    }

    internal static void EndSourceContext()
    {
        if (SourceContexts.Count > 0)
        {
            SourceContexts.Pop();
        }
    }

    internal static void EndSpawnContext()
    {
        if (SpawnContextDepth <= 0)
        {
            return;
        }

        SpawnContextDepth--;
        if (SpawnContextDepth > 0)
        {
            return;
        }

        try
        {
            foreach (Character character in SpawnedCharacters.Where(character => character != null).Distinct())
            {
                CreatureManagerCharacterLifecycle.ApplyLevelAndModifiers(character);
            }
        }
        finally
        {
            SpawnedCharacters.Clear();
        }
    }

    internal static void RecordAwake(Character character)
    {
        if (character == null)
        {
            return;
        }

        if (SourceContexts.Count > 0)
        {
            MarkSpawnSource(character, SourceContexts.Peek());
            return;
        }

        if (SpawnContextDepth <= 0)
        {
            return;
        }

        SpawnedCharacters.Add(character);
        ManagedSpawnCharacterIds.Add(character.GetInstanceID());
    }

    internal static bool IsManagedSpawn(Character character)
    {
        return character != null && ManagedSpawnCharacterIds.Contains(character.GetInstanceID());
    }

    internal static bool IsCommandSpawn(Character character)
    {
        return GetSpawnSource(character) == CreatureSpawnSourceKind.Command;
    }

    internal static CreatureSpawnSourceKind GetSpawnSource(Character character)
    {
        if (character == null)
        {
            return CreatureSpawnSourceKind.Unknown;
        }

        if (CommandSpawnCharacterIds.Contains(character.GetInstanceID()))
        {
            return CreatureSpawnSourceKind.Command;
        }

        ZNetView? nview = character.m_nview;
        if (nview != null && nview.IsValid())
        {
            ZDO zdo = nview.GetZDO();
            if (zdo != null)
            {
                CreatureSpawnSourceKind stored = GetSpawnSource(zdo);
                if (stored != CreatureSpawnSourceKind.Unknown)
                {
                    return stored;
                }
            }
        }

        if (IsManagedSpawn(character))
        {
            return CreatureSpawnSourceKind.Managed;
        }

        return character.IsTamed()
            ? CreatureSpawnSourceKind.TamedRestore
            : CreatureSpawnSourceKind.Unknown;
    }

    internal static CreatureSpawnSourceKind GetSpawnSource(ZDO zdo)
    {
        if (zdo == null)
        {
            return CreatureSpawnSourceKind.Unknown;
        }

        int stored = zdo.GetInt(SpawnSourceKey, 0);
        return stored != 0 && Enum.IsDefined(typeof(CreatureSpawnSourceKind), stored)
            ? (CreatureSpawnSourceKind)stored
            : CreatureSpawnSourceKind.Unknown;
    }

    internal static IEnumerator WrapSourceContext(IEnumerator inner, CreatureSpawnSourceKind source)
    {
        while (true)
        {
            object current;
            BeginSourceContext(source);
            try
            {
                if (!inner.MoveNext())
                {
                    yield break;
                }

                current = inner.Current;
            }
            finally
            {
                EndSourceContext();
            }

            yield return current;
        }
    }

    internal static bool IsBloodMagicItem(ItemDrop.ItemData? item)
    {
        try
        {
            return item?.m_shared != null && item.m_shared.m_skillType == Skills.SkillType.BloodMagic;
        }
        catch
        {
            return false;
        }
    }

    internal static void ForgetCharacter(Character character)
    {
        if (character == null)
        {
            return;
        }

        int id = character.GetInstanceID();
        ManagedSpawnCharacterIds.Remove(id);
        CommandSpawnCharacterIds.Remove(id);
        SpawnedCharacters.RemoveAll(candidate => candidate == null || candidate == character);
    }

    internal static void ResetRuntimeState()
    {
        SpawnedCharacters.Clear();
        ManagedSpawnCharacterIds.Clear();
        CommandSpawnCharacterIds.Clear();
        SourceContexts.Clear();
        SpawnContextDepth = 0;
    }

    private static void MarkSpawnSource(Character character, CreatureSpawnSourceKind source)
    {
        if (source == CreatureSpawnSourceKind.Command)
        {
            CommandSpawnCharacterIds.Add(character.GetInstanceID());
        }

        ZNetView? nview = character.m_nview;
        if (nview == null || !nview.IsValid())
        {
            return;
        }

        ZDO zdo = nview.GetZDO();
        if (zdo == null)
        {
            return;
        }

        zdo.Set(SpawnSourceKey, (int)source);
    }
}

[HarmonyPatch(typeof(Character), "OnDestroy")]
internal static class CreatureManagerCharacterOnDestroyPatch
{
    private static void Prefix(Character __instance)
    {
        CreatureManagerSpawnLifecycle.ForgetCharacter(__instance);
        CreatureKarmaManager.ForgetCharacter(__instance);
        CreatureModifierManager.ForgetCharacter(__instance);
    }
}

[HarmonyPatch(typeof(ZNetScene), "OnDestroy")]
internal static class CreatureManagerZNetSceneOnDestroyPatch
{
    private static void Prefix()
    {
        CreatureDomainManager.NotifyGameDataUnavailable();
        CreatureManagerRandomHairRuntime.Reset();
        CreatureManagerSpawnLifecycle.ResetRuntimeState();
        CreatureKarmaManager.ResetRuntimeState();
        CreatureModifierManager.ResetRuntimeState();
        CreatureLevelManager.ResetRuntimeState();
    }
}

[HarmonyPatch(typeof(Character), nameof(Character.Start))]
internal static class CreatureManagerCharacterStartPatch
{
    private static void Postfix(Character __instance)
    {
        CreatureKarmaManager.RefreshStoredEnforcerLoot(__instance);
        CreatureManagerCharacterLifecycle.ApplyLevelAndModifiers(__instance);
    }
}

[HarmonyPatch(typeof(Humanoid), nameof(Humanoid.Start))]
internal static class CreatureManagerHumanoidStartPatch
{
    private static void Postfix(Humanoid __instance)
    {
        CreatureManagerRandomHairRuntime.Initialize(__instance);
        CreatureManagerCharacterLifecycle.ApplyLevelAndModifiers(__instance);
    }
}

[HarmonyPatch(typeof(Humanoid), "OnRagdollCreated")]
internal static class CreatureManagerHumanoidOnRagdollCreatedRandomHairPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(Humanoid __instance, Ragdoll ragdoll)
    {
        CreatureManagerRandomHairRuntime.ApplyToRagdoll(__instance, ragdoll);
    }
}

[HarmonyPatch(typeof(Ragdoll), nameof(Ragdoll.Setup))]
internal static class CreatureManagerRagdollSetupVisualScalePatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(Ragdoll __instance)
    {
        CreatureDomainManager.ApplyConfiguredRagdollScale(__instance);
    }
}

[HarmonyPatch(typeof(Growup), "GrowUpdate")]
internal static class CreatureManagerGrowupGrowUpdatePatch
{
    private static void Prefix(Growup __instance, out bool __state)
    {
        __state = ShouldTrackExplicitLevel(__instance);
        if (__state)
        {
            CreatureManagerSpawnLifecycle.BeginSourceContext(CreatureSpawnSourceKind.Growup);
            CreatureLevelManager.BeginExplicitExternalLevelContext("growup", __instance.GetComponent<Character>());
        }
    }

    private static Exception? Finalizer(Exception? __exception, bool __state)
    {
        if (__state)
        {
            CreatureLevelManager.EndExplicitExternalLevelContext();
            CreatureManagerSpawnLifecycle.EndSourceContext();
        }

        return __exception;
    }

    private static bool ShouldTrackExplicitLevel(Growup growup)
    {
        if (growup == null || growup.m_nview == null || !growup.m_nview.IsValid() || !growup.m_nview.IsOwner())
        {
            return false;
        }

        return growup.m_baseAI != null &&
               growup.m_baseAI.GetTimeSinceSpawned().TotalSeconds > growup.m_growTime;
    }
}

[HarmonyPatch(typeof(EggGrow), "GrowUpdate")]
internal static class CreatureManagerEggGrowUpdatePatch
{
    private static void Prefix(EggGrow __instance, out bool __state)
    {
        __state = ShouldTrackExplicitLevel(__instance);
        if (__state)
        {
            CreatureManagerSpawnLifecycle.BeginSourceContext(CreatureSpawnSourceKind.Egg);
            CreatureLevelManager.BeginExplicitExternalLevelContext("egggrow");
        }
    }

    private static Exception? Finalizer(Exception? __exception, bool __state)
    {
        if (__state)
        {
            CreatureLevelManager.EndExplicitExternalLevelContext();
            CreatureManagerSpawnLifecycle.EndSourceContext();
        }

        return __exception;
    }

    private static bool ShouldTrackExplicitLevel(EggGrow eggGrow)
    {
        if (eggGrow == null || eggGrow.m_nview == null || !eggGrow.m_nview.IsValid() || !eggGrow.m_nview.IsOwner())
        {
            return false;
        }

        if (eggGrow.m_item == null || eggGrow.m_item.m_itemData.m_stack > 1 || ZNet.instance == null)
        {
            return false;
        }

        float growStart = eggGrow.m_nview.GetZDO().GetFloat(ZDOVars.s_growStart);
        return growStart > 0f && ZNet.instance.GetTimeSeconds() > growStart + eggGrow.m_growTime;
    }
}

[HarmonyPatch(typeof(Procreation), "Procreate")]
internal static class CreatureManagerProcreationProcreatePatch
{
    private static void Prefix(Procreation __instance, out bool __state)
    {
        __state = ShouldTrackExplicitLevel(__instance);
        if (__state)
        {
            CreatureManagerSpawnLifecycle.BeginSourceContext(CreatureSpawnSourceKind.Breeding);
            CreatureLevelManager.BeginExplicitExternalLevelContext("procreation");
        }
    }

    private static Exception? Finalizer(Exception? __exception, bool __state)
    {
        if (__state)
        {
            CreatureLevelManager.EndExplicitExternalLevelContext();
            CreatureManagerSpawnLifecycle.EndSourceContext();
        }

        return __exception;
    }

    private static bool ShouldTrackExplicitLevel(Procreation procreation)
    {
        if (procreation == null || procreation.m_nview == null || !procreation.m_nview.IsValid() || !procreation.m_nview.IsOwner())
        {
            return false;
        }

        return procreation.m_tameable != null &&
               procreation.m_tameable.IsTamed() &&
               procreation.IsPregnant() &&
               procreation.IsDue();
    }
}

[HarmonyPatch(typeof(Character), nameof(Character.SetLevel))]
internal static class CreatureManagerCharacterSetLevelPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(Character __instance, int level)
    {
        if (CreatureManagerSpawnLifecycle.IsCommandSpawn(__instance))
        {
            return;
        }

        if (!CreatureLevelManager.TryAdoptContextualExternalLevel(__instance, level))
        {
            CreatureLevelManager.RestoreConfiguredLevel(__instance, level);
        }

        CreatureLevelManager.ApplyRuntimeVisuals(__instance);
        if (CreatureLevelManager.HasManagedLevel(__instance))
        {
            CreatureModifierManager.TryRollModifiers(__instance);
        }

        CreatureModifierManager.RefreshStoredReapingScale(__instance);
    }
}

[HarmonyPatch(typeof(LevelEffects), "SetupLevelVisualization")]
internal static class CreatureManagerLevelEffectsSetupLevelVisualizationPatch
{
    private static bool Prefix(LevelEffects __instance, int level)
    {
        return !CreatureLevelManager.TryApplyRotatedLevelEffects(__instance, level);
    }
}

[HarmonyPatch]
internal static class CreatureManagerUndodgeableAttackScopePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (string methodName in new[]
                 {
                     "DoMeleeAttack",
                     "DoAreaAttack",
                     "ProjectileAttackTriggered"
                 })
        {
            MethodInfo? method = CreatureHarmonyTargetResolver.FindDeclared(
                typeof(Attack),
                methodName,
                "Undodgeable attack handling",
                Type.EmptyTypes);
            if (method != null)
            {
                yield return method;
            }
        }
    }

    [HarmonyPriority(Priority.Last)]
    private static void Prefix(
        Attack __instance,
        MethodBase __originalMethod,
        out CreatureModifierManager.UndodgeableScopeState __state)
    {
        CreatureModifierManager.UndodgeableSourcePath sourcePath = __originalMethod.Name switch
        {
            "DoMeleeAttack" => CreatureModifierManager.UndodgeableSourcePath.Melee,
            "DoAreaAttack" => CreatureModifierManager.UndodgeableSourcePath.Area,
            _ => CreatureModifierManager.UndodgeableSourcePath.None
        };
        __state = CreatureModifierManager.BeginUndodgeableAttackScope(__instance, sourcePath);
    }

    [HarmonyPriority(Priority.First)]
    private static Exception? Finalizer(
        Exception? __exception,
        ref CreatureModifierManager.UndodgeableScopeState __state)
    {
        CreatureModifierManager.EndUndodgeableScope(ref __state);
        return __exception;
    }
}

[HarmonyPatch]
internal static class CreatureManagerUndodgeableProjectileScopePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        MethodInfo? method = CreatureHarmonyTargetResolver.FindDeclared(
            typeof(Projectile),
            "IsValidTarget",
            "Undodgeable projectile handling",
            new[] { typeof(IDestructible) });
        if (method != null)
        {
            yield return method;
        }
    }

    [HarmonyPriority(Priority.Last)]
    private static void Prefix(
        Projectile __instance,
        IDestructible destr,
        out CreatureModifierManager.UndodgeableScopeState __state)
    {
        __state = CreatureModifierManager.BeginUndodgeableProjectileScope(__instance, destr);
    }

    [HarmonyPriority(Priority.First)]
    private static Exception? Finalizer(
        Exception? __exception,
        ref CreatureModifierManager.UndodgeableScopeState __state)
    {
        CreatureModifierManager.EndUndodgeableScope(ref __state);
        return __exception;
    }
}

[HarmonyPatch(
    typeof(Projectile),
    nameof(Projectile.OnHit),
    new[] { typeof(Collider), typeof(Vector3), typeof(bool), typeof(Vector3) })]
internal static class CreatureManagerVortexProjectileImpactScopePatch
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(
        Projectile __instance,
        Collider collider,
        bool water,
        out CreatureModifierManager.VortexProjectileImpactScopeState __state)
    {
        __state = CreatureModifierManager.BeginVortexProjectileImpact(__instance, collider, water);
    }

    [HarmonyPriority(Priority.Last)]
    private static Exception? Finalizer(
        Exception? __exception,
        ref CreatureModifierManager.VortexProjectileImpactScopeState __state)
    {
        CreatureModifierManager.EndVortexProjectileImpact(ref __state);
        return __exception;
    }
}

[HarmonyPatch]
internal static class CreatureManagerUndodgeableAoeScopePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        MethodInfo? method = CreatureHarmonyTargetResolver.FindDeclared(
            typeof(Aoe),
            "ShouldHit",
            "Undodgeable area-attack handling",
            new[] { typeof(Collider) });
        if (method != null)
        {
            yield return method;
        }
    }

    [HarmonyPriority(Priority.Last)]
    private static void Prefix(
        Aoe __instance,
        Collider collider,
        out CreatureModifierManager.UndodgeableScopeState __state)
    {
        __state = CreatureModifierManager.BeginUndodgeableAoeScope(__instance, collider);
    }

    [HarmonyPriority(Priority.First)]
    private static Exception? Finalizer(
        Exception? __exception,
        ref CreatureModifierManager.UndodgeableScopeState __state)
    {
        CreatureModifierManager.EndUndodgeableScope(ref __state);
        return __exception;
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.IsDodgeInvincible))]
internal static class CreatureManagerUndodgeableDodgeQueryPatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix(Player __instance, ref bool __result)
    {
        CreatureModifierManager.ApplyUndodgeableDodgeOverride(__instance, ref __result);
    }
}

[HarmonyPatch(typeof(Character), nameof(Character.Damage), new[] { typeof(HitData) })]
internal static class CreatureManagerUndodgeableDamageScopePatch
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(out CreatureModifierManager.UndodgeableDamageScopeState __state)
    {
        __state = CreatureModifierManager.BeginUndodgeableDamageScope();
    }

    [HarmonyPriority(Priority.Last)]
    private static Exception? Finalizer(
        Exception? __exception,
        ref CreatureModifierManager.UndodgeableDamageScopeState __state)
    {
        CreatureModifierManager.EndUndodgeableDamageScope(ref __state);
        return __exception;
    }
}

[HarmonyPatch(typeof(Character), nameof(Character.Damage), new[] { typeof(HitData) })]
internal static class CreatureManagerVortexDirectProjectileDamagePatch
{
    [HarmonyPriority(Priority.Last)]
    private static void Prefix(
        Character __instance,
        HitData hit,
        out CreatureModifierManager.VortexDirectProjectileDamageState __state)
    {
        __state = CreatureModifierManager.PrepareVortexDirectProjectileDamage(__instance, hit);
    }

    [HarmonyPriority(Priority.First)]
    private static Exception? Finalizer(
        Exception? __exception,
        ref CreatureModifierManager.VortexDirectProjectileDamageState __state)
    {
        CreatureModifierManager.RestoreVortexDirectProjectileDamage(ref __state);
        return __exception;
    }
}

[HarmonyPatch]
internal static class CreatureManagerUndodgeableDamageDispatchPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        MethodInfo? method = CreatureHarmonyTargetResolver.FindDeclared(
            typeof(Character),
            nameof(Character.Damage),
            "Undodgeable damage dispatch",
            new[] { typeof(HitData) });
        if (method != null)
        {
            yield return method;
        }
    }

    [HarmonyPriority(Priority.Last)]
    private static void Prefix(Character __instance, HitData hit)
    {
        CreatureModifierManager.ApplyUndodgeableBeforeDamage(__instance, hit);
    }
}

[HarmonyPatch(typeof(Character), "RPC_Damage")]
internal static class CreatureManagerVortexRpcDamageMarkerPatch
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix(HitData hit)
    {
        CreatureModifierManager.CaptureVortexProjectileDecision(hit);
    }
}

[HarmonyPatch(typeof(Character), "RPC_Damage")]
internal static class CreatureManagerCharacterRpcDamagePatch
{
    private static bool Prefix(
        Character __instance,
        ref HitData hit,
        out CreatureModifierManager.RpcDamageScopeState __state)
    {
        __state = CreatureModifierManager.BeginRpcDamageScope(__instance, hit);
        return CreatureModifierManager.ApplyDamageModifiers(__instance, hit);
    }

    [HarmonyPriority(Priority.Last)]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> codes = instructions.ToList();
        FieldInfo? damageField = AccessTools.Field(typeof(HitData), nameof(HitData.m_damage));
        FieldInfo? poisonField = AccessTools.Field(typeof(HitData.DamageTypes), nameof(HitData.DamageTypes.m_poison));
        MethodInfo? capture = AccessTools.Method(
            typeof(CreatureModifierManager),
            nameof(CreatureModifierManager.CapturePostMitigationDelayedDamage));
        if (damageField == null || poisonField == null || capture == null)
        {
            CreatureManagerPlugin.Log.LogError(
                "Failed to resolve delayed-damage capture members; pure poison/fire/spirit modifier support is disabled.");
            return codes;
        }

        List<int> matches = new();
        for (int index = 0; index <= codes.Count - 4; index++)
        {
            if (codes[index].opcode == OpCodes.Ldarg_2 &&
                codes[index + 1].opcode == OpCodes.Ldflda &&
                Equals(codes[index + 1].operand, damageField) &&
                codes[index + 2].opcode == OpCodes.Ldc_R4 &&
                codes[index + 2].operand is float zero &&
                zero == 0f &&
                codes[index + 3].opcode == OpCodes.Stfld &&
                Equals(codes[index + 3].operand, poisonField))
            {
                matches.Add(index);
            }
        }

        if (matches.Count != 1)
        {
            CreatureManagerPlugin.Log.LogError(
                $"Expected one delayed-damage extraction point in Character.RPC_Damage, found {matches.Count}; pure poison/fire/spirit modifier support is disabled.");
            return codes;
        }

        int insertionIndex = matches[0];
        CodeInstruction loadTarget = new(OpCodes.Ldarg_0);
        loadTarget.labels.AddRange(codes[insertionIndex].labels);
        loadTarget.blocks.AddRange(codes[insertionIndex].blocks);
        codes[insertionIndex].labels.Clear();
        codes[insertionIndex].blocks.Clear();
        codes.InsertRange(
            insertionIndex,
            new[]
            {
                loadTarget,
                new CodeInstruction(OpCodes.Ldarg_2),
                new CodeInstruction(OpCodes.Call, capture)
            });
        return codes;
    }

    private static Exception? Finalizer(
        HitData hit,
        CreatureModifierManager.RpcDamageScopeState __state,
        Exception? __exception)
    {
        try
        {
            CreatureModifierManager.EndRpcDamageScope(__state);
        }
        finally
        {
            CreatureModifierManager.ClearTransientDamageContext(hit);
        }

        return __exception;
    }
}

[HarmonyPatch]
internal static class CreatureManagerCharacterApplyPushbackPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        MethodInfo? method = CreatureHarmonyTargetResolver.FindDeclared(
            typeof(Character),
            "ApplyPushback",
            "Juggernaut cooldown confirmation",
            new[] { typeof(HitData) });
        if (method != null)
        {
            yield return method;
        }
    }

    private static void Postfix(Character __instance, HitData hit)
    {
        CreatureModifierManager.ConfirmKnockbackPush(__instance, hit);
    }
}

[HarmonyPatch]
internal static class CreatureManagerHumanoidBlockStaggerScopePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        MethodInfo? method = CreatureHarmonyTargetResolver.FindDeclared(
            typeof(Humanoid),
            "BlockAttack",
            "Staggering block handling",
            new[] { typeof(HitData), typeof(Character) });
        if (method != null)
        {
            yield return method;
        }
    }

    private static void Prefix(Character attacker, out CreatureModifierManager.BlockAttackModifierState __state)
    {
        __state = CreatureModifierManager.BeginBlockAttackModifierScope(attacker);
    }

    private static Exception? Finalizer(Exception? __exception, CreatureModifierManager.BlockAttackModifierState __state)
    {
        CreatureModifierManager.EndBlockAttackModifierScope(__state);
        return __exception;
    }
}

[HarmonyPatch]
internal static class CreatureManagerCharacterBlockStaggerDamagePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        MethodInfo? method = CreatureHarmonyTargetResolver.FindDeclared(
            typeof(Character),
            "AddStaggerDamage",
            "Staggering block buildup",
            new[] { typeof(float), typeof(Vector3), typeof(HitData) });
        if (method != null)
        {
            yield return method;
        }
    }

    private static void Prefix(ref float damage)
    {
        CreatureModifierManager.ApplyBlockStaggerBonus(ref damage);
    }
}

[HarmonyPatch(
    typeof(Character),
    nameof(Character.ApplyDamage),
    new[] { typeof(HitData), typeof(bool), typeof(bool), typeof(HitData.DamageModifier) })]
internal static class CreatureManagerCharacterApplyDamagePatch
{
    private static void Prefix(Character __instance, HitData hit, out CreatureModifierManager.ApplyDamageState __state)
    {
        __state = CreatureModifierManager.BeginApplyDamage(__instance, hit);
    }

    [HarmonyPriority(Priority.Last)]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> codes = instructions.ToList();
        MethodInfo? getTotalDamage = AccessTools.Method(
            typeof(HitData),
            nameof(HitData.GetTotalDamage),
            Type.EmptyTypes);
        MethodInfo? resolveDeathward = AccessTools.Method(
            typeof(CreatureModifierManager),
            nameof(CreatureModifierManager.ResolveFinalDeathwardDamage),
            new[] { typeof(Character), typeof(HitData), typeof(float) });
        if (getTotalDamage == null || resolveDeathward == null)
        {
            CreatureManagerPlugin.Log.LogError(
                "Failed to resolve final-damage Deathward members; Deathward lethal prevention is disabled.");
            return codes;
        }

        List<int> matches = new();
        for (int index = 0; index <= codes.Count - 5; index++)
        {
            if (codes[index].opcode == OpCodes.Ldarg_1 &&
                codes[index + 1].Calls(getTotalDamage) &&
                codes[index + 2].opcode == OpCodes.Stloc_1 &&
                codes[index + 3].opcode == OpCodes.Ldloc_1 &&
                codes[index + 4].opcode == OpCodes.Ldc_R4 &&
                codes[index + 4].operand is float threshold &&
                threshold == 0.1f)
            {
                matches.Add(index + 3);
            }
        }

        if (matches.Count != 1)
        {
            CreatureManagerPlugin.Log.LogError(
                $"Expected one final-damage checkpoint in Character.ApplyDamage, found {matches.Count}; Deathward lethal prevention is disabled.");
            return codes;
        }

        int insertionIndex = matches[0];
        CodeInstruction loadTarget = new(OpCodes.Ldarg_0);
        loadTarget.labels.AddRange(codes[insertionIndex].labels);
        loadTarget.blocks.AddRange(codes[insertionIndex].blocks);
        codes[insertionIndex].labels.Clear();
        codes[insertionIndex].blocks.Clear();
        codes.InsertRange(
            insertionIndex,
            new[]
            {
                loadTarget,
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Ldloc_1),
                new CodeInstruction(OpCodes.Call, resolveDeathward),
                new CodeInstruction(OpCodes.Stloc_1)
            });
        return codes;
    }

    private static void Postfix(Character __instance, HitData hit, CreatureModifierManager.ApplyDamageState __state)
    {
        CreatureModifierManager.CompleteApplyDamage(__instance, hit, __state);
    }

    private static Exception? Finalizer(HitData hit, Exception? __exception)
    {
        if (__exception != null)
        {
            CreatureModifierManager.ClearTransientDamageContext(hit);
        }

        return __exception;
    }
}

[HarmonyPatch(typeof(SE_Poison), nameof(SE_Poison.AddDamage), new[] { typeof(float) })]
internal static class CreatureManagerPoisonSourcePatch
{
    private static void Prefix(SE_Poison __instance, float damage, out bool __state)
    {
        __state = damage >= __instance.m_damageLeft;
    }

    private static void Postfix(SE_Poison __instance, bool __state)
    {
        CreatureModifierManager.RecordPoisonDamageSource(__instance, __state);
    }
}

[HarmonyPatch(typeof(SE_Burning), nameof(SE_Burning.AddFireDamage), new[] { typeof(float) })]
internal static class CreatureManagerFireSourcePatch
{
    private static void Prefix(SE_Burning __instance, out bool __state)
    {
        __state = __instance.m_fireDamageLeft <= 0f;
    }

    private static void Postfix(SE_Burning __instance, bool __state, bool __result)
    {
        CreatureModifierManager.RecordFireDamageSource(__instance, __state, __result);
    }
}

[HarmonyPatch(typeof(SE_Burning), nameof(SE_Burning.AddSpiritDamage), new[] { typeof(float) })]
internal static class CreatureManagerSpiritSourcePatch
{
    private static void Prefix(SE_Burning __instance, out bool __state)
    {
        __state = __instance.m_spiritDamageLeft <= 0f;
    }

    private static void Postfix(SE_Burning __instance, bool __state, bool __result)
    {
        CreatureModifierManager.RecordSpiritDamageSource(__instance, __state, __result);
    }
}

[HarmonyPatch(typeof(SE_Poison), nameof(SE_Poison.UpdateStatusEffect), new[] { typeof(float) })]
internal static class CreatureManagerPoisonTickScopePatch
{
    private static void Prefix(
        SE_Poison __instance,
        out CreatureModifierManager.DelayedDamageTickScopeState __state)
    {
        __state = CreatureModifierManager.BeginPoisonDamageTick(__instance);
    }

    private static Exception? Finalizer(
        Exception? __exception,
        CreatureModifierManager.DelayedDamageTickScopeState __state)
    {
        CreatureModifierManager.EndDelayedDamageTick(__state);
        return __exception;
    }
}

[HarmonyPatch(typeof(SE_Burning), nameof(SE_Burning.UpdateStatusEffect), new[] { typeof(float) })]
internal static class CreatureManagerBurningTickScopePatch
{
    private static void Prefix(
        SE_Burning __instance,
        out CreatureModifierManager.DelayedDamageTickScopeState __state)
    {
        __state = CreatureModifierManager.BeginBurningDamageTick(__instance);
    }

    private static Exception? Finalizer(
        Exception? __exception,
        CreatureModifierManager.DelayedDamageTickScopeState __state)
    {
        CreatureModifierManager.EndDelayedDamageTick(__state);
        return __exception;
    }
}

[HarmonyPatch(typeof(Character), nameof(Character.Heal))]
internal static class CreatureManagerCharacterHealPatch
{
    private static void Prefix(Character __instance, ref float __0)
    {
        CreatureModifierManager.ApplyPlayerHealingDebuff(__instance, ref __0);
    }

    private static void Postfix(Character __instance)
    {
        CreatureModifierManager.ClearRecoveredDelayedDamageDeathCredit(__instance);
    }
}

[HarmonyPatch(typeof(Character), nameof(Character.SetHealth), new[] { typeof(float) })]
internal static class CreatureManagerCharacterSetHealthPatch
{
    private static void Postfix(Character __instance)
    {
        CreatureModifierManager.ClearRecoveredDelayedDamageDeathCredit(__instance);
    }
}

[HarmonyPatch]
internal static class CreatureManagerPlayerControlDebuffUpdatePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        MethodInfo? update = CreatureHarmonyTargetResolver.FindDeclared(
            typeof(Player),
            "Update",
            "player control debuffs",
            Type.EmptyTypes);
        if (update != null)
        {
            yield return update;
        }
    }

    private static void Postfix(Player __instance)
    {
        if (CreatureModifierManager.ShouldUpdatePlayerControlDebuffs(__instance))
        {
            CreatureModifierManager.UpdatePlayerControlDebuffs(__instance);
        }
    }
}

[HarmonyPatch(typeof(Character), "OnDeath")]
internal static class CreatureManagerCharacterOnDeathPatch
{
    private static void Prefix(Character __instance)
    {
        CreatureModifierManager.FinalDeathAttribution attribution =
            CreatureModifierManager.CaptureFinalDeathAttribution(__instance);
        CreatureModifierManager.HandleDeath(__instance, attribution);
        if (!__instance.IsPlayer())
        {
            CreatureKarmaManager.RecordDeath(__instance, attribution);
        }
    }
}

[HarmonyPatch]
internal static class CreatureManagerCharacterModifierUpdatePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        MethodInfo? customFixedUpdate = CreatureHarmonyTargetResolver.FindDeclared(
            typeof(Character),
            "CustomFixedUpdate",
            "passive modifier updates",
            new[] { typeof(float) },
            warnIfMissing: false);
        if (customFixedUpdate != null)
        {
            yield return customFixedUpdate;
            yield break;
        }

        MethodInfo? fixedUpdate = CreatureHarmonyTargetResolver.FindDeclared(
            typeof(Character),
            "FixedUpdate",
            "passive modifier update fallback",
            Type.EmptyTypes,
            warnIfMissing: false);
        if (fixedUpdate != null)
        {
            yield return fixedUpdate;
            yield break;
        }

        CreatureManagerPlugin.Log.LogWarning(
            "Could not find optional Harmony targets Character.CustomFixedUpdate(float) or Character.FixedUpdate(); " +
            "passive modifier updates are disabled for this game version.");
    }

    private static void Postfix(Character __instance)
    {
        if (!CreatureModifierManager.NeedsPassiveModifierUpdate(__instance))
        {
            return;
        }

        CreatureModifierManager.UpdatePassiveModifiers(__instance);
    }
}

[HarmonyPatch]
internal static class CreatureManagerPlayerGetBodyArmorPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        MethodInfo? method = CreatureHarmonyTargetResolver.FindDeclared(
            typeof(Player),
            "GetBodyArmor",
            "Armor Piercing",
            Type.EmptyTypes);
        if (method != null)
        {
            yield return method;
        }
    }

    private static void Postfix(ref float __result)
    {
        CreatureModifierManager.ApplyArmorPiercing(ref __result);
    }
}

[HarmonyPatch]
internal static class CreatureManagerHumanoidOnStopMovingCompatibilityPatch
{
    private static bool _warningLogged;

    private static IEnumerable<MethodBase> TargetMethods()
    {
        MethodInfo? method = CreatureHarmonyTargetResolver.FindDeclared(
            typeof(Humanoid),
            nameof(Humanoid.OnStopMoving),
            "Humanoid.OnStopMoving current-attack null-condition compatibility fix",
            Type.EmptyTypes);
        if (method != null)
        {
            yield return method;
        }
    }

    [HarmonyPriority(Priority.Last)]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> codes = instructions.ToList();
        FieldInfo? currentAttackField = AccessTools.Field(typeof(Humanoid), nameof(Humanoid.m_currentAttack));
        FieldInfo? speedFactorField = AccessTools.Field(typeof(Attack), nameof(Attack.m_speedFactor));
        FieldInfo? rotationSpeedFactorField = AccessTools.Field(typeof(Attack), nameof(Attack.m_speedFactorRotation));
        MethodInfo? inAttackMethod = AccessTools.Method(typeof(Character), nameof(Character.InAttack), Type.EmptyTypes);
        if (currentAttackField == null ||
            speedFactorField == null ||
            rotationSpeedFactorField == null ||
            inAttackMethod == null)
        {
            WarnOnce(
                "Could not resolve Humanoid.OnStopMoving compatibility members; the vanilla null-condition fix was not applied.");
            return codes;
        }

        List<int> defectiveGuards = FindKnownGuardMatches(
            codes,
            currentAttackField,
            speedFactorField,
            rotationSpeedFactorField,
            inAttackMethod,
            branchWhenNull: true);
        List<int> correctedGuards = FindKnownGuardMatches(
            codes,
            currentAttackField,
            speedFactorField,
            rotationSpeedFactorField,
            inAttackMethod,
            branchWhenNull: false);

        if (defectiveGuards.Count == 1 && correctedGuards.Count == 0)
        {
            int branchIndex = defectiveGuards[0];
            codes[branchIndex].opcode = codes[branchIndex].opcode == OpCodes.Brfalse_S
                ? OpCodes.Brtrue_S
                : OpCodes.Brtrue;
            return codes;
        }

        if (defectiveGuards.Count == 0 && correctedGuards.Count == 1)
        {
            // Valheim or another transpiler has already corrected the guard.
            return codes;
        }

        WarnOnce(
            $"Expected one known Humanoid.OnStopMoving current-attack guard, found " +
            $"{defectiveGuards.Count} defective and {correctedGuards.Count} corrected matches; " +
            "the vanilla null-condition compatibility fix was not applied.");
        return codes;
    }

    private static List<int> FindKnownGuardMatches(
        IReadOnlyList<CodeInstruction> codes,
        FieldInfo currentAttackField,
        FieldInfo speedFactorField,
        FieldInfo rotationSpeedFactorField,
        MethodInfo inAttackMethod,
        bool branchWhenNull)
    {
        List<int> matches = new();
        for (int index = 0; index <= codes.Count - 6; index++)
        {
            OpCode branchOpcode = codes[index + 2].opcode;
            bool expectedBranch = branchWhenNull
                ? branchOpcode == OpCodes.Brfalse || branchOpcode == OpCodes.Brfalse_S
                : branchOpcode == OpCodes.Brtrue || branchOpcode == OpCodes.Brtrue_S;
            if (!expectedBranch ||
                codes[index].opcode != OpCodes.Ldarg_0 ||
                codes[index + 1].opcode != OpCodes.Ldfld ||
                !Equals(codes[index + 1].operand, currentAttackField) ||
                codes[index + 3].opcode != OpCodes.Ret ||
                codes[index + 4].opcode != OpCodes.Ldarg_0 ||
                !codes[index + 5].Calls(inAttackMethod) ||
                !BranchesTo(codes[index + 2], codes[index + 4]) ||
                CountCurrentAttackZeroStores(codes, index + 4, currentAttackField, speedFactorField) != 1 ||
                CountCurrentAttackZeroStores(codes, index + 4, currentAttackField, rotationSpeedFactorField) != 1)
            {
                continue;
            }

            matches.Add(index + 2);
        }

        return matches;
    }

    private static int CountCurrentAttackZeroStores(
        IReadOnlyList<CodeInstruction> codes,
        int startIndex,
        FieldInfo currentAttackField,
        FieldInfo destinationField)
    {
        int matches = 0;
        for (int index = startIndex; index <= codes.Count - 4; index++)
        {
            if (codes[index].opcode == OpCodes.Ldarg_0 &&
                codes[index + 1].opcode == OpCodes.Ldfld &&
                Equals(codes[index + 1].operand, currentAttackField) &&
                codes[index + 2].opcode == OpCodes.Ldc_R4 &&
                codes[index + 2].operand is float value &&
                value == 0f &&
                codes[index + 3].opcode == OpCodes.Stfld &&
                Equals(codes[index + 3].operand, destinationField))
            {
                matches++;
            }
        }

        return matches;
    }

    private static bool BranchesTo(CodeInstruction branch, CodeInstruction target)
    {
        return branch.operand switch
        {
            Label label => target.labels.Contains(label),
            CodeInstruction instruction => ReferenceEquals(instruction, target),
            _ => false
        };
    }

    private static void WarnOnce(string message)
    {
        if (_warningLogged)
        {
            return;
        }

        _warningLogged = true;
        CreatureManagerPlugin.Log.LogWarning(message);
    }
}

[HarmonyPatch]
internal static class CreatureManagerCharacterAnimEventSpeedPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        MethodInfo? method = CreatureHarmonyTargetResolver.FindDeclared(
            typeof(CharacterAnimEvent),
            "CustomFixedUpdate",
            "Attack Speed animation updates",
            new[] { typeof(float) });
        if (method != null)
        {
            yield return method;
        }
    }

    private static void Postfix(CharacterAnimEvent __instance)
    {
        CreatureModifierManager.ApplyAttackAnimationSpeed(__instance);
    }
}

[HarmonyPatch(typeof(Humanoid), nameof(Humanoid.GetTimeSinceLastAttack))]
internal static class CreatureManagerHumanoidGetTimeSinceLastAttackPatch
{
    private static void Postfix(Humanoid __instance, ref float __result)
    {
        CreatureModifierManager.ApplyMinimumAttackIntervalSpeed(__instance, ref __result);
    }
}

[HarmonyPatch]
internal static class CreatureManagerMonsterAiBlamerFleePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        MethodInfo? method = CreatureHarmonyTargetResolver.FindDeclared(
            typeof(MonsterAI),
            nameof(MonsterAI.UpdateAI),
            "Blamer flee behavior",
            new[] { typeof(float) });
        if (method != null)
        {
            yield return method;
        }
    }

    private static void Prefix(MonsterAI __instance, out CreatureModifierManager.BlamerFleeOverrideState __state)
    {
        __state = CreatureModifierManager.BeginBlamerFleeOverride(__instance);
    }

    private static Exception? Finalizer(Exception? __exception, CreatureModifierManager.BlamerFleeOverrideState __state)
    {
        CreatureModifierManager.EndBlamerFleeOverride(__state);
        return __exception;
    }
}

[HarmonyPatch]
internal static class CreatureManagerMonsterAiBlinkRangePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        MethodInfo? method = CreatureHarmonyTargetResolver.FindDeclared(
            typeof(MonsterAI),
            nameof(MonsterAI.UpdateAI),
            "Blink attack range handling",
            new[] { typeof(float) });
        if (method != null)
        {
            yield return method;
        }
    }

    private static void Prefix(MonsterAI __instance, out CreatureModifierManager.BlinkAttackAiOverrideState? __state)
    {
        __state = CreatureModifierManager.BeginBlinkAttackAiOverride(__instance);
    }

    private static Exception? Finalizer(Exception? __exception, CreatureModifierManager.BlinkAttackAiOverrideState? __state)
    {
        CreatureModifierManager.EndBlinkAttackAiOverride(__state);
        return __exception;
    }
}

[HarmonyPatch]
internal static class CreatureManagerAttackStartSpeedPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        MethodInfo? method = CreatureHarmonyTargetResolver.FindDeclared(
            typeof(Attack),
            "Start",
            "Blink and Attack Speed attack-start handling",
            new[]
            {
                typeof(Humanoid),
                typeof(Rigidbody),
                typeof(ZSyncAnimation),
                typeof(CharacterAnimEvent),
                typeof(VisEquipment),
                typeof(ItemDrop.ItemData),
                typeof(Attack),
                typeof(float),
                typeof(float)
            });
        if (method != null)
        {
            yield return method;
        }
    }

    private static void Postfix(Humanoid character, ItemDrop.ItemData weapon, bool __result)
    {
        CreatureModifierManager.TryBlinkOnAttackStart(character, weapon, __result);
        CreatureModifierManager.ApplyAttackIntervalSpeed(character, weapon, __result);
    }
}

[HarmonyPatch(typeof(Player), "OnDeath")]
internal static class CreatureManagerPlayerOnDeathPatch
{
    private static void Prefix(Player __instance)
    {
        CreatureModifierManager.HandlePlayerDeath(__instance);
    }

    private static void Postfix(Player __instance)
    {
        CreatureKarmaManager.RecordDeath(__instance);
    }
}

[HarmonyPatch]
internal static class CreatureManagerPlayerOnRespawnPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        MethodInfo? method = CreatureHarmonyTargetResolver.FindDeclared(
            typeof(Player),
            nameof(Player.OnRespawn),
            "Reaping replay reset after player respawn",
            Type.EmptyTypes);
        if (method != null)
        {
            yield return method;
        }
    }

    private static void Postfix(Player __instance)
    {
        CreatureModifierManager.NotifyPlayerRespawn(__instance);
    }
}

[HarmonyPatch(typeof(EnemyHud), "ShowHud")]
internal static class CreatureManagerEnemyHudShowHudPatch
{
    private static void Prefix(Character c, out bool __state)
    {
        __state = CreatureBossHudOnlyScope.Begin(c);
    }

    private static Exception? Finalizer(Exception? __exception, Character c, bool __state)
    {
        CreatureBossHudOnlyScope.End(c, __state);
        return __exception;
    }
}

[HarmonyPatch(typeof(EnemyHud), "UpdateHuds")]
internal static class CreatureManagerEnemyHudUpdateHudsPatch
{
    private static void Prefix(EnemyHud __instance, out List<Character>? __state)
    {
        __state = CreatureBossHudOnlyScope.Begin(__instance);
    }

    private static void Postfix(EnemyHud __instance, List<Character>? __state)
    {
        CreatureModifierManager.UpdateEnemyHuds(__instance);
    }

    private static Exception? Finalizer(Exception? __exception, List<Character>? __state)
    {
        CreatureBossHudOnlyScope.End(__state);
        return __exception;
    }
}

[HarmonyPatch(typeof(EnemyHud), "TestShow")]
internal static class CreatureManagerEnemyHudTestShowPatch
{
    private static void Prefix(Character c, out bool __state)
    {
        __state = CreatureBossHudOnlyScope.Begin(c);
    }

    private static Exception? Finalizer(Exception? __exception, Character c, bool __state)
    {
        CreatureBossHudOnlyScope.End(c, __state);
        return __exception;
    }
}

[HarmonyPatch(typeof(BaseAI), nameof(BaseAI.IsEnemy), new[] { typeof(Character), typeof(Character) })]
internal static class CreatureManagerBaseAiIsEnemyPatch
{
    private static bool Prefix(Character a, Character b, ref bool __result)
    {
        if (!CreatureFactionManager.TryIsEnemy(a, b, out bool isEnemy))
        {
            return true;
        }

        __result = isEnemy;
        return false;
    }
}
