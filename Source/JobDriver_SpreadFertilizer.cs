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
 * A grower walks to a fertilizer pack on natural ground and spreads one pack
 * into the soil immediately, instead of leaving the eight-day clock to do it.
 */
public class JobDriver_SpreadFertilizer : JobDriver
{
    public const TargetIndex PackInd = TargetIndex.A;

    /// Work at normal plant speed. A stack of five is a short task, not a day.
    public const int SpreadWorkTicks = 300;

    Thing Pack => job.GetTarget(PackInd).Thing;

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        return Pack != null && pawn.Reserve(Pack, job, 1, 1, null, errorOnFailed);
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDestroyedOrNull(PackInd);
        this.FailOnForbidden(PackInd);
        this.FailOn(() => Pack.TryGetComp<CompFertilizerDissolution>()?.CanBeSpread != true);

        yield return Toils_Goto.GotoThing(PackInd, PathEndMode.ClosestTouch);

        Toil spread = Toils_General.Wait(SpreadWorkTicks, PackInd)
            .WithProgressBarToilDelay(PackInd, SpreadWorkTicks);
        spread.tickAction = delegate
        {
            pawn.rotationTracker.FaceTarget(Pack);
            pawn.skills?.Learn(SkillDefOf.Plants, 0.1f);
        };
        yield return spread;

        Toil apply = ToilMaker.MakeToil("ApplyFertilizer");
        apply.initAction = delegate
        {
            CompFertilizerDissolution comp = Pack?.TryGetComp<CompFertilizerDissolution>();
            if (comp != null && comp.CanBeSpread)
            {
                comp.TriggerDissolution(1);
            }
        };
        apply.defaultCompleteMode = ToilCompleteMode.Instant;
        yield return apply;
    }
}
