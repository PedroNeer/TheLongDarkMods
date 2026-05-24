using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Il2Cpp;
using Pathoschild.TheLongDarkMods.Common;
using Pathoschild.TheLongDarkMods.FastTravel.Framework.DataModels;

namespace Pathoschild.TheLongDarkMods.FastTravel.Framework;

/// <summary>Handles checking for fast travel restrictions.</summary>
internal class FastTravelRestrictionHelper
{
    /*********
    ** Fields
    *********/
    /// <summary>The mod settings.</summary>
    private readonly ModConfig Config;


    /*********
    ** Public methods
    *********/
    /// <summary>Construct an instance.</summary>
    /// <param name="config">The mod settings.</param>
    public FastTravelRestrictionHelper(ModConfig config)
    {
        this.Config = config;
    }

    /// <summary>Get whether the player's mod settings prohibit a fast travel.</summary>
    /// <param name="from">The scene from which the player would travel.</param>
    /// <param name="to">The scene in which the player would arrive.</param>
    /// <param name="data">The saved fast travel destinations.</param>
    /// <param name="reasonPhrase">If fast travel is restricted, a phrase which can fit in the sentence <c>无法快速旅行{0}</c>.</param>
    /// <returns>Returns whether restrictions prohibit this fast travel.</returns>
    public bool IsAllowed(Destination from, Destination? to, SaveModel data, [NotNullWhen(false)] out string? reasonPhrase)
    {
        bool isSameScene = from.Scene.Name == to?.Scene.Name;
        bool isFromOutside = SceneHelper.IsOutdoors(from.Scene.Name);

        // disabled
        if (!this.Config.CanTravel)
        {
            reasonPhrase = "，因为快速旅行已关闭";
            return false;
        }

        // from non-fast travel point
        if (!this.Config.CanTravelFromNonFastTravelPoint && data.Destinations.All(entry => entry.Location.Scene.Name != from.Scene.Name))
        {
            reasonPhrase = "，因为当前位置不是已保存目的地";
            return false;
        }

        // from outside
        if (!this.Config.CanTravelFromOutside && isFromOutside)
        {
            reasonPhrase = "，因为你在室外";
            return false;
        }

        // from non-safehouse
        if (!this.Config.CanTravelFromNonSafehouseInterior && !isFromOutside && !SceneHelper.IsCustomizableSafehouse())
        {
            reasonPhrase = "，因为当前位置是非安全屋室内";
            return false;
        }

        // from within scene
        if (!this.Config.CanTravelWithinScene && isSameScene)
        {
            reasonPhrase = "，因为目标在同一场景";
            return false;
        }

        // under attack
        if (!this.Config.CanTravelWhileUnderAttack && this.IsAnyAnimalHostile())
        {
            reasonPhrase = "，因为你正受到攻击";
            return false;
        }

        // restrict by weather
        if (!this.CanTravelDuringWeather(out reasonPhrase))
            return false;

        reasonPhrase = null;
        return true;
    }


    /*********
    ** Private methods
    *********/
    /// <summary>Get whether the player can travel during the current weather in their departure region.</summary>
    /// <param name="reasonPhrase">If fast travel is restricted, a phrase which can fit in the sentence <c>无法快速旅行{0}</c>.</param>
    /// <returns>Returns whether travel is allowed.</returns>
    private bool CanTravelDuringWeather([NotNullWhen(false)] out string? reasonPhrase)
    {
        switch (GameManager.GetWeatherComponent().GetWeatherStage())
        {
            case WeatherStage.ClearAurora:
                reasonPhrase = "，因为当前是极光";
                return this.Config.CanTravelDuringAurora;

            case WeatherStage.DenseFog:
                reasonPhrase = "，因为当前有浓雾";
                return this.Config.CanTravelDuringDenseFog;

            case WeatherStage.ElectrostaticFog:
                reasonPhrase = "，因为当前有闪光雾";
                return this.Config.CanTravelDuringGlimmerFog;

            case WeatherStage.LightSnow:
                reasonPhrase = "，因为当前有小雪";
                return this.Config.CanTravelDuringLightSnowfall;

            case WeatherStage.HeavySnow:
                reasonPhrase = "，因为当前有大雪";
                return this.Config.CanTravelDuringHeavySnowfall;

            case WeatherStage.Blizzard:
                reasonPhrase = "，因为当前是暴风雪";
                return this.Config.CanTravelDuringBlizzard;

            default:
                reasonPhrase = null;
                return true;
        }
    }

    /// <summary>Get whether any animals are currently hostile towards the player.</summary>
    private bool IsAnyAnimalHostile()
    {
        foreach (BaseAi animal in BaseAiManager.m_BaseAis)
        {
            if (!animal.IsPlayerFacingAi())
                continue;

            switch (animal.GetAiMode())
            {
                case AiMode.Attack:
                case AiMode.HoldGround:
                case AiMode.Howl:
                case AiMode.PassingAttack:
                case AiMode.Stalking:
                case AiMode.Struggle:
                    return true;
            }
        }

        return false;
    }
}
