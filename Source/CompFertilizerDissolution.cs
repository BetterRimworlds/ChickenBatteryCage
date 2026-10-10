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
using UnityEngine;
using Verse;

namespace BetterRimworlds.ChickenBatteryCage;

/**
 * Turns a rotted Fertilizer Pack into fertile ground.
 *
 * A pack now spoils exactly like food. It carries vanilla CompRottable, so its
 * rot progress advances with temperature the same way a raw corn cob's does:
 * full speed at ten degrees and above, proportionally slower down to freezing,
 * and stopped below zero. Anything that preserves food — a freezer, a cold snap
 * — preserves fertilizer too, and the usual "Fresh / Rotting" and refrigerated
 * or frozen hints come from vanilla.
 *
 * This comp supplies the only fertilizer-specific behaviour. Vanilla CompRottable
 * is not allowed to remove the stack (the def leaves rotDestroys off and
 * rotDamagePerDay at zero), because the ground must be improved first. So when
 * the stack's rot stage leaves Fresh, the whole stack dissolves at once: every
 * pack in it improves its cells, and the stack is removed. This mirrors raw
 * corn, which is likewise destroyed all at once the moment it begins to rot,
 * except that this pack leaves fertile ground behind.
 */
public class CompFertilizerDissolution : ThingComp
{
    CompProperties_FertilizerDissolution Props => (CompProperties_FertilizerDissolution)props;

    /// Vanilla rot progress for this stack, or null if the def is misconfigured.
    CompRottable Rottable => parent.GetComp<CompRottable>();

    public override void CompTickRare()
    {
        base.CompTickRare();
        TryDissolveRottenStack();
    }

    public override void CompTickInterval(int delta)
    {
        base.CompTickInterval(delta);
        TryDissolveRottenStack();
    }

    /// Once the stack has begun to rot, dissolve all of it at once.
    void TryDissolveRottenStack()
    {
        if (parent == null || parent.Destroyed || parent.MapHeld == null)
        {
            return;
        }

        CompRottable rottable = Rottable;
        if (rottable == null || rottable.Stage == RotStage.Fresh)
        {
            return;
        }

        TriggerDissolution(parent.stackCount);
    }

    /// Dissolves up to <paramref name="amount"/> packs and improves the ground
    /// for each, returning how many cells were improved. Also used when the
    /// pack is destroyed by force, so a smashed pack still fertilizes the
    /// ground rather than vanishing.
    public int TriggerDissolution(int amount)
    {
        if (parent == null || parent.Destroyed || parent.MapHeld == null)
        {
            return 0;
        }

        int toDissolve = Mathf.Min(amount, parent.stackCount);
        if (toDissolve <= 0)
        {
            return 0;
        }

        int improved = FertilizerSpread.GrowAt(
            parent.PositionHeld,
            parent.MapHeld,
            CageWasteMath.CellsImprovedByPacks(toDissolve));

        parent.stackCount -= toDissolve;

        if (parent.stackCount <= 0 && !parent.Destroyed)
        {
            parent.Destroy();
        }

        return improved;
    }

    public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
    {
        base.PostPreApplyDamage(ref dinfo, out absorbed);

        if (absorbed || parent == null || parent.Destroyed)
        {
            return;
        }

        if (dinfo.Def != null && dinfo.Def.harmsHealth && dinfo.Amount >= parent.HitPoints)
        {
            TriggerDissolution(parent.stackCount);
        }
    }

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        if (!DebugSettings.ShowDevGizmos)
        {
            yield break;
        }

        yield return new Command_Action
        {
            defaultLabel = "DEV: Fertilizer dissolve stack now",
            action = delegate { TriggerDissolution(parent.stackCount); },
        };
    }
}
