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
 * The Verse-free arithmetic behind caged chicken aging.
 *
 * Keeping this separate from CagedChickenRecord lets the exact-aging rules be
 * unit tested without launching RimWorld. The production record merely feeds
 * its persisted fields through these functions.
 */
public static class CagedChickenMath
{
    /// Mirrors Verse.GenDate.TicksPerYear: 60,000 ticks per day, 60 days per
    /// year. Kept here only so the math stays usable outside the game.
    public const int TicksPerYear = 3600000;

    public const int TicksPerDay = 60000;

    /**
     * Exact biological age from stored entry data. No scheduled task mutates
     * the record while a bird is caged; elapsed time alone supplies the age.
     */
    public static long BiologicalAgeTicksAt(
        long biologicalAgeTicksAtEntry,
        int enteredAtGameTick,
        int currentGameTick)
    {
        return biologicalAgeTicksAtEntry + (long)(currentGameTick - enteredAtGameTick);
    }

    public static bool IsAdult(long biologicalAgeTicks, long adultMinAgeTicks)
    {
        return biologicalAgeTicks >= adultMinAgeTicks;
    }

    /**
     * Which single developmental stage a bird of this age is generated with.
     *
     * RimWorld's generator applies one stage per pawn and logs an error if a
     * request asks for several at once ("Trying to generate a newborn and
     * other developmental stages simultaneously"), so exactly one must be
     * chosen. A chicken has a single growth threshold — chick to adult — hence
     * a bird is either a newborn or an adult.
     *
     * An unknown threshold (zero, the defs not being loaded yet) resolves to
     * Adult, so an unresolved threshold never claims a newborn's stage.
     */
    public static bool IsNewbornStage(long biologicalAgeTicks, long adultMinAgeTicks)
    {
        return adultMinAgeTicks > 0 && biologicalAgeTicks < adultMinAgeTicks;
    }
}
