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
 * finished, and what is left strictly alone.
 */
[TestFixture]
public class FertilizerTerrainRulesTests
{
    [Test]
    public void SoftSandBecomesSand()
    {
        Assert.AreEqual(FertilizerTerrainRules.Sand, FertilizerTerrainRules.TargetFor("SoftSand"));
    }

    [Test]
    public void SandBecomesGravel()
    {
        Assert.AreEqual(FertilizerTerrainRules.Gravel, FertilizerTerrainRules.TargetFor("Sand"));
    }

    [Test]
    public void GravelBecomesMossyTerrain()
    {
        Assert.AreEqual(FertilizerTerrainRules.MossyTerrain, FertilizerTerrainRules.TargetFor("Gravel"));
    }

    [Test]
    public void MossyTerrainBecomesSoil()
    {
        Assert.AreEqual(FertilizerTerrainRules.Soil, FertilizerTerrainRules.TargetFor("MossyTerrain"));
    }

    [Test]
    public void SoilBecomesRichSoil()
    {
        Assert.AreEqual(FertilizerTerrainRules.SoilRich, FertilizerTerrainRules.TargetFor("Soil"));
    }

    [Test]
    public void MarshyTerrainBecomesGravel()
    {
        Assert.AreEqual(FertilizerTerrainRules.Gravel, FertilizerTerrainRules.TargetFor("MarshyTerrain"));
    }

    [Test]
    public void RoughNaturalCaveStoneBecomesGravel()
    {
        Assert.AreEqual(FertilizerTerrainRules.Gravel, FertilizerTerrainRules.TargetFor("Granite_Rough"));
        Assert.AreEqual(FertilizerTerrainRules.Gravel, FertilizerTerrainRules.TargetFor("Slate_RoughHewn"));
    }

    [Test]
    public void RichSoilIsAlreadyFinished()
    {
        Assert.IsTrue(FertilizerTerrainRules.IsImproved("SoilRich"));
        Assert.IsTrue(FertilizerTerrainRules.IsImproved("Riverbank"));
        Assert.IsNull(FertilizerTerrainRules.TargetFor("SoilRich"));
        Assert.IsNull(FertilizerTerrainRules.TargetFor("Riverbank"));
    }

    [Test]
    public void IntermediateGroundIsNotYetFinished()
    {
        Assert.IsFalse(FertilizerTerrainRules.IsImproved("Soil"));
        Assert.IsFalse(FertilizerTerrainRules.IsImproved("Gravel"));
        Assert.IsFalse(FertilizerTerrainRules.IsImproved("MossyTerrain"));
        Assert.IsFalse(FertilizerTerrainRules.IsImproved("MarshyTerrain"));
    }

    [Test]
    public void ConstructedTerrainAndWaterAreNeverTargets()
    {
        Assert.IsNull(FertilizerTerrainRules.TargetFor("Concrete"));
        Assert.IsNull(FertilizerTerrainRules.TargetFor("PavedTile"));
        Assert.IsNull(FertilizerTerrainRules.TargetFor("Bridge"));
        Assert.IsNull(FertilizerTerrainRules.TargetFor("BrokenAsphalt"));
        Assert.IsNull(FertilizerTerrainRules.TargetFor("WaterDeep"));
        Assert.IsNull(FertilizerTerrainRules.TargetFor("Mud"));
        Assert.IsNull(FertilizerTerrainRules.TargetFor("Ice"));
    }
}
