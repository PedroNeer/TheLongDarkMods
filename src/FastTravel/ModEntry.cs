using System;
using System.Collections.Generic;
using System.Linq;
using Il2Cpp;
using MelonLoader;
using Pathoschild.TheLongDarkMods.Common;
using Pathoschild.TheLongDarkMods.FastTravel.Framework;
using Pathoschild.TheLongDarkMods.FastTravel.Framework.DataModels;
using Pathoschild.TheLongDarkMods.FastTravel.Framework.Patches;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Pathoschild.TheLongDarkMods.FastTravel;

/// <inheritdoc />
public class ModEntry : MelonMod
{
    /*********
    ** Fields
    *********/
    /// <summary>The furthest map landmark to use for generated destination names.</summary>
    private const float MaxGeneratedNameLandmarkDistance = 250f;

    /// <summary>The distance within which the destination is considered to be at a map landmark.</summary>
    private const float LandmarkNameOnlyDistance = 50f;


    /// <summary>The mod settings.</summary>
    private readonly ModConfig Config = new();

    /// <summary>The log instance.</summary>
    private MelonLogger.Instance Log = null!; // set in OnInitializeMelon

    /// <summary>Tracks the persisted destinations.</summary>
    private DestinationManager DestinationManager = null!; // set in OnInitializeMelon

    /// <summary>Provides utility methods for reading input and showing UI.</summary>
    private InteractionHelper InteractionHelper = null!; // set in OnInitializeMelon

    /// <summary>Handles checking for fast travel restrictions.</summary>
    private FastTravelRestrictionHelper FastTravelRestrictions = null!; // set in OnInitializeMelon

    /// <summary>The player's ongoing fast travel transition, if they haven't arrived yet.</summary>
    private FastTravelTransition? FastTravel;

    /// <summary>The destination info to show to the player on arrival.</summary>
    /// <remarks>This is cached temporarily for the location name shown on-screen when the player arrives.</remarks>
    private Destination? ShowOnArrival;

    /// <summary>An overlay which lists available fast travel destinations.</summary>
    private DestinationListOverlay DestinationListOverlay = null!; // set in OnInitializeMelon


    /*********
    ** Public methods
    *********/
    /// <inheritdoc />
    public override void OnInitializeMelon()
    {
        this.Log = Melon<ModEntry>.Logger;
        this.DestinationManager = new DestinationManager(this.Log, this.Config);
        this.InteractionHelper = new InteractionHelper(this.Log);
        this.DestinationListOverlay = DestinationListOverlay.Create();
        this.FastTravelRestrictions = new FastTravelRestrictionHelper(this.Config);

        PanelHudPatches.Initialize(this.Log, this.ConsumeDestinationOnArrival);

        this.Config.AddToModSettings(ModInfo.DisplayName);
    }

    /// <inheritdoc />
    public override void OnUpdate()
    {
        // hide overlay on exit
        if (this.DestinationListOverlay.IsVisible && !SceneHelper.IsSaveLoaded())
            this.DestinationListOverlay.Hide();

        if (!SceneHelper.IsSaveLoaded())
            return;

        if (this.DestinationListOverlay.IsVisible)
        {
            if (this.InteractionHelper.IsKeyJustPressed(this.Config.ShowListKey))
                this.DestinationListOverlay.Hide();
            else
                this.DestinationListOverlay.HandleInput(this.InteractionHelper, this.Config);

            return;
        }

        // handle key presses
        if (InputManager.HasPressedKey())
        {
            // toggle overlay
            if (this.InteractionHelper.IsKeyJustPressed(this.Config.ShowListKey))
                this.ShowDestinationList(this.DestinationManager.GetData());

            // return warp
            else if (this.InteractionHelper.IsKeyJustPressed(this.Config.ReturnPointKey))
                this.InteractivelyReturn();

            // saved destination
            else
            {
                foreach (KeyCode key in this.Config.GetDestinationKeys().Distinct())
                {
                    if (key == KeyCode.None || !this.InteractionHelper.IsKeyJustPressed(key))
                        continue;

                    // apply
                    if (this.InteractionHelper.IsKeyDown(this.Config.SaveModifierKey))
                        this.InteractivelySave(key);
                    else if (this.InteractionHelper.IsKeyDown(this.Config.DeleteModifierKey))
                        this.InteractivelyDelete(key);
                    else
                        this.InteractivelyFastTravel(key);
                    break;
                }
            }
        }
    }

