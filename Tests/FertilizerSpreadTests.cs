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

using System.Collections.Generic;
using NUnit.Framework;
using Cell = BetterRimworlds.ChickenBatteryCage.FertilizerSpreadMath.Cell;

namespace BetterRimworlds.ChickenBatteryCage.Tests;

/**
 * Exercises the fertilizer spread against a synthetic grid: the six-cells-per-
 * pack rule, outward growth across repeated dissolution, and the blocking done
 * by walls and constructed ground.
 */
[TestFixture]
public class FertilizerSpreadTests
{
    sealed class TestGrid
    {
        public readonly int Width;
        public readonly int Height;
        public readonly HashSet<Cell> Improved = new HashSet<Cell>();
        public readonly HashSet<Cell> Blocked = new HashSet<Cell>();

        public TestGrid(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public bool InBounds(int x, int z)
        {
            return x >= 0 && x < Width && z >= 0 && z < Height;
        }

        public bool IsImproved(int x, int z)
        {
            return Improved.Contains(new Cell(x, z));
        }

        public bool CanImprove(int x, int z)
        {
            return !Improved.Contains(new Cell(x, z)) && !Blocked.Contains(new Cell(x, z));
        }
    }

    static List<Cell> Grow(TestGrid grid, int rootX, int rootZ, int count)
    {
        return FertilizerSpreadMath.GrowAt(
            new Cell(rootX, rootZ),
            count,
            grid.InBounds,
            grid.IsImproved,
            grid.CanImprove,
            (x, z) =>
            {
                int dx = x - rootX;
                int dz = z - rootZ;
                return -(float)(dx * dx + dz * dz);
            },
            cell => grid.Improved.Add(cell));
    }

    [Test]
    public void OnePackTargetsSixCellsWhenSixAreAvailable()
    {
        var grid = new TestGrid(50, 50);
        List<Cell> chosen = Grow(grid, 25, 25, 6);

        Assert.AreEqual(6, chosen.Count);
        Assert.AreEqual(6, grid.Improved.Count);
    }

    [Test]
    public void RepeatedDissolutionExpandsOutwardWithoutReselecting()
    {
        var grid = new TestGrid(50, 50);

        List<Cell> first = Grow(grid, 25, 25, 6);
        List<Cell> second = Grow(grid, 25, 25, 6);
        List<Cell> third = Grow(grid, 25, 25, 6);

        AssertNoOverlap(first, second);
        AssertNoOverlap(first, third);
        AssertNoOverlap(second, third);
        Assert.AreEqual(18, grid.Improved.Count);

        // The later patches are further from the root, so the blob is growing
        // outward rather than re-consuming ground it already improved.
        Assert.Greater(MinimumDistance(second, 25, 25), 0);
        Assert.Greater(AverageDistance(third, 25, 25), AverageDistance(first, 25, 25));
    }

    [Test]
    public void NaturalStoneWallsConstrainTheSpread()
    {
        var grid = new TestGrid(20, 20);
        for (int z = 0; z < 20; z++)
        {
            grid.Blocked.Add(new Cell(10, z));
        }

        List<Cell> chosen = Grow(grid, 5, 10, 100);

        Assert.AreEqual(100, chosen.Count);
        foreach (Cell cell in chosen)
        {
            Assert.Less(cell.X, 10, "no cell beyond the wall may be improved");
        }
    }

    [Test]
    public void ConstructedGroundIsNeverImproved()
    {
        var grid = new TestGrid(20, 20);
        grid.Blocked.Add(new Cell(10, 10));
        grid.Blocked.Add(new Cell(11, 10));
        grid.Blocked.Add(new Cell(10, 11));

        List<Cell> chosen = Grow(grid, 9, 10, 50);

        foreach (Cell cell in chosen)
        {
            Assert.IsFalse(grid.Blocked.Contains(cell), "constructed ground must not be replaced");
        }
    }

    [Test]
    public void FinishedGroundIsTreatedAsAlreadyImproved()
    {
        var grid = new TestGrid(20, 20);
        grid.Improved.Add(new Cell(10, 10));

        List<Cell> chosen = Grow(grid, 10, 10, 6);

        Assert.AreEqual(6, chosen.Count);
        Assert.IsFalse(chosen.Contains(new Cell(10, 10)), "finished ground must not be re-improved");
    }

    [Test]
    public void ACompletelyBlockedCellImprovesNothing()
    {
        var grid = new TestGrid(3, 3);
        for (int x = 0; x < 3; x++)
        {
            for (int z = 0; z < 3; z++)
            {
                grid.Blocked.Add(new Cell(x, z));
            }
        }

        Assert.AreEqual(0, Grow(grid, 1, 1, 6).Count);
    }

    static void AssertNoOverlap(List<Cell> a, List<Cell> b)
    {
        var seen = new HashSet<Cell>(a);
        foreach (Cell cell in b)
        {
            Assert.IsFalse(seen.Contains(cell), "a cell was improved twice");
        }
    }

    static int MinimumDistance(List<Cell> cells, int x, int z)
    {
        int best = int.MaxValue;
        foreach (Cell cell in cells)
        {
            int distance = System.Math.Abs(cell.X - x) + System.Math.Abs(cell.Z - z);
            if (distance < best)
            {
                best = distance;
            }
        }

        return best;
    }

    static double AverageDistance(List<Cell> cells, int x, int z)
    {
        if (cells.Count == 0)
        {
            return 0;
        }

        double total = 0;
        foreach (Cell cell in cells)
        {
            total += System.Math.Abs(cell.X - x) + System.Math.Abs(cell.Z - z);
        }

        return total / cells.Count;
    }
}
