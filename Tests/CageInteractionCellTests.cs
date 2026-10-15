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
 * Guards that the cage has no fixed interaction spot.
 *
 * A hard-coded interaction offset makes RimWorld's placement check refuse to
 * spawn a cage whenever that single cell is not standable, which blocks
 * packing cages into networks of odd shapes. The cage therefore declares no
 * offset: RimWorld resolves InteractionCell at runtime from the first
 * standable cell around the footprint, so interactions still have a place to
 * stand while placement stays free. The graphics are unaffected.
 */
[TestFixture]
public class CageInteractionCellTests
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

    [Test]
    public void CageFootprintIsTwoByTwo()
    {
        XElement def = CageDef();
        Assert.That((string)def.Element("size"), Is.EqualTo("(2,2)"));
        Assert.That((string)def.Element("graphicData")?.Element("drawSize"), Is.EqualTo("(2,2)"));
    }

    [Test]
    public void CageDeclaresNoFixedInteractionCell()
    {
        XElement def = CageDef();
        Assert.That(def.Element("hasInteractionCell"), Is.Null,
            "A fixed interaction spot would block placing cages that share the " +
            "spot's cell, which is what makes odd-shaped networks hard to build.");
        Assert.That(def.Element("interactionCellOffset"), Is.Null,
            "A fixed offset is meaningless without hasInteractionCell and would " +
            "read as if a spot were still pinned to one face.");
    }

    [Test]
    public void CagePlaceWorkerDrawsTheOccupiedFootprint()
    {
        XElement def = CageDef();
        Assert.That((string)def.Element("drawPlaceWorkersWhileSelected"), Is.EqualTo("true"));
        Assert.That(
            def.Element("placeWorkers")?.Elements("li").Select(e => (string)e),
            Does.Contain("BetterRimworlds.ChickenBatteryCage.PlaceWorker_MustBeRoofed"));
    }
}