    /// <inheritdoc />
    public override void OnSceneWasInitialized(int buildIndex, string sceneName)
    {
        // skip if save isn't loaded, or we're in the mid-warp 'Empty' scene
        if (!SceneHelper.IsPlayableScene())
            return;

        // log debug info
        if (this.Config.LogDebugInfo)
        {
            Destination location = this.DestinationManager.GetCurrentLocation();

            this.Log.Msg(
                $"""
                Scene initialized:
                    buildIndex: {buildIndex}
                    sceneName: '{sceneName}'
                    save name: '{SaveGameSystem.GetCurrentSaveName()}'

                    location: {location}
                    is outside: {SceneHelper.IsOutdoors(location.Scene.Name)}
                    is safehouse: {SceneHelper.IsCustomizableSafehouse()}
                    was restored: {GameManager.m_SceneWasRestored}
                    weather: {GameManager.GetWeatherComponent().GetWeatherStage()}

                    Unity scene:
                        name: {location.Scene.Name}
                        guid: {location.Scene.Guid}
                        path: {location.Scene.Path}
                        isSubScene: {location.Scene.IsSubScene}

                    Fast travel:
                        from: {this.FastTravel?.From.ToString() ?? "null"}
                        to:   {this.FastTravel?.To.ToString() ?? "null"}

                {this.GetTransitionDebugSummary("transition", location.LastTransition)}
                """
            );
        }

        // update position after travel
        if (this.FastTravel != null)
        {
            Destination destination = this.FastTravel.To;
            if (sceneName != destination.Scene.Name)
                this.Log.Warning($"Failed setting position after warp back: arrived in scene '{sceneName}' instead of the expected '{destination.Scene.Name}'.");
            else
                this.SnapPlayerTo(destination.Position.ToVector3(), destination.CameraPitch, destination.CameraYaw);

            this.FastTravel = null;
        }
    }


    /*********
    ** Private methods
    *********/
    /// <summary>Delete the destination bound to a hotkey with player interaction.</summary>
    /// <param name="hotkey">The destination hotkey.</param>
    private void InteractivelyDelete(KeyCode hotkey)
    {
        if (!this.Config.CanEditDestinations)
        {
            this.Log.Warning("无法编辑快速旅行目的地（已在模组设置中禁用）。");
            return;
        }

        SaveModel data = this.DestinationManager.GetData();
        DestinationEntry? entry = data.GetByHotkey(hotkey);

        if (entry is null)
            return; // nothing to delete

        this.InteractivelyDelete(entry);
    }

    /// <summary>Delete a destination with player interaction.</summary>
    /// <param name="entry">The destination entry.</param>
    private void InteractivelyDelete(DestinationEntry entry)
    {
        if (!this.Config.CanEditDestinations)
        {
            this.Log.Warning("无法编辑快速旅行目的地（已在模组设置中禁用）。");
            return;
        }

        this.InteractionHelper.ShowConfirmDialogue(
            $"要删除快速旅行目的地“{entry.GetDisplayName()}”吗？",
            () =>
            {
                SaveModel data = this.DestinationManager.GetData();
                DestinationEntry? saved = data.Get(entry.Id);
                if (saved is not null)
                    data.Remove(saved);

                this.DestinationManager.SaveData(data);
                this.UpdateDestinationListIfVisible(data);
            }
        );
    }

