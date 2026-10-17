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
 * Animal-handling work that unloads a marked chicken from a battery cage.
 *
 * The player only marks birds for unloading; a handler walks to the cage and
 * resolves the front of the unload queue, which materializes the bird on the
 * map. An interrupted job leaves the mark in place, so nothing is lost.
 */
public class JobDriver_UnloadBatteryCage : JobDriver
{
    public const TargetIndex CageInd = TargetIndex.A;

    /// The network member the handler walks to. It may differ from
    /// <see cref="Cage"/> when the cage holding the mark is sealed inside a
    /// packed network and a neighbouring cage is the only reachable side.
    public const TargetIndex ServiceCageInd = TargetIndex.B;

    Building_ChickenBatteryCage Cage =>
        job.GetTarget(CageInd).Thing as Building_ChickenBatteryCage;

    /// Falls back to <see cref="Cage"/> for jobs saved before the service cage
    /// existed, so an in-progress unload still completes.
    Building_ChickenBatteryCage ServiceCage =>
        job.GetTarget(ServiceCageInd).Thing as Building_ChickenBatteryCage ?? Cage;

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        Building_ChickenBatteryCage cage = Cage;
        Building_ChickenBatteryCage service = ServiceCage;
        if (cage == null || service == null)
        {
            return false;
        }

        if (!pawn.Reserve(cage, job, 1, -1, null, errorOnFailed))
        {
            return false;
        }

        if (service != cage && !pawn.Reserve(service, job, 1, -1, null, errorOnFailed))
        {
            return false;
        }

        return true;
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedNullOrForbidden(CageInd);
        this.FailOn(() => Cage == null || !Cage.HasPendingUnload);
        this.FailOn(() => ServiceCage == null || !ServiceCage.Spawned || ServiceCage.IsForbidden(pawn));

        TargetIndex gotoInd = job.GetTarget(ServiceCageInd).HasThing ? ServiceCageInd : CageInd;
        yield return Toils_Goto.GotoThing(gotoInd, PathEndMode.Touch);
        yield return Toils_General.Wait(180);

        Toil unload = ToilMaker.MakeToil("UnloadChickenFromBatteryCage");
        unload.initAction = delegate
        {
            Building_ChickenBatteryCage cage = Cage;
            if (cage != null)
            {
                cage.TryUnloadNext(ServiceCage);
            }
        };
        unload.defaultCompleteMode = ToilCompleteMode.Instant;
        yield return unload;
    }
}
