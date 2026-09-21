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
 * The collective store holds six days of caged demand per bird slot.
 */
[TestFixture]
public class CageNutritionTests
{
    const int Day = CagedChickenMath.TicksPerDay;
    const float PerChicken = CageNutritionMath.DefaultNutritionPerChickenPerDay;


    [Test]
    public void CapacityIsSixDaysOfFeedPerBird()
    {
        Assert.AreEqual(
            CageNutritionMath.MaxDaysOfFeedPerChicken * PerChicken,
            CageNutritionMath.MaxNutritionPerChicken,
            0.0001f);
    }
}
