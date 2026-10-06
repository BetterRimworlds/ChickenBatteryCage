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
 * The cage's persisted floating-point totals must never survive loading as a
 * NaN or an infinity. Plain range comparisons miss both, and either would
 * poison the shared network math the store and egg box feed.
 */
[TestFixture]
public class CageTotalSanitizationTests
{
    const float Capacity = 60f;

    [Test]
    public void NaNNutritionIsResetToEmpty()
    {
        Assert.AreEqual(0f, CagedChickenValidation.RepairStored(float.NaN, Capacity));
    }

    [Test]
    public void InfiniteNutritionIsResetToEmpty()
    {
        Assert.AreEqual(0f, CagedChickenValidation.RepairStored(float.PositiveInfinity, Capacity));
        Assert.AreEqual(0f, CagedChickenValidation.RepairStored(float.NegativeInfinity, Capacity));
    }

    [Test]
    public void NegativeNutritionIsResetToEmpty()
    {
        Assert.AreEqual(0f, CagedChickenValidation.RepairStored(-1f, Capacity));
    }

    [Test]
    public void NutritionOverCapacityClampsToCapacity()
    {
        Assert.AreEqual(Capacity, CagedChickenValidation.RepairStored(Capacity + 100f, Capacity));
    }

    [Test]
    public void ValidNutritionIsUnchanged()
    {
        Assert.AreEqual(12.5f, CagedChickenValidation.RepairStored(12.5f, Capacity));
    }

    [Test]
    public void NaNEggProgressIsResetToEmpty()
    {
        Assert.AreEqual(0f, CagedChickenValidation.RepairEggProgress(float.NaN));
    }

    [Test]
    public void InfiniteEggProgressIsResetToEmpty()
    {
        Assert.AreEqual(0f, CagedChickenValidation.RepairEggProgress(float.PositiveInfinity));
        Assert.AreEqual(0f, CagedChickenValidation.RepairEggProgress(float.NegativeInfinity));
    }

    [Test]
    public void NegativeEggProgressIsResetToEmpty()
    {
        Assert.AreEqual(0f, CagedChickenValidation.RepairEggProgress(-0.5f));
    }

    [Test]
    public void ValidEggProgressIsUnchanged()
    {
        Assert.AreEqual(3.25f, CagedChickenValidation.RepairEggProgress(3.25f));
    }

    [Test]
    public void IsFiniteDistinguishesFiniteValues()
    {
        Assert.IsTrue(CagedChickenValidation.IsFinite(0f));
        Assert.IsTrue(CagedChickenValidation.IsFinite(-12.5f));
        Assert.IsFalse(CagedChickenValidation.IsFinite(float.NaN));
        Assert.IsFalse(CagedChickenValidation.IsFinite(float.PositiveInfinity));
        Assert.IsFalse(CagedChickenValidation.IsFinite(double.NaN));
        Assert.IsFalse(CagedChickenValidation.IsFinite(double.PositiveInfinity));
    }
}
