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
 * Covers the pure half of the fertilizer dissolution that does not need a live
 * map. The temperature-driven rot itself is vanilla CompRottable, so it is not
 * re-tested here; what is ours is how much ground a dissolving stack improves.
 */
[TestFixture]
public class FertilizerDissolutionTests
{
    [Test]
    public void OnePackImprovesSixCells()
    {
        Assert.AreEqual(6, CageWasteMath.CellsImprovedPerPack);
    }

    [Test]
    public void AWholeStackImprovesSixCellsForEachPackItHolds()
    {
        Assert.AreEqual(0, CageWasteMath.CellsImprovedByPacks(0));
        Assert.AreEqual(6, CageWasteMath.CellsImprovedByPacks(1));
        Assert.AreEqual(30, CageWasteMath.CellsImprovedByPacks(5));
    }

    [Test]
    public void NegativeOrEmptyStacksImproveNothing()
    {
        Assert.AreEqual(0, CageWasteMath.CellsImprovedByPacks(-3));
    }
}
