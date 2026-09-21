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

    /// How many whole batches a pool of eggs fills. A cage releases its eggs in
    /// whole stacks (see Building_ChickenBatteryCage), so this is the number of
    /// stacks it can spit out at once.
    public static int FullBatches(float eggs, int eggsPerBatch)
    {
        if (eggs <= 0f || eggsPerBatch <= 0)
        {
            return 0;
        }

        return (int)Math.Floor(eggs / eggsPerBatch);
    }

    /**
     * The size of one released egg stack: a whole day's laying across the
     * whole network, floored to whole eggs and never smaller than
     * <paramref name="minimum"/>. A lone cage produces at most
     * <see cref="EggsPerHenPerDayAtPrime"/> eggs per hen per day, so the floor
     * keeps a single box at its familiar size while a network scales each
     * stack to a full day of its combined output.
     */
    public static int EggsPerStack(float eggsPerDay, int minimum)
    {
        if (minimum < 1)
        {
            minimum = 1;
        }

        if (eggsPerDay <= 0f)
        {
            return minimum;
        }

        double whole = Math.Floor((double)eggsPerDay);
        if (whole >= int.MaxValue)
        {
            return int.MaxValue;
        }

        return whole > minimum ? (int)whole : minimum;
    }

    /// How many whole eggs are waiting in the box for the next release.
    public static int WholeEggsInBox(float eggs)
    {
        return eggs > 0f ? (int)Math.Floor(eggs) : 0;
    }

    /// Fractional progress toward the next egg, in the range [0, 1). Used by
    /// the inspection readout to show how close the box is to another egg.
    public static float ProgressToNextEgg(float eggs)
    {
        if (eggs <= 0f)
        {
            return 0f;
        }

        return eggs - (float)Math.Floor(eggs);
    }
}
