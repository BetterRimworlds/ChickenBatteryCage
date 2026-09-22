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

[TestFixture]
public class CageUnloadTests
{
    [Test]
    public void CompetingAdultRequestsCannotReuseTheOnlyAdult()
    {
        var requests = new List<ChickenReleaseFilter>
        {
            ChickenReleaseFilter.AdultHen, ChickenReleaseFilter.AdultHen, ChickenReleaseFilter.Juvenile,
        };
        CageUnloadMath.Reconcile(requests, 3, 1, 2);
        CollectionAssert.AreEqual(new[] { ChickenReleaseFilter.AdultHen, ChickenReleaseFilter.Juvenile }, requests);
    }

    [Test]
    public void UnrestrictedRequestCanLeaveTheAdultForARestrictedRequest()
    {
        var requests = new List<ChickenReleaseFilter>
        {
            ChickenReleaseFilter.Random, ChickenReleaseFilter.AdultHen,
        };
        CageUnloadMath.Reconcile(requests, 2, 1, 1);
        Assert.AreEqual(2, requests.Count);
    }

    [Test]
    public void EarlierRequestsWinWhenTotalPopulationShrinks()
    {
        var requests = new List<ChickenReleaseFilter>
        {
            ChickenReleaseFilter.Youngest, ChickenReleaseFilter.AdultHen, ChickenReleaseFilter.Juvenile,
        };
        CageUnloadMath.Reconcile(requests, 2, 1, 1);
        CollectionAssert.AreEqual(new[] { ChickenReleaseFilter.Youngest, ChickenReleaseFilter.AdultHen }, requests);
    }

    [Test]
    public void MissingCategoriesDoNotBlockLaterSatisfiableRequests()
    {
        var requests = new List<ChickenReleaseFilter>
        {
            ChickenReleaseFilter.AdultHen, ChickenReleaseFilter.Juvenile, ChickenReleaseFilter.Oldest,
        };
        CageUnloadMath.Reconcile(requests, 2, 0, 2);
        CollectionAssert.AreEqual(new[] { ChickenReleaseFilter.Juvenile, ChickenReleaseFilter.Oldest }, requests);
        CageUnloadMath.Reconcile(requests, 0, 0, 0);
        Assert.IsEmpty(requests);
    }

    [Test]
    public void AllRequestsSurviveReconciliationWhileBirdsRemain()
    {
        var requests = new List<ChickenReleaseFilter>
        {
            ChickenReleaseFilter.All, ChickenReleaseFilter.All,
        };
        CageUnloadMath.Reconcile(requests, 2, 2, 0);
        CollectionAssert.AreEqual(
            new[] { ChickenReleaseFilter.All, ChickenReleaseFilter.All }, requests);
    }

    [Test]
    public void ExcessAllRequestsAreDroppedOnlyWhenTheFlockEmpties()
    {
        var requests = new List<ChickenReleaseFilter>
        {
            ChickenReleaseFilter.All, ChickenReleaseFilter.All, ChickenReleaseFilter.All,
        };
        // A bird died after two All marks were queued: keep both, drop one.
        CageUnloadMath.Reconcile(requests, 2, 2, 0);
        CollectionAssert.AreEqual(
            new[] { ChickenReleaseFilter.All, ChickenReleaseFilter.All }, requests);

        // One more release succeeds: the last mark still has a bird to free.
        CageUnloadMath.Reconcile(requests, 1, 1, 0);
        Assert.AreEqual(1, requests.Count);

        // The flock is gone: nothing remains to release.
        CageUnloadMath.Reconcile(requests, 0, 0, 0);
        Assert.IsEmpty(requests);
    }

    [Test]
    public void MixedSelectorsAndAllRequestsShareTheShrinkingFlock()
    {
        var requests = new List<ChickenReleaseFilter>
        {
            ChickenReleaseFilter.All, ChickenReleaseFilter.AdultHen, ChickenReleaseFilter.Juvenile,
        };
        CageUnloadMath.Reconcile(requests, 3, 2, 1);
        Assert.AreEqual(3, requests.Count);

        // One hen and one juvenile died; the All mark covers the survivor,
        // leaving no bird for the AdultHen mark.
        CageUnloadMath.Reconcile(requests, 1, 1, 0);
        CollectionAssert.AreEqual(new[] { ChickenReleaseFilter.All }, requests);
    }

    [Test]
    public void DeadSpecificTargetDoesNotHoldASlotFromALivingOne()
    {
        object dead = new object();
        object alive = new object();
        var requests = new List<ChickenReleaseFilter>
        {
            ChickenReleaseFilter.Specific, ChickenReleaseFilter.Specific,
        };
        var targets = new List<object> { dead, alive };
        var surviving = new List<object> { alive };

        // The first specific bird died, leaving a single living bird. Without
        // validating the dead target the stale mark would consume the only
        // slot and drop the living bird's mark.
        CageUnloadMath.Reconcile(requests, 1, 1, 0, targets, surviving);

        CollectionAssert.AreEqual(new[] { ChickenReleaseFilter.Specific }, requests);
        CollectionAssert.AreEqual(new[] { alive }, targets);
    }

    [Test]
    public void LivingSpecificTargetsKeepTheirSlots()
    {
        object first = new object();
        object second = new object();
        var requests = new List<ChickenReleaseFilter>
        {
            ChickenReleaseFilter.Specific, ChickenReleaseFilter.Specific,
        };
        var targets = new List<object> { first, second };
        var surviving = new List<object> { first, second };

        CageUnloadMath.Reconcile(requests, 2, 2, 0, targets, surviving);

        Assert.AreEqual(2, requests.Count);
        CollectionAssert.AreEqual(new[] { first, second }, targets);
    }
}
