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
using RimWorld;
using Verse;
using Verse.AI;

namespace BetterRimworlds.ChickenBatteryCage;

/**
 * Servicing work that empties a dirty battery cage into Fertilizer Packs.
 *
 * A cage only becomes a work target once its cluster's accumulated manure
 * reaches the cleaning threshold, so ordinary accumulation sits unattended and
 * a large installation is serviced in infrequent batches instead of every day.
 */
public class WorkGiver_CleanBatteryCages : WorkGiver_Scanner
{
    public override PathEndMode PathEndMode => PathEndMode.ClosestTouch;

    public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
    {
        return pawn.Map.listerBuildings.AllBuildingsColonistOfClass<Building_ChickenBatteryCage>();
    }

    public override bool ShouldSkip(Pawn pawn, bool forced)
    {
        return !Building_ChickenBatteryCage.AnyCageNeedingCleaning(pawn.Map);
    }

    public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        Building_ChickenBatteryCage cage = t as Building_ChickenBatteryCage;
        if (cage == null || !cage.NeedsCleaning)
        {
            return false;
        }

        if (cage.IsForbidden(pawn))
        {
            return false;
        }

        // A cage another cleaner has already claimed must not be offered, or the
        // job start logs a failed reservation and immediately bails out. This
        // mirrors JobDriver_CleanBatteryCage.TryMakePreToilReservations.
        if (!pawn.CanReserve(cage))
        {
            return false;
        }

        if (!pawn.CanReach(cage, PathEndMode.ClosestTouch, Danger.Deadly))
        {
            JobFailReason.Is("NoPath".Translate());
            return false;
        }

        return true;
    }

    public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        Building_ChickenBatteryCage cage = t as Building_ChickenBatteryCage;
        if (cage == null || !cage.NeedsCleaning)
        {
            return null;
        }

        return JobMaker.MakeJob(ChickenBatteryCageDefOf.CBC_CleanBatteryCage, cage);
    }
}
