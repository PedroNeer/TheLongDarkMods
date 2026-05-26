using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using MelonLoader;
using Pathoschild.TheLongDarkMods.Common;
using Pathoschild.TheLongDarkMods.FastTravel.Framework.DataModels;
using UnityEngine;

namespace Pathoschild.TheLongDarkMods.FastTravel.Framework;

/// <summary>An overlay which lets the player choose a fast travel destination.</summary>
[RegisterTypeInIl2Cpp]
internal class DestinationListOverlay : MonoBehaviour
{
    /*********
    ** Fields
    *********/
    /// <summary>The preferred number of destination rows shown in each column.</summary>
    private const int PreferredVisibleRowsPerColumn = 9;

    /// <summary>The number of destination columns shown at once.</summary>
    private const int VisibleColumnCount = 2;

    /// <summary>The pixel scaling to apply to the destination list UI.</summary>
    private static readonly float[] UiScaleSteps = [1f, 1.5f, 2f, 2.5f, 3f, 3.5f, 4f, 4.5f, 5f];

    /// <summary>The preferred default UI scale on screens where it can fit the full list page.</summary>
    private const float PreferredDefaultUiScale = 3.5f;

    /// <summary>The unscaled vertical padding inside the outer list box.</summary>
    private const float ContentVerticalPadding = 28f;

    /// <summary>The estimated unscaled vertical space used by non-list labels, gaps, and shortcut hints.</summary>
    private const float NonListContentHeight = 112f;

    /// <summary>The unscaled height of the title line.</summary>
    private const float TitleLineHeight = 26f;

    /// <summary>The unscaled height of one help or shortcut line.</summary>
    private const float HelpLineHeight = 20f;

    /// <summary>The estimated unscaled height of one destination row.</summary>
    private const float DestinationRowHeight = 20f;

    /// <summary>The entries shown in the list.</summary>
    private readonly List<DestinationEntry> Entries = [];

    /// <summary>The current title text.</summary>
    private string Title = "快速旅行目的地";

    /// <summary>The selected UI scale step.</summary>
    private int UiScaleIndex = -1;

    /// <summary>The current return point.</summary>
    private Destination? ReturnPoint;

    /// <summary>The configured return point hotkey.</summary>
    private KeyCode ReturnPointKey;

    /// <summary>The selected entry index.</summary>
    private int SelectedIndex;

    /// <summary>Whether the next destination hotkey should rebind the selected entry.</summary>
    private bool IsRebinding;

    /// <summary>The callback to invoke when an entry is selected.</summary>
    private Action<DestinationEntry>? OnSelect;

    /// <summary>The callback to invoke when an entry should be deleted.</summary>
    private Action<DestinationEntry>? OnDelete;

    /// <summary>The callback to invoke when an entry should be rebound to a new hotkey.</summary>
    private Action<DestinationEntry, KeyCode>? OnRebind;

    /// <summary>The callback to invoke when an entry should be renamed.</summary>
    private Action<DestinationEntry>? OnRename;

    /// <summary>The title label style.</summary>
    private GUIStyle? TitleStyle;

    /// <summary>The list label style.</summary>
    private GUIStyle? RowStyle;

    /// <summary>The selected list label style.</summary>
    private GUIStyle? SelectedRowStyle;

    /// <summary>The help label style.</summary>
    private GUIStyle? HelpStyle;


    /*********
    ** Accessors
    *********/
    /// <summary>Whether the overlay is currently visible.</summary>
    internal bool IsVisible { get; private set; }

    /// <summary>The selected UI scale.</summary>
    private float UiScale => UiScaleSteps[Math.Max(this.UiScaleIndex, 0)];


    /*********
    ** Public methods
    *********/
    /// <inheritdoc />
    public DestinationListOverlay(IntPtr pointer)
        : base(pointer) { }

