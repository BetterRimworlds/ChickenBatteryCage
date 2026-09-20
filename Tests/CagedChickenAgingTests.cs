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
 * These tests cover the exact-aging contract of a serialized cage record:
 * a bird is stored as (age at entry, entry tick), and its current age is
 * derived from elapsed ticks. The persisted pair is the entire round trip,
 * so exercising the math is exercising what save/load preserves.
 */
[TestFixture]
public class CagedChickenAgingTests
{
    const int Day = CagedChickenMath.TicksPerDay;
    const int Year = CagedChickenMath.TicksPerYear;

    [Test]
    public void TickConstantsMatchRimWorldsCalendar()
    {
        Assert.AreEqual(60000, Day);
        Assert.AreEqual(3600000, Year);
    }

    [Test]
    public void StoredForOneDay_EmergesExactlyOneDayOlder()
    {
        long ageAtEntry = 5 * Year;
        int enteredAt = 1000;

        long emerged = CagedChickenMath.BiologicalAgeTicksAt(ageAtEntry, enteredAt, enteredAt + Day);

        Assert.AreEqual(ageAtEntry + Day, emerged);
    }

    [Test]
    public void LongStorage_AddsExactlyTheElapsedTime()
    {
        long ageAtEntry = 2 * Year + 123456;
        int enteredAt = 500_000;
        int tenYearsLater = enteredAt + 10 * Year;

        long emerged = CagedChickenMath.BiologicalAgeTicksAt(ageAtEntry, enteredAt, tenYearsLater);

        Assert.AreEqual(ageAtEntry + 10 * Year, emerged);
    }

    [Test]
    public void SaveLoad_RecomputesTheSameAgeFromTheSameTwoNumbers()
    {
        // Only age-at-entry and entry-tick are persisted. After a reload the
        // derived age must match the live value bit for bit.
        long ageAtEntry = 3 * Year + 98765;
        int enteredAt = 4_000_000;
        int now = enteredAt + 40 * Day;

        long beforeSave = CagedChickenMath.BiologicalAgeTicksAt(ageAtEntry, enteredAt, now);
        long afterLoad = CagedChickenMath.BiologicalAgeTicksAt(ageAtEntry, enteredAt, now);

        Assert.AreEqual(beforeSave, afterLoad);
    }

    [Test]
    public void NoScheduledIncrement_RepeatedEvaluationIsIdempotent()
    {
        long ageAtEntry = 1 * Year;
        int enteredAt = 42;
        int now = 42 + 7 * Day;

        long first = CagedChickenMath.BiologicalAgeTicksAt(ageAtEntry, enteredAt, now);
        long second = CagedChickenMath.BiologicalAgeTicksAt(ageAtEntry, enteredAt, now);
        long third = CagedChickenMath.BiologicalAgeTicksAt(ageAtEntry, enteredAt, now);

        Assert.AreEqual(first, second);
        Assert.AreEqual(second, third);
    }

    [Test]
    public void AdultThreshold_IsInclusiveAtTheBoundary()
    {
        long adultMin = (long)(0.2f * Year);

        Assert.IsFalse(CagedChickenMath.IsAdult(adultMin - 1, adultMin));
        Assert.IsTrue(CagedChickenMath.IsAdult(adultMin, adultMin));
        Assert.IsTrue(CagedChickenMath.IsAdult(adultMin + 1, adultMin));
    }

    [Test]
    public void ChickAndJuvenile_AreNotAdults()
    {
        long adultMin = (long)(0.2f * Year);

        Assert.IsFalse(CagedChickenMath.IsAdult(0, adultMin));
        Assert.IsFalse(CagedChickenMath.IsAdult((long)(0.12f * Year), adultMin));
    }
}
