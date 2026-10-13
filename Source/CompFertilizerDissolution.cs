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
 *
 * Outdoors the pack also deteriorates like raw food: about ten days of hit
 * points in fair weather, and five times that fast while rain or snow falls
 * on an unroofed cell, which is vanilla's own rain factor. Wearing away
 * dissolves the stack the same way rot does.
 *
 * A build from <c>./build.sh fast</c> shortens the rot wait to one warm day, and
 * rain on an unroofed cell dissolves it four times as fast, in six hours.
 * Cold still slows the stack, and freezing still stops it, exactly as it does
 * for food. Vanilla rot is
 * patched out for this def in that build so the ten-day timer does not also
 * run; the fast clock applies the same temperature curve itself.
 */
public class CompFertilizerDissolution : ThingComp
{
#if CBC_FAST_DECOMPOSITION
    /// Warm-fair-equivalent ticks accumulated while this stack waits to
    /// dissolve. Present only in a <c>./build.sh fast</c> build, so a normal
    /// build does not write it into saves.
    float fastTicks;
#endif

    CompProperties_FertilizerDissolution Props => (CompProperties_FertilizerDissolution)props;

    /// Vanilla rot progress for this stack, or null if the def is misconfigured.
    CompRottable Rottable => parent.GetComp<CompRottable>();

    public override void CompTickRare()
    {
        base.CompTickRare();
#if CBC_FAST_DECOMPOSITION
        TickFastDecomposition(GenTicks.TickRareInterval);
#endif
        TryDissolveRottenStack();
    }

    public override void CompTickInterval(int delta)
    {
        base.CompTickInterval(delta);
#if CBC_FAST_DECOMPOSITION
        TickFastDecomposition(delta);
#endif
        TryDissolveRottenStack();
    }

#if CBC_FAST_DECOMPOSITION
    /// Advances the one-day clock. Compiled only into a <c>./build.sh fast</c>
    /// build; a normal build keeps the ten-day rot timer and nothing else.
    void TickFastDecomposition(int delta)
    {
        if (parent == null || parent.Destroyed || parent.MapHeld == null)
        {
            return;
        }

        bool raining = IsRainedOn();
        float rotRate = FertilizerFastDecompositionMath.TemperatureRotRate(parent.AmbientTemperature);
        fastTicks += FertilizerFastDecompositionMath.EquivalentTicks(delta, raining, rotRate);
        if (FertilizerFastDecompositionMath.ShouldDissolve(fastTicks))
        {
            TriggerDissolution(parent.stackCount);
        }
    }

    /// True when rain is falling on this pack's cell. Vanilla's plain Rain
    /// def has a rain rate of 1 and leaves isBad false, so the bad-weather
    /// flag does not see it. A roof keeps the one-day rate.
    bool IsRainedOn()
    {
        Map map = parent.MapHeld;
        if (map == null || map.roofGrid.Roofed(parent.PositionHeld))
        {
            return false;
        }

        return FertilizerFastDecompositionMath.CountsAsRain(map.weatherManager.RainRate);
    }
#endif

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

#if CBC_FAST_DECOMPOSITION
    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref fastTicks, "fastTicks", 0);
    }

    public override void PreAbsorbStack(Thing otherStack, int count)
    {
        base.PreAbsorbStack(otherStack, count);

        if (parent == null || count <= 0)
        {
            return;
        }

        CompFertilizerDissolution other = otherStack.TryGetComp<CompFertilizerDissolution>();
        if (other == null)
        {
            return;
        }

        float proportionOther = count / (float)(parent.stackCount + count);
        fastTicks = Mathf.Lerp(fastTicks, other.fastTicks, proportionOther);
    }

    public override void PostSplitOff(Thing piece)
    {
        base.PostSplitOff(piece);

        CompFertilizerDissolution other = piece.TryGetComp<CompFertilizerDissolution>();
        if (other != null)
        {
            other.fastTicks = fastTicks;
        }
    }

    public override string CompInspectStringExtra()
    {
        if (parent == null || parent.Destroyed || parent.MapHeld == null)
        {
            return null;
        }

        float rotRate = FertilizerFastDecompositionMath.TemperatureRotRate(parent.AmbientTemperature);
        if (rotRate <= 0f)
        {
            return "ChickenBatteryCage.Fertilizer.Frozen".Translate();
        }

        bool raining = IsRainedOn();
        bool cold = rotRate < 0.999f;
        int remaining = FertilizerFastDecompositionMath.RealTicksRemaining(fastTicks, raining, rotRate);
        string period = remaining.ToStringTicksToPeriod();
        if (raining && cold)
        {
            return "ChickenBatteryCage.Fertilizer.DissolvesInRainCold".Translate(period);
        }

        if (raining)
        {
            return "ChickenBatteryCage.Fertilizer.DissolvesInRain".Translate(period);
        }

        if (cold)
        {
            return "ChickenBatteryCage.Fertilizer.DissolvesInCold".Translate(period);
        }

        return "ChickenBatteryCage.Fertilizer.DissolvesIn".Translate(period);
    }
#endif

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
