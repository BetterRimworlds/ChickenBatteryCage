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

using NUnit.Framework;

namespace BetterRimworlds.ChickenBatteryCage.Tests;

/**
 * Covers the cage-level nutrition contract: a flock consumes its collective
 * store by summed demand over elapsed time, never by any per-bird schedule.
 */
[TestFixture]
public class CageNutritionTests
{
    const int Day = CagedChickenMath.TicksPerDay;
    const float PerChicken = CageNutritionMath.DefaultNutritionPerChickenPerDay;

    [Test]
    public void DemandScalesWithFlockSize()
    {
        Assert.AreEqual(10 * PerChicken, CageNutritionMath.DemandPerDay(10, PerChicken), 0.0001f);
        Assert.AreEqual(0f, CageNutritionMath.DemandPerDay(0, PerChicken));
    }

    [Test]
    public void OneDayOfFeedIsConsumedExactly()
    {
        // 10 birds, one day, starting with a full day's feed, ends empty.
        float oneDay = CageNutritionMath.DemandPerDay(10, PerChicken);
        float left = CageNutritionMath.RemainingAfter(oneDay, 10, PerChicken, Day);

        Assert.AreEqual(0f, left, 0.0001f);
    }

    [Test]
    public void HalfTheTimeConsumesHalfTheFeed()
    {
        float oneDay = CageNutritionMath.DemandPerDay(4, PerChicken);
        float left = CageNutritionMath.RemainingAfter(oneDay, 4, PerChicken, Day / 2);

        Assert.AreEqual(oneDay / 2f, left, 0.0001f);
    }

    [Test]
    public void EmptyStoreNeverGoesNegative()
    {
        float left = CageNutritionMath.RemainingAfter(0f, 10, PerChicken, 30 * Day);

        Assert.AreEqual(0f, left);
    }

    [Test]
    public void EmptyCageConsumesNothing()
    {
        float left = CageNutritionMath.RemainingAfter(5f, 0, PerChicken, 10 * Day);

        Assert.AreEqual(5f, left);
    }

    [Test]
    public void CapacityScalesPerBird()
    {
        Assert.AreEqual(
            CageNutritionMath.MaxNutritionPerChicken * 10f,
            CageNutritionMath.MaxNutrition(10),
            0.0001f);
    }

    [Test]
    public void WellStockedFlockReadsFed()
    {
        CageNutritionState state = CageNutritionMath.Classify(10f, 10, PerChicken, 0f);

        Assert.AreEqual(CageNutritionState.Fed, state);
    }

    [Test]
    public void EmptyStoreReadsHungryUntilItHasStarvedAWhile()
    {
        Assert.AreEqual(
            CageNutritionState.Hungry,
            CageNutritionMath.Classify(0f, 10, PerChicken, 0f));
        Assert.AreEqual(
            CageNutritionState.Starving,
            CageNutritionMath.Classify(0f, 10, PerChicken, CageNutritionMath.StarvingAfterDays));
    }
}
