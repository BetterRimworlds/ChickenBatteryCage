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
 * Pins the fertilizer terrain table: what becomes what, what already counts as
 * improved, and what is left strictly alone.
 */
[TestFixture]
public class FertilizerTerrainRulesTests
{
    [Test]
    public void SandBecomesSoil()
    {
        Assert.AreEqual(FertilizerTerrainRules.Soil, FertilizerTerrainRules.TargetFor("Sand"));
        Assert.AreEqual(FertilizerTerrainRules.Soil, FertilizerTerrainRules.TargetFor("SoftSand"));
    }

    [Test]
    public void GravelBecomesSoil()
    {
        Assert.AreEqual(FertilizerTerrainRules.Soil, FertilizerTerrainRules.TargetFor("Gravel"));
    }

    [Test]
    public void RoughNaturalCaveStoneBecomesSoil()
    {
        Assert.AreEqual(FertilizerTerrainRules.Soil, FertilizerTerrainRules.TargetFor("Granite_Rough"));
        Assert.AreEqual(FertilizerTerrainRules.Soil, FertilizerTerrainRules.TargetFor("Slate_RoughHewn"));
    }

    [Test]
    public void ExistingSoilIsAlreadyImproved()
    {
        Assert.IsTrue(FertilizerTerrainRules.IsImproved("Soil"));
        Assert.IsTrue(FertilizerTerrainRules.IsImproved("SoilRich"));
        Assert.IsNull(FertilizerTerrainRules.TargetFor("Soil"));
    }

    [Test]
    public void ConstructedTerrainAndWaterAreNeverTargets()
    {
        Assert.IsNull(FertilizerTerrainRules.TargetFor("Concrete"));
        Assert.IsNull(FertilizerTerrainRules.TargetFor("PavedTile"));
        Assert.IsNull(FertilizerTerrainRules.TargetFor("Bridge"));
        Assert.IsNull(FertilizerTerrainRules.TargetFor("BrokenAsphalt"));
        Assert.IsNull(FertilizerTerrainRules.TargetFor("WaterDeep"));
    }

    [Test]
    public void GravelIsNotYetImproved()
    {
        Assert.IsFalse(FertilizerTerrainRules.IsImproved("Gravel"));
    }
}
