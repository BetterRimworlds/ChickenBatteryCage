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

[TestFixture]
public class CageMortalityTests
{
    const int Day = CagedChickenMath.TicksPerDay;

    [Test]
    public void ExposureBeginsAtEntryOrLastSettlementWhicheverIsLater()
    {
        Assert.AreEqual(900, CageMortalityMath.ExposureStartTick(100, 900));
        Assert.AreEqual(900, CageMortalityMath.ExposureStartTick(900, 100));
    }

    [Test]
    public void ExposureBeginsAtTheLaterTickAcrossTheWrap()
    {
        // A hen housed just before the counter wrapped keeps a positive entry
        // tick, while the settle marker has gone negative. The settle is the
        // later tick in wrapping order, so the start must not jump back to the
        // numerically larger pre-wrap entry.
        const int JustBeforeWrap = int.MaxValue - 100;
        const int JustAfterWrap = int.MinValue + 100;

        Assert.AreEqual(JustAfterWrap,
            CageMortalityMath.ExposureStartTick(JustAfterWrap, JustBeforeWrap));
        Assert.AreEqual(JustAfterWrap,
            CageMortalityMath.ExposureStartTick(JustBeforeWrap, JustAfterWrap));
    }

    [Test]
    public void AccumulatedExposureSurvivesSettlementBoundaries()
    {
        double exposure = CageMortalityMath.ExposureOverTicks(0.2f, Day / 3)
            + CageMortalityMath.ExposureOverTicks(0.2f, Day - Day / 3);
        Assert.AreEqual(0.2f, CageMortalityMath.ChanceFromExposure(exposure), 0.000001f);
        Assert.AreEqual(0f, CageMortalityMath.ExposureOverTicks(0.2f, -1));
    }

    [Test]
    public void NoHazardMeansNoDeaths()
    {
        Assert.AreEqual(0f, CageMortalityMath.ChanceOverTicks(0f, Day));
    }

    [Test]
    public void OneDayOfHazardIsTheDailyChance()
    {
        Assert.AreEqual(0.1f, CageMortalityMath.ChanceOverTicks(0.1f, Day), 0.0001f);
    }

    [Test]
    public void HalfADayIsLessThanAFullDay()
    {
        float half = CageMortalityMath.ChanceOverTicks(0.1f, Day / 2);
        float full = CageMortalityMath.ChanceOverTicks(0.1f, Day);

        Assert.Less(half, full);
        Assert.Greater(half, 0f);
    }

    [Test]
    public void CompoundingTwoHalvesMatchesOneFull()
    {
        float daily = 0.2f;
        float full = CageMortalityMath.ChanceOverTicks(daily, Day);
        float halfA = CageMortalityMath.ChanceOverTicks(daily, Day / 2);
        float halfB = CageMortalityMath.ChanceOverTicks(daily, Day / 2);
        float twoHalves = 1f - (1f - halfA) * (1f - halfB);

        Assert.AreEqual(full, twoHalves, 0.0005f);
    }

    [Test]
    public void HazardAtLifeExpectancyIsTheCalibrationPoint()
    {
        float chance = CageMortalityMath.NaturalDailyChance(6f, 6f);

        Assert.AreEqual(CageMortalityMath.NaturalDailyChanceAtLifeExpectancy, chance, 0.0000001f);
    }

    [Test]
    public void AgeExposureIsIndependentOfSettlementPartitioning()
    {
        long age = 8L * CagedChickenMath.TicksPerYear;
        double whole = CageMortalityMath.NaturalExposureOverTicks(age, 6f, Day);
        double split = CageMortalityMath.NaturalExposureOverTicks(age, 6f, 12345)
            + CageMortalityMath.NaturalExposureOverTicks(age + 12345, 6f, Day - 12345);
        Assert.AreEqual(whole, split, 0.00000001);
    }

    [Test]
    public void AgeExposureUsesTheIntervalRatherThanOnlyTheEndingAge()
    {
        long age = 6L * CagedChickenMath.TicksPerYear;
        int year = CagedChickenMath.TicksPerYear;
        double integrated = CageMortalityMath.NaturalExposureOverTicks(age, 6f, year);
        double endOnly = CageMortalityMath.ExposureOverTicks(
            CageMortalityMath.NaturalDailyChance(7f, 6f), year);
        Assert.Less(integrated, endOnly);
        Assert.Greater(integrated, CageMortalityMath.ExposureOverTicks(0.001f, year));
    }

