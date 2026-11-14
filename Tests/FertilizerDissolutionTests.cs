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
 * Covers how many cells a dissolving pack strikes. Transforming one of those
 * cells takes three strikes. The dissolution interval itself is covered by
 * FertilizerDissolutionMathTests.
 */
[TestFixture]
public class FertilizerDissolutionTests
{
    [Test]
    public void OnePackStrikesSixCells()
    {
        Assert.AreEqual(6, CageWasteMath.CellsImprovedPerPack);
    }

    [Test]
    public void AWholeStackStrikesSixCellsForEachPackItHolds()
    {
        Assert.AreEqual(0, CageWasteMath.CellsImprovedByPacks(0));
        Assert.AreEqual(6, CageWasteMath.CellsImprovedByPacks(1));
        Assert.AreEqual(30, CageWasteMath.CellsImprovedByPacks(5));
    }

    [Test]
    public void ConfiguredCellsPerPackIsHonoured()
    {
        Assert.AreEqual(4, CageWasteMath.CellsImprovedByPacks(2, 2));
        Assert.AreEqual(0, CageWasteMath.CellsImprovedByPacks(2, 0));
    }

    [Test]
    public void NegativeOrEmptyStacksImproveNothing()
    {
        Assert.AreEqual(0, CageWasteMath.CellsImprovedByPacks(-3));
    }
}
