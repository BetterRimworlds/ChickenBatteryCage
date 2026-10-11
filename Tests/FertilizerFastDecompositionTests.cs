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
 * Pins the fast-build fertilizer clock: one day in fair weather, four times
 * that rate while the pack is exposed to bad weather. The compile flag that
 * turns the clock on is not visible to this assembly.
 */
[TestFixture]
public class FertilizerFastDecompositionTests
{
    const int Day = CagedChickenMath.TicksPerDay;

    /// Mirrors Verse.GenTicks.TickRareInterval. Fertilizer packs tick rarely.
    const int RareInterval = 250;

    [Test]
    public void FairWeatherTakesOneDay()
    {
        Assert.AreEqual(1f, FertilizerFastDecompositionMath.DaysToDissolve);
        Assert.AreEqual(Day, FertilizerFastDecompositionMath.TicksToDissolve);
        Assert.AreEqual(Day, FertilizerFastDecompositionMath.RealTicksRemaining(0, false));
    }

    [Test]
    public void ADayOfRareTicksDissolvesTheStack()
    {
        int accumulated = 0;
        int intervals = Day / RareInterval;
        for (int i = 0; i < intervals; i++)
        {
            accumulated += FertilizerFastDecompositionMath.EquivalentTicks(RareInterval, false);
        }

        Assert.AreEqual(Day, accumulated);
        Assert.IsTrue(FertilizerFastDecompositionMath.ShouldDissolve(accumulated));
        Assert.IsFalse(FertilizerFastDecompositionMath.ShouldDissolve(accumulated - RareInterval));
    }

    [Test]
    public void BadWeatherDissolvesFourTimesFaster()
    {
        int multiplier = (int)CageDegradationMath.BadWeatherMultiplier;
        int accumulated = 0;
        int intervals = Day / RareInterval / multiplier;
        for (int i = 0; i < intervals; i++)
        {
            accumulated += FertilizerFastDecompositionMath.EquivalentTicks(RareInterval, true);
        }

        Assert.AreEqual(Day, accumulated);
        Assert.IsTrue(FertilizerFastDecompositionMath.ShouldDissolve(accumulated));
        Assert.AreEqual(Day / multiplier, FertilizerFastDecompositionMath.RealTicksRemaining(0, true));
        Assert.IsFalse(
            FertilizerFastDecompositionMath.ShouldDissolve(accumulated - RareInterval * multiplier));
    }

    [Test]
    public void MixedWeatherAddsOnlyTheBadIntervalsAtTheFasterRate()
    {
        int fair = FertilizerFastDecompositionMath.EquivalentTicks(Day / 2, false);
        int foul = FertilizerFastDecompositionMath.EquivalentTicks(Day / 8, true);
        Assert.AreEqual(Day, fair + foul);
        Assert.IsTrue(FertilizerFastDecompositionMath.ShouldDissolve(fair + foul));
        Assert.IsFalse(FertilizerFastDecompositionMath.ShouldDissolve(fair + foul - 1));
    }

    [Test]
    public void EmptyOrBackwardWindowsAddNothing()
    {
        Assert.AreEqual(0, FertilizerFastDecompositionMath.EquivalentTicks(0, false));
        Assert.AreEqual(0, FertilizerFastDecompositionMath.EquivalentTicks(0, true));
        Assert.AreEqual(0, FertilizerFastDecompositionMath.EquivalentTicks(-250, true));
    }

    [Test]
    public void AFinishedClockReportsNoTimeLeft()
    {
        Assert.AreEqual(0, FertilizerFastDecompositionMath.RealTicksRemaining(Day, false));
        Assert.AreEqual(0, FertilizerFastDecompositionMath.RealTicksRemaining(Day + 10, true));
    }
}
