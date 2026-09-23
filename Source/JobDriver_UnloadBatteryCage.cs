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

    Building_ChickenBatteryCage Cage =>
        job.GetTarget(CageInd).Thing as Building_ChickenBatteryCage;

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        Building_ChickenBatteryCage cage = Cage;
        if (cage == null)
        {
            return false;
        }

        return pawn.Reserve(cage, job, 1, -1, null, errorOnFailed);
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedNullOrForbidden(CageInd);
        this.FailOn(() => Cage == null || !Cage.HasPendingUnload);

        yield return Toils_Goto.GotoThing(CageInd, PathEndMode.Touch);
        yield return Toils_General.Wait(180);

        Toil unload = ToilMaker.MakeToil("UnloadChickenFromBatteryCage");
        unload.initAction = delegate
        {
            Building_ChickenBatteryCage cage = Cage;
            if (cage != null)
            {
                cage.TryUnloadNext();
            }
        };
        unload.defaultCompleteMode = ToilCompleteMode.Instant;
        yield return unload;
    }
}
