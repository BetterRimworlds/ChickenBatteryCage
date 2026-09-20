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
 * Handling work that unloads chickens the player marked for release.
 *
 * A cage only becomes a work target once the player has marked at least one
 * bird, so this never competes with ordinary handling while the flock is
 * settled.
 */
public class WorkGiver_UnloadBatteryCages : WorkGiver_Scanner
{
    public override PathEndMode PathEndMode => PathEndMode.InteractionCell;

    public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
    {
        return pawn.Map.listerBuildings.AllBuildingsColonistOfClass<Building_ChickenBatteryCage>();
    }

    public override bool ShouldSkip(Pawn pawn, bool forced)
    {
        return !Building_ChickenBatteryCage.AnyCageWithPendingUnload(pawn.Map);
    }

    public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        Building_ChickenBatteryCage cage = t as Building_ChickenBatteryCage;
        if (cage == null || !cage.HasPendingUnload)
        {
            return false;
        }

        if (cage.IsForbidden(pawn))
        {
            return false;
        }

        if (!pawn.CanReserve(cage))
        {
            return false;
        }

        if (!pawn.CanReach(cage, PathEndMode.InteractionCell, Danger.Deadly))
        {
            JobFailReason.Is("NoPath".Translate());
            return false;
        }

        return true;
    }

    public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        return JobMaker.MakeJob(ChickenBatteryCageDefOf.CBC_UnloadBatteryCage, t);
    }
}
