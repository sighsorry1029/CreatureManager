using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CreatureManager;

internal static class CreatureCompendiumManager
{
    private const string PageTopic = "CreatureManager";
    private const string BodyIconPrefix = "CreatureManager_CompendiumModifierIcon_";
    private const string IconLinkPrefix = "cm-modifier-icon-";
    private const char IconPlaceholder = '\uFFFC';
    private const float IconSize = 18f;
    private const float IconTextGap = 5f;

    internal static void AddModifierEntries(TextsDialog dialog)
    {
        if (dialog == null || dialog.m_texts == null)
        {
            return;
        }

        dialog.m_texts.RemoveAll(text => IsCreatureManagerPage(text?.m_topic));
        if (CreatureManagerPlugin.EnableLevelSystem?.Value == CreatureManagerPlugin.Toggle.Off)
        {
            return;
        }

        List<CompendiumModifierEntry> entries = BuildEntries();
        if (entries.Count == 0)
        {
            return;
        }

        TextsDialog.TextInfo info = new(PageTopic, BuildPageText(entries));
        dialog.m_texts.Add(info);
        dialog.m_texts.Sort((a, b) => a.m_topic.CompareTo(b.m_topic));
    }

    internal static void RefreshPageContentIcons(TextsDialog dialog, TextsDialog.TextInfo info)
    {
        if (dialog == null || info == null || dialog.m_textArea == null)
        {
            return;
        }

        ClearPageContentIcons(dialog);
        if (!IsCreatureManagerPage(info.m_topic))
        {
            return;
        }

        List<CompendiumModifierEntry> entries = BuildEntries();
        if (entries.Count == 0)
        {
            return;
        }

        TMP_Text textArea = dialog.m_textArea;
        RectTransform? content = textArea.transform.parent as RectTransform;
        if (content == null)
        {
            return;
        }

        textArea.ForceMeshUpdate();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        textArea.ForceMeshUpdate();
        TMP_TextInfo textInfo = textArea.textInfo;
        for (int linkIndex = 0; linkIndex < textInfo.linkCount; linkIndex++)
        {
            TMP_LinkInfo link = textInfo.linkInfo[linkIndex];
            string linkId = link.GetLinkID();
            if (!linkId.StartsWith(IconLinkPrefix, StringComparison.Ordinal) ||
                !int.TryParse(linkId.Substring(IconLinkPrefix.Length), out int entryIndex) ||
                entryIndex < 0 ||
                entryIndex >= entries.Count ||
                link.linkTextfirstCharacterIndex < 0 ||
                link.linkTextfirstCharacterIndex >= textInfo.characterCount)
            {
                continue;
            }

            TMP_CharacterInfo character = textInfo.characterInfo[link.linkTextfirstCharacterIndex];
            AttachBodyIcon(content, textArea.rectTransform, character, entries[entryIndex].Sprite, entryIndex);
        }
    }

    private static List<CompendiumModifierEntry> BuildEntries()
    {
        List<CompendiumModifierEntry> entries = new();
        Dictionary<string, ModifierDefinition> globalModifiers = CreatureLevelManager.GetGlobalModifierDefinitions();
        if (globalModifiers.Count == 0)
        {
            return entries;
        }

        foreach (string modifier in CreatureModifierManager.GetKnownModifierKeys())
        {
            if (!globalModifiers.TryGetValue(modifier, out ModifierDefinition definition) ||
                !CreatureModifierManager.TryGetModifierSprite(modifier, out Sprite sprite))
            {
                continue;
            }

            entries.Add(new CompendiumModifierEntry(
                CreatureModifierManager.GetModifierGroupHeading(modifier),
                CreatureModifierManager.GetModifierDisplayName(modifier),
                CreatureModifierManager.GetModifierCompendiumText(modifier, definition),
                sprite));
        }

        return entries;
    }

    private static string BuildPageText(List<CompendiumModifierEntry> entries)
    {
        StringBuilder builder = new();
        string previousGroup = string.Empty;
        for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
        {
            CompendiumModifierEntry entry = entries[entryIndex];
            if (!string.Equals(previousGroup, entry.GroupHeading, StringComparison.Ordinal))
            {
                if (builder.Length > 0)
                {
                    builder.Append('\n');
                }

                builder
                    .Append("<color=#FFD27A><b>")
                    .Append(entry.GroupHeading)
                    .Append("</b></color>\n\n");
                previousGroup = entry.GroupHeading;
            }

            builder
                .Append("<link=\"")
                .Append(IconLinkPrefix)
                .Append(entryIndex)
                .Append("\"><color=#00000000>")
                .Append(IconPlaceholder)
                .Append("</color></link> ")
                .Append("   ")
                .Append("<color=orange><b>")
                .Append(entry.Name)
                .Append("</b></color>")
                .Append('\n')
                .Append("     ")
                .Append(entry.Description)
                .Append("\n\n");
        }

        return builder.ToString().TrimEnd();
    }

    private static void AttachBodyIcon(
        RectTransform content,
        RectTransform textArea,
        TMP_CharacterInfo marker,
        Sprite sprite,
        int index)
    {
        if (content == null || textArea == null || sprite == null)
        {
            return;
        }

        GameObject icon = new($"{BodyIconPrefix}{index}", typeof(RectTransform), typeof(Image), typeof(LayoutElement))
        {
            layer = textArea.gameObject.layer
        };
        RectTransform rect = (RectTransform)icon.transform;
        rect.SetParent(content, false);
        rect.anchorMin = content.pivot;
        rect.anchorMax = content.pivot;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(IconSize, IconSize);

        Vector3 center = (marker.bottomLeft + marker.topLeft) * 0.5f;
        center.x += IconSize * 0.5f + IconTextGap;
        Vector3 worldCenter = textArea.TransformPoint(center);
        Vector3 contentCenter = content.InverseTransformPoint(worldCenter);
        rect.anchoredPosition = new Vector2(contentCenter.x, contentCenter.y);

        Image image = icon.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;

        LayoutElement layout = icon.GetComponent<LayoutElement>();
        layout.ignoreLayout = true;
    }

    private static void ClearPageContentIcons(TextsDialog dialog)
    {
        if (dialog?.m_textArea == null)
        {
            return;
        }

        ClearIconChildren(dialog.m_textArea.transform);
        if (dialog.m_textArea.transform.parent != null)
        {
            ClearIconChildren(dialog.m_textArea.transform.parent);
        }
    }

    private static void ClearIconChildren(Transform parent)
    {
        for (int index = parent.childCount - 1; index >= 0; index--)
        {
            Transform child = parent.GetChild(index);
            if (child.name.StartsWith(BodyIconPrefix, StringComparison.Ordinal))
            {
                UnityEngine.Object.Destroy(child.gameObject);
            }
        }
    }

    private static bool IsCreatureManagerPage(string? topic)
    {
        return string.Equals(topic, PageTopic, StringComparison.Ordinal);
    }

    private readonly struct CompendiumModifierEntry
    {
        internal readonly string GroupHeading;
        internal readonly string Name;
        internal readonly string Description;
        internal readonly Sprite Sprite;

        internal CompendiumModifierEntry(string groupHeading, string name, string description, Sprite sprite)
        {
            GroupHeading = groupHeading;
            Name = name;
            Description = description;
            Sprite = sprite;
        }
    }
}
