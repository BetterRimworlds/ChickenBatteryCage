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
 * The Verse-free clock behind a fertilizer pack left outdoors.
 *
 * Dried fertilizer does not dissolve indoors, so this clock has no indoor
 * rate. Outdoors, one pack dissolves every eight days on natural ground, and
 * rain or snow — a weather rain rate above one tenth — divides that by two.
 * Freezing is not a rate here: the caller simply stops advancing the clock at
 * zero degrees and below. A <c>./build.sh fast</c> build passes
 * <see cref="FastDays"/> instead of eight, and keeps the rain factor.
 */
public static class FertilizerDissolutionMath
{
    /// Days between packs outdoors, in fair weather.
    public const float DaysToDissolve = 8f;

    /// Base days used by <c>./build.sh fast</c>. The rain factor stays.
    public const float FastDays = 1f;

    /// Divides the outdoor interval while rain or snow is falling.
    public const float RainFactor = 2f;

    /// Rain or snow counts only above this weather rain rate. Snow is 1.
    public const float MinRainRate = 0.1f;

    /// Hit points per day on a toxic wastepack. Default max hit points are 100.
    public const float DeteriorationRateLikeWastepack = 4f;

    /// True when a weather rain rate would speed an outdoor pack.
    public static bool CountsAsRain(float rainRate)
    {
        return rainRate > MinRainRate;
    }

    /// Ticks between dissolving one outdoor pack. Rain divides the interval.
    public static int IntervalTicks(float days, bool raining, float rainFactor = RainFactor)
    {
        if (days <= 0f)
        {
            return 0;
        }

        float ticks = days * CagedChickenMath.TicksPerDay;
        if (raining && rainFactor > 0f)
        {
            ticks /= rainFactor;
        }

        return (int)ticks;
    }
}
