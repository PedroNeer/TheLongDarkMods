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

namespace Pathoschild.TheLongDarkMods.FastTravel;

/// <inheritdoc />
public class ModEntry : MelonMod
{
    /*********
    ** Fields
    *********/
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

        // handle key presses
        if (InputManager.HasPressedKey() && SceneHelper.IsSaveLoaded())
        {
            if (this.DestinationListOverlay.IsVisible)
            {
                if (this.InteractionHelper.IsKeyJustPressed(this.Config.ShowListKey))
                    this.DestinationListOverlay.Hide();
                else
                    this.DestinationListOverlay.HandleInput(this.InteractionHelper, this.Config);

                return;
            }

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
    /// <summary>Delete destinations bound to a hotkey with player interaction.</summary>
    /// <param name="hotkey">The destination hotkey.</param>
    private void InteractivelyDelete(KeyCode hotkey)
    {
        if (!this.Config.CanEditDestinations)
        {
            this.Log.Warning("Can't edit fast travel destinations (per your mod settings).");
            return;
        }

        SaveModel data = this.DestinationManager.GetData();
        DestinationEntry[] matches = data.GetByHotkey(hotkey).ToArray();

        if (matches.Length == 0)
            return; // nothing to delete

        if (matches.Length == 1)
            this.InteractivelyDelete(matches[0]);
        else
            this.ShowDestinationList(data, matches, $"Forget which destination bound to {hotkey}?", this.InteractivelyDelete);
    }

    /// <summary>Delete a destination with player interaction.</summary>
    /// <param name="entry">The destination entry.</param>
    private void InteractivelyDelete(DestinationEntry entry)
    {
        if (!this.Config.CanEditDestinations)
        {
            this.Log.Warning("Can't edit fast travel destinations (per your mod settings).");
            return;
        }

        this.InteractionHelper.ShowConfirmDialogue(
            $"Do you want to forget {entry.GetDisplayName()}?",
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
            this.Log.Warning("Can't edit fast travel destinations (per your mod settings).");
            return;
        }

        // apply
        SaveModel data = this.DestinationManager.GetData();
        Destination here = this.DestinationManager.GetCurrentLocation();
        int existingBindings = data.GetByHotkey(hotkey).Count();

        string question = $"Save {here.GetDisplayName()} as a new fast travel destination bound to {hotkey}?";
        if (existingBindings > 0)
            question += $"\n\nThis key already has {existingBindings} saved destination{(existingBindings == 1 ? "" : "s")}. The old destination{(existingBindings == 1 ? "" : "s")} will stay in your list.";

        this.InteractionHelper.ShowConfirmDialogue(
            question,
            () =>
            {
                data.Add(new DestinationEntry
                {
                    Hotkey = hotkey,
                    Location = here
                });
                this.DestinationManager.SaveData(data);
                this.UpdateDestinationListIfVisible(data);
            }
        );
    }

    /// <summary>Fast travel to destinations bound to a hotkey with player interaction.</summary>
    /// <param name="hotkey">The destination hotkey.</param>
    private void InteractivelyFastTravel(KeyCode hotkey)
    {
        SaveModel data = this.DestinationManager.GetData();
        DestinationEntry[] matches = data.GetByHotkey(hotkey).ToArray();

        if (matches.Length == 0)
        {
            string message = $"You haven't saved any fast travel destinations bound to {hotkey} yet.";
            if (this.Config.ShowUsageHints)
                message += $"\n\nPress {this.Config.SaveModifierKey} + {hotkey} to save your current location with that key.";

            this.InteractionHelper.ShowMessageBox(message);
            return;
        }

        if (matches.Length == 1)
            this.InteractivelyFastTravel(matches[0]);
        else
            this.ShowDestinationList(data, matches, $"Travel to which destination bound to {hotkey}?", this.InteractivelyFastTravel);
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
            this.InteractionHelper.ShowMessageBox("That fast travel destination no longer exists.");
            return;
        }

        // check restrictions
        if (!this.FastTravelRestrictions.IsAllowed(here, destination, data, out string? reasonPhrase))
        {
            this.Log.Warning($"Can't fast travel {reasonPhrase} (per your mod settings).");
            return;
        }

        // else travel
        string question = $"Travel to {destination.GetDisplayName()}?";
        if (this.Config.ReturnPointKey != KeyCode.None && this.Config.ShowUsageHints)
        {
            if (returnPoint != null && returnPoint.Scene.Name != here.Scene.Name)
                question += $"\n\nThis will replace your previous return point ({returnPoint.GetDisplayName()}).";

            question += $"\n\nYou can return here later by pressing {this.Config.ReturnPointKey}.";
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
            this.Log.Warning("Can't edit fast travel destinations (per your mod settings).");
            return;
        }

        SaveModel data = this.DestinationManager.GetData();
        DestinationEntry? savedEntry = data.Get(entry.Id);
        if (savedEntry is null)
            return;

        savedEntry.Hotkey = hotkey;
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
            this.Log.Warning($"Can't fast travel {reasonPhrase} (per your mod settings).");
            return;
        }

        if (returnPoint is null)
        {
            string message = "You haven't fast traveled anywhere yet.";
            if (this.Config.ShowUsageHints)
                message += $"\n\nAfter you fast travel at least once, you'll be able to return to your departure point by pressing {this.Config.ReturnPointKey}.";

            this.InteractionHelper.ShowMessageBox(message);
            return;
        }

        string question = $"Travel back to {returnPoint.GetDisplayName()}?";
        if (here.Scene.Name != returnPoint.Scene.Name)
            question += $"\n\nThis will set {here.GetDisplayName()} as your new return point.";

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
            title ?? "Fast travel destinations",
            entries ?? data.Destinations,
            data.ReturnPoint,
            this.Config.ReturnPointKey,
            onSelect ?? this.InteractivelyFastTravel,
            this.InteractivelyDelete,
            this.RebindDestination
        );
    }

    /// <summary>Update the destination list if it's currently being shown.</summary>
    /// <param name="data">The data to show.</param>
    private void UpdateDestinationListIfVisible(SaveModel data)
    {
        if (this.DestinationListOverlay.IsVisible)
            this.ShowDestinationList(data);
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
