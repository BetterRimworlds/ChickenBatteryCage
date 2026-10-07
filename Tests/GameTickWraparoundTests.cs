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
 * The game-tick counter is a wrapping int. These tests pin the wrapping tick
 * helpers and the migration guard that depend on them: a bird housed before
 * the counter wrapped must not be mistaken for one housed in the future and
 * must keep the elapsed caged aging her entry tick encodes.
 */
[TestFixture]
public class GameTickWraparoundTests
{
    // A tick just before int.MaxValue and one just after the wrap to
    // int.MinValue, 201 ticks apart in wrapping order.
    const int JustBeforeWrap = int.MaxValue - 100;
    const int JustAfterWrap = int.MinValue + 100;

    [Test]
    public void TicksSince_IsExactAcrossTheWrap()
    {
        Assert.AreEqual(201, CagedChickenMath.TicksSince(JustBeforeWrap, JustAfterWrap));
    }

    [Test]
    public void TicksSince_IsAPlainDifferenceWithinACycle()
    {
        Assert.AreEqual(60000, CagedChickenMath.TicksSince(1_000_000, 1_060_000));
    }

    [Test]
    public void IsAfter_OrdersAcrossTheWrap()
    {
        Assert.IsTrue(CagedChickenMath.IsAfter(JustAfterWrap, JustBeforeWrap));
        Assert.IsFalse(CagedChickenMath.IsAfter(JustBeforeWrap, JustAfterWrap));
        Assert.IsFalse(CagedChickenMath.IsAfter(5, 5));
    }

    [Test]
    public void IsFutureTick_TreatsAWrappedPastTickAsPast()
    {
        Assert.IsFalse(CagedChickenMath.IsFutureTick(JustBeforeWrap, JustAfterWrap));
    }

    [Test]
    public void IsFutureTick_StillDetectsAGenuinelyFutureTick()
    {
        Assert.IsTrue(CagedChickenMath.IsFutureTick(1_050, 1_000));
        Assert.IsTrue(CagedChickenMath.IsFutureTick(JustAfterWrap + 50, JustAfterWrap));
    }

    [Test]
    public void IsImpossibleTick_RejectsANegativeTickBeforeTheWrap()
    {
        Assert.IsTrue(CagedChickenMath.IsImpossibleTick(-5, 1_000));
    }

    [Test]
    public void IsImpossibleTick_AcceptsANegativeTickAfterTheWrap()
    {
        Assert.IsFalse(CagedChickenMath.IsImpossibleTick(-100, -50));
    }

    [Test]
    public void PreWrapEntryTick_SurvivesAfterTheCounterWraps()
    {
        long age = 2 * CagedChickenMath.TicksPerYear;
        int entered = JustBeforeWrap;

        bool valid = CagedChickenValidation.TryRepair(ref age, ref entered, JustAfterWrap);

        Assert.IsTrue(valid);
        Assert.AreEqual(JustBeforeWrap, entered, "the pre-wrap entry tick must not be reset");
        Assert.AreEqual(
            age + CagedChickenMath.TicksSince(entered, JustAfterWrap),
            CagedChickenMath.BiologicalAgeTicksAt(age, entered, JustAfterWrap));
    }

    [Test]
    public void EntryTickAfterTheWrap_IsKept()
    {
        long age = CagedChickenMath.TicksPerYear;
        int entered = -100;

        bool valid = CagedChickenValidation.TryRepair(ref age, ref entered, -50);

        Assert.IsTrue(valid);
        Assert.AreEqual(-100, entered);
    }

    [Test]
    public void FutureEntryTickAfterTheWrap_IsClampedToNow()
    {
        long age = CagedChickenMath.TicksPerYear;
        int entered = JustAfterWrap + 50;

        bool valid = CagedChickenValidation.TryRepair(ref age, ref entered, JustAfterWrap);

        Assert.IsTrue(valid);
        Assert.AreEqual(JustAfterWrap, entered);
    }
}