    /// <summary>Save a new destination with player interaction.</summary>
    /// <param name="hotkey">The destination hotkey to bind.</param>
    private void InteractivelySave(KeyCode hotkey)
    {
        // check restriction
        if (!this.Config.CanEditDestinations)
        {
            this.Log.Warning("无法编辑快速旅行目的地（已在模组设置中禁用）。");
            return;
        }

        // apply
        SaveModel data = this.DestinationManager.GetData();
        Destination here = this.DestinationManager.GetCurrentLocation();
        DestinationEntry? oldEntry = data.GetByHotkey(hotkey);
        string autoName = this.GetAutoDestinationName(here);

        string question = $"将当前位置保存为“{autoName}”，并绑定到 {this.FormatKey(hotkey)} 吗？";
        if (oldEntry is not null)
            question += $"\n\n这会覆盖快捷键 {this.FormatKey(hotkey)} 当前绑定的“{oldEntry.GetDisplayName()}”，把该快捷键替换到新目的地；旧目的地仍会保留在列表中。";

        this.InteractionHelper.ShowConfirmDialogue(
            question,
            () =>
            {
                DestinationEntry entry = new()
                {
                    AutoName = autoName,
                    Location = here
                };

                data.Add(entry);
                data.BindHotkey(entry, hotkey);
                this.DestinationManager.SaveData(data);
                this.UpdateDestinationListIfVisible(data);
            }
        );
    }

    /// <summary>Fast travel to the destination bound to a hotkey with player interaction.</summary>
    /// <param name="hotkey">The destination hotkey.</param>
    private void InteractivelyFastTravel(KeyCode hotkey)
    {
        SaveModel data = this.DestinationManager.GetData();
        DestinationEntry? entry = data.GetByHotkey(hotkey);

        if (entry is null)
        {
            string message = $"还没有保存任何绑定到 {this.FormatKey(hotkey)} 的快速旅行目的地。";
            if (this.Config.ShowUsageHints)
                message += $"\n\n按 {this.FormatKey(this.Config.SaveModifierKey)} + {this.FormatKey(hotkey)} 可将当前位置保存并绑定到该快捷键。";

            this.InteractionHelper.ShowMessageBox(message);
            return;
        }

        this.InteractivelyFastTravel(entry);
    }

    /// <summary>Fast travel to a saved destination with player interaction.</summary>
    /// <param name="entry">The destination entry.</param>
    private void InteractivelyFastTravel(DestinationEntry entry)
    {
        SaveModel data = this.DestinationManager.GetData();
        DestinationEntry? savedEntry = data.Get(entry.Id);
        Destination? destination = savedEntry?.Location;
        Destination here = this.DestinationManager.GetCurrentLocation();
        Destination? returnPoint = data.ReturnPoint;

        // not set yet
        if (destination is null)
        {
            this.InteractionHelper.ShowMessageBox("这个快速旅行目的地已经不存在。");
            return;
        }

        // check restrictions
        if (!this.FastTravelRestrictions.IsAllowed(here, destination, data, out string? reasonPhrase))
        {
            this.Log.Warning($"无法快速旅行{reasonPhrase}（根据你的模组设置）。");
            return;
        }

        // else travel
        string question = $"前往“{destination.GetDisplayName()}”吗？";
        if (this.Config.ReturnPointKey != KeyCode.None && this.Config.ShowUsageHints)
        {
            if (returnPoint != null && returnPoint.Scene.Name != here.Scene.Name)
                question += $"\n\n这会替换之前的返回点（{returnPoint.GetDisplayName()}）。";

            question += $"\n\n之后可以按 {this.FormatKey(this.Config.ReturnPointKey)} 回到这里。";
        }

        this.InteractionHelper.ShowConfirmDialogue(
            question,
            () =>
            {
                data.ReturnPoint = here;
                this.DestinationManager.SaveData(data);
                this.UpdateDestinationListIfVisible(data);
                this.FastTravelTo(destination);
            }
        );
    }

    /// <summary>Rebind a destination entry to a new hotkey.</summary>
    /// <param name="entry">The destination entry.</param>
    /// <param name="hotkey">The new hotkey.</param>
    private void RebindDestination(DestinationEntry entry, KeyCode hotkey)
    {
        if (!this.Config.CanEditDestinations)
        {
            this.Log.Warning("无法编辑快速旅行目的地（已在模组设置中禁用）。");
            return;
        }

        SaveModel data = this.DestinationManager.GetData();
        DestinationEntry? savedEntry = data.Get(entry.Id);
        if (savedEntry is null)
            return;

        DestinationEntry? oldEntry = data.GetByHotkey(hotkey);
        if (oldEntry is not null && oldEntry.Id != savedEntry.Id)
        {
            this.DestinationListOverlay.Hide();
            this.InteractionHelper.ShowConfirmDialogue(
                $"要把快捷键 {this.FormatKey(hotkey)} 改绑到“{savedEntry.GetDisplayName()}”吗？\n\n这会覆盖当前绑定的“{oldEntry.GetDisplayName()}”；旧目的地仍会保留在列表中。",
                () => this.ApplyHotkeyRebind(entry.Id, hotkey)
            );
            return;
        }

        data.BindHotkey(savedEntry, hotkey);
        this.DestinationManager.SaveData(data);
        this.UpdateDestinationListIfVisible(data);
    }

