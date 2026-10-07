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

using NUnit.Framework;

namespace BetterRimworlds.ChickenBatteryCage.Tests;

/**
 * Locks in the savegame-budget claim behind virtualization: caged chickens are
 * compact records, so even ten thousand of them stay tiny compared with the
 * Pawn graph they replace.
 */
[TestFixture]
public class CageSaveAuditTests
{
    const long Kilobyte = 1024;

    [Test]
    public void EmptyColonyCostsAlmostNothing()
    {
        Assert.AreEqual(0L, CageSaveSize.EstimatedBytes(0, 0));
    }

    [Test]
    public void TenThousandCagedChickensStayUnderTwoMegabytes()
    {
        long bytes = CageSaveSize.EstimatedBytesForRecords(10000);

        Assert.Less(bytes, 2 * 1024 * Kilobyte);
    }

    [Test]
    public void CagedChickenRecordsScaleLinearly()
    {
        long oneHundred = CageSaveSize.EstimatedBytesForRecords(100);
        long oneThousand = CageSaveSize.EstimatedBytesForRecords(1000);

        Assert.AreEqual(oneHundred * 10, oneThousand);
    }

    [Test]
    public void PerRecordCostIsSmallEnoughToBeatPawnGraphs()
    {
        // A single caged bird must cost far less than a serialized Pawn.
        Assert.Less(CageSaveSize.EstimatedBytesPerRecord, 1024);
    }

    [Test]
    public void PerRecordCostIsDerivedFromThePersistedFieldSet()
    {
        int expected = 0;
        foreach (PersistedField field in CagedChickenFields.All)
        {
            expected += CageSaveSize.ElementBytes(field);
        }

        Assert.AreEqual(expected, CageSaveSize.EstimatedBytesPerRecord);
    }

    [Test]
    public void EveryPersistedFieldHasANameAndAValueWidth()
    {
        Assert.IsNotEmpty(CagedChickenFields.All);
        foreach (PersistedField field in CagedChickenFields.All)
        {
            Assert.IsFalse(string.IsNullOrEmpty(field.Name), "a persisted field needs a Scribe name");
            Assert.Greater(field.MaxValueWidth, 0, field.Name + " needs a value width");
        }
    }

    [Test]
    public void ElementBytesChargeTheNameTwiceAndTheValue()
    {
        foreach (PersistedField field in CagedChickenFields.All)
        {
            int minimum = field.Name.Length * 2 + field.MaxValueWidth;
            Assert.GreaterOrEqual(CageSaveSize.ElementBytes(field), minimum, field.Name);
        }
    }

    [Test]
    public void CageOverheadScalesWithCageCount()
    {
        long tenCages = CageSaveSize.EstimatedBytes(10, 0);

        Assert.AreEqual(CageSaveSize.EstimatedBytesPerCage * 10L, tenCages);
    }
}
