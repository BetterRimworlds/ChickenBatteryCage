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
 * A cell transforms on the third pack that strikes it, and not before. The
 * charge then clears so the next step of the terrain progression costs another
 * three packs.
 */
[TestFixture]
public class FertilizerChargeTests
{
    [Test]
    public void ThreePacksTransformACell()
    {
        Assert.AreEqual(3, FertilizerChargeMath.PacksToTransform);

        Assert.IsFalse(FertilizerChargeMath.ApplyHit(0, out int afterFirst));
        Assert.AreEqual(1, afterFirst);

        Assert.IsFalse(FertilizerChargeMath.ApplyHit(afterFirst, out int afterSecond));
        Assert.AreEqual(2, afterSecond);

        Assert.IsTrue(FertilizerChargeMath.ApplyHit(afterSecond, out int afterThird));
        Assert.AreEqual(0, afterThird);
    }

    [Test]
    public void TheNextStepStartsFromZero()
    {
        Assert.IsFalse(FertilizerChargeMath.ApplyHit(0, out int charge));
        Assert.AreEqual(1, charge);
    }

    [Test]
    public void ACorruptNegativeChargeCountsAsNone()
    {
        Assert.IsFalse(FertilizerChargeMath.ApplyHit(-4, out int charge));
        Assert.AreEqual(1, charge);
    }
}