    /// <summary>Bind a destination entry to a hotkey, reloading the latest save data first.</summary>
    /// <param name="entryId">The destination entry ID.</param>
    /// <param name="hotkey">The hotkey to bind.</param>
    private void ApplyHotkeyRebind(string entryId, KeyCode hotkey)
    {
        SaveModel data = this.DestinationManager.GetData();
        DestinationEntry? savedEntry = data.Get(entryId);
        if (savedEntry is null)
        {
            this.InteractionHelper.ShowMessageBox("这个快速旅行目的地已经不存在。");
            return;
        }

        data.BindHotkey(savedEntry, hotkey);
        this.DestinationManager.SaveData(data);
        this.UpdateDestinationListIfVisible(data);
    }

    /// <summary>Rename a destination entry with player interaction.</summary>
    /// <param name="entry">The destination entry.</param>
    private void InteractivelyRename(DestinationEntry entry)
    {
        if (!this.Config.CanEditDestinations)
        {
            this.Log.Warning("无法编辑快速旅行目的地（已在模组设置中禁用）。");
            return;
        }

        SaveModel data = this.DestinationManager.GetData();
        DestinationEntry? savedEntry = data.Get(entry.Id);
        if (savedEntry is null)
        {
            this.InteractionHelper.ShowMessageBox("这个快速旅行目的地已经不存在。");
            return;
        }

        this.InteractionHelper.ShowTextInputDialogue(
            $"输入“{savedEntry.GetDisplayName(showRegion: true)}”的新名称（留空恢复默认名称）：",
            savedEntry.CustomName,
            customName =>
            {
                this.RenameDestination(entry, customName);
                this.ShowDestinationList(this.DestinationManager.GetData());
            }
        );
    }

    /// <summary>Rename a destination entry.</summary>
    /// <param name="entry">The destination entry.</param>
    /// <param name="customName">The new custom name, or <c>null</c> to reset it.</param>
    private void RenameDestination(DestinationEntry entry, string? customName)
    {
        if (!this.Config.CanEditDestinations)
        {
            this.Log.Warning("无法编辑快速旅行目的地（已在模组设置中禁用）。");
            return;
        }

        SaveModel data = this.DestinationManager.GetData();
        DestinationEntry? savedEntry = data.Get(entry.Id);
        if (savedEntry is null)
        {
            this.InteractionHelper.ShowMessageBox("这个快速旅行目的地已经不存在。");
            return;
        }

        savedEntry.CustomName = string.IsNullOrWhiteSpace(customName)
            ? null
            : customName.Trim();

        this.DestinationManager.SaveData(data);
        this.UpdateDestinationListIfVisible(data);
    }

    /// <summary>Handle the player requesting to fast travel to their last return point.</summary>
    private void InteractivelyReturn()
    {
        SaveModel data = this.DestinationManager.GetData();
        Destination here = this.DestinationManager.GetCurrentLocation();
        Destination? returnPoint = data.ReturnPoint;

        // check restrictions
        if (!this.FastTravelRestrictions.IsAllowed(here, returnPoint, data, out string? reasonPhrase))
        {
            this.Log.Warning($"无法快速旅行{reasonPhrase}（根据你的模组设置）。");
            return;
        }

        if (returnPoint is null)
        {
            string message = "你还没有进行过快速旅行。";
            if (this.Config.ShowUsageHints)
                message += $"\n\n第一次快速旅行后，可以按 {this.FormatKey(this.Config.ReturnPointKey)} 返回出发点。";

            this.InteractionHelper.ShowMessageBox(message);
            return;
        }

        string question = $"返回“{returnPoint.GetDisplayName()}”吗？";
        if (here.Scene.Name != returnPoint.Scene.Name)
            question += $"\n\n这会把“{here.GetDisplayName()}”设为新的返回点。";

        this.InteractionHelper.ShowConfirmDialogue(
            question,
            () =>
            {
                data.ReturnPoint = here;
                this.DestinationManager.SaveData(data);
                this.UpdateDestinationListIfVisible(data);
                this.FastTravelTo(returnPoint);
            }
        );
    }

