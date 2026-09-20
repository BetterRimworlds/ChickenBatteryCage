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
        if (record == null || map == null || ChickenBatteryCageDefOf.Chicken == null)
        {
            return null;
        }

        int now = GenTicks.TicksAbs;
        long biologicalAgeTicks = record.BiologicalAgeTicksAt(now);
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
                developmentalStages: DevelopmentalStage.Newborn
                    | DevelopmentalStage.Baby
                    | DevelopmentalStage.Child
                    | DevelopmentalStage.Adult);

            chicken = PawnGenerator.GeneratePawn(request);
        }
        catch (Exception ex)
        {
            Log.Error("[ChickenBatteryCage] Exception while regenerating a chicken from a caged record: " + ex);
            return null;
        }

        if (chicken == null)
        {
            Log.Error("[ChickenBatteryCage] Failed to regenerate a chicken from a caged record; the record was kept.");
            return null;
        }

        // Pin the age to the exact tick rather than the float year used by the
        // generation request, so release matches the stored biology precisely.
        chicken.ageTracker.AgeBiologicalTicks = biologicalAgeTicks;
        chicken.ageTracker.BirthAbsTicks = now - biologicalAgeTicks;

        IntVec3 spawnCell = FindStandableCellNear(map, near);
        if (!spawnCell.IsValid || !GenPlace.TryPlaceThing(chicken, spawnCell, map, ThingPlaceMode.Near))
        {
            Log.Error("[ChickenBatteryCage] Could not place a released chicken on the map; the record was kept.");
            CageHenIntake.UnmakeWithoutDeath(chicken);
            return null;
        }

        return chicken;
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
