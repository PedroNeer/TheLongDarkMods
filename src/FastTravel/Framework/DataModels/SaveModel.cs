using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Pathoschild.TheLongDarkMods.FastTravel.Framework.DataModels;

/// <summary>The data model for data persisted to ModData.</summary>
internal class SaveModel
{
    /*********
    ** Accessors
    *********/
    /// <summary>The mod version which saved this data.</summary>
    public string? Version { get; set; }

    /// <summary>The player's location before their most recent fast travel.</summary>
    public Destination? ReturnPoint { get; set; }

    /// <summary>The saved destinations.</summary>
    public List<DestinationEntry> Destinations { get; set; } = [];


    /*********
    ** Public methods
    *********/
    /// <summary>Get a saved destination entry by ID, if it exists.</summary>
    /// <param name="id">The destination entry ID.</param>
    public DestinationEntry? Get(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("The destination entry ID can't be empty.", nameof(id));

        return this.Destinations.FirstOrDefault(entry => entry.Id == id);
    }

    /// <summary>Get the saved destination entry bound to a hotkey.</summary>
    /// <param name="hotkey">The hotkey to match.</param>
    public DestinationEntry? GetByHotkey(KeyCode hotkey)
    {
        if (hotkey == KeyCode.None)
            return null;

        return this.Destinations.LastOrDefault(entry => entry.Hotkey == hotkey);
    }

    /// <summary>Add a new saved destination entry.</summary>
    /// <param name="entry">The destination entry to add.</param>
    public void Add(DestinationEntry entry)
    {
        if (entry.Location is null)
            throw new ArgumentException("The destination entry must have a location.", nameof(entry));

        if (string.IsNullOrWhiteSpace(entry.Id))
            entry.Id = Guid.NewGuid().ToString("N");

        this.Destinations.Add(entry);
    }

    /// <summary>Bind an entry to a hotkey, unbinding any other entry currently using it.</summary>
    /// <param name="entry">The destination entry to bind.</param>
    /// <param name="hotkey">The hotkey to bind.</param>
    public void BindHotkey(DestinationEntry entry, KeyCode hotkey)
    {
        if (hotkey != KeyCode.None)
        {
            foreach (DestinationEntry saved in this.Destinations)
            {
                if (saved.Id != entry.Id && saved.Hotkey == hotkey)
                    saved.Hotkey = KeyCode.None;
            }
        }

        entry.Hotkey = hotkey;
    }

    /// <summary>Remove a saved destination entry.</summary>
    /// <param name="entry">The destination entry to remove.</param>
    public void Remove(DestinationEntry entry)
    {
        this.Destinations.RemoveAll(saved => saved.Id == entry.Id);
    }
}
