using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace CreatureManager;

// Optional, presentation-only adapter. PoV still owns modifier selection, names,
// assets and ailments; CM owns the shared icon geometry and the level badge.
internal static class CreaturePovHud
{
    private const int Columns = 4, Size = 17, Step = 18;
    private static Harmony? _harmony;
    private static bool _enabled;
    private static Func<Character, int>? _modifiers;
    private static Func<int, Sprite>? _tile;
    private static IDictionary? _foreignRows;
    private static Func<object, RectTransform>? _foreignRoot;
    private static readonly List<(int Mask, int Tile)> Catalog = new();
    private static ConditionalWeakTable<GameObject, Row> Rows = new();

    internal sealed class Row
    {
        internal readonly List<Sprite> Sprites = new();
        internal readonly List<Sprite> Combined = new();
        internal readonly List<Image> Icons = new();
        internal RectTransform? Root;
        internal RectTransform? Resistance, Statuses, StatusParent;
        internal readonly List<(RectTransform Slot, Text? Counter)> StatusSlots = new();
        internal readonly List<(RectTransform Slot, Text? Counter)> ActiveStatuses = new();
        internal int Mask = -1;
        internal float NextRefresh, ResistanceOffset;
        internal bool Ready;
    }

    internal static bool Enabled => _enabled;

    internal static void Install(Assembly assembly, bool allowed)
    {
        if (!allowed) return; // Another star provider keeps its existing HUD policy.
        Type hud = assembly.GetType("PassiveTree.RarityHud", true)!;
        Type mods = assembly.GetType("PassiveTree.RarityMods", true)!;
        Type bits = mods.GetNestedType("Mod")!;
        if (bits == null || !bits.IsEnum || Enum.GetUnderlyingType(bits) != typeof(int))
            throw new NotSupportedException("PoV modifier HUD representation changed");
        MethodInfo on = Method(mods, "On", bits, typeof(Character));
        var character = Expression.Parameter(typeof(Character));
        _modifiers = Expression.Lambda<Func<Character, int>>(Expression.Convert(Expression.Call(on, character), typeof(int)), character).Compile();
        _tile = (Func<int, Sprite>)Delegate.CreateDelegate(typeof(Func<int, Sprite>),
            Method(assembly.GetType("PassiveTree.UI.UiKit", true)!, "Tile", typeof(Sprite), typeof(int)));
        var map = (Dictionary<string, int>)AccessTools.DeclaredField(assembly.GetType("PassiveTree.IconMap", true), "ByGroup").GetValue(null);
        MethodInfo icon = Method(mods, "Icon", typeof(string), bits);
        MethodInfo each = Method(mods, "Each", typeof(IEnumerable<>).MakeGenericType(bits), bits);
        Catalog.Clear();
        foreach (object bit in (IEnumerable)each.Invoke(null, new[] { Enum.ToObject(bits, -1) })!)
        {
            string key = (string)icon.Invoke(null, new[] { bit })!;
            if (key != null && map.TryGetValue(key, out int index)) Catalog.Add((Convert.ToInt32(bit), index));
        }
        if (Catalog.Count == 0) throw new NotSupportedException("PoV modifier icon catalog is empty");
        FieldInfo rows = AccessTools.DeclaredField(hud, "rows");
        FieldInfo root = AccessTools.DeclaredField(hud.GetNestedType("Row", BindingFlags.NonPublic), "Root");
        if (rows == null || !rows.IsStatic || !typeof(IDictionary).IsAssignableFrom(rows.FieldType) || root?.FieldType != typeof(RectTransform))
            throw new NotSupportedException("PoV modifier row lifecycle changed");
        _foreignRows = (IDictionary)rows.GetValue(null);
        var row = Expression.Parameter(typeof(object));
        _foreignRoot = Expression.Lambda<Func<object, RectTransform>>(Expression.Field(Expression.Convert(row, root.DeclaringType!), root), row).Compile();
        MethodInfo icons = Method(hud, "Icons", typeof(void), typeof(EnemyHud.HudData), typeof(Character));
        MethodInfo draw = Method(assembly.GetType("PassiveTree.UI.AilmentHudBridge", true)!, "Draw", typeof(bool), typeof(EnemyHud.HudData), typeof(Character));
        Type view = assembly.GetType("PathOfValheiman.AilmentHud.AilmentHudView", true)!;
        MethodInfo apply = AccessTools.DeclaredMethod(view, "Apply");
        if (apply == null || apply.IsStatic || apply.ReturnType != typeof(void))
            throw new NotSupportedException("PoV status HUD lifecycle changed");
        RequireCall(draw, apply);
        var layoutNames = new HashSet<string>(view.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .SelectMany(ctor => PatchProcessor.GetOriginalInstructions(ctor))
            .Where(code => code.operand is string).Select(code => (string)code.operand));
        if (!layoutNames.IsSupersetOf(new[] { "PoV_AilmentHud", "Statuses", "Count" }))
            throw new NotSupportedException("PoV status HUD hierarchy changed");
        RequireCall(Method(hud.GetNestedType("EnemyHud_UpdateHuds_Rarity", BindingFlags.NonPublic)!, "Postfix", typeof(void), typeof(EnemyHud)), icons);
        RequireCall(Method(assembly.GetType("PassiveTree.UI.GaugeHud+UpdateHuds", true)!, "Postfix", typeof(void), typeof(EnemyHud)), draw);
        _harmony = new Harmony("sighsorry.CreatureManager.PoV.Hud");
        _harmony.Patch(icons, prefix: new HarmonyMethod(typeof(CreaturePovHud), nameof(IconsPrefix)));
        _harmony.Patch(draw, postfix: new HarmonyMethod(typeof(CreaturePovHud), nameof(StatusPostfix)));
        RebuildCaller();
        _enabled = true;
    }

