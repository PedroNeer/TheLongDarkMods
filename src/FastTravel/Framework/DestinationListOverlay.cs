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
    /// <summary>The maximum number of destination rows shown at once.</summary>
    private const int MaxVisibleDestinations = 9;

    /// <summary>The pixel scaling to apply to the destination list UI.</summary>
    private static readonly float[] UiScaleSteps = [1f, 1.5f, 2f, 2.5f, 3f, 3.5f, 4f, 4.5f, 5f];

    /// <summary>The maximum length for a custom destination name.</summary>
    private const int MaxCustomNameLength = 80;

    /// <summary>The GUI control name for the rename text field.</summary>
    private const string RenameFieldName = "FastTravelRenameField";

    /// <summary>The entries shown in the list.</summary>
    private readonly List<DestinationEntry> Entries = [];

    /// <summary>The current title text.</summary>
    private string Title = "快速旅行目的地";

    /// <summary>The selected UI scale step.</summary>
    private int UiScaleIndex = 4; // default to 3x for 4K testing

    /// <summary>The current return point.</summary>
    private Destination? ReturnPoint;

    /// <summary>The configured return point hotkey.</summary>
    private KeyCode ReturnPointKey;

    /// <summary>The selected entry index.</summary>
    private int SelectedIndex;

    /// <summary>Whether the next destination hotkey should rebind the selected entry.</summary>
    private bool IsRebinding;

    /// <summary>Whether the selected entry is being renamed.</summary>
    private bool IsRenaming;

    /// <summary>The custom name currently being edited.</summary>
    private string RenameText = "";

    /// <summary>Whether the rename text field should be focused on the next draw.</summary>
    private bool ShouldFocusRenameField;

    /// <summary>The callback to invoke when an entry is selected.</summary>
    private Action<DestinationEntry>? OnSelect;

    /// <summary>The callback to invoke when an entry should be deleted.</summary>
    private Action<DestinationEntry>? OnDelete;

    /// <summary>The callback to invoke when an entry should be rebound to a new hotkey.</summary>
    private Action<DestinationEntry, KeyCode>? OnRebind;

    /// <summary>The callback to invoke when an entry should be renamed.</summary>
    private Action<DestinationEntry, string?>? OnRename;

    /// <summary>The title label style.</summary>
    private GUIStyle? TitleStyle;

    /// <summary>The list label style.</summary>
    private GUIStyle? RowStyle;

    /// <summary>The selected list label style.</summary>
    private GUIStyle? SelectedRowStyle;

    /// <summary>The help label style.</summary>
    private GUIStyle? HelpStyle;

    /// <summary>The text field style.</summary>
    private GUIStyle? TextFieldStyle;


    /*********
    ** Accessors
    *********/
    /// <summary>Whether the overlay is currently visible.</summary>
    internal bool IsVisible { get; private set; }

    /// <summary>The selected UI scale.</summary>
    private float UiScale => UiScaleSteps[this.UiScaleIndex];


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
    internal void Show(string title, IEnumerable<DestinationEntry> entries, Destination? returnPoint, KeyCode returnPointKey, Action<DestinationEntry> onSelect, Action<DestinationEntry> onDelete, Action<DestinationEntry, KeyCode> onRebind, Action<DestinationEntry, string?> onRename)
    {
        this.Title = title;
        this.ReturnPoint = returnPoint;
        this.ReturnPointKey = returnPointKey;
        this.OnSelect = onSelect;
        this.OnDelete = onDelete;
        this.OnRebind = onRebind;
        this.OnRename = onRename;

        this.Entries.Clear();
        this.Entries.AddRange(entries.Where(entry => entry.Location is not null));

        this.SelectedIndex = Math.Min(this.SelectedIndex, Math.Max(this.Entries.Count - 1, 0));
        this.IsRebinding = false;
        this.IsRenaming = false;
        this.IsVisible = true;
    }

    /// <summary>Hide the overlay.</summary>
    internal void Hide()
    {
        this.IsVisible = false;
        this.IsRebinding = false;
        this.IsRenaming = false;
    }

    /// <summary>Handle a key press while the overlay is open.</summary>
    /// <param name="input">The input helper.</param>
    /// <param name="config">The mod settings.</param>
    internal void HandleInput(InteractionHelper input, ModConfig config)
    {
        if (!this.IsVisible)
            return;

        if (this.IsRenaming)
        {
            if (input.IsKeyJustPressed(KeyCode.Escape))
            {
                this.IsRenaming = false;
                return;
            }

            if (input.IsKeyJustPressed(KeyCode.Return) || input.IsKeyJustPressed(KeyCode.KeypadEnter))
            {
                this.CommitRename();
                return;
            }

            return;
        }

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
            this.BeginRename();
            return;
        }

        if (input.IsKeyJustPressed(KeyCode.LeftArrow))
        {
            this.ChangeUiScale();
            return;
        }

        int quickIndex = this.GetPressedQuickIndex(input);
        if (quickIndex >= 0 && quickIndex < this.Entries.Count)
        {
            this.SelectedIndex = quickIndex;
            this.Hide();
            this.OnSelect?.Invoke(this.Entries[quickIndex]);
        }
    }

    /// <summary>Draw the overlay.</summary>
    [SuppressMessage("ReSharper", "UnusedMember.Global", Justification = "Unity loads the method dynamically.")]
    public void OnGUI()
    {
        if (!this.IsVisible)
            return;

        this.InitializeStyles();

        float margin = this.Scale(40f);
        float width = Math.Min(this.Scale(760f), Screen.width - margin);
        float height = Math.Min(this.Scale(440f), Screen.height - margin);
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
            int startIndex = this.GetFirstVisibleIndex();
            int endIndex = Math.Min(startIndex + MaxVisibleDestinations, this.Entries.Count);

            for (int i = startIndex; i < endIndex; i++)
            {
                DestinationEntry entry = this.Entries[i];
                bool isSelected = i == this.SelectedIndex;
                string selector = isSelected ? ">" : " ";
                string row = $"{selector} {i + 1}. [{this.FormatHotkey(entry.Hotkey)}] {entry.GetDisplayName(showRegion: true)}";

                GUILayout.Label(row, isSelected ? this.SelectedRowStyle : this.RowStyle);
            }
        }

        GUILayout.FlexibleSpace();

        string scaleLabel = $"字号：{this.UiScale:0.#}x（标题 {this.ScaleFont(20)} / 列表 {this.ScaleFont(16)} / 提示 {this.ScaleFont(13)}）";
        GUILayout.Label(scaleLabel, this.HelpStyle);

        if (this.IsRenaming)
        {
            GUILayout.Label("输入新名称（留空使用默认地点名）：", this.HelpStyle);
            GUI.SetNextControlName(RenameFieldName);
            this.RenameText = GUILayout.TextField(this.RenameText, MaxCustomNameLength, this.TextFieldStyle!, GUILayout.Height(this.Scale(32f)));
            if (this.ShouldFocusRenameField)
            {
                GUI.FocusControl(RenameFieldName);
                this.ShouldFocusRenameField = false;
            }

            GUILayout.Label("Enter保存  Esc取消", this.HelpStyle);
        }
        else
        {
            if (this.IsRebinding)
                this.DrawHintRow("1-9 绑定", "Esc 取消");
            else
            {
                this.DrawHintRow("↑↓ 选择", "Enter 前往", "→ 改名", "← 字号");
                this.DrawHintRow("+ 改绑", "-/Del 删除", "Esc 关闭");
            }
        }
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
            normal = { textColor = Color.white }
        };

        this.RowStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = this.ScaleFont(16),
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
            normal = { textColor = new Color(0.72f, 0.72f, 0.68f) }
        };

        this.TextFieldStyle = new GUIStyle(GUI.skin.textField)
        {
            fontSize = this.ScaleFont(16),
            padding = new RectOffset(this.ScaleInt(6), this.ScaleInt(6), this.ScaleInt(4), this.ScaleInt(4))
        };
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

    /// <summary>Reset cached GUI styles so they're recreated with the current scale.</summary>
    private void ResetStyles()
    {
        this.TitleStyle = null;
        this.RowStyle = null;
        this.SelectedRowStyle = null;
        this.HelpStyle = null;
        this.TextFieldStyle = null;
    }

    /// <summary>Draw a row of concise shortcut hints.</summary>
    /// <param name="hints">The hints to show.</param>
    private void DrawHintRow(params string[] hints)
    {
        GUILayout.BeginHorizontal();
        foreach (string hint in hints)
        {
            GUILayout.Label(hint, this.HelpStyle, GUILayout.ExpandWidth(false));
            GUILayout.Space(this.Scale(18f));
        }
        GUILayout.EndHorizontal();
    }

    /// <summary>Start renaming the selected destination.</summary>
    private void BeginRename()
    {
        DestinationEntry? selected = this.GetSelectedEntry();
        if (selected is null)
            return;

        this.IsRenaming = true;
        this.RenameText = selected.CustomName ?? "";
        this.ShouldFocusRenameField = true;
    }

    /// <summary>Save the current rename text.</summary>
    private void CommitRename()
    {
        DestinationEntry? selected = this.GetSelectedEntry();
        if (selected is null)
            return;

        this.IsRenaming = false;
        this.OnRename?.Invoke(selected, this.RenameText);
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

    /// <summary>Get the first entry index visible in the list.</summary>
    private int GetFirstVisibleIndex()
    {
        if (this.Entries.Count <= MaxVisibleDestinations)
            return 0;

        int first = this.SelectedIndex - MaxVisibleDestinations + 1;
        if (first < 0)
            return 0;

        int maxFirst = this.Entries.Count - MaxVisibleDestinations;
        return Math.Min(first, maxFirst);
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