    /// <summary>Fast travel to the given destination.</summary>
    /// <param name="destination">The destination to travel to.</param>
    private void FastTravelTo(Destination destination)
    {
        // trigger autosave
        // (This is needed to persist any changes made to the location; otherwise they'd be discarded when we leave.)
        string currentSceneName = SceneHelper.GetSceneName();
        SaveGameSystem.SaveGame("autosave", currentSceneName);

        // fade out and warp
        CameraFade.FadeOut(
            time: GameManager.m_SceneTransitionFadeOutTime,
            onFadeFinished: (Action)(() =>
            {
                TransitionModel original = destination.LastTransition;

                // recreate saved transition
                GameManager.m_SceneTransitionData = new SceneTransitionData
                {
                    m_SceneSaveFilenameCurrent = original.FromSceneId, // note: deliberately reuse original departure point (not our current scene) to avoid confusing the game
                    m_SceneSaveFilenameNextLoad = original.ToSceneId,
                    m_ForceNextSceneLoadTriggerScene = original.ForceNextSceneLoadTriggerScene,
                    m_SceneLocationLocIDOverride = original.SceneLocationLocIdOverride,
                    m_GameRandomSeed = original.GameRandomSeed,
                    m_Location = original.Location,
                    m_LastOutdoorScene = original.LastOutdoorScene,
                    m_PosBeforeInteriorLoad = original.LastOutdoorPosition.ToVector3(),
                    m_TeleportPlayerSaveGamePosition = true // mark as normal transition (e.g. not a new-game spawn)

                    // deliberately don't set m_SpawnPointName/m_SpawnPointAudio, since we want to restore the saved position
                };

                // log debug info
                if (this.Config.LogDebugInfo)
                {
                    this.Log.Msg(
                        $"""
                        Starting fast travel:
                            save name: '{SaveGameSystem.GetCurrentSaveName()}'

                            from location: '{currentSceneName}'
                            from outside: {SceneHelper.IsOutdoors(currentSceneName)}
                            from safehouse: {SceneHelper.IsCustomizableSafehouse()}

                            destination: {destination}
                            destination is outside: {SceneHelper.IsOutdoors(destination.Scene.Name)}

                        {this.GetTransitionDebugSummary("transition", new TransitionModel(GameManager.m_SceneTransitionData))}
                        """
                    );
                }

                // start transition
                this.FastTravel = new FastTravelTransition(this.DestinationManager.GetCurrentLocation(), destination);
                this.ShowOnArrival = destination;
                GameManager.LoadScene(destination.Scene.Name, SaveGameSystem.GetCurrentSaveName()); // need to load the Unity scene name; the game will get the instance ID from the transition data
            })
        );
    }

    /// <summary>Snap the player to a position within their current scene.</summary>
    /// <param name="position">The three-dimensional position within the scene.</param>
    /// <param name="cameraPitch">The camera's vertical rotation angle in degrees.</param>
    /// <param name="cameraYaw">The camera's horizontal rotation angle in degrees.</param>
    private void SnapPlayerTo(Vector3 position, float cameraPitch, float cameraYaw)
    {
        GameObject player = GameManager.GetPlayerObject();
        CharacterController playerController = player.GetComponent<CharacterController>();
        vp_FPSCamera camera = GameManager.GetVpFPSCamera();

        playerController?.enabled = false; // prevent Unity from snapping player back to its next calculated position (e.g. based on gravity)
        try
        {
            player.transform.position = position;
        }
        finally
        {
            playerController?.enabled = true; // resume Unity control from new position
        }

        camera.m_Pitch = cameraPitch;
        camera.m_TargetPitch = cameraPitch;
        camera.m_CurrentPitch = cameraPitch;

        camera.m_Yaw = cameraYaw;
        camera.m_TargetYaw = cameraYaw;
        camera.m_CurrentYaw = cameraYaw;
    }

