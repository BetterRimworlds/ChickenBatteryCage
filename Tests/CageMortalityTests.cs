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
    public void CombinedChanceIsCappedBelowCertainty()
    {
        float combined = CageMortalityMath.CombinedDailyChance(0.4f, 0.4f);

        Assert.AreEqual(CageMortalityMath.MaxDailyChance, combined, 0.0001f);
    }
}
