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
 * The Verse-free laying arithmetic for caged hens.
 *
 * A caged hen has no individual egg-production component; her output is a
 * fraction of a cage-level rate derived from her exact biological age. Young
 * adults lay at the vanilla interval, and the rate declines slowly after her
 * prime years without ever quite reaching zero.
 *
 * These are genetically engineered super chickens, so they shed only part of
 * the usual age-related reduction in laying: every drop from the prime rate is
 * softened by <see cref="ReductionSeverity"/>.
 */
public static class CageEggMath
{
    /// Vanilla chickens lay one egg per day during their prime.
    public const float EggsPerHenPerDayAtPrime = 1f;

    /// Laying holds at the prime rate until this age, then declines.
    public const float DeclineStartYears = 2f;

    /// How fast the rate falls once decline begins.
    public const float DeclineRate = 0.5f;

    /// Even a very old hen occasionally lays, so output never reaches zero.
    public const float MinLayingFraction = 0.1f;

    /// Only this share of the vanilla age-related reduction applies to a super
    /// chicken, so the drop from the prime rate is 25% less extreme.
    public const float ReductionSeverity = 0.75f;

    /// The lowest fraction a very old hen declines to, after softening.
    public static float MinSoftenedLayingFraction =>
        1f - (1f - MinLayingFraction) * ReductionSeverity;

    public static float EggsPerHenPerDay(float ageYears, float adultMinAgeYears)
    {
        if (ageYears < adultMinAgeYears)
        {
            return 0f;
        }

        if (ageYears <= DeclineStartYears)
        {
            return EggsPerHenPerDayAtPrime;
        }

        double primeFraction = 1.0 / (1.0 + DeclineRate * (ageYears - DeclineStartYears));
        float softenedReduction = (float)(1.0 - primeFraction) * ReductionSeverity;
        float rate = EggsPerHenPerDayAtPrime * (1f - softenedReduction);
        float floor = EggsPerHenPerDayAtPrime * MinSoftenedLayingFraction;
        return Math.Max(floor, rate);
    }

    /// Eggs accumulated over elapsed time at a given daily rate.
    public static float EggsOverTicks(float eggsPerDay, int ticks)
    {
        if (eggsPerDay <= 0f || ticks <= 0)
        {
            return 0f;
        }

        return eggsPerDay * ticks / CagedChickenMath.TicksPerDay;
    }

    /// How many whole twelve-egg cartons a batch of eggs fills.
    public static int FullCartons(float eggs, int eggsPerCarton)
    {
        if (eggs <= 0f || eggsPerCarton <= 0)
        {
            return 0;
        }

        return (int)Math.Floor(eggs / eggsPerCarton);
    }
}
