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
 * A build from <c>./build.sh fast</c> replaces that wait. The stack then
 * dissolves after one day, or four times as fast while its cell is unroofed
 * and the map's weather is bad. Vanilla rot is patched out for this def in
 * that build, so a freezer does not hold the stack back.
 */
public class CompFertilizerDissolution : ThingComp
{
#if CBC_FAST_DECOMPOSITION
    /// Fair-weather-equivalent ticks accumulated while this stack waits to
    /// dissolve. Present only in a <c>./build.sh fast</c> build, so a normal
    /// build does not write it into saves.
    int fastTicks;
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

        bool badWeather = IsExposedToBadWeather();
        fastTicks += FertilizerFastDecompositionMath.EquivalentTicks(delta, badWeather);
        if (FertilizerFastDecompositionMath.ShouldDissolve(fastTicks))
        {
            TriggerDissolution(parent.stackCount);
        }
    }

    /// True when this pack's cell is open to a bad sky. A roof keeps the
    /// fair-weather rate even while it is raining on the map.
    bool IsExposedToBadWeather()
    {
        Map map = parent.MapHeld;
        WeatherDef weather = map?.weatherManager?.curWeather;
        if (map == null || weather == null || !weather.isBad)
        {
            return false;
        }

        return !map.roofGrid.Roofed(parent.PositionHeld);
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
        fastTicks = (int)Mathf.Lerp(fastTicks, other.fastTicks, proportionOther);
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

        bool badWeather = IsExposedToBadWeather();
        int remaining = FertilizerFastDecompositionMath.RealTicksRemaining(fastTicks, badWeather);
        string period = remaining.ToStringTicksToPeriod();
        return badWeather
            ? "ChickenBatteryCage.Fertilizer.DissolvesInBadWeather".Translate(period)
            : "ChickenBatteryCage.Fertilizer.DissolvesIn".Translate(period);
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
