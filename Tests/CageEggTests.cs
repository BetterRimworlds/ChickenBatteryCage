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
public class CageEggTests
{
    const int Day = CagedChickenMath.TicksPerDay;
    const float Adult = 0.2f;

    [Test]
    public void JuvenilesLayNothing()
    {
        Assert.AreEqual(0f, CageEggMath.EggsPerHenPerDay(0.1f, Adult));
    }

    [Test]
    public void PrimeHensLayOneEggADay()
    {
        Assert.AreEqual(
            CageEggMath.EggsPerHenPerDayAtPrime,
            CageEggMath.EggsPerHenPerDay(1f, Adult),
            0.0001f);
    }

    [Test]
    public void LayingDeclinesWithAgeButNeverStops()
    {
        float prime = CageEggMath.EggsPerHenPerDay(1.5f, Adult);
        float older = CageEggMath.EggsPerHenPerDay(6f, Adult);
        float ancient = CageEggMath.EggsPerHenPerDay(20f, Adult);

        Assert.Less(older, prime);
        Assert.Greater(older, 0f);
        Assert.GreaterOrEqual(
            ancient,
            CageEggMath.EggsPerHenPerDayAtPrime * CageEggMath.MinSoftenedLayingFraction);
    }

    [Test]
    public void AgeReductionIsTwentyFivePercentLessExtreme()
    {
        // At age 6 a vanilla hen would have fallen to 1/3 of her prime rate.
        // A super chicken keeps 25% of that reduction back.
        const float vanillaReduction = 1f - 1f / 3f;
        float expected = 1f - vanillaReduction * CageEggMath.ReductionSeverity;

        Assert.AreEqual(expected, CageEggMath.EggsPerHenPerDay(6f, Adult), 0.0001f);
        Assert.AreEqual(0.5f, expected, 0.0001f);
    }

    [Test]
    public void OutputScalesWithElapsedTime()
    {
        Assert.AreEqual(1f, CageEggMath.EggsOverTicks(1f, Day), 0.0001f);
        Assert.AreEqual(0.5f, CageEggMath.EggsOverTicks(1f, Day / 2), 0.0001f);
        Assert.AreEqual(0f, CageEggMath.EggsOverTicks(1f, 0));
    }

    [Test]
    public void FullCartonsCountOnlyWholeTwelves()
    {
        Assert.AreEqual(0, CageEggMath.FullCartons(11.9f, 12));
        Assert.AreEqual(1, CageEggMath.FullCartons(12f, 12));
        Assert.AreEqual(2, CageEggMath.FullCartons(24.5f, 12));
    }
}
