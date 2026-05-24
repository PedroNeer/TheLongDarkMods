using System;
using System.Collections.Generic;
using ModSettings;
using UnityEngine;

namespace Pathoschild.TheLongDarkMods.FastTravel.Framework;

/// <summary>The mod config model.</summary>
internal class ModConfig : JsonModSettings
{
    /*********
    ** Fields
    *********/
    /// <summary>The number of legacy destination hotkeys exposed in the mod settings.</summary>
    public const int MaxLegacyDestinationKeys = 9;


    /*********
    ** Accessors
    *********/
    /****
    ** Main options
    ****/
    [Section("主要选项")]
    [Name("允许快速旅行")]
    [Description("是否启用快速旅行。\n\n如果你只想在特定情况下使用快速旅行，可以关闭此项。")]
    public bool CanTravel = true;

    [Name("允许编辑目的地")]
    [Description("是否允许编辑快速旅行目的地。\n\n设置好目的地后可以关闭此项，避免误操作。")]
    public bool CanEditDestinations = true;

    /****
    ** Restrict by location
    ****/
    [Section("地点限制")]
    [Name("允许从室外出发")]
    [Description("是否允许在室外快速旅行。\n\n关闭后可避免在危险情况下用快速旅行脱身。")]
    public bool CanTravelFromOutside = true;

    [Name("允许从非安全屋室内出发")]
    [Description("是否允许从洞穴等不可自定义的室内地点快速旅行。")]
    public bool CanTravelFromNonSafehouseInterior = true;

    [Name("允许从未保存地点出发")]
    [Description("是否允许从没有快速旅行点的地点出发。\n\n如果你只想在已保存地点之间点对点旅行，可以关闭此项。")]
    public bool CanTravelFromNonFastTravelPoint = true;

    [Name("允许同一地点内旅行")]
    [Description("是否允许在同一个场景内从一个点快速旅行到另一个点。")]
    public bool CanTravelWithinScene = true;

    [Name("允许受攻击时旅行")]
    [Description("是否允许在敌对动物攻击、跟踪或追随你时快速旅行。")]
    public bool CanTravelWhileUnderAttack = true;

    /****
    ** Restrict by weather
    ****/
    [Section("天气限制")]
    [Name("允许极光期间旅行")]
    [Description("是否允许在极光期间快速旅行。")]
    public bool CanTravelDuringAurora = true;

    [Name("允许浓雾中旅行")]
    [Description("是否允许在出发区域有浓雾时快速旅行。")]
    public bool CanTravelDuringDenseFog = true;

    [Name("允许闪光雾中旅行")]
    [Description("是否允许在出发区域有闪光雾时快速旅行。")]
    public bool CanTravelDuringGlimmerFog = true;

    [Name("允许小雪中旅行")]
    [Description("是否允许在出发区域正常降雪时快速旅行。")]
    public bool CanTravelDuringLightSnowfall = true;

    [Name("允许大雪中旅行")]
    [Description("是否允许在出发区域大雪时快速旅行。")]
    public bool CanTravelDuringHeavySnowfall = true;

    [Name("允许暴风雪中旅行")]
    [Description("是否允许在出发区域暴风雪时快速旅行。")]
    public bool CanTravelDuringBlizzard = true;

    /****
    ** Modifier keys
    ****/
    [Section("组合键")]
    [Name("保存目的地")]
    [Description("按住此键，再按下面的目的地快捷键，可将当前位置保存为新目的地并绑定到该快捷键。\n\n如果该快捷键已有目的地，会改绑到新目的地；旧目的地仍保留在列表中。")]
    public KeyCode SaveModifierKey = KeyCode.KeypadPlus;

    [Name("删除目的地")]
    [Description("按住此键，再按下面的目的地快捷键，可删除绑定到该快捷键的目的地。")]
    public KeyCode DeleteModifierKey = KeyCode.KeypadMinus;

