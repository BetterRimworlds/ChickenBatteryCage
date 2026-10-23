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
 * Covers the cage-level manure contract: production is a pure function of hen
 * count and elapsed time, cleaning removes only whole packs, and the virtual
 * Toxic Buildup follows vanilla's gain and recovery bands.
 */
[TestFixture]
public class CageWasteTests
{
    const int Day = CagedChickenMath.TicksPerDay;
    const float Epsilon = 0.0001f;

    [Test]
    public void FiftyHensMakeOnePackPerDay()
    {
        Assert.AreEqual(1f, CageWasteMath.PacksOverTicks(50, Day), Epsilon);
    }

    [Test]
    public void TenHensMakeOnePackPerFiveDays()
    {
        Assert.AreEqual(0.2f, CageWasteMath.PacksOverTicks(10, Day), Epsilon);
        Assert.AreEqual(1f, CageWasteMath.PacksOverTicks(10, 5 * Day), Epsilon);
    }

    [Test]
    public void HundredHensMakeTwoPacksPerDay()
    {
        Assert.AreEqual(2f, CageWasteMath.PacksOverTicks(100, Day), Epsilon);
    }

    [Test]
    public void NoHensProduceNoManure()
    {
        Assert.AreEqual(0f, CageWasteMath.PacksOverTicks(0, 1000 * Day));
    }

    [Test]
    public void FractionalManureAccumulatesWithoutRoundingAway()
    {
        float first = CageWasteMath.PacksOverTicks(10, Day);
        float second = CageWasteMath.PacksOverTicks(10, Day);

        Assert.AreEqual(0.4f, first + second, Epsilon);
        Assert.AreEqual(0, CageWasteMath.WholePacks(first + second));
    }

    [Test]
    public void LongElapsedIntervalsEqualEquivalentShorterOnes()
    {
        float oneShot = CageWasteMath.PacksOverTicks(10, 10 * Day);

        float stepwise = 0f;
        for (int i = 0; i < 10; i++)
        {
            stepwise += CageWasteMath.PacksOverTicks(10, Day);
        }

        Assert.AreEqual(oneShot, stepwise, Epsilon);
    }

    [Test]
    public void WrappedTickIntervalsDoNotDuplicateManure()
    {
        // 201 ticks measured across the wrap of the game-tick counter.
        int justBeforeWrap = int.MaxValue - 100;
        int justAfterWrap = int.MinValue + 100;
        int elapsed = CagedChickenMath.TicksSince(justBeforeWrap, justAfterWrap);

        Assert.AreEqual(201, elapsed);
        Assert.AreEqual(
            CageWasteMath.PacksOverTicks(50, 201),
            CageWasteMath.PacksOverTicks(50, elapsed),
            Epsilon);
        Assert.GreaterOrEqual(elapsed, 0, "a wrapped interval must never read as negative");
    }

    [Test]
    public void SaveLoadRoundTripKeepsFractionalProgress()
    {
        // A stored total survives a save untouched; adding a later interval
        // must build on it, not restart from zero.
        float stored = CageWasteMath.PacksOverTicks(10, Day);
        float afterReload = stored + CageWasteMath.PacksOverTicks(10, Day);

        Assert.AreEqual(0.4f, afterReload, Epsilon);
    }

    [Test]
    public void CleaningRemovesOnlyWholePacks()
    {
        Assert.AreEqual(5, CageWasteMath.WholePacks(5.7f));
        Assert.AreEqual(0.7f, CageWasteMath.FractionalPacks(5.7f), Epsilon);

        float remaining = CageWasteMath.FractionalPacks(5.7f);
        Assert.AreEqual(0.7f, remaining, Epsilon);
    }

    [Test]
    public void ClusterTotalDoesNotDoubleCount()
    {
        Assert.AreEqual(0.75f, CageWasteMath.TotalWaste(new[] { 0.5f, 0.25f, 0f }), Epsilon);
    }

    [Test]
    public void CleaningThresholdScalesToTheFlock()
    {
        // A hundred hens make two packs a day, so the five-day threshold is ten
        // packs, exactly the intended service interval.
        Assert.AreEqual(10f, CageWasteMath.CleaningThresholdPacks(100), Epsilon);
        Assert.AreEqual(1f, CageWasteMath.CleaningThresholdPacks(10), Epsilon);
    }

    [Test]
    public void CleaningThresholdNeverFallsBelowOneCollectiblePack()
    {
        // A lone hen would otherwise request cleaning at 0.1 packs, but a
        // cleaning cannot remove less than one whole pack.
        Assert.AreEqual(1f, CageWasteMath.CleaningThresholdPacks(1), Epsilon);
        Assert.AreEqual(1f, CageWasteMath.CleaningThresholdPacks(5), Epsilon);
    }

    [Test]
    public void CleaningStatusFollowsTheGraceBand()
    {
        // Clean below the threshold, needs cleaning up to the safe level,
        // critical beyond it.
        Assert.AreEqual(
            CageWasteMath.CleaningStatus.Clean,
            CageWasteMath.ClassifyCleaning(4f, 100));
        Assert.AreEqual(
            CageWasteMath.CleaningStatus.NeedsCleaning,
            CageWasteMath.ClassifyCleaning(12f, 100));
        Assert.AreEqual(
            CageWasteMath.CleaningStatus.Critical,
            CageWasteMath.ClassifyCleaning(25f, 100));
    }

