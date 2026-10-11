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

using System;

namespace BetterRimworlds.ChickenBatteryCage;

/**
 * The Verse-free clock behind a fast-built fertilizer stack.
 *
 * A normal build leaves spoilage to vanilla rot: about ten days in the warm
 * open, slower in the cold, stopped below freezing. <c>./build.sh fast</c>
 * compiles that wait out and uses this clock instead. The whole stack then
 * dissolves after one day of fair weather. Bad weather — the same rain, fog,
 * snow, and thunderstorms that rot an unroofed cage — multiplies the clock by
 * <see cref="CageDegradationMath.BadWeatherMultiplier"/>, so a stack left in
 * the open during a storm is gone in a quarter of a day. The caller decides
 * whether the pack is actually exposed; a roof keeps the fair-weather rate.
 */
public static class FertilizerFastDecompositionMath
{
    /// Fair-weather days until a fast-built stack dissolves.
    public const float DaysToDissolve = 1f;

    /// Equivalent ticks of fair weather that finish the clock.
    public static int TicksToDissolve =>
        (int)(DaysToDissolve * CagedChickenMath.TicksPerDay);

    /**
     * Fair-weather-equivalent ticks to add for an elapsed window. Bad weather
     * counts each real tick four times, matching an unroofed cage.
     */
    public static int EquivalentTicks(int elapsedTicks, bool badWeather)
    {
        if (elapsedTicks <= 0)
        {
            return 0;
        }

        double multiplier = CageDegradationMath.WeatherMultiplier(badWeather);
        return (int)Math.Round(elapsedTicks * multiplier, MidpointRounding.AwayFromZero);
    }

    /// True once the accumulated fair-weather-equivalent ticks cover the clock.
    public static bool ShouldDissolve(int accumulatedEquivalentTicks)
    {
        return accumulatedEquivalentTicks >= TicksToDissolve;
    }

    /**
     * Real ticks still to wait at the current weather. The estimate shortens
     * while the weather is bad and lengthens again when it clears.
     */
    public static int RealTicksRemaining(int accumulatedEquivalentTicks, bool badWeather)
    {
        int left = TicksToDissolve - accumulatedEquivalentTicks;
        if (left <= 0)
        {
            return 0;
        }

        double multiplier = CageDegradationMath.WeatherMultiplier(badWeather);
        return (int)Math.Ceiling(left / multiplier);
    }
}
