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
}