    private static MethodInfo Method(Type type, string name, Type returns, params Type[] parameters)
    {
        MethodInfo method = AccessTools.DeclaredMethod(type, name, parameters);
        if (method == null || !method.IsStatic || method.ReturnType != returns)
            throw new NotSupportedException($"PoV HUD contract changed: {type?.Name}.{name}");
        return method;
    }

    private static void RequireCall(MethodInfo caller, MethodInfo callee)
    {
        if (!PatchProcessor.GetOriginalInstructions(caller).Any(code => code.Calls(callee)))
            throw new NotSupportedException("PoV HUD caller changed: " + caller.DeclaringType?.FullName);
    }

    internal static Row? Read(Character character, EnemyHud.HudData hud)
    {
        if (!_enabled || hud.m_isMount || character.IsPlayer() || hud.m_gui == null) return null;
        Row row = Rows.GetValue(hud.m_gui, _ => new Row());
        row.Ready = false;
        if (Time.unscaledTime < row.NextRefresh) return row;
        row.NextRefresh = Time.unscaledTime + .25f;
        try
        {
            int mask = _modifiers!(character);
            if (mask == row.Mask && !row.Sprites.Any(sprite => sprite == null)) return row;
            row.Sprites.Clear();
            bool complete = true;
            foreach (var entry in Catalog)
            {
                if ((mask & entry.Mask) == 0) continue;
                Sprite sprite = _tile!(entry.Tile);
                if (sprite != null) row.Sprites.Add(sprite);
                else complete = false; // Assets may not be ready yet; retry next refresh.
            }
            row.Mask = complete ? mask : -1;
            return row;
        }
        catch (Exception ex)
        {
            _enabled = false;
            CreatureManagerPlugin.Log.LogWarning($"PoV combined HUD disabled until restart; original display retained: {ex.Message}");
            return null;
        }
    }

    internal static int RowCount(int count) => (Math.Max(0, count) + Columns - 1) / Columns;
    internal static float Height(int count, bool stars) => Math.Max(stars ? 20 : 0, RowCount(count) * Step - (count > 0 ? 1 : 0));
    internal static Vector2 IconPosition(int index, int count)
    {
        int inRow = Math.Min(Columns, count - index / Columns * Columns);
        return new Vector2(-(inRow - 1 - index % Columns) * Step, -(index / Columns) * Step);
    }

