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

using RimWorld;
using UnityEngine;
using Verse;

namespace BetterRimworlds.ChickenBatteryCage;

/**
 * Slowly improves the ground under a Fertilizer Pack.
 *
 * The implementation mirrors vanilla CompDissolution's pattern — a per-stack
 * elapsed-tick counter, a frozen pack pausing, and one pack consumed each
 * interval — but deliberately drops every toxic behaviour: no pollution, no tox
 * gas, no wastepack infestation, no faction retaliation. A stack standing on
 * the ground releases one pack every ten days and improves six cells with it,
 * which is exactly the beneficial inverse of one wastepack polluting six.
 *
 * The timer runs on the rare tick rather than every tick: a pack can wait
 * patiently, so it does not need per-tick attention.
 */
public class CompFertilizerDissolution : ThingComp
{
    int dissolveTicks;

    CompProperties_FertilizerDissolution Props => (CompProperties_FertilizerDissolution)props;

    bool IsOutdoors
    {
        get
        {
            Map map = parent.MapHeld;
            if (map != null)
            {
                Room room = parent.Position.GetRoom(map);
                if (room != null && room.UsesOutdoorTemperature)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// A frozen pack is inert, exactly as a frozen wastepack is.
    bool IsFrozen => parent.AmbientTemperature <= 0f;

    bool IsBeingRainedOn
    {
        get
        {
            WeatherManager weather = parent.MapHeld?.weatherManager;
            return IsOutdoors && weather?.curWeather != null && weather.curWeather.rainRate > 0.1f;
        }
    }

    /// Ticks between one pack dissolving and the next in this stack.
    public int DissolutionIntervalTicks => CageWasteMath.DecompositionIntervalTicks(
        Props.dissolutionAfterDays,
        IsOutdoors,
        IsBeingRainedOn,
        Props.indoorsFactor,
        Props.rainFactor);

    public override void CompTickRare()
    {
        base.CompTickRare();

        if (parent == null || parent.Destroyed || parent.MapHeld == null || IsFrozen)
        {
            return;
        }

        dissolveTicks += GenTicks.TickRareInterval;
        int interval = DissolutionIntervalTicks;
        if (interval > 0 && dissolveTicks >= interval)
        {
            TriggerDissolution(1);
        }
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
            toDissolve * Props.cellsImprovedPerPack);

        parent.stackCount -= toDissolve;
        dissolveTicks = 0;

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

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref dissolveTicks, "dissolveTicks", 0);
    }

    public override string CompInspectStringExtra()
    {
        if (IsFrozen)
        {
            return "ChickenBatteryCage.Fertilizer.Frozen".Translate();
        }

        return "ChickenBatteryCage.Fertilizer.Dissolves".Translate(
            DissolutionIntervalTicks.ToStringTicksToPeriod());
    }
}