    /// <summary>Get the destination that should be shown on-screen when the player arrives. This deletes the cached value, if any.</summary>
    private Destination? ConsumeDestinationOnArrival()
    {
        Destination? destination = this.ShowOnArrival;
        this.ShowOnArrival = null;
        return destination;
    }

    /// <summary>Show or reset the destination list overlay.</summary>
    /// <param name="data">The data to show.</param>
    /// <param name="entries">The entries to show, or <c>null</c> to show every destination.</param>
    /// <param name="title">The overlay title.</param>
    /// <param name="onSelect">The action to run when the player selects a destination.</param>
    private void ShowDestinationList(SaveModel data, IEnumerable<DestinationEntry>? entries = null, string? title = null, Action<DestinationEntry>? onSelect = null)
    {
        this.DestinationListOverlay.Show(
            title ?? "快速旅行目的地",
            entries ?? this.GetDestinationsForDisplay(data.Destinations),
            data.ReturnPoint,
            this.Config.ReturnPointKey,
            onSelect ?? this.InteractivelyFastTravel,
            this.InteractivelyDelete,
            this.RebindDestination,
            this.InteractivelyRename
        );
    }

    /// <summary>Get destinations in the order shown to the player.</summary>
    /// <param name="entries">The destination entries to sort.</param>
    private IEnumerable<DestinationEntry> GetDestinationsForDisplay(IEnumerable<DestinationEntry> entries)
    {
        Dictionary<KeyCode, int> hotkeyOrder = [];
        foreach (KeyCode hotkey in this.Config.GetDestinationKeys())
        {
            if (hotkey != KeyCode.None && !hotkeyOrder.ContainsKey(hotkey))
                hotkeyOrder[hotkey] = hotkeyOrder.Count;
        }

        return entries
            .Select((entry, index) => new { Entry = entry, Index = index })
            .OrderBy(item => this.GetDestinationDisplayGroup(item.Entry))
            .ThenBy(item => hotkeyOrder.TryGetValue(item.Entry.Hotkey, out int order) ? order : int.MaxValue)
            .ThenBy(item => item.Index)
            .Select(item => item.Entry);
    }

    /// <summary>Get the display group for a destination entry.</summary>
    /// <param name="entry">The destination entry.</param>
    private int GetDestinationDisplayGroup(DestinationEntry entry)
    {
        if (entry.Hotkey != KeyCode.None)
            return 0;

        return string.IsNullOrWhiteSpace(entry.CustomName)
            ? 2
            : 1;
    }

    /// <summary>Update the destination list if it's currently being shown.</summary>
    /// <param name="data">The data to show.</param>
    private void UpdateDestinationListIfVisible(SaveModel data)
    {
        if (this.DestinationListOverlay.IsVisible)
            this.ShowDestinationList(data);
    }

    /// <summary>Generate a default player-facing name for a saved destination.</summary>
    /// <param name="destination">The destination to name.</param>
    private string GetAutoDestinationName(Destination destination)
    {
        string regionName = this.GetRegionDisplayName(destination);
        Vector3 position = destination.Position.ToVector3();

        if (this.TryGetNearestMapDetailName(position, out string? landmarkName, out Vector3 landmarkPosition, out float landmarkDistance))
        {
            string locationName = landmarkDistance <= LandmarkNameOnlyDistance
                ? landmarkName
                : $"{landmarkName}{this.GetDirectionFrom(landmarkPosition, position)} {Mathf.RoundToInt(landmarkDistance)}m";

            return this.FormatRegionLocationName(regionName, locationName);
        }

        string sceneName = destination.GetDisplayName();
        if (!string.IsNullOrWhiteSpace(sceneName) && !string.Equals(sceneName, regionName, StringComparison.Ordinal))
            return this.FormatRegionLocationName(regionName, sceneName);

        return !string.IsNullOrWhiteSpace(regionName)
            ? $"{regionName}-{Mathf.RoundToInt(destination.Position.X)},{Mathf.RoundToInt(destination.Position.Z)}"
            : destination.GetDisplayName(showRegion: true);
    }

