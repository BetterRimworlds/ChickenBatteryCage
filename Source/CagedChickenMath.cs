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

    /**
     * Wrapping game-tick arithmetic.
     *
     * GenTicks.TicksAbs is an int that wraps: after roughly 596 in-game years
     * it passes int.MaxValue and continues from int.MinValue. Elapsed time is
     * still exact when measured with a wrapping int subtraction, but a plain
     * "tick > now" comparison mistakes a legitimate pre-wrap tick for a future
     * one and resets it, silently discarding the elapsed time it encoded.
     * These helpers compare and subtract ticks the same wrapping way the aging
     * math already does.
     */

    /// Ticks elapsed from <paramref name="fromTick"/> to
    /// <paramref name="now"/>, correct across one wrap of the counter.
    public static int TicksSince(int fromTick, int now)
    {
        return now - fromTick;
    }

    /// True when <paramref name="later"/> names a tick after
    /// <paramref name="earlier"/> in the wrapping game-tick order.
    public static bool IsAfter(int later, int earlier)
    {
        return later - earlier > 0;
    }

    /// True when <paramref name="tick"/> lies after <paramref name="now"/>,
    /// which can only be corrupt data: a real tick is never in the future.
    public static bool IsFutureTick(int tick, int now)
    {
        return now - tick < 0;
    }

    /**
     * True when a persisted tick cannot be a real point at or before now and
     * must be reset: either a future tick, or a negative value while the
     * counter has not yet wrapped. Ticks only go negative after a wrap, so a
     * negative tick paired with a nonnegative now is corrupt.
     */
    public static bool IsImpossibleTick(int tick, int now)
    {
        return IsFutureTick(tick, now) || (tick < 0 && now >= 0);
    }
}
