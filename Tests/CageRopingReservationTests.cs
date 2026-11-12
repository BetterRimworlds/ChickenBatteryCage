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

/// <summary>
/// Source-contract regressions: the unit-test assembly does not load RimWorld.
/// Both selection paths must reject birds already roped or reserved before
/// choosing them. Reserving the destination cage alone cannot protect birds
/// from handlers working at different cages or vanilla pens.
/// </summary>
[TestFixture]
public class CageRopingReservationTests
{
    [TestCase("WorkGiver_TakeHensToBatteryCage.cs", "public override Job JobOnThing")]
    [TestCase("JobDriver_RopeHenToBatteryCage.cs", "protected override bool ShouldOpportunisticallyRopeAnimal")]
    public void SelectionRejectsBirdsRopedByAnyHandler(string fileName, string method)
    {
        string body = MethodBody(fileName, method);

        Assert.That(body, Does.Match(
            @"if\s*\([^{};]*animal\.roping\.RopedByPawn\s*!=\s*null[^{};]*\)\s*\{\s*return\s+(null|false)\s*;"),
            "Reject every already-roped bird, not just birds roped by this handler.");
    }

    [TestCase("WorkGiver_TakeHensToBatteryCage.cs", "public override Job JobOnThing")]
    [TestCase("JobDriver_RopeHenToBatteryCage.cs", "protected override bool ShouldOpportunisticallyRopeAnimal")]
    public void SelectionRespectsOtherHandlersReservations(string fileName, string method)
    {
        string body = MethodBody(fileName, method);

        Assert.That(body, Does.Match(
            @"if\s*\([^{};]*!pawn\.CanReserve\(animal\)[^{};]*\)\s*\{\s*return\s+(null|false)\s*;"),
            "A handler approaching a reserved bird must not compete with another handler, even for forced work.");
    }

    [Test]
    public void LeadingRouteUsesBirdOriginAndAnimalFenceRestrictions()
    {
        string body = MethodBody("JobDriver_RopeHenToBatteryCage.cs", "public static bool CanLeadToCell");

        Assert.That(body, Does.Match(@"CanReach\(animal\.Position,\s*destination,\s*PathEndMode\.OnCell,\s*TraverseParms\.For\(handler\)\.WithFenceblockedOf\(animal\)\)"),
            "Handler-only reachability allows pickup from pens the chicken cannot leave.");
    }

    [Test]
    public void InitialPickupUsesStandCellValidatedDuringCageSearch()
    {
        string body = MethodBody("WorkGiver_TakeHensToBatteryCage.cs", "public override Job JobOnThing");

        Assert.That(body, Does.Contain("out IntVec3 standCell"));
        Assert.That(body, Does.Not.Contain("FindStandCellForHandler"));
    }

    [Test]
    public void StandCellSearchChecksLeadingRouteBeforeAcceptingEachCell()
    {
        string body = MethodBody("Building_ChickenBatteryCage.cs", "public IntVec3 FindStandCellForHandler");

        Assert.That(body, Does.Contain("foreach (IntVec3 cell in GenAdj.CellsAdjacent8Way(this))"));
        Assert.That(body, Does.Match(@"if\s*\(IsGoodStandCell\(cell, handler\)\s*&&\s*\(chicken == null \|\| JobDriver_RopeHenToBatteryCage\.CanLeadToCell\(handler, chicken, cell\)\)\)\s*\{\s*return cell;"));
    }

    [Test]
    public void CageSearchPropagatesHenToServiceAndStandCellSearches()
    {
        string selection = MethodBody("Building_ChickenBatteryCage.cs", "public static bool TryFindAcceptingCage");
        string service = MethodBody("Building_ChickenBatteryCage.cs", "public static Building_ChickenBatteryCage FindServiceCage");
        string usable = MethodBody("Building_ChickenBatteryCage.cs", "static bool IsServiceable");

        Assert.That(selection, Does.Contain("FindServiceCage(cage, handler, chicken)"));
        Assert.That(selection, Does.Match(@"if \(candidateService == null\)\s*\{\s*continue;"));
        Assert.That(selection, Does.Contain("standCell = candidateService.FindStandCellForHandler(handler, chicken)"));
        Assert.That(service, Does.Contain("IsServiceable(owner, handler, chicken)"));
        Assert.That(service, Does.Match(@"if \(!IsServiceable\(cage, handler, chicken\)\)\s*\{\s*continue;"));
        Assert.That(usable, Does.Contain("cage.FindStandCellForHandler(handler, chicken).IsValid"));
    }

    [Test]
    public void ExtraPickupsCheckLeadingRouteToJobDestination()
    {
        string body = MethodBody("JobDriver_RopeHenToBatteryCage.cs", "protected override bool ShouldOpportunisticallyRopeAnimal");

        Assert.That(body, Does.Contain("&& CanLeadToCell(pawn, animal, job.GetTarget(TargetIndex.B).Cell)"));
    }

    [Test]
    public void BlockedRoutesAbortActiveJobForAllLedBirds()
    {
        string toils = MethodBody("JobDriver_RopeHenToBatteryCage.cs", "protected override IEnumerable<Toil> MakeNewToils");
        string check = MethodBody("JobDriver_RopeHenToBatteryCage.cs", "private bool CanLeadRopeesToDestination");

        Assert.That(toils, Does.Contain("this.FailOn(() => !CanLeadRopeesToDestination())"));
        Assert.That(check, Does.Contain("foreach (Pawn animal in pawn.roping.Ropees)"));
        Assert.That(check, Does.Match(@"if\s*\(!CanLeadToCell\(pawn, animal, destination\)\)\s*\{\s*return false;"),
            "If a gate becomes blocked after pickup, abort so the base driver's finish action drops the ropes.");
    }

    private static string MethodBody(string fileName, string method)
    {
        string path = Path.Combine(TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "Source", fileName);
        string source = File.ReadAllText(path);
        // Ignore comments so prose cannot accidentally satisfy a guard check.
        source = Regex.Replace(source, @"/\*.*?\*/|//[^\r\n]*", "", RegexOptions.Singleline);
        int start = source.IndexOf(method, System.StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0), $"Missing selection method in {fileName}.");
        start = source.IndexOf('{', start);
        int depth = 1;
        int end = start + 1;
        for (; end < source.Length && depth > 0; end++)
        {
            if (source[end] == '{') depth++;
            if (source[end] == '}') depth--;
        }
        return source.Substring(start + 1, end - start - 2);
    }
}
