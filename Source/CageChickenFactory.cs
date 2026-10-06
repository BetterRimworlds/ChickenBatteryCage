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

using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace BetterRimworlds.ChickenBatteryCage;

/**
 * Materializes a fresh chicken Pawn from a serialized cage record.
 *
 * This is the only place where a cage chicken becomes a real Pawn again. No
 * original ThingID, name, or history is carried over: the record holds only
 * the biology required to generate an equivalent bird.
 */
public static class CageChickenFactory
{
    public static Pawn Generate(CagedChickenRecord record, Map map, IntVec3 near)
    {
        Pawn chicken = Reconstruct(record, map, GenTicks.TicksAbs);
        if (chicken == null)
        {
            return null;
        }

        IntVec3 spawnCell = FindStandableCellNear(map, near);
        if (!spawnCell.IsValid || !GenPlace.TryPlaceThing(chicken, spawnCell, map, ThingPlaceMode.Near))
        {
            Log.Error("[ChickenBatteryCage] Could not place a released chicken; the caller retains her record.");
            CageHenIntake.UnmakeWithoutDeath(chicken);
            return null;
        }

        return chicken;
    }

    /// Creates the body off-map. Corpse placement does not require a living
    /// animal's stand cell, and a failed placement can retain this same body.
    public static Corpse GenerateCorpse(CagedChickenRecord record, Map map, int diedAtTick)
    {
        Pawn chicken = Reconstruct(record, map, diedAtTick);
        if (chicken == null)
        {
            return null;
        }

        chicken.Kill(null);
        Corpse corpse = chicken.Corpse;
        if (corpse == null)
        {
            Log.Error("[ChickenBatteryCage] Could not create a chicken corpse; the caller retains the dead record.");
            CageHenIntake.UnmakeWithoutDeath(chicken);
        }
        return corpse;
    }

    static Pawn Reconstruct(CagedChickenRecord record, Map map, int biologicalTick)
    {
        if (record == null || map == null || ChickenBatteryCageDefOf.Chicken == null)
        {
            return null;
        }

        int now = GenTicks.TicksAbs;
        long biologicalAgeTicks = record.BiologicalAgeTicksAt(biologicalTick);
        float biologicalAgeYears = biologicalAgeTicks / (float)GenDate.TicksPerYear;

        Pawn chicken;
        try
        {
            PawnGenerationRequest request = new PawnGenerationRequest(
                ChickenBatteryCageDefOf.Chicken,
                Faction.OfPlayer,
                PawnGenerationContext.NonPlayer,
                tile: map.Tile,
                forceGenerateNewPawn: true,
                canGeneratePawnRelations: false,
                fixedBiologicalAge: biologicalAgeYears,
                fixedChronologicalAge: biologicalAgeYears,
                fixedGender: record.gender,
                forceNoIdeo: true,
                developmentalStages: DevelopmentalStageFor(biologicalAgeTicks));

            chicken = PawnGenerator.GeneratePawn(request);
        }
        catch (Exception ex)
        {
            Log.Error("[ChickenBatteryCage] Exception while regenerating a chicken from a caged record: " + ex);
            return null;
        }

        if (chicken == null)
        {
            Log.Error("[ChickenBatteryCage] Failed to reconstruct a chicken; the caller retains the record.");
            return null;
        }

        // Pin the age to the exact tick rather than the float year used by the
        // generation request, so release matches the stored biology precisely.
        chicken.ageTracker.AgeBiologicalTicks = biologicalAgeTicks;
        chicken.ageTracker.BirthAbsTicks = now - biologicalAgeTicks;

        return chicken;
    }

    /**
     * The single developmental stage a regenerated bird may carry. RimWorld's
     * generator applies one stage at a time — asking for several at once makes
     * it log "Trying to generate a newborn and other developmental stages
     * simultaneously" and then distrust the requested age. A chicken only
     * grows from chick straight to adult, so the stage is simply whichever
     * side of that single threshold the recorded age sits on. The exact age is
     * pinned afterwards, which also lands the bird in the matching life stage.
     */
    internal static DevelopmentalStage DevelopmentalStageFor(long biologicalAgeTicks)
    {
        return CagedChickenMath.IsNewbornStage(biologicalAgeTicks, AdultMinAgeTicks())
            ? DevelopmentalStage.Newborn
            : DevelopmentalStage.Adult;
    }

    /// The chicken's own adult threshold, taken from its life stages so this
    /// stays correct if the bird's ages are ever rebalanced. Returns 0 while the
    /// defs are unavailable.
    static long AdultMinAgeTicks()
    {
        ThingDef chickenDef = ChickenBatteryCageDefOf.Chicken?.race;
        List<LifeStageAge> stages = chickenDef?.race?.lifeStageAges;
        if (stages == null || stages.Count == 0)
        {
            return 0;
        }

        // The final stage is the bird's adult form; its age is the threshold.
        return (long)(stages[stages.Count - 1].minAge * GenDate.TicksPerYear);
    }

    static IntVec3 FindStandableCellNear(Map map, IntVec3 near)
    {
        if (near.InBounds(map) && near.Standable(map))
        {
            return near;
        }

        if (CellFinder.TryFindRandomCellNear(
                near,
                map,
                8,
                c => c.InBounds(map) && c.Standable(map),
                out IntVec3 found))
        {
            return found;
        }

        return IntVec3.Invalid;
    }
}
