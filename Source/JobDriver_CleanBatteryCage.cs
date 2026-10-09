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
 * Servicing job that empties a battery cage's accumulated manure into Fertilizer
 * Packs.
 *
 * A single toil performs the whole service: the cleaner walks to the cage, the
 * pooled waste is reduced by every whole pack it holds, and the packs drop on
 * the ground as ordinary haulable stacks. No per-hen work happens and the flock
 * stays serialized throughout.
 */
public class JobDriver_CleanBatteryCage : JobDriver
{
    public const TargetIndex CageInd = TargetIndex.A;

    Building_ChickenBatteryCage Cage =>
        job.GetTarget(CageInd).Thing as Building_ChickenBatteryCage;

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        // Reserve the cage so two cleaners never service the same cage at once.
        return Cage != null && pawn.Reserve(Cage, job, 1, -1, null, errorOnFailed);
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedNullOrForbidden(CageInd);

        yield return Toils_Goto.GotoThing(CageInd, PathEndMode.ClosestTouch);

        Toil clean = ToilMaker.MakeToil("CleanBatteryCage");
        clean.initAction = delegate
        {
            Building_ChickenBatteryCage cage = Cage;
            if (cage != null)
            {
                CageCleaning.Clean(cage);
            }
        };
        clean.defaultCompleteMode = ToilCompleteMode.Instant;
        yield return clean;
    }
}
