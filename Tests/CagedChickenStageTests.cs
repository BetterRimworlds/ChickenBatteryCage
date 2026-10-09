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
 * The pickup window classifies stored ages into chick, juvenile, and adult.
 * These tests pin the boundaries and the display ordering, using the real
 * chicken def's thresholds so a rebalanced vanilla def is still exercised.
 */
[TestFixture]
public class CagedChickenStageTests
{
    const int Year = CagedChickenMath.TicksPerYear;

    // Chicken lifeStageAges: AnimalBabyTiny at 0, AnimalJuvenile at 0.12,
    // AnimalAdult at 0.2.
    static readonly long JuvenileMin = (long)(0.12f * Year);
    static readonly long AdultMin = (long)(0.2f * Year);

    static CagedChickenStage StageAt(long ageTicks)
    {
        return CagedChickenStageMath.StageAt(ageTicks, JuvenileMin, AdultMin);
    }

    [Test]
    public void NewlyLaidEggAge_IsAChick()
    {
        Assert.AreEqual(CagedChickenStage.Chick, StageAt(0));
    }

    [Test]
    public void JustBelowJuvenile_IsStillAChick()
    {
        Assert.AreEqual(CagedChickenStage.Chick, StageAt(JuvenileMin - 1));
    }

    [Test]
    public void ExactlyAtJuvenileThreshold_IsAJuvenile()
    {
        Assert.AreEqual(CagedChickenStage.Juvenile, StageAt(JuvenileMin));
    }

    [Test]
    public void JustBelowAdult_IsAJuvenile()
    {
        Assert.AreEqual(CagedChickenStage.Juvenile, StageAt(AdultMin - 1));
    }

    [Test]
    public void ExactlyAtAdultThreshold_IsAnAdult()
    {
        Assert.AreEqual(CagedChickenStage.Adult, StageAt(AdultMin));
    }

    [Test]
    public void GrownHen_IsAnAdult()
    {
        Assert.AreEqual(CagedChickenStage.Adult, StageAt(3 * Year));
    }

    [Test]
    public void UnknownJuvenileThreshold_SkipsTheJuvenileBand()
    {
        // A zero juvenile threshold means it is unresolved; a bird below the
        // adult line is then a chick rather than being mislabelled juvenile.
        Assert.AreEqual(CagedChickenStage.Chick, CagedChickenStageMath.StageAt(0, 0, AdultMin));
        Assert.AreEqual(CagedChickenStage.Chick, CagedChickenStageMath.StageAt(JuvenileMin, 0, AdultMin));
    }

    [Test]
    public void UnknownAdultThreshold_ResolvesToAdult()
    {
        // The defs are not loaded yet: never claim a grown bird is a chick.
        Assert.AreEqual(CagedChickenStage.Adult, CagedChickenStageMath.StageAt(0, 0, 0));
        Assert.AreEqual(CagedChickenStage.Adult, CagedChickenStageMath.StageAt(Year, 0, 0));
    }

    [Test]
    public void SortRank_OrdersChickThenJuvenileThenAdult()
    {
        Assert.Less(
            CagedChickenStageMath.SortRank(CagedChickenStage.Chick),
            CagedChickenStageMath.SortRank(CagedChickenStage.Juvenile));
        Assert.Less(
            CagedChickenStageMath.SortRank(CagedChickenStage.Juvenile),
            CagedChickenStageMath.SortRank(CagedChickenStage.Adult));
    }

    [Test]
    public void Stage_IsMonotonicInAge()
    {
        // Walking ages upward must never move a bird backwards through stages.
        int last = -1;
        for (long age = 0; age <= 2 * Year; age += Year / 20)
        {
            int rank = CagedChickenStageMath.SortRank(StageAt(age));
            Assert.GreaterOrEqual(rank, last);
            last = rank;
        }
    }
}
