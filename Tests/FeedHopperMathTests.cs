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
 * Covers the hopper pour rules: the only path by which food enters a battery
 * cage. A pour must take whole units, never exceed the cage's remaining
 * space, never exceed the stack, and never destroy a unit that does not fit.
 */
[TestFixture]
public class FeedHopperMathTests
{
    [Test]
    public void TakesWholeUnitsThatFit()
    {
        // 5 units of space, 0.25 nutrition per unit: all 20 units fit.
        Assert.AreEqual(20, FeedHopperMath.ConsumeUnits(5f, 0.25f, 20));
    }

    [Test]
    public void NeverTakesMoreThanTheStackHolds()
    {
        Assert.AreEqual(8, FeedHopperMath.ConsumeUnits(5f, 0.25f, 8));
    }

    [Test]
    public void FloorsToWholeUnits()
    {
        // 5f / 0.3f = 16.67, so 16 whole units are poured; the 17th unit does
        // not fit and stays in the hopper rather than being wasted.
        Assert.AreEqual(16, FeedHopperMath.ConsumeUnits(5f, 0.3f, 100));
    }

    [Test]
    public void FullCageConsumesNothing()
    {
        Assert.AreEqual(0, FeedHopperMath.ConsumeUnits(0f, 0.25f, 10));
        Assert.AreEqual(0, FeedHopperMath.ConsumeUnits(-1f, 0.25f, 10));
    }

    [Test]
    public void EmptyStackConsumesNothing()
    {
        Assert.AreEqual(0, FeedHopperMath.ConsumeUnits(5f, 0.25f, 0));
    }

    [Test]
    public void ZeroNutritionPerUnitConsumesNothing()
    {
        // Guards a divide-by-zero when a def lacks a nutrition stat.
        Assert.AreEqual(0, FeedHopperMath.ConsumeUnits(5f, 0f, 10));
    }

    [Test]
    public void RefusesAPartialUnitLargerThanTheSpace()
    {
        // One unit of 6 nutrition does not fit into 5 units of space.
        Assert.AreEqual(0, FeedHopperMath.ConsumeUnits(5f, 6f, 4));
    }
}
