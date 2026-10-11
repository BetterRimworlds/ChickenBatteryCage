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
 * Pins the wastepack dissolution clock: eight days outdoors, twice as long
 * indoors, half as long in rain or snow, and a one-day base for a fast build.
 */
[TestFixture]
public class FertilizerDissolutionMathTests
{
    const int Day = CagedChickenMath.TicksPerDay;

    static readonly string TestRoot = Path.Combine(
        TestContext.CurrentContext.TestDirectory,
        "..", "..", "..", "..");

    [Test]
    public void OutdoorsInFairWeatherTakesEightDays()
    {
        Assert.AreEqual(8f, FertilizerDissolutionMath.DaysToDissolve);
        Assert.AreEqual(8 * Day, FertilizerDissolutionMath.IntervalTicks(8f, outdoors: true, raining: false));
    }

    [Test]
    public void IndoorsTakesTwiceAsLong()
    {
        Assert.AreEqual(16 * Day, FertilizerDissolutionMath.IntervalTicks(8f, outdoors: false, raining: false));
    }

    [Test]
    public void OutdoorRainOrSnowTakesFourDays()
    {
        Assert.IsTrue(FertilizerDissolutionMath.CountsAsRain(1f));
        Assert.IsFalse(FertilizerDissolutionMath.CountsAsRain(0f));
        Assert.IsFalse(FertilizerDissolutionMath.CountsAsRain(FertilizerDissolutionMath.MinRainRate));
        Assert.AreEqual(4 * Day, FertilizerDissolutionMath.IntervalTicks(8f, outdoors: true, raining: true));
    }

    [Test]
    public void AFastBuildKeepsTheFactorsAndShortensTheBaseToOneDay()
    {
        Assert.AreEqual(1f, FertilizerDissolutionMath.FastDays);
        Assert.AreEqual(Day, FertilizerDissolutionMath.IntervalTicks(1f, outdoors: true, raining: false));
        Assert.AreEqual(2 * Day, FertilizerDissolutionMath.IntervalTicks(1f, outdoors: false, raining: false));
        Assert.AreEqual(Day / 2, FertilizerDissolutionMath.IntervalTicks(1f, outdoors: true, raining: true));
    }

    [Test]
    public void TheDefMatchesTheWastepackNumbers()
    {
        string path = Path.Combine(
            TestRoot, "ChickenBatteryCage", "Defs", "ThingDefs_Items", "FertilizerPack.xml");
        Assert.IsTrue(File.Exists(path), $"Expected def file missing: {path}");

        XElement def = XDocument.Load(path).Root?
            .Elements("ThingDef")
            .First(item => (string)item.Element("defName") == "CBC_FertilizerPack");

        Assert.AreEqual("Normal", (string)def.Element("tickerType"));
        Assert.AreEqual(
            FertilizerDissolutionMath.DeteriorationRateLikeWastepack.ToString("0"),
            (string)def.Element("statBases").Element("DeteriorationRate"));
        Assert.IsNull(def.Element("statBases").Element("MaxHitPoints"));
        Assert.IsNull(def.Descendants("CompProperties_Rottable").FirstOrDefault());

        XElement comp = def.Descendants("li")
            .First(item => (string)item.Attribute("Class") ==
                "BetterRimworlds.ChickenBatteryCage.CompProperties_FertilizerDissolution");
        Assert.AreEqual("8", (string)comp.Element("dissolutionAfterDays"));
        Assert.AreEqual("0.5", (string)comp.Element("dissolutionFactorIndoors"));
        Assert.AreEqual("2", (string)comp.Element("dissolutionFactorRain"));
    }
}
