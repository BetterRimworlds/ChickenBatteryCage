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
 * Offers a fertilizer pack on natural ground so a grower can spread the whole
 * stack. Packs on a floor or a shelf are storage and are never offered. An
 * indoor pack is left alone unless the player orders the work by hand.
 */
public class WorkGiver_SpreadFertilizer : WorkGiver_Scanner
{
    public override PathEndMode PathEndMode => PathEndMode.ClosestTouch;

    public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
    {
        return pawn.Map.listerThings.ThingsOfDef(ChickenBatteryCageDefOf.CBC_FertilizerPack);
    }

    public override bool ShouldSkip(Pawn pawn, bool forced)
    {
        List<Thing> packs = pawn.Map.listerThings.ThingsOfDef(ChickenBatteryCageDefOf.CBC_FertilizerPack);
        if (packs.Count == 0)
        {
            return true;
        }

        if (forced)
        {
            return false;
        }

        for (int i = 0; i < packs.Count; i++)
        {
            CompFertilizerDissolution comp = packs[i].TryGetComp<CompFertilizerDissolution>();
            if (comp != null && comp.CanBeSpread && comp.IsOutdoors)
            {
                return false;
            }
        }

        return true;
    }

    public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        if (t == null || t.Destroyed || t.def != ChickenBatteryCageDefOf.CBC_FertilizerPack)
        {
            return false;
        }

        CompFertilizerDissolution comp = t.TryGetComp<CompFertilizerDissolution>();
        if (comp == null || !comp.CanBeSpread)
        {
            return false;
        }

        // Indoor packs do not dissolve on their own. A colonist spreads one
        // only when the player selects the order.
        if (!forced && !comp.IsOutdoors)
        {
            return false;
        }

        // The job consumes the whole stack, so the reservation has to cover
        // every pack in it.
        if (t.IsForbidden(pawn) || !pawn.CanReserve(t, 1, -1))
        {
            return false;
        }

        if (!pawn.CanReach(t, PathEndMode.ClosestTouch, Danger.Deadly))
        {
            JobFailReason.Is("NoPath".Translate());
            return false;
        }

        return true;
    }

    public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        if (!HasJobOnThing(pawn, t, forced))
        {
            return null;
        }

        Job job = JobMaker.MakeJob(ChickenBatteryCageDefOf.CBC_SpreadFertilizer, t);
        job.count = t.stackCount;
        return job;
    }
}
