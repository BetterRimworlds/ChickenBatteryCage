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
 * Covers the per-stack fertilizer decomposition timer and the pure half of the
 * spread that does not need a live map.
 */
[TestFixture]
public class FertilizerDissolutionTests
{
    const int Day = CagedChickenMath.TicksPerDay;

    [Test]
    public void OnePackDecomposesEveryTenDays()
    {
        Assert.AreEqual(
            10 * Day,
            CageWasteMath.DecompositionIntervalTicks(CageWasteMath.DecompositionDaysPerPack,
                outdoors: true, rainedOn: false));
    }

    [Test]
    public void NineIndependentStacksEachLoseOnePack()
    {
        int[] stacks = { 5, 5, 5, 5, 5, 5, 5, 5, 5 };
        int active = 0;
        for (int i = 0; i < stacks.Length; i++)
        {
            if (stacks[i] > 0)
            {
                stacks[i] = CageWasteMath.PacksAfterDissolution(stacks[i]);
                active++;
            }
        }

        Assert.AreEqual(9, active);
        foreach (int remaining in stacks)
        {
            Assert.AreEqual(4, remaining);
        }
    }

    [Test]
    public void AStackDissolvesOnePackAtATimeUntilEmpty()
    {
        int count = 3;
        for (int i = 0; i < 3; i++)
        {
            count = CageWasteMath.PacksAfterDissolution(count);
        }

        Assert.AreEqual(0, count);
    }

    [Test]
    public void OnePackImprovesSixCells()
    {
        Assert.AreEqual(6, CageWasteMath.CellsImprovedPerPack);
    }
}