    /****
    ** Fast travel keys
    ****/
    [Section("目的地快捷键")]
    [Name("打开目的地列表")]
    [Description("按此键打开已保存目的地列表。")]
    public KeyCode ShowListKey = KeyCode.KeypadPeriod;

    [Name("目的地快捷键 1")]
    [Description("按此键前往当前绑定的目的地。可使用上方组合键保存新目的地或更改绑定。")]
    public KeyCode Destination1 = KeyCode.Keypad1;

    [Name("目的地快捷键 2")]
    [Description("按此键前往当前绑定的目的地。可使用上方组合键保存新目的地或更改绑定。")]
    public KeyCode Destination2 = KeyCode.Keypad2;

    [Name("目的地快捷键 3")]
    [Description("按此键前往当前绑定的目的地。可使用上方组合键保存新目的地或更改绑定。")]
    public KeyCode Destination3 = KeyCode.Keypad3;

    [Name("目的地快捷键 4")]
    [Description("按此键前往当前绑定的目的地。可使用上方组合键保存新目的地或更改绑定。")]
    public KeyCode Destination4 = KeyCode.Keypad4;

    [Name("目的地快捷键 5")]
    [Description("按此键前往当前绑定的目的地。可使用上方组合键保存新目的地或更改绑定。")]
    public KeyCode Destination5 = KeyCode.Keypad5;

    [Name("目的地快捷键 6")]
    [Description("按此键前往当前绑定的目的地。可使用上方组合键保存新目的地或更改绑定。")]
    public KeyCode Destination6 = KeyCode.Keypad6;

    [Name("目的地快捷键 7")]
    [Description("按此键前往当前绑定的目的地。可使用上方组合键保存新目的地或更改绑定。")]
    public KeyCode Destination7 = KeyCode.Keypad7;

    [Name("目的地快捷键 8")]
    [Description("按此键前往当前绑定的目的地。可使用上方组合键保存新目的地或更改绑定。")]
    public KeyCode Destination8 = KeyCode.Keypad8;

    [Name("目的地快捷键 9")]
    [Description("按此键前往当前绑定的目的地。可使用上方组合键保存新目的地或更改绑定。")]
    public KeyCode Destination9 = KeyCode.Keypad9;

    [Name("返回上一个位置")]
    [Description("按此键返回你最近一次快速旅行前所在的位置。")]
    public KeyCode ReturnPointKey = KeyCode.Keypad0;

    /****
    ** Other
    ****/
    [Section("其他")]
    [Name("显示使用提示")]
    [Description("游戏内消息是否显示类似“之后可以按某个按键回到这里”的使用提示。\n\n熟悉本模组后可关闭，以减少打破沉浸感的消息。")]
    public bool ShowUsageHints = true;

    [Name("记录调试信息")]
    [Description("是否记录场景切换和快速旅行相关调试信息。此选项用于排查问题，不影响游戏内行为。")]
    public bool LogDebugInfo = false;


    /*********
    ** Public methods
    *********/
    /// <summary>Get the configured destination hotkey for a legacy slot.</summary>
    /// <param name="slotIndex">The legacy slot index.</param>
    public KeyCode GetDestinationKey(int slotIndex)
    {
        return slotIndex switch
        {
            0 => this.Destination1,
            1 => this.Destination2,
            2 => this.Destination3,
            3 => this.Destination4,
            4 => this.Destination5,
            5 => this.Destination6,
            6 => this.Destination7,
            7 => this.Destination8,
            8 => this.Destination9,
            _ => throw new InvalidOperationException($"Unsupported destination slot {slotIndex}.")
        };
    }

    /// <summary>Get all configured destination hotkeys.</summary>
    public IEnumerable<KeyCode> GetDestinationKeys()
    {
        for (int i = 0; i < MaxLegacyDestinationKeys; i++)
            yield return this.GetDestinationKey(i);
    }
}
