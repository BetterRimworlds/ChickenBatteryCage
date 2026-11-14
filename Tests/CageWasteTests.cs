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

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void MaintainedSmallFlocksCanBeCleanedBeforeToxicExposure(int hens)
    {
        // Exercise both production extremes, including the fractional waste
        // carried between visits when a whole pack is not yet collectible.
        foreach (float rate in new[] {
            CageWasteMath.MinPacksPerHenPerDay,
            CageWasteMath.MaxPacksPerHenPerDay })
        {
            float waste = 0f;
            int ticksSinceCleaning = 0;
            int visits = 0;
            for (int day = 0; day < 60; day++)
            {
                waste += hens * rate;
                ticksSinceCleaning += Day;
                Assert.Less(waste, CageWasteMath.SafeWastePacks(hens),
                    "daily maintenance must remain below toxic exposure");
                Assert.AreEqual(CageWasteMath.ToxicTrend.Stable,
                    CageWasteMath.ClassifyTrend(0f, waste, hens));

                if (CageWasteMath.NeedsCleaning(waste, hens, ticksSinceCleaning))
                {
                    Assert.Greater(CageWasteMath.WholePacks(waste), 0);
                    waste = CageWasteMath.FractionalPacks(waste);
                    ticksSinceCleaning = 0;
                    visits++;
                }
            }

            Assert.Greater(visits, 0, "small flocks must allow productive cleaning");
        }
    }

    [Test]
    public void FiftyHensMakeTwentySixAndAQuarterPacksPerDayOnAverage()
    {
        Assert.AreEqual(26.25f, CageWasteMath.PacksOverTicks(50, Day), Epsilon);
    }

    [Test]
    public void TenHensMakeFiveAndAQuarterPacksPerDayOnAverage()
    {
        Assert.AreEqual(5.25f, CageWasteMath.PacksOverTicks(10, Day), Epsilon);
        Assert.AreEqual(26.25f, CageWasteMath.PacksOverTicks(10, 5 * Day), Epsilon);
    }

    [Test]
    public void HundredHensMakeFiftyTwoAndAHalfPacksPerDayOnAverage()
    {
        Assert.AreEqual(52.5f, CageWasteMath.PacksOverTicks(100, Day), Epsilon);
    }

    [Test]
    public void DailyProductionVariesWithinTheSuperchickenBand()
    {
        const uint seed = 424242u;
        bool sawLow = false;
        bool sawHigh = false;
        float previous = -1f;

        for (int day = 0; day < 30; day++)
        {
            float packs = CageWasteMath.PacksOverTicks(100, day * Day, Day, seed);

            Assert.GreaterOrEqual(packs, 25f - Epsilon);
            Assert.LessOrEqual(packs, 80f + Epsilon);
            sawLow |= packs < 40f;
            sawHigh |= packs > 65f;
            Assert.IsTrue(day == 0
                || System.Math.Abs(packs - previous) > Epsilon);
            previous = packs;
        }

        Assert.IsTrue(sawLow, "the band must actually roll low days");
        Assert.IsTrue(sawHigh, "the band must actually roll high days");
    }

    [Test]
    public void RandomProductionSplitsCleanlyAtDayBoundaries()
    {
        const uint seed = 90210u;
        const int start = 12345;
        int cut = Day - start;

        float whole = CageWasteMath.PacksOverTicks(100, start, 2 * Day, seed);
        float first = CageWasteMath.PacksOverTicks(100, start, cut, seed);
        float second = CageWasteMath.PacksOverTicks(100, start + cut, 2 * Day - cut, seed);

        Assert.AreEqual(whole, first + second, 0.001f);
    }

    [Test]
    public void TheDailyRollIsStableForASeedAndDay()
    {
        Assert.AreEqual(
            CageWasteMath.RateForDay(7u, 42),
            CageWasteMath.RateForDay(7u, 42),
            Epsilon);
        Assert.That(
            System.Math.Abs(
                CageWasteMath.RateForDay(7u, 42)
                - CageWasteMath.RateForDay(7u, 43)),
            Is.GreaterThan(Epsilon));
    }

    [Test]
    public void NoHensProduceNoManure()
    {
        Assert.AreEqual(0f, CageWasteMath.PacksOverTicks(0, 1000 * Day));
    }

    [Test]
    public void FractionalManureAccumulatesWithoutRoundingAway()
    {
        float first = CageWasteMath.PacksOverTicks(1, Day);
        float second = CageWasteMath.PacksOverTicks(1, Day);

        Assert.AreEqual(0.525f, first, Epsilon);
        Assert.AreEqual(1.05f, first + second, Epsilon);
        Assert.AreEqual(0, CageWasteMath.WholePacks(first));
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
        float stored = CageWasteMath.PacksOverTicks(1, Day);
        float afterReload = stored + CageWasteMath.PacksOverTicks(1, Day);

        Assert.AreEqual(1.05f, afterReload, Epsilon);
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
        // The reference daily output is the mean of the 25-80 band.
        Assert.AreEqual(52.5f, CageWasteMath.CleaningThresholdPacks(100), Epsilon);
        Assert.AreEqual(5.25f, CageWasteMath.CleaningThresholdPacks(10), Epsilon);
    }

    [Test]
    public void CleaningThresholdNeverFallsBelowOneCollectiblePack()
    {
        // A lone hen would otherwise reference 0.525 packs, but a cleaning
        // cannot remove less than one whole pack.
        Assert.AreEqual(1f, CageWasteMath.CleaningThresholdPacks(1), Epsilon);
        Assert.AreEqual(1.05f, CageWasteMath.CleaningThresholdPacks(2), Epsilon);
    }

    [Test]
    public void CleaningStatusFollowsTheDailyClockAndTheGraceBand()
    {
        const int due = CageWasteMath.CleaningIntervalTicks;

        // Clean while the 24-hour clock is still running.
        Assert.AreEqual(
            CageWasteMath.CleaningStatus.Clean,
            CageWasteMath.ClassifyCleaning(60f, 100, due - 1));

        // Due once a full day has passed.
        Assert.AreEqual(
            CageWasteMath.CleaningStatus.NeedsCleaning,
            CageWasteMath.ClassifyCleaning(60f, 100, due));

        // Toxic filth outranks the clock.
        Assert.AreEqual(
            CageWasteMath.CleaningStatus.Critical,
            CageWasteMath.ClassifyCleaning(525f, 100, 0));
    }

    [Test]
    public void CleaningIsNotAdvertisedWithoutAWholePack()
    {
        const int due = CageWasteMath.CleaningIntervalTicks;

        // One hen's waste reaches its one-pack threshold after two days, but
        // there is still nothing a cleaner could collect before then.
        Assert.AreEqual(
            CageWasteMath.CleaningStatus.Clean,
            CageWasteMath.ClassifyCleaning(0.9f, 1, due));
        Assert.AreEqual(
            CageWasteMath.CleaningStatus.NeedsCleaning,
            CageWasteMath.ClassifyCleaning(1f, 1, due));
        Assert.AreEqual(
            CageWasteMath.CleaningStatus.Critical,
            CageWasteMath.ClassifyCleaning(5.25f, 1, due));

        // A cage emptied of hens keeps its leftover manure, but again only a
        // whole pack is worth a service visit.
        Assert.AreEqual(
            CageWasteMath.CleaningStatus.Clean,
            CageWasteMath.ClassifyCleaning(0.5f, 0, due));
        Assert.AreEqual(
            CageWasteMath.CleaningStatus.NeedsCleaning,
            CageWasteMath.ClassifyCleaning(1.5f, 0, due));
    }

    [Test]
    public void CleaningWaitsForTheFullDailyCountdown()
    {
        const int due = CageWasteMath.CleaningIntervalTicks;

        Assert.AreEqual(24f, CageWasteMath.HoursUntilCleaning(0), Epsilon);
        Assert.AreEqual(12f, CageWasteMath.HoursUntilCleaning(due / 2), Epsilon);
        Assert.AreEqual(0f, CageWasteMath.HoursUntilCleaning(due), Epsilon);
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
        // A hundred hens, no waste, average a pack every 1/52.5 days, and the
        // daily cleaning countdown is one full day.
        Assert.AreEqual(1f / 52.5f, CageWasteMath.DaysUntilNextPack(0f, 100), Epsilon);
        Assert.AreEqual(24f, CageWasteMath.HoursUntilCleaning(0), Epsilon);
    }
}
