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

    /// Nutrition a cage can hold per bird. A ten-bird cage therefore buffers
    /// 33 nutrition, about seventeen days of feed for a full flock, which
    /// covers a long absence without letting a player stockpile forever.
    public const float MaxNutritionPerChicken = 3.3f;

    /// A flock with less than this many days of feed reads as hungry.
    public const float HungryThresholdDays = 1f;

    /// In-game hours in a day, used to express the starvation clock the way a
    /// player observes it.
    public const float HoursPerDay = 24f;

    /// Hours an empty store may last before the flock begins to starve.
    /// Measured in vanilla RimWorld: a chicken's Food need runs out about 27
    /// hours after its last meal, and that is when Malnutrition appears.
    public const float StarvationOnsetHours = 27f;

    /// Severity gained per hour once starving. Measured in vanilla RimWorld:
    /// malnutrition climbs about 5% every two hours.
    public const float StarvationSeverityPerHour = 0.05f / 2f;

    /// Severity at which vanilla malnutrition is lethal.
    public const float LethalStarvationSeverity = 1f;

    /// A flock that has been empty of feed this many days reads as starving.
    public const float StarvingAfterDays = StarvationOnsetHours / HoursPerDay;

    /// Hours from the onset of starvation to a lethal severity. With the
    /// measured 5% every two hours this is forty hours, so a flock left with
    /// no feed at all dies about 67 hours after its last meal.
    public const float HoursFromOnsetToLethal =
        LethalStarvationSeverity / StarvationSeverityPerHour;

    public static float MaxNutrition(int chickenCapacity)
    {
        return chickenCapacity * MaxNutritionPerChicken;
    }

    public static float DemandPerDay(int chickenCount, float perChickenPerDay)
    {
        return chickenCount <= 0 ? 0f : chickenCount * perChickenPerDay;
    }

    /// In-game days the given store lasts at the flock's summed appetite.
    /// Fewer birds stretch the same feed further; a flock that consumes
    /// nothing has no finite horizon, so it reports zero.
    public static float DaysOfFeed(float stored, int chickenCount, float perChickenPerDay)
    {
        float demand = DemandPerDay(chickenCount, perChickenPerDay);
        return demand <= 0f ? 0f : stored / demand;
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
