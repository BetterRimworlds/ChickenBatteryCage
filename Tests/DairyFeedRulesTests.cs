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

namespace BetterRimworlds.ChickenBatteryCage.Tests;

/**
 * Covers the dairy ban on battery-cage feed. Chickens cannot digest lactose, so
 * the cage must not treat milk as feed. RimWorld has no dairy category, so the
 * rules combine the animal-product/fluid food-type signature with a def-name
 * list. These tests pin both signals and the settings-field parsing.
 */
[TestFixture]
public class DairyFeedRulesTests
{
    [Test]
    public void AnimalProductAndFluidIsDairy()
    {
        // The vanilla signature of milk: an animal product that is also a fluid.
        Assert.IsTrue(DairyFeedRules.IsDairy(isAnimalProduct: true, isFluid: true));
        Assert.IsTrue(DairyFeedRules.IsForbidden("SomeModdedMilk", isAnimalProduct: true, isFluid: true));
    }

    [Test]
    public void FluidWithoutAnimalProductIsNotDairy()
    {
        // Beer, psychite tea, and go-juice are fluids but not animal products.
        Assert.IsFalse(DairyFeedRules.IsDairy(isAnimalProduct: false, isFluid: true));
        Assert.IsFalse(DairyFeedRules.IsForbidden("Beer", isAnimalProduct: false, isFluid: true));
    }

    [Test]
    public void AnimalProductWithoutFluidIsNotDairy()
    {
        // Raw meat and eggs are animal products but not fluids, and remain feed.
        Assert.IsFalse(DairyFeedRules.IsDairy(isAnimalProduct: true, isFluid: false));
        Assert.IsFalse(DairyFeedRules.IsForbidden("Meat_Chicken", isAnimalProduct: true, isFluid: false));
        Assert.IsFalse(DairyFeedRules.IsForbidden("EggChickenUnfertilized", isAnimalProduct: true, isFluid: false));
    }

    [Test]
    public void PlainFoodIsNotDairy()
    {
        Assert.IsFalse(DairyFeedRules.IsForbidden("Hay", isAnimalProduct: false, isFluid: false));
        Assert.IsFalse(DairyFeedRules.IsForbidden("Kibble", isAnimalProduct: false, isFluid: false));
    }

    [Test]
    public void VanillaMilkDefNameIsAlwaysForbidden()
    {
        // Even if a mod retags Milk, the canonical def name is refused.
        Assert.IsTrue(DairyFeedRules.IsForbidden("Milk", isAnimalProduct: false, isFluid: false));
        Assert.IsTrue(DairyFeedRules.IsForbidden("milk", isAnimalProduct: false, isFluid: false));
    }

    [Test]
    public void ExtraDefNamesAreForbidden()
    {
        var extras = new List<string> { "Cheese", "Butter" };
        Assert.IsTrue(DairyFeedRules.IsForbidden("Cheese", false, false, extras));
        Assert.IsTrue(DairyFeedRules.IsForbidden("Butter", false, false, extras));
        Assert.IsFalse(DairyFeedRules.IsForbidden("Yogurt", false, false, extras));
    }

    [Test]
    public void ExtraDefNamesMatchCaseInsensitivelyAndTrim()
    {
        var extras = new List<string> { "  Cheese  " };
        Assert.IsTrue(DairyFeedRules.IsForbidden("cheese", false, false, extras));
    }

    [Test]
    public void NullExtraListIsSafe()
    {
        Assert.IsFalse(DairyFeedRules.IsForbidden("Hay", false, false, null));
        Assert.IsTrue(DairyFeedRules.IsForbidden("Milk", false, false, null));
    }

    [Test]
    public void ParseDefNamesSplitsOnPunctuationAndWhitespace()
    {
        List<string> names = DairyFeedRules.ParseDefNames("Cheese, Butter;Yogurt\nCream\tIceCream");
        CollectionAssert.AreEqual(
            new[] { "Cheese", "Butter", "Yogurt", "Cream", "IceCream" }, names);
    }

    [Test]
    public void ParseDefNamesDropsBlanksAndDuplicates()
    {
        List<string> names = DairyFeedRules.ParseDefNames("Cheese, ,cheese;;CHEESE");
        CollectionAssert.AreEqual(new[] { "Cheese" }, names);
    }

    [Test]
    public void ParseDefNamesHandlesNullAndEmpty()
    {
        Assert.IsEmpty(DairyFeedRules.ParseDefNames(null));
        Assert.IsEmpty(DairyFeedRules.ParseDefNames(""));
        Assert.IsEmpty(DairyFeedRules.ParseDefNames("   "));
    }
}
