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

using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace BetterRimworlds.ChickenBatteryCage.Tests;

// Source contracts complement the exact age-boundary tests: the test assembly
// cannot load Unity, so in-game handler integration still needs a play check.
public class PenIntakeAgeTests
{
    static string Source(string name)
    {
        string path = Path.Combine(TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "Source", name);
        return Regex.Replace(File.ReadAllText(path), @"/\*.*?\*/|//[^\r\n]*", "", RegexOptions.Singleline);
    }

    [Test]
    public void OnlyHandlerDestinationChecksAreFiltered()
    {
        string source = Source("PenIntakeAge.cs");
        Assert.That(source, Does.Contain("[HarmonyPatch(typeof(AnimalPenUtility), \"CheckUseAndReach\")]"));
        Assert.That(source, Does.Contain("!__result || roper == null || penMarker.PenState.ContainsConnectedRegion(animal.GetRegion())"));
        Assert.That(source, Does.Not.Contain("[HarmonyPatch(typeof(CompAnimalPenMarker), \"AcceptsToPen\")]"));
        Assert.That(source, Does.Contain("CageIntakeMath.AcceptsAge(animal.ageTracker.AgeBiologicalTicks"));
    }

    [Test]
    public void EachMarkerSavesOneLimitAndDefaultsToUnrestricted()
    {
        string source = Source("PenIntakeAge.cs");
        Assert.That(source, Does.Contain("ConditionalWeakTable<CompAnimalPenMarker, PenIntakeAge>"));
        Assert.That(source, Does.Contain("float maximumYears = -1f"));
        Assert.That(source, Does.Contain("maximumYears = years < 0f ? -1f : years"));
        Assert.That(source, Does.Contain("Scribe_Values.Look(ref maximumYears, \"CBC_penIntakeMaximumAgeYears\", -1f)"));
        Assert.That(source, Does.Contain("PenIntakeAge.For(penMarker).Maximum, GenDate.TicksPerYear"));
        Assert.That(source, Does.Contain("nameof(CompAnimalPenMarker.PostExposeData)"));
    }

    [Test]
    public void AnimalsTabOpensSharedLimitWithoutSpeciesSelector()
    {
        Assert.That(Source("PenIntakeAge.cs"), Does.Contain("[HarmonyPatch(typeof(ITab_PenAnimals), \"FillTab\")]"));
        string window = Source("Window_PenIntakeAge.cs");
        Assert.That(window, Does.Not.Contain("FloatMenu"));
        Assert.That(window, Does.Not.Contain("ThingDef"));
        Assert.That(window, Does.Contain("settings.SetMaximum(age)"));
        Assert.That(window, Does.Contain("settings.SetMaximum(-1f)"));
    }
}