    /// <summary>Get the localized region name for a destination.</summary>
    /// <param name="destination">The destination whose region to name.</param>
    private string GetRegionDisplayName(Destination destination)
    {
        if (destination.Region is not null)
        {
            string regionName = this.GetLocalizedText(destination.Region.NameLocalizationId);
            if (!string.IsNullOrWhiteSpace(regionName) && regionName != destination.Region.NameLocalizationId)
                return regionName.Trim();

            if (!string.IsNullOrWhiteSpace(destination.Region.Name))
                return destination.Region.Name.Trim();
        }

        return destination.GetDisplayName(showRegion: true);
    }

    /// <summary>Get the nearest useful map landmark name for a world position.</summary>
    /// <param name="position">The world position.</param>
    /// <param name="name">The landmark name, if found.</param>
    /// <param name="landmarkPosition">The landmark world position, if found.</param>
    /// <param name="distance">The horizontal distance from the landmark, if found.</param>
    private bool TryGetNearestMapDetailName(Vector3 position, out string? name, out Vector3 landmarkPosition, out float distance)
    {
        name = null;
        landmarkPosition = Vector3.zero;
        distance = 0f;

        float bestScore = float.MaxValue;
        try
        {
            foreach (MapDetail detail in Object.FindObjectsOfType<MapDetail>())
            {
                if (detail is null || !this.TryGetMapDetailDisplayName(detail, out string? detailName))
                    continue;

                Vector3 detailPosition;
                try
                {
                    detailPosition = detail.GetWorldPosition();
                }
                catch
                {
                    detailPosition = detail.transform.position;
                }

                float detailDistance = this.GetHorizontalDistance(position, detailPosition);
                if (detailDistance > MaxGeneratedNameLandmarkDistance)
                    continue;

                float score = detailDistance + (this.IsPreferredMapDetail(detail) ? 0f : MaxGeneratedNameLandmarkDistance);
                if (score < bestScore)
                {
                    bestScore = score;
                    name = detailName;
                    landmarkPosition = detailPosition;
                    distance = detailDistance;
                }
            }
        }
        catch (Exception ex)
        {
            if (this.Config.LogDebugInfo)
                this.Log.Warning($"无法扫描地图地点名称：{ex.Message}");

            return false;
        }

        return !string.IsNullOrWhiteSpace(name);
    }

    /// <summary>Get the horizontal distance between two world positions.</summary>
    /// <param name="from">The first world position.</param>
    /// <param name="to">The second world position.</param>
    private float GetHorizontalDistance(Vector3 from, Vector3 to)
    {
        float x = to.x - from.x;
        float z = to.z - from.z;

        return Mathf.Sqrt((x * x) + (z * z));
    }

    /// <summary>Get the eight-way direction from one world position to another.</summary>
    /// <param name="from">The origin world position.</param>
    /// <param name="to">The target world position.</param>
    private string GetDirectionFrom(Vector3 from, Vector3 to)
    {
        Vector2 offset = new(to.x - from.x, to.z - from.z);
        if (offset.sqrMagnitude < 0.01f)
            return "";

        float angle = Mathf.Atan2(offset.x, offset.y) * Mathf.Rad2Deg;
        if (angle < 0)
            angle += 360f;

        string[] directions =
        [
            "北",
            "东北",
            "东",
            "东南",
            "南",
            "西南",
            "西",
            "西北"
        ];

        int index = Mathf.RoundToInt(angle / 45f) % directions.Length;
        return directions[index];
    }

    /// <summary>Get the localized display name for a map detail if it's useful for destination naming.</summary>
    /// <param name="detail">The map detail.</param>
    /// <param name="name">The localized name, if found.</param>
    private bool TryGetMapDetailDisplayName(MapDetail detail, out string? name)
    {
        name = null;

        string locId = detail.m_LocID;
        if (string.IsNullOrWhiteSpace(locId))
            return false;

        string localizedName = this.GetLocalizedText(locId);
        if (string.IsNullOrWhiteSpace(localizedName))
            return false;

        localizedName = localizedName.Trim();
        if (localizedName == locId && locId.Contains('_'))
            return false;

        if (this.IsIgnoredMapDetail(detail, locId, localizedName))
            return false;

        name = localizedName;
        return true;
    }

