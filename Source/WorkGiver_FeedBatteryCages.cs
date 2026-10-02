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
 * Hauling work that tops up the collective feed store of battery cages.
 *
 * A cage is only a work target while its pool is below the low-water mark, and
 * it stays a target until filled, so a settled flock costs no hauling labor.
 * Feed is found in ordinary colony stockpiles and delivered with the normal
 * carrying pipeline; no hopper is involved.
 */
public class WorkGiver_FeedBatteryCages : WorkGiver_Scanner
{
    public override PathEndMode PathEndMode => PathEndMode.ClosestTouch;

    public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
    {
        return pawn.Map.listerBuildings.AllBuildingsColonistOfClass<Building_ChickenBatteryCage>();
    }

    public override bool ShouldSkip(Pawn pawn, bool forced)
    {
        return !Building_ChickenBatteryCage.AnyCageNeedingFeed(pawn.Map);
    }

    public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        Building_ChickenBatteryCage cage = t as Building_ChickenBatteryCage;
        if (cage == null || !cage.NeedsFeeding)
        {
            return false;
        }

        if (cage.IsForbidden(pawn))
        {
            return false;
        }

        if (!pawn.CanReach(cage, PathEndMode.ClosestTouch, Danger.Deadly))
        {
            JobFailReason.Is("NoPath".Translate());
            return false;
        }

        // A target that cannot actually be fed must not be offered: reporting
        // a job here and then returning null from JobOnThing makes vanilla log
        // that the CanGiveJob and JobOnX methods are out of sync.
        if (!TryFindFeed(pawn, cage, out _))
        {
            JobFailReason.Is("ChickenBatteryCage.Feed.None".Translate());
            return false;
        }

        return true;
    }

    public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        Building_ChickenBatteryCage cage = t as Building_ChickenBatteryCage;
        if (cage == null || !cage.NeedsFeeding)
        {
            return null;
        }

        if (!TryFindFeed(pawn, cage, out Thing feed))
        {
            JobFailReason.Is("ChickenBatteryCage.Feed.None".Translate());
            return null;
        }

        // The hauling toils carry job.count units, but Job defaults count to
        // -1. Toils_Haul.ErrorCheckForCarry then logs "Invalid count" and
        // clamps the job to a single unit, so set the count to what the shared
        // pool can absorb, capped by what the stack holds.
        int units = CageFeedMath.ConsumeUnits(
            cage.ClusterNutritionSpace,
            CageFeed.NutritionPerUnit(feed),
            feed.stackCount);

        Job job = JobMaker.MakeJob(
            ChickenBatteryCageDefOf.CBC_FeedBatteryCage,
            feed,
            cage);
        job.count = units > 0 ? units : 1;
        return job;
    }

    /// Single source of truth for whether a hauler can actually feed this cage.
    /// Both HasJobOnThing and JobOnThing call it so the two cannot disagree.
    static bool TryFindFeed(Pawn pawn, Building_ChickenBatteryCage cage, out Thing feed)
    {
        feed = CageFeed.FindFeed(pawn, cage);
        return feed != null;
    }
}
