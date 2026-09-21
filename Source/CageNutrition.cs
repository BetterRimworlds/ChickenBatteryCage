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
 * How well fed a confined flock is.
 *
 * The state is derived from the collective nutrition store, never from an
 * individual bird, because a caged chicken has no Need_Food of its own.
 */
public enum CageNutritionState
{
    Fed,
    Hungry,
    Starving,
}

/**
 * The Verse-free arithmetic behind cage-level nutrition.
 *
 * A battery cage feeds its flock collectively: the whole store is drained by
 * the summed demand of every housed bird over elapsed game time. Keeping the
 * arithmetic here lets the feeding and starvation rules be unit tested
 * without launching RimWorld.
 */
public static class CageNutritionMath
{
    /// Nutrition a free-range vanilla chicken consumes per in-game day. The
    /// race's baseHungerRate (0.14) is not itself nutrition per day: Need_Food
    /// scales it by BaseFoodFallPerTick (2.6666667E-05) over 60,000 ticks, so
    /// a bird eats 0.14 * 1.6 = 0.224 nutrition per day.
    public const float VanillaChickenNutritionPerDay = 0.14f * 1.6f;

    /// A battery cage confines its flock in a warm, predator-free house, so a
    /// caged hen spends less energy on locomotion and climate control and
    /// wastes less feed than a free-range bird. Credit that saving explicitly
    /// rather than inheriting it from a mistaken hunger-rate conversion.
    public const float ConfinementDiscount = 0.15f;

    /// Nutrition each caged chicken consumes per in-game day: the free-range
    /// rate less the confinement discount.
    public const float DefaultNutritionPerChickenPerDay =
        VanillaChickenNutritionPerDay * (1f - ConfinementDiscount);

    /// How many days of feed a cage can hold per bird. Full cages therefore
    /// buffer a long absence without letting a player stockpile forever.
    public const float MaxNutritionPerChicken = 6f;

    /// A flock with less than this many days of feed reads as hungry.
    public const float HungryThresholdDays = 1f;

    /// A flock that has been empty of feed this many days reads as starving.
    public const float StarvingAfterDays = 0.5f;

    public static float MaxNutrition(int chickenCapacity)
    {
        return chickenCapacity * MaxNutritionPerChicken;
    }

    public static float DemandPerDay(int chickenCount, float perChickenPerDay)
    {
        return chickenCount <= 0 ? 0f : chickenCount * perChickenPerDay;
    }

    /**
     * Nutrition left after the flock has eaten for the elapsed ticks. The
     * store never goes negative: an empty cage simply stops consuming.
     */
    public static float RemainingAfter(
        float stored,
        int chickenCount,
        float perChickenPerDay,
        int elapsedTicks)
    {
        if (stored <= 0f)
        {
            return 0f;
        }

        if (chickenCount <= 0 || elapsedTicks <= 0)
        {
            return stored;
        }

        float used = DemandPerDay(chickenCount, perChickenPerDay)
            * elapsedTicks
            / CagedChickenMath.TicksPerDay;
        float left = stored - used;
        return left > 0f ? left : 0f;
    }

    /**
     * Starving time after another stretch of elapsed ticks. If the store had
     * enough feed to outlast the stretch the counter resets; otherwise only
     * the time past the moment the store ran dry is added. Keeping this exact
     * avoids counting a bird as starving while it still has feed left.
     */
    public static int StarvingTicksAfter(
        int currentStarvingTicks,
        float stored,
        int chickenCount,
        float perChickenPerDay,
        int elapsedTicks)
    {
        if (chickenCount <= 0 || elapsedTicks <= 0)
        {
            return 0;
        }

        float demandPerTick = DemandPerDay(chickenCount, perChickenPerDay)
            / CagedChickenMath.TicksPerDay;
        if (demandPerTick <= 0f)
        {
            return 0;
        }

        float ticksOfFeed = stored > 0f ? stored / demandPerTick : 0f;
        if (elapsedTicks <= ticksOfFeed)
        {
            return 0;
        }

        double starvingElapsed = elapsedTicks - ticksOfFeed;
        double total = currentStarvingTicks + starvingElapsed;
        return total > int.MaxValue ? int.MaxValue : (int)total;
    }

    public static CageNutritionState Classify(
        float stored,
        int chickenCount,
        float perChickenPerDay,
        float starvingDays)
    {
        if (chickenCount <= 0)
        {
            return CageNutritionState.Fed;
        }

        if (starvingDays >= StarvingAfterDays)
        {
            return CageNutritionState.Starving;
        }

        if (stored <= 0f)
        {
            return CageNutritionState.Hungry;
        }

        float oneDay = DemandPerDay(chickenCount, perChickenPerDay) * HungryThresholdDays;
        return stored < oneDay ? CageNutritionState.Hungry : CageNutritionState.Fed;
    }
}