    /// <summary>Get whether a map detail is likely to be a named place rather than a resource detail.</summary>
    /// <param name="detail">The map detail.</param>
    private bool IsPreferredMapDetail(MapDetail detail)
    {
        return detail.m_IconType is MapIcon.MapIconType.TopIcon or MapIcon.MapIconType.Text or MapIcon.MapIconType.Area;
    }

    /// <summary>Get whether a map detail should be ignored for generated destination names.</summary>
    /// <param name="detail">The map detail.</param>
    /// <param name="locId">The map detail localization ID.</param>
    /// <param name="localizedName">The localized map detail name.</param>
    private bool IsIgnoredMapDetail(MapDetail detail, string locId, string localizedName)
    {
        string value = $"{locId} {localizedName} {detail.m_SpriteName}".ToLowerInvariant();
        string[] ignoredTerms =
        [
            "spray",
            "rockcache",
            "rock cache",
            "corpse",
            "carcass",
            "harvest",
            "reishi",
            "rosehip",
            "rose hip",
            "cattail",
            "sapling",
            "cedar",
            "fir",
            "birch",
            "maple",
            "stick",
            "limb",
            "coal",
            "acorn",
            "lichen",
            "mushroom",
            "resource"
        ];

        return ignoredTerms.Any(value.Contains);
    }

    /// <summary>Get localized text for a localization ID, or the ID if localization fails.</summary>
    /// <param name="locId">The localization ID.</param>
    private string GetLocalizedText(string locId)
    {
        try
        {
            return Localization.Get(locId);
        }
        catch (Exception ex)
        {
            if (this.Config.LogDebugInfo)
                this.Log.Warning($"无法读取本地化文本“{locId}”：{ex.Message}");

            return locId;
        }
    }

    /// <summary>Join a region and local landmark name into the generated destination naming format.</summary>
    /// <param name="regionName">The localized region name.</param>
    /// <param name="locationName">The localized location name.</param>
    private string FormatRegionLocationName(string regionName, string locationName)
    {
        regionName = regionName.Trim();
        locationName = locationName.Trim();

        if (string.IsNullOrWhiteSpace(regionName))
            return locationName;

        if (string.IsNullOrWhiteSpace(locationName) || string.Equals(regionName, locationName, StringComparison.Ordinal))
            return regionName;

        if (locationName.StartsWith(regionName, StringComparison.Ordinal))
        {
            string suffix = locationName.Substring(regionName.Length).TrimStart(' ', '-', '：', ':', '（', '(');
            if (!string.IsNullOrWhiteSpace(suffix))
                return $"{regionName}-{suffix}";

            return regionName;
        }

        return $"{regionName}-{locationName}";
    }

    /// <summary>Get a player-facing key label.</summary>
    /// <param name="key">The key to display.</param>
    private string FormatKey(KeyCode key)
    {
        return key == KeyCode.None
            ? "未绑定"
            : key.ToString();
    }

    /// <summary>Get a debug log representation of a scene transition.</summary>
    /// <param name="label">The label for the section.</param>
    /// <param name="transition">The scene transition to dump.</param>
    /// <param name="indent">The left indent with which to prefix each line.</param>
    private string GetTransitionDebugSummary(string label, TransitionModel transition, string indent = "    ")
    {
        return $"""
        {indent}{label}:
        {indent}    FromSceneId: {transition.FromSceneId ?? "<null>"}
        {indent}    ToSceneId: {transition.ToSceneId ?? "<null>"}
        {indent}    ToSpawnPoint: {transition.ToSpawnPoint ?? "<null>"}
        {indent}    ToSpawnPointAudio: {transition.ToSpawnPointAudio ?? "<null>"}
        {indent}    RestorePlayerPosition: {transition.RestorePlayerPosition}
        {indent}    LastOutdoorScene: {transition.LastOutdoorScene ?? "<null>"}
        {indent}    LastOutdoorPosition: {transition.LastOutdoorPosition}
        {indent}    GameRandomSeed: {transition.GameRandomSeed}
        {indent}    ForceNextSceneLoadTriggerScene: {transition.ForceNextSceneLoadTriggerScene ?? "<null>"}
        {indent}    SceneLocationLocIdOverride: {transition.SceneLocationLocIdOverride ?? "<null>"}
        {indent}    Location: {transition.Location ?? "<null>"}
        """;
    }
}