    internal static void Render(Row row, RectTransform content, Image?[] cm)
    {
        if (row.Root == null || row.Root.parent != content)
        {
            if (row.Root != null) UnityEngine.Object.Destroy(row.Root.gameObject);
            row.Root = content.Find("CreatureManager_PovModifiers") as RectTransform ??
                (RectTransform)new GameObject("CreatureManager_PovModifiers", typeof(RectTransform), typeof(LayoutElement)).transform;
            row.Root.SetParent(content, false);
            row.Root.GetComponent<LayoutElement>().ignoreLayout = true;
            row.Root.anchorMin = row.Root.anchorMax = row.Root.pivot = Vector2.one;
            row.Icons.Clear();
            for (int i = 0; i < row.Root.childCount; i++)
                if (row.Root.GetChild(i).TryGetComponent(out Image existing)) row.Icons.Add(existing);
        }
        row.Root.gameObject.SetActive(true);
        row.Combined.Clear();
        row.Combined.AddRange(row.Sprites);
        foreach (Image? icon in cm) if (icon != null && icon.enabled && icon.sprite != null) row.Combined.Add(icon.sprite);
        int count = row.Combined.Count;
        Vector2 size = new(Columns * Step - 1, Height(count, false));
        if (row.Root.sizeDelta != size) row.Root.sizeDelta = size;
        for (int i = 0; i < count; i++)
        {
            if (i == row.Icons.Count)
            {
                Image icon = new GameObject("Modifier", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                icon.rectTransform.SetParent(row.Root, false);
                icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = icon.rectTransform.pivot = Vector2.one;
                icon.raycastTarget = false;
                icon.preserveAspect = true;
                row.Icons.Add(icon);
            }
            Image image = row.Icons[i];
            if (!image.gameObject.activeSelf) image.gameObject.SetActive(true);
            if (image.sprite != row.Combined[i]) image.sprite = row.Combined[i];
            Vector2 iconSize = new(Size, Size);
            if (image.rectTransform.sizeDelta != iconSize) image.rectTransform.sizeDelta = iconSize;
            Vector2 position = IconPosition(i, count);
            if (image.rectTransform.anchoredPosition != position) image.rectTransform.anchoredPosition = position;
        }
        for (int i = count; i < row.Icons.Count; i++) if (row.Icons[i].gameObject.activeSelf) row.Icons[i].gameObject.SetActive(false);
        row.Ready = true;
    }

    internal static void Hide(EnemyHud.HudData hud)
    {
        if (hud.m_gui != null && Rows.TryGetValue(hud.m_gui, out Row row))
        {
            row.Ready = false;
            if (row.Root != null) row.Root.gameObject.SetActive(false);
            foreach (var status in row.StatusSlots) if (status.Slot != null) status.Slot.localScale = Vector3.one;
        }
    }

    private static void StatusPostfix(EnemyHud.HudData hud, bool __result)
    {
        if (!_enabled || hud == null || hud.m_isMount || hud.m_gui == null || !Rows.TryGetValue(hud.m_gui, out Row row) || !row.Ready ||
            row.Root == null || row.Root.parent is not RectTransform content) return;
        if (!__result) { MoveResistance(row, float.PositiveInfinity); return; }
        GuiBar bar = hud.m_healthFast != null ? hud.m_healthFast : hud.m_healthFastFriendly;
        RectTransform? parent = bar != null ? bar.transform.parent as RectTransform : null;
        if (parent == null) return;
        if (row.Statuses == null || row.StatusParent != parent)
        {
            row.StatusParent = parent;
            row.Statuses = parent.Find("PoV_AilmentHud/Statuses") as RectTransform;
            row.StatusSlots.Clear();
            if (row.Statuses != null)
                for (int i = 0; i < row.Statuses.childCount; i++)
                    if (row.Statuses.GetChild(i) is RectTransform slot)
                        row.StatusSlots.Add((slot, slot.Find("Count")?.GetComponent<Text>()));
        }
        if (row.Statuses == null || !row.Statuses.gameObject.activeInHierarchy) { MoveResistance(row, float.PositiveInfinity); return; }
        row.ActiveStatuses.Clear();
        foreach (var status in row.StatusSlots)
        {
            if (status.Slot == null || !status.Slot.gameObject.activeSelf) continue;
            // PoV has just placed visible statuses in its own order. Preserve it
            // without allocating a List.Sort comparison wrapper each frame.
            int index = 0;
            while (index < row.ActiveStatuses.Count && row.ActiveStatuses[index].Slot.anchoredPosition.x < status.Slot.anchoredPosition.x) index++;
            row.ActiveStatuses.Insert(index, status);
        }
        float bottom = float.PositiveInfinity, rowY = content.sizeDelta.y + 2f, rowHeight = Step;
        Vector3 contentScale = content.lossyScale;
        Vector3 parentScale = row.Statuses.lossyScale;
        if (!UsableScale(contentScale) || !UsableScale(parentScale))
        {
            MoveResistance(row, float.PositiveInfinity);
            return;
        }
        for (int i = 0; i < row.ActiveStatuses.Count; i++)
        {
            var status = row.ActiveStatuses[i];
            RectTransform slot = status.Slot;
            Vector3 scale = new(Size / Math.Max(1f, slot.rect.width) * contentScale.x / parentScale.x,
                Size / Math.Max(1f, slot.rect.height) * contentScale.y / parentScale.y, 1f);
            if (slot.localScale != scale) slot.localScale = scale;
            Vector2 offset = IconPosition(i, row.ActiveStatuses.Count);
            Vector3 position = content.TransformPoint(new Vector3(content.rect.xMax - Size * .5f + offset.x,
                content.rect.yMax - rowY - Size * .5f));
            if (slot.position != position) slot.position = position;
            float extent = Size;
            Text? text = status.Counter;
            if (text != null && text.gameObject.activeSelf && !string.IsNullOrEmpty(text.text))
            {
                Vector3 counterBottom = text.rectTransform.TransformPoint(new Vector3(0f, text.rectTransform.rect.yMin));
                extent = Math.Max(extent, content.rect.yMax - rowY - content.InverseTransformPoint(counterBottom).y);
            }
            rowHeight = Math.Max(rowHeight, extent + 2f);
            if (row.Resistance != null)
            {
                Vector3 localBottom = new(0f, content.rect.yMax - rowY - extent);
                bottom = Math.Min(bottom, row.Resistance.parent.InverseTransformPoint(content.TransformPoint(localBottom)).y);
            }
            if ((i + 1) % Columns == 0) { rowY += rowHeight; rowHeight = Step; }
        }
        MoveResistance(row, bottom);
    }

    private static bool UsableScale(Vector3 scale) =>
        Math.Abs(scale.x) > .0001f && Math.Abs(scale.x) < float.PositiveInfinity &&
        Math.Abs(scale.y) > .0001f && Math.Abs(scale.y) < float.PositiveInfinity;

    internal static float ResistanceOffset(EnemyHud.HudData hud) => _enabled && hud.m_gui != null &&
        Rows.TryGetValue(hud.m_gui, out Row row) && row.Ready ? row.ResistanceOffset : 0f;

    internal static float? ContentHeight(EnemyHud.HudData hud) => _enabled && hud.m_gui != null &&
        Rows.TryGetValue(hud.m_gui, out Row row) && row.Ready && row.Root != null && row.Root.parent is RectTransform content
            ? content.sizeDelta.y : null;

    private static void MoveResistance(Row row, float statusBottom)
    {
        if (row.Resistance == null || !row.Resistance.gameObject.activeSelf) return;
        RectTransform resistance = row.Resistance;
        float baseTop = resistance.localPosition.y + resistance.rect.yMax + row.ResistanceOffset;
        float offset = float.IsPositiveInfinity(statusBottom) ? 0f : Math.Max(0f, baseTop - statusBottom + 2f);
        if (!Mathf.Approximately(offset, row.ResistanceOffset))
        {
            Vector3 position = resistance.localPosition;
            position.y += row.ResistanceOffset - offset;
            resistance.localPosition = position;
            row.ResistanceOffset = offset;
        }
    }

    private static bool IconsPrefix(EnemyHud.HudData hud)
    {
        if (!_enabled || hud == null || hud.m_isMount || hud.m_gui == null || !Rows.TryGetValue(hud.m_gui, out Row row) || !row.Ready) return true;
        object? foreign = _foreignRows![hud.m_gui];
        if (foreign != null)
        {
            RectTransform root = _foreignRoot!(foreign);
            if (root != null && root.gameObject.activeSelf) root.gameObject.SetActive(false);
        }
        return false;
    }

    internal static void ClearRows()
    {
        // PoV repositions its slots each draw, but does not restore their scale.
        // Reset surviving views before dropping the weak references.
        if (EnemyHud.instance is EnemyHud enemyHud)
            foreach (EnemyHud.HudData hud in enemyHud.m_huds.Values)
            {
                if (hud.m_gui != null && Rows.TryGetValue(hud.m_gui, out Row row) && row.Ready)
                    MoveResistance(row, float.PositiveInfinity);
                Hide(hud);
            }
        Rows = new ConditionalWeakTable<GameObject, Row>();
    }

    private static void RebuildCaller() => _harmony!.CreateProcessor(AccessTools.DeclaredMethod(typeof(EnemyHud), "UpdateHuds")).Patch();

    internal static void Remove()
    {
        _enabled = false;
        if (_harmony != null) { _harmony.UnpatchSelf(); RebuildCaller(); _harmony = null; }
        ClearRows();
        Catalog.Clear();
        _modifiers = null; _tile = null; _foreignRows = null; _foreignRoot = null;
    }
}
