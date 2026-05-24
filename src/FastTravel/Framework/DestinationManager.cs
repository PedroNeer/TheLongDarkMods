using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Il2Cpp;
using MelonLoader;
using ModData;
using Pathoschild.TheLongDarkMods.Common;
using Pathoschild.TheLongDarkMods.FastTravel.Framework.DataModels;
using UnityEngine;

namespace Pathoschild.TheLongDarkMods.FastTravel.Framework;

/// <summary>Tracks persisted destination endpoints.</summary>
internal class DestinationManager
{
    /*********
    ** Fields
    *********/
    /// <summary>The log instance.</summary>
    private readonly MelonLogger.Instance Log;

    /// <summary>The mod settings.</summary>
    private readonly ModConfig Config;

    /// <summary>The JSON options for save data.</summary>
    private readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };


    /*********
    ** Public methods
    *********/
    /// <summary>Construct an instance.</summary>
    /// <param name="log">The log instance.</param>
    /// <param name="config">The mod settings.</param>
    public DestinationManager(MelonLogger.Instance log, ModConfig config)
    {
        this.Log = log;
        this.Config = config;
    }

    /// <summary>Get the saved data on disk.</summary>
    public SaveModel GetData()
    {
        ModDataManager dataManager = this.CreateDataManager();
        SaveModel? data = this.DeserializeRaw(dataManager.Load());

        return data ?? new SaveModel();
    }

    /// <summary>Save data back to disk.</summary>
    /// <param name="data">The data to save.</param>
    public void SaveData(SaveModel data)
    {
        data.Version = ModInfo.Version;

        this
            .CreateDataManager()
            .Save(this.Serialize(data));
    }

    /// <summary>Get the destination info for the player's current position.</summary>
    public Destination GetCurrentLocation()
    {
        vp_FPSCamera camera = GameManager.GetVpFPSCamera();
        Transform player = GameManager.GetPlayerObject().transform;

        return new Destination(
            region: SceneHelper.TryGetRegion(),
            scene: SceneHelper.GetScene(),
            position: player.position,
            cameraPitch: camera.m_Pitch,
            cameraYaw: camera.m_Yaw,
            lastTransition: GameManager.m_SceneTransitionData
        );
    }


    /*********
    ** Private methods
    *********/
    /// <summary>Create a mod data manager.</summary>
    private ModDataManager CreateDataManager()
    {
        return new ModDataManager("FastTravel");
    }

    /// <summary>Deserialize raw data into the data model, if it's valid.</summary>
    /// <param name="rawData">The raw data to deserialize.</param>
    private SaveModel? DeserializeRaw(string? rawData)
    {
        if (rawData is not null)
        {
            try
            {
                SaveModel? data = JsonSerializer.Deserialize<SaveModel>(rawData, this.JsonOptions);
                if (data?.Destinations != null)
                {
                    this.Normalize(data);
                    return data;
                }
            }
            catch (JsonException)
            {
                try
                {
                    LegacySaveModel? data = JsonSerializer.Deserialize<LegacySaveModel>(rawData, this.JsonOptions);
                    if (data?.Destinations != null)
                        return this.MigrateLegacy(data);
                }
                catch (JsonException ex)
                {
                    this.Log.Error("Can't load saved destinations; the data will be reset.", ex);
                }
            }
        }

        return null;
    }

    /// <summary>Serialize a data model into raw data.</summary>
    /// <param name="data">The data to serialize.</param>
    private string Serialize(SaveModel data)
    {
        return JsonSerializer.Serialize(data, this.JsonOptions);
    }

    /// <summary>Normalize the save model after loading it from disk.</summary>
    /// <param name="data">The data to normalize.</param>
    private void Normalize(SaveModel data)
    {
        data.Destinations.RemoveAll(entry => entry.Location is null);

        foreach (DestinationEntry entry in data.Destinations)
        {
            if (string.IsNullOrWhiteSpace(entry.Id))
                entry.Id = System.Guid.NewGuid().ToString("N");
        }

        HashSet<KeyCode> usedHotkeys = [];
        for (int i = data.Destinations.Count - 1; i >= 0; i--)
        {
            DestinationEntry entry = data.Destinations[i];
            if (entry.Hotkey == KeyCode.None)
                continue;

            if (!usedHotkeys.Add(entry.Hotkey))
                entry.Hotkey = KeyCode.None;
        }
    }

    /// <summary>Migrate legacy fixed-slot save data to the destination list model.</summary>
    /// <param name="legacy">The legacy save data.</param>
    private SaveModel MigrateLegacy(LegacySaveModel legacy)
    {
        SaveModel data = new()
        {
            Version = legacy.Version,
            ReturnPoint = legacy.ReturnPoint
        };

        foreach (KeyValuePair<int, Destination> pair in legacy.Destinations.OrderBy(p => p.Key))
        {
            KeyCode hotkey = pair.Key is >= 0 and < ModConfig.MaxLegacyDestinationKeys
                ? this.Config.GetDestinationKey(pair.Key)
                : KeyCode.None;

            data.Add(new DestinationEntry
            {
                Id = $"legacy-slot-{pair.Key}",
                Hotkey = hotkey,
                Location = pair.Value
            });
        }

        return data;
    }

    /// <summary>The legacy data model persisted by Fast Travel 0.3.1 and earlier.</summary>
    private class LegacySaveModel
    {
        /// <summary>The mod version which saved this data.</summary>
        public string? Version { get; set; }

        /// <summary>The player's location before their most recent fast travel.</summary>
        public Destination? ReturnPoint { get; set; }

        /// <summary>The saved destinations keyed by fixed slot index.</summary>
        public Dictionary<int, Destination> Destinations { get; set; } = [];
    }
}