    /// <summary>Create and attach the component to a persistent GameObject.</summary>
    public static DestinationListOverlay Create()
    {
        var gameObj = new GameObject($"{ModInfo.UniqueId}_{nameof(DestinationListOverlay)}");
        GameObject.DontDestroyOnLoad(gameObj);
        return gameObj.AddComponent<DestinationListOverlay>();
    }

    /// <summary>Show the overlay.</summary>
    /// <param name="title">The overlay title.</param>
    /// <param name="entries">The entries to display.</param>
    /// <param name="returnPoint">The player's current return point.</param>
    /// <param name="returnPointKey">The configured return point key.</param>
    /// <param name="onSelect">The callback to invoke when an entry is selected.</param>
    /// <param name="onDelete">The callback to invoke when an entry should be deleted.</param>
    /// <param name="onRebind">The callback to invoke when an entry should be rebound to a new hotkey.</param>
    /// <param name="onRename">The callback to invoke when an entry should be renamed.</param>
    internal void Show(string title, IEnumerable<DestinationEntry> entries, Destination? returnPoint, KeyCode returnPointKey, Action<DestinationEntry> onSelect, Action<DestinationEntry> onDelete, Action<DestinationEntry, KeyCode> onRebind, Action<DestinationEntry> onRename)
    {
        this.Title = title;
        this.ReturnPoint = returnPoint;
        this.ReturnPointKey = returnPointKey;
        this.OnSelect = onSelect;
        this.OnDelete = onDelete;
        this.OnRebind = onRebind;
        this.OnRename = onRename;

        if (this.UiScaleIndex < 0)
            this.SetDefaultUiScaleForScreen();

        string? selectedId = this.GetSelectedEntry()?.Id;
        this.Entries.Clear();
        this.Entries.AddRange(entries.Where(entry => entry.Location is not null));

        if (selectedId is not null)
        {
            int selectedIndex = this.Entries.FindIndex(entry => entry.Id == selectedId);
            if (selectedIndex >= 0)
                this.SelectedIndex = selectedIndex;
        }

        this.SelectedIndex = Math.Min(this.SelectedIndex, Math.Max(this.Entries.Count - 1, 0));
        this.IsRebinding = false;
        this.IsVisible = true;
    }

    /// <summary>Hide the overlay.</summary>
    internal void Hide()
    {
        this.IsVisible = false;
        this.IsRebinding = false;
    }

    /// <summary>Handle a key press while the overlay is open.</summary>
    /// <param name="input">The input helper.</param>
    /// <param name="config">The mod settings.</param>
    internal void HandleInput(InteractionHelper input, ModConfig config)
    {
        if (!this.IsVisible)
            return;

        if (input.IsKeyJustPressed(KeyCode.Escape))
        {
            this.Hide();
            return;
        }

        if (this.IsRebinding)
        {
            if (this.TryGetPressedDestinationKey(input, config, out KeyCode hotkey))
            {
                DestinationEntry? selected = this.GetSelectedEntry();
                if (selected is not null)
                    this.OnRebind?.Invoke(selected, hotkey);

                this.IsRebinding = false;
            }

            return;
        }

        if (input.IsKeyJustPressed(KeyCode.PageUp))
        {
            this.MovePage(-1);
            return;
        }

        if (input.IsKeyJustPressed(KeyCode.PageDown))
        {
            this.MovePage(1);
            return;
        }

        if (input.IsKeyJustPressed(KeyCode.UpArrow))
        {
            this.MoveSelection(-1);
            return;
        }

        if (input.IsKeyJustPressed(KeyCode.DownArrow))
        {
            this.MoveSelection(1);
            return;
        }

        if (input.IsKeyJustPressed(KeyCode.Return) || input.IsKeyJustPressed(KeyCode.KeypadEnter))
        {
            DestinationEntry? selected = this.GetSelectedEntry();
            if (selected is not null)
            {
                this.Hide();
                this.OnSelect?.Invoke(selected);
            }

            return;
        }

        if (input.IsKeyJustPressed(config.DeleteModifierKey) || input.IsKeyJustPressed(KeyCode.Delete) || input.IsKeyJustPressed(KeyCode.Backspace))
        {
            DestinationEntry? selected = this.GetSelectedEntry();
            if (selected is not null)
            {
                this.Hide();
                this.OnDelete?.Invoke(selected);
            }

            return;
        }

        if (input.IsKeyJustPressed(config.SaveModifierKey))
        {
            if (this.GetSelectedEntry() is not null)
                this.IsRebinding = true;

            return;
        }

        if (input.IsKeyJustPressed(KeyCode.RightArrow))
        {
            DestinationEntry? selected = this.GetSelectedEntry();
            if (selected is not null)
            {
                this.Hide();
                this.OnRename?.Invoke(selected);
            }

            return;
        }

        if (input.IsKeyJustPressed(KeyCode.LeftArrow))
        {
            this.ChangeUiScale();
            return;
        }

        int quickIndex = this.GetPressedQuickIndex(input);
        if (quickIndex >= 0)
        {
            int visibleIndex = this.GetFirstVisibleIndex(this.GetVisibleRowsPerColumn(this.GetOverlayHeight())) + quickIndex;
            if (visibleIndex >= this.Entries.Count)
                return;

            this.SelectedIndex = visibleIndex;
            this.Hide();
            this.OnSelect?.Invoke(this.Entries[visibleIndex]);
        }
    }

