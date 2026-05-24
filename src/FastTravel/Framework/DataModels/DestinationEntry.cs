using System;
using UnityEngine;

namespace Pathoschild.TheLongDarkMods.FastTravel.Framework.DataModels;

/// <summary>A player-saved fast travel destination with player-facing metadata.</summary>
internal class DestinationEntry
{
    /*********
    ** Accessors
    *********/
    /// <summary>The stable ID for this saved destination.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>The hotkey which quickly opens this destination.</summary>
    public KeyCode Hotkey { get; set; } = KeyCode.None;

    /// <summary>The custom player-facing name, if set.</summary>
    public string? CustomName { get; set; }

    /// <summary>The saved location.</summary>
    public Destination Location { get; set; } = null!;


    /*********
    ** Public methods
    *********/
    /// <summary>Get the destination's display name.</summary>
    /// <param name="showRegion">Whether to include the region name, or <c>null</c> to show it if different from the player's current region.</param>
    public string GetDisplayName(bool? showRegion = false)
    {
        return !string.IsNullOrWhiteSpace(this.CustomName)
            ? this.CustomName
            : this.Location.GetDisplayName(showRegion);
    }
}