    [Test]
    public void CleaningIsNotAdvertisedWithoutAWholePack()
    {
        // One hen's waste passes its safe band at 0.2 packs, but there is still
        // nothing a cleaner could collect until a whole pack has accrued.
        Assert.AreEqual(
            CageWasteMath.CleaningStatus.Clean,
            CageWasteMath.ClassifyCleaning(0.9f, 1));
        Assert.AreEqual(
            CageWasteMath.CleaningStatus.Critical,
            CageWasteMath.ClassifyCleaning(1f, 1));

        // A cage emptied of hens keeps its leftover manure, but again only a
        // whole pack is worth a service visit.
        Assert.AreEqual(
            CageWasteMath.CleaningStatus.Clean,
            CageWasteMath.ClassifyCleaning(0.5f, 0));
        Assert.AreEqual(
            CageWasteMath.CleaningStatus.NeedsCleaning,
            CageWasteMath.ClassifyCleaning(1.5f, 0));
    }

    [Test]
    public void ToxicBuildupStaysAtZeroInsideTheGraceBand()
    {
        float severity = CageWasteMath.ToxicBuildupAfter(0f, 0f, 100, 5 * Day);

        Assert.AreEqual(0f, severity, Epsilon);
    }

    [Test]
    public void ToxicBuildupGrowsAtTheVanillaPollutedRateOnceNeglected()
    {
        // Start already past the safe level; two days at 0.4 a day.
        float safe = CageWasteMath.SafeWastePacks(100);
        float severity = CageWasteMath.ToxicBuildupAfter(0f, safe + 1f, 100, 2 * Day);

        Assert.AreEqual(0.8f, severity, Epsilon);
    }

    [Test]
    public void ToxicBuildupRecoversWhileClean()
    {
        float severity = CageWasteMath.ToxicBuildupAfter(0.5f, 0f, 100, Day);

        Assert.AreEqual(0.42f, severity, Epsilon);
    }

    [Test]
    public void ToxicBuildupLongIntervalMatchesTwoShortOnes()
    {
        float oneShot = CageWasteMath.ToxicBuildupAfter(0.5f, 9f, 50, 2 * Day);

        float first = CageWasteMath.ToxicBuildupAfter(0.5f, 9f, 50, Day);
        float second = CageWasteMath.ToxicBuildupAfter(first, 10f, 50, Day);

        Assert.AreEqual(oneShot, second, Epsilon);
    }

    [Test]
    public void ToxicBuildupIsRepairedIntoTheVanillaBand()
    {
        Assert.AreEqual(0f, CageWasteMath.RepairSeverity(-3f));
        Assert.AreEqual(1f, CageWasteMath.RepairSeverity(5f));
        Assert.AreEqual(0f, CageWasteMath.RepairSeverity(float.NaN));
    }

    [Test]
    public void ToxicBuildupIsForgottenWhenTheFlockIsEmpty()
    {
        // A departed flock's exposure must not pass to the next birds placed
        // in the cage, even before any elapsed time is settled.
        Assert.AreEqual(0f, CageWasteMath.ToxicBuildupAfter(0.8f, 20f, 0, Day), Epsilon);
        Assert.AreEqual(0f, CageWasteMath.ToxicBuildupAfter(0.8f, 20f, 0, 0), Epsilon);
    }

    [Test]
    public void ToxicTrendReportsDirection()
    {
        Assert.AreEqual(
            CageWasteMath.ToxicTrend.Increasing,
            CageWasteMath.ClassifyTrend(0.5f, CageWasteMath.SafeWastePacks(100) + 1f, 100));
        Assert.AreEqual(
            CageWasteMath.ToxicTrend.Recovering,
            CageWasteMath.ClassifyTrend(0.5f, 0f, 100));
        Assert.AreEqual(
            CageWasteMath.ToxicTrend.Stable,
            CageWasteMath.ClassifyTrend(0f, 0f, 100));
    }

    [Test]
    public void ToxicMortalityOnlyAppearsPastTheOnsetSeverity()
    {
        Assert.AreEqual(0f, CageWasteMath.ToxicDailyChance(0.1f));
        Assert.Greater(CageWasteMath.ToxicDailyChance(1f), 0f);
        Assert.LessOrEqual(CageWasteMath.ToxicDailyChance(1f), CageWasteMath.MaxToxicDailyChance);
    }

    [Test]
    public void ToxicExposureChargesEachIntervalAtItsOwnSeverity()
    {
        // Half a day very dirty followed by half a day recovered must charge
        // more than judging the whole day by the final, lower severity.
        int halfDay = Day / 2;
        double dirtyThenRecovered =
            CageWasteMath.ToxicExposureOverTicks(0.8f, halfDay)
            + CageWasteMath.ToxicExposureOverTicks(0.3f, halfDay);
        double finalRateOnly = CageWasteMath.ToxicExposureOverTicks(0.3f, Day);

        Assert.Greater(dirtyThenRecovered, finalRateOnly);
        Assert.AreEqual(0.0, CageWasteMath.ToxicExposureOverTicks(0.1f, Day));
    }

    [Test]
    public void EstimatedTimesUseTheFlockRate()
    {
        // A hundred hens, no waste, produce a pack every half day, and reach
        // the ten-pack cleaning threshold in five days.
        Assert.AreEqual(0.5f, CageWasteMath.DaysUntilNextPack(0f, 100), Epsilon);
        Assert.AreEqual(5f, CageWasteMath.DaysUntilCleaningThreshold(0f, 100), Epsilon);
    }
}
