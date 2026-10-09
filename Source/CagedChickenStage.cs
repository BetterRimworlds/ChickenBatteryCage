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

namespace BetterRimworlds.ChickenBatteryCage;

/**
 * The three life stages a chicken passes through, oldest last.
 *
 * This is a display-only classification derived from the exact biological age
 * already stored in a cage record. It is deliberately separate from
 * Verse.DevelopmentalStage, which RimWorld's pawn generator only splits into
 * Newborn and Adult: a chicken def carries a distinct Juvenile stage between
 * the two, and the player choosing which birds to unload cares about that
 * distinction.
 */
public enum CagedChickenStage
{
    /// AnimalBabyTiny, the bird a chicken def calls a chick.
    Chick,

    /// AnimalJuvenile, grown past a chick but not yet laying.
    Juvenile,

    /// AnimalAdult, the layout-age bird.
    Adult,
}

/**
 * Maps an exact biological age onto the chicken life stages.
 *
 * Kept Verse-free so the ordering and boundary rules can be unit tested
 * without launching RimWorld; the thresholds themselves are read from the
 * chicken def's own lifeStageAges by the caller, so a rebalanced def stays
 * correct rather than being hard-coded here.
 */
public static class CagedChickenStageMath
{
    /**
     * The stage a bird of this age belongs to.
     *
     * Thresholds are the minAge of the Juvenile and Adult stages, in ticks.
     * A non-positive adult threshold means it is unknown, which can only be
     * the case before the defs load; that resolves to Adult (matching
     * CagedChickenMath.IsNewbornStage) so an unresolved def never mislabels a
     * grown bird.
     *
     * Boundaries are inclusive: a bird exactly at the juvenile threshold is a
     * juvenile, and one exactly at the adult threshold is an adult.
     */
    public static CagedChickenStage StageAt(
        long biologicalAgeTicks,
        long juvenileMinAgeTicks,
        long adultMinAgeTicks)
    {
        if (adultMinAgeTicks <= 0)
        {
            // Defs are not loaded; treat every bird as an adult rather than
            // asserting an age we cannot actually check.
            return CagedChickenStage.Adult;
        }

        if (biologicalAgeTicks >= adultMinAgeTicks)
        {
            return CagedChickenStage.Adult;
        }

        if (juvenileMinAgeTicks > 0 && biologicalAgeTicks >= juvenileMinAgeTicks)
        {
            return CagedChickenStage.Juvenile;
        }

        return CagedChickenStage.Chick;
    }

    /**
     * Sort key that groups stages in the order a player reads them: chicks
     * first, then juveniles, then adults. Within a stage, age decides.
     */
    public static int SortRank(CagedChickenStage stage)
    {
        switch (stage)
        {
            case CagedChickenStage.Chick:
                return 0;
            case CagedChickenStage.Juvenile:
                return 1;
            default:
                return 2;
        }
    }
}
