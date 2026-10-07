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
 * The Verse-free survival arithmetic for caged chickens.
 *
 * Mortality is probabilistic, never a fixed expiration date, so a well-kept
 * flock can still produce rare long-lived birds. Nothing here materializes a
 * Pawn: callers turn a failed survival roll into a corpse.
 */
public static class CageMortalityMath
{
    /// Daily death chance reached at exactly the species life expectancy.
    public const float NaturalDailyChanceAtLifeExpectancy = 0.001f;

    /// How sharply the hazard climbs beyond life expectancy. A larger value
    /// makes extreme ages rarer without ever ruling them out.
    public const float NaturalChanceGrowth = 2.5f;

    /// No single daily roll may ever be a certainty, so old outliers survive.
    public const float MaxDailyChance = 0.5f;

    /// Fallback when the species def cannot be read.
    public const float DefaultLifeExpectancyYears = 6f;

    /**
     * Age-dependent natural hazard. Low for young birds, climbing smoothly
     * through the species life expectancy and beyond, so a 10-year-old
     * chicken is unlikely but never impossible.
     */
    public static float NaturalDailyChance(float ageYears, float lifeExpectancyYears)
    {
        if (ageYears <= 0f)
        {
            return 0f;
        }

        float expectancy = lifeExpectancyYears > 0.01f
            ? lifeExpectancyYears
            : DefaultLifeExpectancyYears;

        double ratio = (ageYears - expectancy) / expectancy;
        double chance = NaturalDailyChanceAtLifeExpectancy * Math.Exp(NaturalChanceGrowth * ratio);
        return (float)Math.Min(MaxDailyChance, chance);
    }

    /// Starvation hazard on the first day past the starvation threshold.
    public const float StarvationBaseDailyChance = 0.05f;

    /**
     * Hazard from an empty store. A flock only begins to die once it has gone
     * past the starvation threshold, and the risk climbs with every further
     * day without feed.
     */
    public static float StarvationDailyChance(float starvingDays)
    {
        if (starvingDays <= CageNutritionMath.StarvingAfterDays)
        {
            return 0f;
        }

        float daysOver = starvingDays - CageNutritionMath.StarvingAfterDays;
        return Math.Min(MaxDailyChance, StarvationBaseDailyChance * (1f + daysOver));
    }

    public static float CombinedDailyChance(float natural, float starvation)
    {
        float combined = natural + starvation;
        return combined > MaxDailyChance ? MaxDailyChance : combined;
    }

    /**
     * Converts a per-day hazard into the hazard for the ticks actually
     * evaluated. For a constant daily chance, compounding keeps a long
     * evaluation window equivalent to several short ones.
     */
    public static float ChanceOverTicks(float dailyChance, int ticks)
    {
        if (dailyChance <= 0f || ticks <= 0)
        {
            return 0f;
        }

        if (dailyChance >= 1f)
        {
            return 1f;
        }

        return ChanceFromExposure(ExposureOverTicks(dailyChance, ticks));
    }

    public static double ExposureOverTicks(float dailyChance, int ticks)
    {
        if (dailyChance <= 0f || ticks <= 0)
        {
            return 0.0;
        }

        return dailyChance >= 1f ? double.PositiveInfinity
            : -Math.Log(1.0 - dailyChance) * ticks / CagedChickenMath.TicksPerDay;
    }

    /// Integrates the age-dependent curve using two-point Gaussian quadrature
    /// in short slices. The curve changes smoothly; its end value is not
    /// charged retroactively to the entire interval.
    public static double NaturalExposureOverTicks(long ageAtStartTicks, float lifeExpectancy, int ticks)
    {
        return CombinedExposureOverTicks(ageAtStartTicks, lifeExpectancy, double.NegativeInfinity, ticks);
    }

    /// Feed remaining is represented as negative days until the store empties.
    public static double StarvingDaysAtStart(int starvingTicks, float stored, int birds, float demandPerBird)
    {
        double demand = CageNutritionMath.DemandPerDay(birds, demandPerBird);
        return demand <= 0.0 ? double.NegativeInfinity
            : stored > 0f ? -stored / demand
            : starvingTicks / (double)CagedChickenMath.TicksPerDay;
    }

    /// Integrates the combined daily chance, splitting at the starvation
    /// threshold so its discontinuity is never spread over a fed interval.
    public static double CombinedExposureOverTicks(
        long ageAtStartTicks, float lifeExpectancy, double starvingDaysAtStart, int ticks)
    {
        double exposure = 0.0;
        const double offset = 0.28867513459481287;
        double thresholdTick = (CageNutritionMath.StarvingAfterDays - starvingDaysAtStart)
            * CagedChickenMath.TicksPerDay;
        for (double elapsed = 0; elapsed < ticks;)
        {
            double slice = Math.Min(2500.0, ticks - elapsed);
            if (thresholdTick > elapsed && thresholdTick < elapsed + slice)
            {
                slice = thresholdTick - elapsed;
            }
            double firstTick = elapsed + slice * (0.5 - offset);
            double secondTick = elapsed + slice * (0.5 + offset);
            float first = DailyChanceAt(firstTick);
            float second = DailyChanceAt(secondTick);
            exposure -= (Math.Log(1.0 - first) + Math.Log(1.0 - second))
                * 0.5 * slice / CagedChickenMath.TicksPerDay;
            elapsed += slice;
        }
        return exposure;

        float DailyChanceAt(double elapsed)
        {
            float natural = NaturalDailyChance(
                (float)((ageAtStartTicks + elapsed) / CagedChickenMath.TicksPerYear), lifeExpectancy);
            float starvation = StarvationDailyChance(
                (float)(starvingDaysAtStart + elapsed / CagedChickenMath.TicksPerDay));
            return CombinedDailyChance(natural, starvation);
        }
    }

    public static float ChanceFromExposure(double exposure)
    {
        return exposure <= 0.0 ? 0f : (float)(1.0 - Math.Exp(-exposure));
    }

    /// A newcomer owes no exposure for the part of a settlement before entry.
    /// The later tick is chosen in wrapping game-tick order, not by numeric
    /// value: after the counter wraps, a hen housed beforehand keeps a
    /// pre-wrap positive entry tick while the settle marker has gone negative,
    /// and a plain numeric max would jump the start back to her original entry
    /// and re-integrate the whole span on every settlement.
    public static int ExposureStartTick(int settledAtTick, int enteredAtTick)
    {
        return CagedChickenMath.IsAfter(enteredAtTick, settledAtTick)
            ? enteredAtTick
            : settledAtTick;
    }
}