    /// <summary>Draw the overlay.</summary>
    [SuppressMessage("ReSharper", "UnusedMember.Global", Justification = "Unity loads the method dynamically.")]
    public void OnGUI()
    {
        if (!this.IsVisible)
            return;

        this.InitializeStyles();

        float margin = this.GetScreenMargin();
        float width = Math.Min(Math.Max(980f, Screen.width * 0.86f), Screen.width - margin);
        float height = this.GetOverlayHeight();
        int visibleRowsPerColumn = this.GetVisibleRowsPerColumn(height);
        float x = (Screen.width - width) / 2f;
        float y = (Screen.height - height) / 2f;

        Rect box = new(x, y, width, height);
        GUI.Box(box, GUIContent.none);

        GUILayout.BeginArea(new Rect(x + this.Scale(18f), y + this.Scale(14f), width - this.Scale(36f), height - this.Scale(28f)));
        GUILayout.Label(this.Title, this.TitleStyle);
        GUILayout.Space(this.Scale(8f));

        string returnPoint = this.ReturnPoint is not null
            ? $"返回点 [{this.FormatHotkey(this.ReturnPointKey)}]：{this.ReturnPoint.GetDisplayName(showRegion: true)}"
            : "返回点：未设置";
        GUILayout.Label(returnPoint, this.HelpStyle);
        GUILayout.Space(this.Scale(10f));

        if (this.Entries.Count == 0)
        {
            GUILayout.Label("还没有保存目的地。", this.RowStyle);
        }
        else
        {
            this.DrawDestinationColumns(width - this.Scale(36f), visibleRowsPerColumn);
        }

        GUILayout.FlexibleSpace();

        int maxVisibleDestinations = visibleRowsPerColumn * VisibleColumnCount;
        int pageIndex = this.Entries.Count > 0 ? this.SelectedIndex / maxVisibleDestinations : 0;
        int pageCount = Math.Max(1, (int)Math.Ceiling(this.Entries.Count / (float)maxVisibleDestinations));
        string scaleLabel = $"字号：{this.UiScale:0.#}x（标题 {this.ScaleFont(20)} / 列表 {this.ScaleFont(16)} / 提示 {this.ScaleFont(13)} / 第 {pageIndex + 1}/{pageCount} 页 / 每页最多 {maxVisibleDestinations} 条）";
        GUILayout.Label(scaleLabel, this.HelpStyle);

        if (this.IsRebinding)
            this.DrawHintRow("1-9 绑定", "Esc 取消");
        else
            this.DrawHintRow("↑↓选择", "1-9当前页", "PgUp/Dn翻页", "Enter前往", "→改名", "←字号", "+改绑", "-删除", "Esc关");
        GUILayout.EndArea();
    }


