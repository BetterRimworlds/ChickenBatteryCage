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
 * Covers how a cluster of touching cages spreads its shared feed pool back
 * over the members' own stores: proportionally, never over capacity, and with
 * the total preserved.
 */
[TestFixture]
public class CageClusterMathTests
{
    static float Sum(float[] values)
    {
        float total = 0f;
        foreach (float value in values)
        {
            total += value;
        }
        return total;
    }

    [Test]
    public void EmptyPoolWritesZeros()
    {
        var result = new float[3];

        CageClusterMath.Distribute(0f, new[] { 60f, 60f, 60f }, result);

        Assert.AreEqual(new[] { 0f, 0f, 0f }, result);
    }

    [Test]
    public void PoolBelowCapacityIsSpreadEvenlyAcrossMatches()
    {
        var result = new float[2];

        CageClusterMath.Distribute(60f, new[] { 60f, 60f }, result);

        Assert.AreEqual(30f, result[0], 0.0001f);
        Assert.AreEqual(30f, result[1], 0.0001f);
        Assert.AreEqual(60f, Sum(result), 0.0001f);
    }

    [Test]
    public void PoolIsSpreadInProportionToCapacity()
    {
        // The bigger cage gets the bigger share of a partly filled pool.
        var result = new float[2];

        CageClusterMath.Distribute(90f, new[] { 60f, 120f }, result);

        Assert.AreEqual(30f, result[0], 0.0001f);
        Assert.AreEqual(60f, result[1], 0.0001f);
        Assert.AreEqual(90f, Sum(result), 0.0001f);
    }

    [Test]
    public void PoolAtOrAboveCapacityFillsEveryCage()
    {
        var result = new float[2];

        CageClusterMath.Distribute(500f, new[] { 60f, 120f }, result);

        Assert.AreEqual(60f, result[0], 0.0001f);
        Assert.AreEqual(120f, result[1], 0.0001f);
    }

    [Test]
    public void NoMemberIsGivenMoreThanItsCapacity()
    {
        var result = new float[3];

        CageClusterMath.Distribute(
            80f,
            new[] { 60f, 6f, 30f },
            result);

        Assert.LessOrEqual(result[0], 60f);
        Assert.LessOrEqual(result[1], 6f);
        Assert.LessOrEqual(result[2], 30f);
        Assert.AreEqual(80f, Sum(result), 0.0001f);
    }

    [Test]
    public void ZeroCapacityMembersReceiveNothing()
    {
        var result = new float[2];

        CageClusterMath.Distribute(30f, new[] { 0f, 60f }, result);

        Assert.AreEqual(0f, result[0], 0.0001f);
        Assert.AreEqual(30f, result[1], 0.0001f);
    }

    [Test]
    public void OddTotalIsNotLostToRounding()
    {
        // Three equal cages and a total that does not divide evenly.
        var result = new float[3];

        CageClusterMath.Distribute(100f, new[] { 60f, 60f, 60f }, result);

        Assert.AreEqual(100f, Sum(result), 0.0001f);
    }
}
