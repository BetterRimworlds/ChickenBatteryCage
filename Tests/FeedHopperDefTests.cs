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
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;

namespace BetterRimworlds.ChickenBatteryCage.Tests;

/**
 * Guards the vanilla-hopper feeding contract at the def level:
 *
 * - the custom feed hopper def is gone; the cage uses the vanilla "Hopper";
 * - the cage def declares wantsHopperAdjacent so the vanilla hopper can be
 *   placed against it and pawns get "filling hopper" haul jobs;
 * - a patch lets the cage research unlock the vanilla hopper;
 * - the old direct-feeding job no longer exists anywhere in the defs.
 */
[TestFixture]
public class FeedHopperDefTests
{
    static readonly string TestRoot = Path.Combine(
        TestContext.CurrentContext.TestDirectory,
        "..", "..", "..", "..");

    static XElement CageDef()
    {
        string path = Path.Combine(
            TestRoot, "ChickenBatteryCage", "Defs", "ThingDefs_Buildings",
            "Buildings_ChickenBatteryCage.xml");

        XElement def = XDocument.Load(path).Root?
            .Elements("ThingDef")
            .FirstOrDefault(e => (string)e.Element("defName") == "ChickenBatteryCage");
        Assert.IsNotNull(def, "ChickenBatteryCage def is missing.");
        return def;
    }

    static string DefFile(params string[] parts)
    {
        string path = Path.Combine(new[] { TestRoot, "ChickenBatteryCage" }.Concat(parts).ToArray());
        Assert.IsTrue(File.Exists(path), $"Expected def file missing: {path}");
        return File.ReadAllText(path);
    }

    [Test]
    public void CustomFeedHopperDefIsGone()
    {
        string path = Path.Combine(
            TestRoot, "ChickenBatteryCage", "Defs", "ThingDefs_Buildings",
            "Buildings_FeedHopper.xml");

        Assert.That(File.Exists(path), Is.False,
            "The custom feed hopper was replaced by the vanilla hopper and its def must not ship.");

        Assert.That(DefFile("Defs", "ThingDefs_Buildings", "Buildings_ChickenBatteryCage.xml"),
            Does.Not.Contain("CBC_FeedHopper"));
    }

    [Test]
    public void CageWantsAHopperAdjacentLikeTheNutrientPasteDispenser()
    {
        XElement building = CageDef().Element("building");

        Assert.IsNotNull(building, "The cage must define a building block.");
        Assert.That((string)building.Element("wantsHopperAdjacent"), Is.EqualTo("true"),
            "The cage must declare wantsHopperAdjacent, like the nutrient paste dispenser, " +
            "so the vanilla hopper can be placed against it and pawns get fill-hopper jobs.");
    }

    [Test]
    public void CageResearchUnlocksTheVanillaHopper()
    {
        string patch = DefFile("Patches", "Research_VanillaHopper.xml");

        Assert.That(patch, Does.Contain("defName=\"Hopper\""),
            "The patch must target the vanilla hopper.");
        Assert.That(patch, Does.Contain("ChickenBatteryCage"),
            "The cage research must also unlock the vanilla hopper.");
    }

    [Test]
    public void DirectFeedingJobNoLongerExists()
    {
        // The rewrite removed every def-level trace of hauling feed straight
        // to a cage; if any of these reappear, food could bypass the hopper.
        Assert.That(DefFile("Defs", "JobDefs", "JobDefs_ChickenBatteryCage.xml"),
            Does.Not.Contain("CBC_FeedBatteryCage"));
        Assert.That(DefFile("Defs", "WorkGiverDefs", "WorkGivers_ChickenBatteryCage.xml"),
            Does.Not.Contain("CBC_FeedBatteryCages"));
    }
}