    /*********
    ** Private methods
    *********/
    /// <summary>Initialize GUI styles if needed.</summary>
    private void InitializeStyles()
    {
        if (this.TitleStyle is not null)
            return;

        this.TitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = this.ScaleFont(20),
            fontStyle = FontStyle.Bold,
            fixedHeight = this.Scale(TitleLineHeight),
            normal = { textColor = Color.white }
        };

        this.RowStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = this.ScaleFont(16),
            clipping = TextClipping.Clip,
            fixedHeight = this.Scale(DestinationRowHeight),
            wordWrap = false,
            normal = { textColor = new Color(0.82f, 0.82f, 0.78f) }
        };

        this.SelectedRowStyle = new GUIStyle(this.RowStyle)
        {
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(1f, 0.93f, 0.68f) }
        };

        this.HelpStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = this.ScaleFont(13),
            fixedHeight = this.Scale(HelpLineHeight),
            normal = { textColor = new Color(0.72f, 0.72f, 0.68f) }
        };

    }

    /// <summary>Draw saved destinations in two page-filled columns.</summary>
    /// <param name="availableWidth">The width available for the columns.</param>
    /// <param name="visibleRowsPerColumn">The number of rows to show in each column.</param>
    private void DrawDestinationColumns(float availableWidth, int visibleRowsPerColumn)
    {
        int startIndex = this.GetFirstVisibleIndex(visibleRowsPerColumn);
        int entriesOnPage = Math.Min(visibleRowsPerColumn * VisibleColumnCount, this.Entries.Count - startIndex);
        int visibleColumnCount = Math.Max(1, Math.Min(VisibleColumnCount, (int)Math.Ceiling(entriesOnPage / (float)visibleRowsPerColumn)));
        float columnGap = this.Scale(24f);
        float columnWidth = Math.Max(240f, (availableWidth - (columnGap * (VisibleColumnCount - 1))) / VisibleColumnCount);

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        for (int column = 0; column < visibleColumnCount; column++)
        {
            if (column > 0)
                GUILayout.Space(columnGap);

            GUILayout.BeginVertical(GUILayout.Width(columnWidth));
            for (int rowIndex = 0; rowIndex < visibleRowsPerColumn; rowIndex++)
            {
                int entryIndex = startIndex + (column * visibleRowsPerColumn) + rowIndex;
                if (entryIndex >= this.Entries.Count)
                    break;

                DestinationEntry entry = this.Entries[entryIndex];
                bool isSelected = entryIndex == this.SelectedIndex;
                string selector = isSelected ? ">" : " ";
                int indexOnPage = entryIndex - startIndex;
                string quickSelect = indexOnPage < 9 ? $"{indexOnPage + 1})" : "  ";
                string row = $"{selector} {quickSelect} #{entryIndex + 1} [{this.FormatHotkey(entry.Hotkey)}] {entry.GetDisplayName(showRegion: true)}";

                GUILayout.Label(row, isSelected ? this.SelectedRowStyle : this.RowStyle, GUILayout.Width(columnWidth));
            }

            GUILayout.EndVertical();
        }

        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
    }

    /// <summary>Scale a pixel size for high-resolution displays.</summary>
    /// <param name="value">The unscaled value.</param>
    private float Scale(float value)
    {
        return value * this.UiScale;
    }

    /// <summary>Scale a font size for high-resolution displays.</summary>
    /// <param name="value">The unscaled font size.</param>
    private int ScaleFont(int value)
    {
        return this.ScaleInt(value);
    }

    /// <summary>Scale an integer pixel size for high-resolution displays.</summary>
    /// <param name="value">The unscaled value.</param>
    private int ScaleInt(int value)
    {
        return (int)Math.Round(value * this.UiScale);
    }

    /// <summary>Move to the next UI scale step.</summary>
    private void ChangeUiScale()
    {
        this.UiScaleIndex = (this.UiScaleIndex + 1) % UiScaleSteps.Length;
        this.ResetStyles();
    }

    /// <summary>Set the largest default UI scale which still fits the preferred page size on the current screen.</summary>
    private void SetDefaultUiScaleForScreen()
    {
        int preferredIndex = this.GetUiScaleIndex(PreferredDefaultUiScale);
        for (int i = preferredIndex; i >= 0; i--)
        {
            if (this.GetVisibleRowsPerColumn(this.GetOverlayHeight(), UiScaleSteps[i]) >= PreferredVisibleRowsPerColumn)
            {
                this.UiScaleIndex = i;
                this.ResetStyles();
                return;
            }
        }

        this.UiScaleIndex = 0;
        this.ResetStyles();
    }

    /// <summary>Get the index for a UI scale value.</summary>
    /// <param name="scale">The scale to find.</param>
    private int GetUiScaleIndex(float scale)
    {
        for (int i = 0; i < UiScaleSteps.Length; i++)
        {
            if (Math.Abs(UiScaleSteps[i] - scale) < 0.01f)
                return i;
        }

        return UiScaleSteps.Length - 1;
    }

    /// <summary>Get the screen margin used by the outer list box.</summary>
    private float GetScreenMargin()
    {
        return Math.Max(40f, Math.Min(160f, Screen.width * 0.04f));
    }

    /// <summary>Get the outer list box height for the current screen.</summary>
    private float GetOverlayHeight()
    {
        return Math.Min(Math.Max(620f, Screen.height * 0.76f), Screen.height - this.GetScreenMargin());
    }

    /// <summary>Get the rows that can fit in each destination column at the current scale.</summary>
    /// <param name="boxHeight">The outer list box height.</param>
    private int GetVisibleRowsPerColumn(float boxHeight)
    {
        return this.GetVisibleRowsPerColumn(boxHeight, this.UiScale);
    }

    /// <summary>Get the rows that can fit in each destination column at a given scale.</summary>
    /// <param name="boxHeight">The outer list box height.</param>
    /// <param name="uiScale">The UI scale to test.</param>
    private int GetVisibleRowsPerColumn(float boxHeight, float uiScale)
    {
        float contentHeight = boxHeight - (ContentVerticalPadding * uiScale);
        float listHeight = contentHeight - (NonListContentHeight * uiScale);
        int rows = (int)Math.Floor(listHeight / (DestinationRowHeight * uiScale));
        return Math.Max(1, Math.Min(PreferredVisibleRowsPerColumn, rows));
    }

    /// <summary>Reset cached GUI styles so they're recreated with the current scale.</summary>
    private void ResetStyles()
    {
        this.TitleStyle = null;
        this.RowStyle = null;
        this.SelectedRowStyle = null;
        this.HelpStyle = null;
    }

    /// <summary>Draw a row of concise shortcut hints.</summary>
    /// <param name="hints">The hints to show.</param>
    private void DrawHintRow(params string[] hints)
    {
        GUILayout.BeginHorizontal();
        foreach (string hint in hints)
        {
            GUILayout.Label(hint, this.HelpStyle, GUILayout.ExpandWidth(false));
            GUILayout.Space(this.Scale(12f));
        }
        GUILayout.EndHorizontal();
    }

    /// <summary>Get the selected destination entry.</summary>
    private DestinationEntry? GetSelectedEntry()
    {
        return this.SelectedIndex >= 0 && this.SelectedIndex < this.Entries.Count
            ? this.Entries[this.SelectedIndex]
            : null;
    }

    /// <summary>Move the selected destination index.</summary>
    /// <param name="offset">The number of rows to move.</param>
    private void MoveSelection(int offset)
    {
        if (this.Entries.Count == 0)
            return;

        this.SelectedIndex = (this.SelectedIndex + offset + this.Entries.Count) % this.Entries.Count;
    }

    /// <summary>Move to another destination list page while preserving the selected position within the page if possible.</summary>
    /// <param name="offset">The number of pages to move.</param>
    private void MovePage(int offset)
    {
        if (this.Entries.Count == 0)
            return;

        int pageSize = this.GetMaxVisibleDestinations();
        int pageCount = Math.Max(1, (int)Math.Ceiling(this.Entries.Count / (float)pageSize));
        int pageIndex = Math.Min(this.SelectedIndex / pageSize, pageCount - 1);
        int indexInPage = this.SelectedIndex % pageSize;
        int targetPage = Math.Max(0, Math.Min(pageCount - 1, pageIndex + offset));

        this.SelectedIndex = Math.Min((targetPage * pageSize) + indexInPage, this.Entries.Count - 1);
    }

    /// <summary>Get the first entry index visible in the list.</summary>
    /// <param name="visibleRowsPerColumn">The number of rows shown in each column.</param>
    private int GetFirstVisibleIndex(int visibleRowsPerColumn)
    {
        int maxVisibleDestinations = visibleRowsPerColumn * VisibleColumnCount;
        if (this.Entries.Count <= maxVisibleDestinations)
            return 0;

        return (this.SelectedIndex / maxVisibleDestinations) * maxVisibleDestinations;
    }

    /// <summary>Get the maximum number of destinations shown on the current list page.</summary>
    private int GetMaxVisibleDestinations()
    {
        return this.GetVisibleRowsPerColumn(this.GetOverlayHeight()) * VisibleColumnCount;
    }

    /// <summary>Get the quick-select index pressed by the player.</summary>
    /// <param name="input">The input helper.</param>
    private int GetPressedQuickIndex(InteractionHelper input)
    {
        KeyCode[] alphaKeys =
        [
            KeyCode.Alpha1,
            KeyCode.Alpha2,
            KeyCode.Alpha3,
            KeyCode.Alpha4,
            KeyCode.Alpha5,
            KeyCode.Alpha6,
            KeyCode.Alpha7,
            KeyCode.Alpha8,
            KeyCode.Alpha9
        ];

        KeyCode[] keypadKeys =
        [
            KeyCode.Keypad1,
            KeyCode.Keypad2,
            KeyCode.Keypad3,
            KeyCode.Keypad4,
            KeyCode.Keypad5,
            KeyCode.Keypad6,
            KeyCode.Keypad7,
            KeyCode.Keypad8,
            KeyCode.Keypad9
        ];

        for (int i = 0; i < alphaKeys.Length; i++)
        {
            if (input.IsKeyJustPressed(alphaKeys[i]) || input.IsKeyJustPressed(keypadKeys[i]))
                return i;
        }

        return -1;
    }

    /// <summary>Get a player-facing hotkey label.</summary>
    /// <param name="hotkey">The hotkey to display.</param>
    private string FormatHotkey(KeyCode hotkey)
    {
        return hotkey == KeyCode.None
            ? "未绑定"
            : hotkey.ToString();
    }

    /// <summary>Get the destination hotkey pressed by the player.</summary>
    /// <param name="input">The input helper.</param>
    /// <param name="config">The mod settings.</param>
    /// <param name="hotkey">The hotkey that was pressed.</param>
    private bool TryGetPressedDestinationKey(InteractionHelper input, ModConfig config, out KeyCode hotkey)
    {
        foreach (KeyCode key in config.GetDestinationKeys().Distinct())
        {
            if (key != KeyCode.None && input.IsKeyJustPressed(key))
            {
                hotkey = key;
                return true;
            }
        }

        hotkey = KeyCode.None;
        return false;
    }
}
