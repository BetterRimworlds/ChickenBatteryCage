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
 * Animal-handling job that ropes a hen and leads her to a battery cage, the
 * same way vanilla handlers rope animals into a pen.
 *
 * The hen stays a spawned Pawn until the handler is waiting at the cage.
 * Delivery then serializes her biology and removes the Pawn. Interrupting the
 * job drops the rope: nothing is duplicated and nothing is deleted.
 */
public class JobDriver_RopeHenToBatteryCage : JobDriver_RopeToDestination
{
    public const TargetIndex DestMarkerInd = TargetIndex.C;

    private Building_ChickenBatteryCage Cage =>
        job.GetTarget(DestMarkerInd).Thing as Building_ChickenBatteryCage;

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        Building_ChickenBatteryCage cage = Cage;
        if (cage == null)
        {
            return false;
        }

        // Reserve the cage so only one handler services it at a time. maxPawns
        // is how many pawns may co-hold this reservation, not the cage's bird
        // capacity: a building's stack count is 1, so a larger value cannot
        // admit a second handler and makes the engine log an error
        // ("maxPawns > 1 and stackCount = All").
        if (!pawn.Reserve(cage, job, 1, -1, null, errorOnFailed))
        {
            return false;
        }

        return base.TryMakePreToilReservations(errorOnFailed);
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedNullOrForbidden(DestMarkerInd);
        this.FailOn(() => Cage == null || !Cage.PenSystemEnabled);
        this.FailOn(() => !CanLeadRopeesToDestination());

        foreach (Toil toil in base.MakeNewToils())
        {
            yield return toil;
        }
    }

    // A handler can cross fences that a led chicken cannot. Match vanilla pen
    // routing: start at the bird and apply her fence restrictions to the handler.
    public static bool CanLeadToCell(Pawn handler, Pawn animal, IntVec3 destination)
    {
        return animal != null && animal.Spawned && handler.Map == animal.Map
            && destination.IsValid
            && animal.Map.reachability.CanReach(animal.Position, destination,
                PathEndMode.OnCell, TraverseParms.For(handler).WithFenceblockedOf(animal));
    }

    private bool CanLeadRopeesToDestination()
    {
        IntVec3 destination = job.GetTarget(TargetIndex.B).Cell;
        foreach (Pawn animal in pawn.roping.Ropees)
        {
            if (!CanLeadToCell(pawn, animal, destination))
            {
                return false;
            }
        }
        return true;
    }

    protected override bool HasRopeeArrived(Pawn ropee, bool roperWaitingAtDest)
    {
        return roperWaitingAtDest;
    }

    protected override void ProcessArrivedRopee(Pawn ropee)
    {
        Building_ChickenBatteryCage cage = Cage;
        if (cage == null || ropee == null)
        {
            return;
        }

        cage.TryAcceptChicken(ropee);
    }

    protected override bool ShouldOpportunisticallyRopeAnimal(Pawn animal)
    {
        // Never take a bird from another handler, including one who has
        // reserved her but has not reached her yet. The base driver reserves
        // each additional animal after this selection succeeds.
        if (animal == null || animal.roping.RopedByPawn != null || !pawn.CanReserve(animal))
        {
            return false;
        }

        Building_ChickenBatteryCage cage = Cage;
        if (cage == null || !Building_ChickenBatteryCage.IsHen(animal) || animal.Faction != pawn.Faction)
        {
            return false;
        }

        if (cage.ChickenCount + pawn.roping.Ropees.Count >= Building_ChickenBatteryCage.ChickenCapacity)
        {
            return false;
        }

        return cage.CanAcceptChicken(animal)
            && CanLeadToCell(pawn, animal, job.GetTarget(TargetIndex.B).Cell);
    }
}
