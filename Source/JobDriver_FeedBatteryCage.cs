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
using Verse;
using Verse.AI;

namespace BetterRimworlds.ChickenBatteryCage;

/**
 * Hauling job that carries an approved feed stack to a battery cage and pours
 * its nutrition into the collective store.
 *
 * The flock is never fed individually: the hauler tips the feed into the cage,
 * the pool absorbs what fits, and the rest is dropped on the floor. The food
 * itself is destroyed on delivery, so the cage needs no hopper and no top-up.
 */
public class JobDriver_FeedBatteryCage : JobDriver
{
    public const TargetIndex FeedInd = TargetIndex.A;

    public const TargetIndex CageInd = TargetIndex.B;

    Thing Feed => job.GetTarget(FeedInd).Thing;

    Building_ChickenBatteryCage Cage =>
        job.GetTarget(CageInd).Thing as Building_ChickenBatteryCage;

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        Thing feed = Feed;
        if (feed == null)
        {
            return false;
        }

        // Only the feed stack is reserved. The cage itself is deliberately not
        // reserved, so several haulers can pour into one cluster at once and
        // refill a low pool in a single burst instead of queueing up.
        return pawn.Reserve(feed, job, 1, -1, null, errorOnFailed);
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDestroyedOrNull(FeedInd);
        this.FailOnForbidden(FeedInd);
        this.FailOnDespawnedNullOrForbidden(CageInd);
        // Stop as soon as the pool is satisfied, so a burst of haulers does not
        // pile food onto a cage that another hauler has just filled.
        this.FailOn(() => Cage == null || Feed == null || !Cage.NeedsFeeding);

        yield return Toils_Goto.GotoThing(FeedInd, PathEndMode.ClosestTouch);
        yield return Toils_Haul.StartCarryThing(FeedInd);
        yield return Toils_Goto.GotoThing(CageInd, PathEndMode.ClosestTouch);

        Toil pour = ToilMaker.MakeToil("FeedBatteryCage");
        pour.initAction = delegate
        {
            Building_ChickenBatteryCage cage = Cage;
            Thing carried = pawn.carryTracker.CarriedThing;
            if (cage == null || carried == null)
            {
                return;
            }

            CageFeed.Feed(cage, carried);

            Thing remainder = pawn.carryTracker.CarriedThing;
            if (remainder != null && remainder.stackCount > 0)
            {
                pawn.carryTracker.TryDropCarriedThing(
                    pawn.Position,
                    ThingPlaceMode.Near,
                    out Thing _);
            }
        };
        pour.defaultCompleteMode = ToilCompleteMode.Instant;
        yield return pour;
    }
}
