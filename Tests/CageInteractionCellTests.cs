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
 * Guards the cage's interaction spot.
 *
 * A 2×3 cage with this offset puts the spot in the middle of a long edge for
 * every rotation (RimWorld rotates the offset with the building):
 *
 *   North  -> (2, 0)   east face  (portrait)
 *   East   -> (0, -2)  south face (landscape, the default)
 *   South  -> (-2, 0)  west face  (portrait)
 *   West   -> (0, 2)   north face (landscape)
 *
 * so rotating 180 degrees swings the spot from north to south, or east to
 * west when the cage is stood on end. The graphics are not affected.
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
    public void CageHasAnInteractionCell()
    {
        Assert.That((string)CageDef().Element("hasInteractionCell"), Is.EqualTo("true"),
            "The cage must expose an interaction spot so pawns know where to stand.");
    }

    [Test]
    public void InteractionSpotSitsOnTheMiddleOfTheLongEdge()
    {
        Assert.That((string)CageDef().Element("interactionCellOffset"), Is.EqualTo("(2,0,0)"),
            "The spot must be centered on a long edge so rotation swings it between " +
            "the two long faces instead of dragging it around a corner.");
    }
}
