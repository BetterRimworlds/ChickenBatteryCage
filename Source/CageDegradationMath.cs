/*
 * This file is part of BetterRimworlds.ChickenBatteryCage, a Better Rimworlds Project.
 *
 * Copyright © 2026 Theodore R. Smith
 * Author: Theodore R. Smith <hopeseekr@gmail.com>
 *   GPG Fingerprint: D8EA 6E4D 5952 159D 7759  2BB4 EEB6 CE72 F441 EC41
 *   https://github.com/BetterRimworlds/BetterRimworlds.ChickenBatteryCage
 *
 * This file is licensed under the MIT License.
 */

namespace BetterRimworlds.ChickenBatteryCage;

/**
 * The Verse-free arithmetic behind an unroofed battery cage falling apart.
 *
 * A roofed cage is sheltered and never decays. Once it is open to the sky the
 * cage corrodes or rots away on a fixed clock, taking damage continuously so it
 * fails after a known number of days: wood and steel give way fastest, every
 * other material lasts twice as long. The clock runs in fair weather and foul
 * alike, but bad weather — rain, fog, snow, a thunderstorm — makes the cage rot
 * four times as fast.
 */
public static class CageDegradationMath
{
    /// Days an unroofed cage of wood or steel survives before it fails.
    public const float WoodOrSteelExposureDays = 30f;

    /// Days an unroofed cage of any other material survives.
    public const float OtherMaterialExposureDays = 60f;

    /// How much faster an unroofed cage rots while the weather is bad.
    public const float BadWeatherMultiplier = 4f;

    /// The two stuff defs that decay on the faster clock: common structural
    /// wood and plain steel.
    public const string SteelStuffDefName = "Steel";
    public const string WoodStuffDefName = "WoodLog";

    /// True when a building stuffed with this material is one of the two that
    /// decay fastest. An unknown material (no stuff at all, or a modded def)
    /// is treated as "other" and lasts the full sixty days.
    public static bool IsWoodOrSteel(string stuffDefName)
    {
        return stuffDefName == SteelStuffDefName || stuffDefName == WoodStuffDefName;
    }

    /// How long an unroofed cage of this material lasts, in days.
    public static float ExposureDays(bool woodOrSteel)
    {
        return woodOrSteel ? WoodOrSteelExposureDays : OtherMaterialExposureDays;
    }

    /// The decay-rate factor for the current weather: four while it is bad,
    /// otherwise the normal rate.
    public static float WeatherMultiplier(bool badWeather)
    {
        return badWeather ? BadWeatherMultiplier : 1f;
    }

    /**
     * Damage to deal over an elapsed unroofed window so that, applied
     * continuously from full health in fair weather, the building reaches zero
     * hit points exactly when the exposure clock runs out. A weather multiplier
     * of four (bad weather) drains it four times as fast.
     */
    public static float DamageOverTicks(
        float maxHitPoints,
        float exposureDays,
        int elapsedTicks,
        float weatherMultiplier = 1f)
    {
        if (maxHitPoints <= 0f
            || exposureDays <= 0f
            || elapsedTicks <= 0
            || weatherMultiplier <= 0f)
        {
            return 0f;
        }

        float totalTicks = exposureDays * CagedChickenMath.TicksPerDay;
        return maxHitPoints * elapsedTicks / totalTicks * weatherMultiplier;
    }

    /**
     * Days left before an unroofed cage of this material would fail, given its
     * current health and the weather. A cage at full health in fair weather
     * reports the whole clock; bad weather shortens it in step with the faster
     * damage.
     */
    public static float DaysUntilFailure(
        float hitPoints,
        float maxHitPoints,
        float exposureDays,
        float weatherMultiplier = 1f)
    {
        if (hitPoints <= 0f
            || maxHitPoints <= 0f
            || exposureDays <= 0f
            || weatherMultiplier <= 0f)
        {
            return 0f;
        }

        return hitPoints / maxHitPoints * exposureDays / weatherMultiplier;
    }
}
