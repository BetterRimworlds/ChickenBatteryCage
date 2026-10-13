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
 * Fertilizer deteriorates like raw plant food. The def supplies the same rate
 * and hit points; vanilla's environmental stat then multiplies an unroofed
 * cell by the rain rate, up to five times (500%) in rain or snow. Nothing in
 * this mod scales that factor.
 */
[TestFixture]
public class FertilizerDeteriorationTests
{
    static readonly string TestRoot = Path.Combine(
        TestContext.CurrentContext.TestDirectory,
        "..", "..", "..", "..");

    [Test]
    public void TheDefUsesTheRawPlantRateAndHitPoints()
    {
        string path = Path.Combine(
            TestRoot, "ChickenBatteryCage", "Defs", "ThingDefs_Items", "FertilizerPack.xml");
        Assert.IsTrue(File.Exists(path), $"Expected def file missing: {path}");

        XElement stats = XDocument.Load(path).Root?
            .Elements("ThingDef")
            .First(def => (string)def.Element("defName") == "CBC_FertilizerPack")
            .Element("statBases");

        Assert.AreEqual("6", (string)stats.Element("DeteriorationRate"));
        Assert.AreEqual("60", (string)stats.Element("MaxHitPoints"));
    }
}
