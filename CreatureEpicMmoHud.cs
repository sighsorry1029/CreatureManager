using System.Collections.Generic;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using UnityEngine;

namespace CreatureManager;

// The optional mod owns label creation and its synced config. CM only adjusts the
// live label after EpicMMO's ShowHud/UpdateHuds patches, including its fallback clone.
internal static class CreatureEpicMmoHud
{
    internal const string PluginGuid = "WackyMole.EpicMMOSystem";
    private static readonly Vector2 Position = new(70f, -15f);
    private static readonly Dictionary<RectTransform, LabelState> Labels = new();
    private static readonly List<RectTransform> StaleNames = new();
    private static ConfigEntry<bool>? EpicLevelControl;
    private static bool? Available;
    private static uint UpdateId;
    private static int SeenCount;

    private sealed class LabelState
    {
        internal RectTransform? Label;
        internal int ChildCount = -1;
        internal Vector2 OriginalPosition;
        internal bool Applied;
        internal uint LastSeen;
    }

    internal static bool BeginUpdate()
    {
        // The first HUD update happens after normal BepInEx plugin loading. Probe
        // once per HUD/world lifetime, without a hard dependency or frame-time reflection.
        if (Available == null)
        {
            Available = Chainloader.PluginInfos.TryGetValue(PluginGuid, out var info) &&
                        info.Instance != null && info.Instance.Config.TryGetEntry(
                            "2.Creature level control", "Enabled_creature_level", out EpicLevelControl);
        }

        if (Available != true || EpicLevelControl?.Value != true ||
            CreatureManagerPlugin.AdjustEpicMmoLevelBarPosition?.Value != CreatureManagerPlugin.Toggle.On ||
            (ZNet.instance != null && ZNet.instance.IsDedicated()))
        {
            ClearLabels();
            return false;
        }

        unchecked { UpdateId++; }
        SeenCount = 0;
        return true;
    }

    internal static void Update(Character character, RectTransform? name, bool isMount)
    {
        // CM's existing boss-HUD scope also marks boss-style Enforcers here.
        if (name == null || character == null || isMount || character.IsPlayer() || character.IsBoss()) return;
        if (!Labels.TryGetValue(name, out LabelState state))
        {
            state = new LabelState();
            Labels.Add(name, state);
        }
        if (state.LastSeen != UpdateId)
        {
            state.LastSeen = UpdateId;
            SeenCount++;
        }

        if (state.Label != null && state.Label.parent != name)
        {
            Restore(state);
            state.Label = null;
            state.ChildCount = -1;
        }
        if (state.Label == null)
        {
            int childCount = name.childCount;
            // A destroyed cached label must be rediscovered even when its replacement
            // keeps the same child count. Stable missing labels need no repeated Find.
            bool destroyed = state.Label is not null;
            if (!destroyed && childCount == state.ChildCount) return;
            state.ChildCount = childCount;
            state.Label = name.Find("Name(Clone)") as RectTransform;
            state.Applied = false;
            if (state.Label == null) return;
        }

        Vector2 current = state.Label.anchoredPosition;
        if (!state.Applied || current != Position)
        {
            // Keep the most recent position supplied by EpicMMO/another layout mod.
            state.OriginalPosition = current;
            state.Applied = true;
            if (current != Position) state.Label.anchoredPosition = Position;
        }
    }

    internal static void EndUpdate()
    {
        // Reuse CM's existing HUD traversal. Only departures/exclusions require a
        // second pass, so destroyed HUDs do not accumulate for the whole session.
        if (SeenCount == Labels.Count) return;
        foreach (var pair in Labels)
        {
            if (pair.Value.LastSeen == UpdateId) continue;
            Restore(pair.Value);
            StaleNames.Add(pair.Key);
        }
        foreach (RectTransform name in StaleNames) Labels.Remove(name);
        StaleNames.Clear();
    }

    private static void Restore(LabelState state)
    {
        if (state.Applied && state.Label != null && state.Label.anchoredPosition == Position)
            state.Label.anchoredPosition = state.OriginalPosition;
        state.Applied = false;
    }

    private static void ClearLabels()
    {
        foreach (LabelState state in Labels.Values) Restore(state);
        Labels.Clear();
        StaleNames.Clear();
        SeenCount = 0;
    }

    internal static void Reset()
    {
        ClearLabels();
        Available = null;
        EpicLevelControl = null;
        UpdateId = 0;
    }
}