    [Test]
    public void YoungBirdsRarelyDie()
    {
        float chick = CageMortalityMath.NaturalDailyChance(0.2f, 6f);
        float adult = CageMortalityMath.NaturalDailyChance(6f, 6f);

        Assert.Less(chick, adult);
        Assert.Greater(chick, 0f);
    }

    [Test]
    public void HazardClimbsWithAgeWithoutEverBeingCertain()
    {
        float six = CageMortalityMath.NaturalDailyChance(6f, 6f);
        float ten = CageMortalityMath.NaturalDailyChance(10f, 6f);

        Assert.Greater(ten, six);
        // A ten-year-old chicken is unlikely to die on any given day, so old
        // outliers remain possible.
        Assert.Less(ten, CageMortalityMath.MaxDailyChance);
    }

    [Test]
    public void FedFlockSuffersNoStarvationHazard()
    {
        Assert.AreEqual(0f, CageMortalityMath.StarvationDailyChance(0f));
        Assert.AreEqual(
            0f,
            CageMortalityMath.StarvationDailyChance(CageNutritionMath.StarvingAfterDays));
    }

    [Test]
    public void StarvationHazardGrowsWithTimeWithoutFeed()
    {
        float oneDay = CageMortalityMath.StarvationDailyChance(1f);
        float threeDays = CageMortalityMath.StarvationDailyChance(3f);

        Assert.Greater(oneDay, 0f);
        Assert.Greater(threeDays, oneDay);
        Assert.LessOrEqual(threeDays, CageMortalityMath.MaxDailyChance);
    }

    [Test]
    public void CombinedChanceIsCappedBelowCertainty()
    {
        float combined = CageMortalityMath.CombinedDailyChance(0.4f, 0.4f);

        Assert.AreEqual(CageMortalityMath.MaxDailyChance, combined, 0.0001f);
    }

    [Test]
    public void AvailableFeedDelaysStarvationExposure()
    {
        long age = CagedChickenMath.TicksPerYear;
        float demand = CageNutritionMath.DefaultNutritionPerChickenPerDay;
        double start = CageMortalityMath.StarvingDaysAtStart(12345, demand * 10, 10, demand);
        Assert.AreEqual(-1.0, start, 0.000001);
        Assert.AreEqual(CageMortalityMath.NaturalExposureOverTicks(age, 6f, Day),
            CageMortalityMath.CombinedExposureOverTicks(age, 6f, start, Day), 0.00000001);
    }

    [Test]
    public void CrossingStarvationThresholdOnlyChargesTimeAfterTheThreshold()
    {
        long age = CagedChickenMath.TicksPerYear;
        double whole = CageMortalityMath.CombinedExposureOverTicks(age, 6f, 0.49, 2500);
        double split = CageMortalityMath.NaturalExposureOverTicks(age, 6f, 600)
            + CageMortalityMath.CombinedExposureOverTicks(age + 600, 6f, 0.5, 1900);
        Assert.AreEqual(split, whole, 0.00000001);
        Assert.Less(whole, CageMortalityMath.ExposureOverTicks(
            CageMortalityMath.CombinedDailyChance(
                CageMortalityMath.NaturalDailyChance(1f, 6f),
                CageMortalityMath.StarvationDailyChance(0.49f + 2500f / Day)), 2500));
    }

    [Test]
    public void FeedingRetainsAlreadyAccruedStarvationExposure()
    {
        long age = CagedChickenMath.TicksPerYear;
        double beforeFeed = CageMortalityMath.CombinedExposureOverTicks(age, 6f, 2.0, 1000);
        double afterFeed = CageMortalityMath.CombinedExposureOverTicks(age + 1000, 6f, -1.0, 1500);
        float accumulated = CageMortalityMath.ChanceFromExposure(beforeFeed + afterFeed);
        float fedOnly = CageMortalityMath.ChanceFromExposure(
            CageMortalityMath.NaturalExposureOverTicks(age, 6f, 2500));
        Assert.Greater(accumulated, fedOnly * 100);
    }

    [Test]
    public void SaturatedStarvationExposureMatchesConstantCappedChance()
    {
        Assert.AreEqual(CageMortalityMath.ExposureOverTicks(0.5f, Day),
            CageMortalityMath.CombinedExposureOverTicks(0, 6f, 20.0, Day), 0.00000001);
    }
}
