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
 * Guards the direct-feeding contract at the def level:
 *
 * - the cage no longer wants a hopper adjacent and nothing references hoppers;
 * - the vanilla-hopper research patch is gone;
 * - a hauling job and work giver carry feed straight to the cage.
 */
[TestFixture]
public class CageFeedDefTests
{
    static readonly string TestRoot = Path.Combine(
        TestContext.CurrentContext.TestDirectory,
        "..", "..", "..", "..");

    static string DefPath(params string[] parts)
    {
        return Path.Combine(new[] { TestRoot, "ChickenBatteryCage" }.Concat(parts).ToArray());
    }

    static XDocument DefXml(params string[] parts)
    {
        string path = DefPath(parts);
        Assert.IsTrue(File.Exists(path), $"Expected def file missing: {path}");
        return XDocument.Load(path);
    }

    static XElement Def(params string[] parts)
    {
        return DefXml(parts).Root;
    }

    static XElement CageDef()
    {
        XElement def = Def("Defs", "ThingDefs_Buildings", "Buildings_ChickenBatteryCage.xml")
            .Elements("ThingDef")
            .FirstOrDefault(e => (string)e.Element("defName") == "ChickenBatteryCage");
        Assert.IsNotNull(def, "ChickenBatteryCage def is missing.");
        return def;
    }

    [Test]
    public void CageNoLongerWantsAHopperAdjacent()
    {
        XElement building = CageDef().Element("building");

        Assert.IsNotNull(building, "The cage must still define a building block.");
        Assert.IsNull(building.Element("wantsHopperAdjacent"),
            "Direct feeding replaces the hopper, so the cage must not request an adjacent hopper.");
    }

    [Test]
    public void VanillaHopperPatchIsGone()
    {
        string path = DefPath("Patches", "Research_VanillaHopper.xml");

        Assert.That(File.Exists(path), Is.False,
            "The cage no longer feeds through the vanilla hopper, so its research patch must not ship.");
    }

    [Test]
    public void DirectFeedingJobExists()
    {
        XElement job = Def("Defs", "JobDefs", "JobDefs_ChickenBatteryCage.xml")
            .Elements("JobDef")
            .FirstOrDefault(e => (string)e.Element("defName") == "CBC_FeedBatteryCage");

        Assert.IsNotNull(job, "A job that carries feed straight to a cage must exist.");
        Assert.That((string)job.Element("driverClass"),
            Is.EqualTo("BetterRimworlds.ChickenBatteryCage.JobDriver_FeedBatteryCage"),
            "The feeding job must point at its driver.");
    }

    [Test]
    public void FeedingWorkGiverIsAutomaticHauling()
    {
        XElement def = Def("Defs", "WorkGiverDefs", "WorkGivers_ChickenBatteryCage.xml")
            .Elements("WorkGiverDef")
            .FirstOrDefault(e => (string)e.Element("defName") == "CBC_FeedBatteryCages");

        Assert.IsNotNull(def, "An automatic feeding work giver must exist.");
        Assert.That((string)def.Element("giverClass"),
            Is.EqualTo("BetterRimworlds.ChickenBatteryCage.WorkGiver_FeedBatteryCages"),
            "The feeding work giver must point at its giver class.");
        Assert.That((string)def.Element("workType"), Is.EqualTo("Hauling"),
            "Feeding is ordinary hauling work so any hauler can do it.");
    }

    [Test]
    public void CageDefNoLongerReferencesHoppers()
    {
        string path = DefPath("Defs", "ThingDefs_Buildings", "Buildings_ChickenBatteryCage.xml");
        Assert.That(File.ReadAllText(path), Does.Not.Contain("Hopper"));
    }
}
