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
    [Section("Main options")]
    [Name("Can travel")]
    [Description("Whether fast traveling is enabled at all.\n\nDisable if you only want to fast travel in specific cases (e.g. when transferring from one base to another).")]
    public bool CanTravel = true;

    [Name("Can edit destinations")]
    [Description("Whether you can edit your fast travel destinations.\n\nDisabling it after you've set up your options can avoid accidental changes.")]
    public bool CanEditDestinations = true;

    /****
    ** Restrict by location
    ****/
    [Section("Restrict by location")]
    [Name("Can travel from outside")]
    [Description("Whether you can fast travel while you're outside.\n\nDisable to avoid the temptation of escaping risky situations with fast travel.")]
    public bool CanTravelFromOutside = true;

    [Name("Can travel from non-safehouse interior")]
    [Description("Whether you can fast travel from non-customizable interiors like caves.")]
    public bool CanTravelFromNonSafehouseInterior = true;

    [Name("Can travel from non-saved location")]
    [Description("Whether you can fast travel from a location that doesn't contain a fast travel point.\n\nDisable if you only want point-to-point fast travel (e.g. between saved home bases).")]
    public bool CanTravelFromNonFastTravelPoint = true;

    [Name("Can travel within same location")]
    [Description("Whether you can fast travel from one point to another in the same location.")]
    public bool CanTravelWithinScene = true;

    [Name("Can travel while under attack")]
    [Description("Whether you can fast travel while hostile animals are attacking, stalking, or following you.")]
    public bool CanTravelWhileUnderAttack = true;

    /****
    ** Restrict by weather
    ****/
    [Section("Restrict by weather")]
    [Name("Can travel during aurora")]
    [Description("Whether you can fast travel during an aurora.")]
    public bool CanTravelDuringAurora = true;

    [Name("Can travel through dense fog")]
    [Description("Whether you can fast travel during dense fog in your departure region.")]
    public bool CanTravelDuringDenseFog = true;

    [Name("Can travel through glimmer fog")]
    [Description("Whether you can fast travel during glimmer fog in your departure region.")]
    public bool CanTravelDuringGlimmerFog = true;

    [Name("Can travel through light snowfall")]
    [Description("Whether you can fast travel during normal snowfall in your departure region.")]
    public bool CanTravelDuringLightSnowfall = true;

    [Name("Can travel through heavy snowfall")]
    [Description("Whether you can fast travel during heavy snowfall in your departure region.")]
    public bool CanTravelDuringHeavySnowfall = true;

    [Name("Can travel through blizzard")]
    [Description("Whether you can fast travel during a blizzard in your departure region.")]
    public bool CanTravelDuringBlizzard = true;

    /****
    ** Modifier keys
    ****/
    [Section("Modifier keys")]
    [Name("Save destination")]
    [Description("Save your current place as a fast travel destination by holding this key, then pressing the destination key below you want to bind it to.")]
    public KeyCode SaveModifierKey = KeyCode.KeypadPlus;

    [Name("Forget destination")]
    [Description("Forget a destination by holding this key, then pressing the destination key below you want to delete.")]
    public KeyCode DeleteModifierKey = KeyCode.KeypadMinus;

    /****
    ** Fast travel keys
    ****/
    [Section("Fast travel keys")]
    [Name("Show destination list")]
    [Description("Press this button to open your saved destination list.")]
    public KeyCode ShowListKey = KeyCode.KeypadPeriod;

    [Name("Fast travel point 1")]
    [Description("Press this button to fast travel to the destination bound to this key. You can add or change bindings using the modifier keys above.")]
    public KeyCode Destination1 = KeyCode.Keypad1;

    [Name("Fast travel point 2")]
    [Description("Press this button to fast travel to the destination bound to this key. You can add or change bindings using the modifier keys above.")]
    public KeyCode Destination2 = KeyCode.Keypad2;

    [Name("Fast travel point 3")]
    [Description("Press this button to fast travel to the destination bound to this key. You can add or change bindings using the modifier keys above.")]
    public KeyCode Destination3 = KeyCode.Keypad3;

    [Name("Fast travel point 4")]
    [Description("Press this button to fast travel to the destination bound to this key. You can add or change bindings using the modifier keys above.")]
    public KeyCode Destination4 = KeyCode.Keypad4;

    [Name("Fast travel point 5")]
    [Description("Press this button to fast travel to the destination bound to this key. You can add or change bindings using the modifier keys above.")]
    public KeyCode Destination5 = KeyCode.Keypad5;

    [Name("Fast travel point 6")]
    [Description("Press this button to fast travel to the destination bound to this key. You can add or change bindings using the modifier keys above.")]
    public KeyCode Destination6 = KeyCode.Keypad6;

    [Name("Fast travel point 7")]
    [Description("Press this button to fast travel to the destination bound to this key. You can add or change bindings using the modifier keys above.")]
    public KeyCode Destination7 = KeyCode.Keypad7;

    [Name("Fast travel point 8")]
    [Description("Press this button to fast travel to the destination bound to this key. You can add or change bindings using the modifier keys above.")]
    public KeyCode Destination8 = KeyCode.Keypad8;

    [Name("Fast travel point 9")]
    [Description("Press this button to fast travel to the destination bound to this key. You can add or change bindings using the modifier keys above.")]
    public KeyCode Destination9 = KeyCode.Keypad9;

    [Name("Return to previous location")]
    [Description("Press this button to return to where you were before your *most recent* fast travel.")]
    public KeyCode ReturnPointKey = KeyCode.Keypad0;

    /****
    ** Other
    ****/
    [Section("Other")]
    [Name("Show usage hints")]
    [Description("Whether in-game messages should include usage hints like \"You can return here later by pressing <key>\".\n\nDisable if you're familiar with the mod and want less immersion-breaking messages.")]
    public bool ShowUsageHints = true;

    [Name("Log debug info")]
    [Description("Whether to log debug information about scene transitions and fast travel. This is meant for troubleshooting, and has no effect on the in-game behavior.")]
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
