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
    public void DeadRecordMarkDoesNotHoldASlotFromALivingOne()
    {
        object dead = new object();
        object alive = new object();
        var marks = new List<object> { dead, alive };
        var surviving = new List<object> { alive };

        // The dead bird's mark must be dropped; the living bird is the only
        // one left to release.
        CageUnloadMath.Reconcile(marks, surviving);

        CollectionAssert.AreEqual(new[] { alive }, marks);
    }

    [Test]
    public void LivingRecordMarksKeepTheirSlots()
    {
        object first = new object();
        object second = new object();
        var marks = new List<object> { first, second };
        var surviving = new List<object> { first, second };

        CageUnloadMath.Reconcile(marks, surviving);

        CollectionAssert.AreEqual(new[] { first, second }, marks);
    }

    [Test]
    public void ReconciliationPreservesQueueOrder()
    {
        object dropped = new object();
        object first = new object();
        object second = new object();
        var marks = new List<object> { dropped, first, second };
        var surviving = new List<object> { first, second };

        CageUnloadMath.Reconcile(marks, surviving);

        CollectionAssert.AreEqual(new[] { first, second }, marks);
    }

    [Test]
    public void ReconciliationEmptiesWhenNoBirdSurvives()
    {
        object first = new object();
        var marks = new List<object> { first };

        CageUnloadMath.Reconcile(marks, new List<object>());

        Assert.IsEmpty(marks);
    }
}
