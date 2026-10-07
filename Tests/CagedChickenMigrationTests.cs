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

[TestFixture]
public class CagedChickenMigrationTests
{
    const int Now = 1_000_000;

    [Test]
    public void AHealthyRecordSurvivesUnchanged()
    {
        long age = 2 * CagedChickenMath.TicksPerYear;
        int entered = 500_000;

        bool valid = CagedChickenValidation.TryRepair(ref age, ref entered, Now);

        Assert.IsTrue(valid);
        Assert.AreEqual(2 * CagedChickenMath.TicksPerYear, age);
        Assert.AreEqual(500_000, entered);
    }

    [Test]
    public void FutureEntryTickIsClampedToNow()
    {
        long age = CagedChickenMath.TicksPerYear;
        int entered = Now + 50_000;

        bool valid = CagedChickenValidation.TryRepair(ref age, ref entered, Now);

        Assert.IsTrue(valid);
        Assert.AreEqual(Now, entered);
    }

    [Test]
    public void NegativeValuesAreClamped()
    {
        long age = -12345;
        int entered = -7;

        bool valid = CagedChickenValidation.TryRepair(ref age, ref entered, Now);

        Assert.IsTrue(valid);
        Assert.AreEqual(0L, age);
        Assert.AreEqual(0, entered);
    }

    [Test]
    public void AnImpossibleAgeIsRejectedRatherThanAccepted()
    {
        long age = CagedChickenValidation.MaxPlausibleAgeTicks + 1;
        int entered = 1000;

        bool valid = CagedChickenValidation.TryRepair(ref age, ref entered, Now);

        Assert.IsFalse(valid);
    }

    [Test]
    public void ExactlyTheMaximumPlausibleAgeIsStillAccepted()
    {
        long age = CagedChickenValidation.MaxPlausibleAgeTicks;
        int entered = 1000;

        bool valid = CagedChickenValidation.TryRepair(ref age, ref entered, Now);

        Assert.IsTrue(valid);
    }

    [Test]
    public void InfiniteMortalityExposureIsReset()
    {
        Assert.AreEqual(0.0, CagedChickenValidation.RepairExposure(double.PositiveInfinity));
        Assert.AreEqual(0.0, CagedChickenValidation.RepairExposure(double.NegativeInfinity));
        Assert.AreEqual(0.0, CagedChickenValidation.RepairExposure(double.NaN));
    }

    [Test]
    public void NegativeMortalityExposureIsReset()
    {
        Assert.AreEqual(0.0, CagedChickenValidation.RepairExposure(-0.25));
    }

    [Test]
    public void ValidMortalityExposureIsKept()
    {
        Assert.AreEqual(0.37, CagedChickenValidation.RepairExposure(0.37));
    }
}
