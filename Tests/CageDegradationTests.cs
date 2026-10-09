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
public class CageDegradationTests
{
    const int Day = CagedChickenMath.TicksPerDay;

    [Test]
    public void OnlySteelAndWoodDecayOnTheFastClock()
    {
        Assert.IsTrue(CageDegradationMath.IsWoodOrSteel("Steel"));
        Assert.IsTrue(CageDegradationMath.IsWoodOrSteel("WoodLog"));
        Assert.IsFalse(CageDegradationMath.IsWoodOrSteel("Plasteel"));
        Assert.IsFalse(CageDegradationMath.IsWoodOrSteel("Gold"));
        Assert.IsFalse(CageDegradationMath.IsWoodOrSteel("Silver"));
        Assert.IsFalse(CageDegradationMath.IsWoodOrSteel("GraniteBlocks"));
        Assert.IsFalse(CageDegradationMath.IsWoodOrSteel(null));
    }

    [Test]
    public void FastMaterialsLastThirtyDaysAndOthersSixty()
    {
        Assert.AreEqual(30f, CageDegradationMath.ExposureDays(true));
        Assert.AreEqual(60f, CageDegradationMath.ExposureDays(false));
    }

    [Test]
    public void AFullWindowOfDamageEqualsTheBuildingsHealth()
    {
        // Whatever the material, damage applied across the whole exposure
        // window from full health must exactly destroy the building.
        Assert.AreEqual(180f, CageDegradationMath.DamageOverTicks(180f, 30f, 30 * Day), 0.0001f);
        Assert.AreEqual(180f, CageDegradationMath.DamageOverTicks(180f, 60f, 60 * Day), 0.0001f);
    }

    [Test]
    public void PerRareTickDamageIsASteadyDrip()
    {
        float tick = CageDegradationMath.DamageOverTicks(180f, 30f, 250);
        Assert.AreEqual(0.025f, tick, 0.00001f);
    }

    [Test]
    public void OtherMaterialsDecayAtHalfTheRate()
    {
        float fast = CageDegradationMath.DamageOverTicks(180f, 30f, 250);
        float slow = CageDegradationMath.DamageOverTicks(180f, 60f, 250);
        Assert.AreEqual(fast, slow * 2f, 0.00001f);
    }

    [Test]
    public void DamageIsPartitionIndependent()
    {
        float whole = CageDegradationMath.DamageOverTicks(180f, 30f, 1000);
        float split = CageDegradationMath.DamageOverTicks(180f, 30f, 400)
            + CageDegradationMath.DamageOverTicks(180f, 30f, 600);
        Assert.AreEqual(whole, split, 0.00001f);
    }

    [Test]
    public void DegenerateInputsDealNoDamage()
    {
        Assert.AreEqual(0f, CageDegradationMath.DamageOverTicks(0f, 30f, 250));
        Assert.AreEqual(0f, CageDegradationMath.DamageOverTicks(180f, 0f, 250));
        Assert.AreEqual(0f, CageDegradationMath.DamageOverTicks(180f, 30f, 0));
        Assert.AreEqual(0f, CageDegradationMath.DamageOverTicks(180f, 30f, -5));
    }

    [Test]
    public void DaysUntilFailureTracksRemainingHealth()
    {
        Assert.AreEqual(30f, CageDegradationMath.DaysUntilFailure(180f, 180f, 30f), 0.0001f);
        Assert.AreEqual(15f, CageDegradationMath.DaysUntilFailure(90f, 180f, 30f), 0.0001f);
        Assert.AreEqual(30f, CageDegradationMath.DaysUntilFailure(90f, 180f, 60f), 0.0001f);
        Assert.AreEqual(0f, CageDegradationMath.DaysUntilFailure(0f, 180f, 30f));
    }

    [Test]
    public void BadWeatherQuadruplesTheDecayRate()
    {
        Assert.AreEqual(4f, CageDegradationMath.WeatherMultiplier(true));
        Assert.AreEqual(1f, CageDegradationMath.WeatherMultiplier(false));

        float fair = CageDegradationMath.DamageOverTicks(180f, 30f, 250);
        float foul = CageDegradationMath.DamageOverTicks(180f, 30f, 250, 4f);
        Assert.AreEqual(fair * 4f, foul, 0.00001f);
    }

    [Test]
    public void BadWeatherCutsTheRemainingDaysToAFraction()
    {
        Assert.AreEqual(7.5f,
            CageDegradationMath.DaysUntilFailure(180f, 180f, 30f, 4f), 0.0001f);
    }

    [Test]
    public void BadWeatherDoesNotSpeedUpTheCoreClock()
    {
        // The 30/60-day clock is the fair-weather baseline; a full fair window
        // still exactly destroys the building.
        Assert.AreEqual(180f, CageDegradationMath.DamageOverTicks(180f, 30f, 30 * Day, 1f), 0.0001f);
    }
}
