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
 * The Verse-free clock behind a fertilizer pack, copied from vanilla
 * <c>CompDissolution</c> on a toxic wastepack.
 *
 * One pack dissolves per interval. Outdoors that interval is eight days.
 * Indoors the interval is divided by one half, so it takes twice as long.
 * Rain or snow, a weather rain rate above one tenth while the room is
 * outdoors, divides the interval by two. Freezing is not a rate here: the
 * caller simply stops advancing the clock at zero degrees and below.
 * A <c>./build.sh fast</c> build passes <see cref="FastDays"/> instead of
 * eight, and keeps these same factors.
 */
public static class FertilizerDissolutionMath
{
    /// Days between packs outdoors, in fair weather. Vanilla wastepacks use 8.
    public const float DaysToDissolve = 8f;

    /// Base days used by <c>./build.sh fast</c>. The indoor and rain factors stay.
    public const float FastDays = 1f;

    /// <c>CompProperties_Dissolution.dissolutinFactorIndoors</c>. Divides the interval.
    public const float IndoorFactor = 0.5f;

    /// <c>CompProperties_Dissolution.dissolutionFactorRain</c>. Divides the interval.
    public const float RainFactor = 2f;

    /// Wastepacks treat the weather as rain only above this rate. Snow is 1.
    public const float MinRainRate = 0.1f;

    /// Hit points per day on a toxic wastepack. Default max hit points are 100.
    public const float DeteriorationRateLikeWastepack = 4f;

    /// True when a weather rain rate would speed a wastepack that is outdoors.
    public static bool CountsAsRain(float rainRate)
    {
        return rainRate > MinRainRate;
    }

    /**
     * Ticks between dissolving one pack. Indoor and rain each divide the
     * interval, in that order, which is how <c>CompDissolution</c> does it.
     * The caller passes raining only while the pack is outdoors.
     */
    public static int IntervalTicks(
        float days,
        bool outdoors,
        bool raining,
        float indoorFactor = IndoorFactor,
        float rainFactor = RainFactor)
    {
        if (days <= 0f)
        {
            return 0;
        }

        float ticks = days * CagedChickenMath.TicksPerDay;
        if (!outdoors && indoorFactor > 0f)
        {
            ticks /= indoorFactor;
        }

        if (raining && rainFactor > 0f)
        {
            ticks /= rainFactor;
        }

        return (int)ticks;
    }
}
