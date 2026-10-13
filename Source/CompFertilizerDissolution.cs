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
 * Dissolves a Fertilizer Pack the way a toxic wastepack dissolves, and leaves
 * fertile ground instead of pollution.
 *
 * Dried fertilizer does not dissolve indoors, so the clock runs only in a room
 * that uses the outdoor temperature, and only on natural ground. One pack
 * leaves the stack every eight days, or every four days in rain or snow. A
 * constructed floor or a shelf never starts it. An underground base is indoors,
 * so a pack there waits until a grower spreads it. At freezing or below the
 * clock also waits. There is no food-rot timer. Hit-point deterioration is the
 * wastepack's own rate, and any damage that destroys the stack — wear, fire, a
 * smash — improves the ground for every pack still in it, then lets the damage
 * destroy the thing.
 *
 * A <c>./build.sh fast</c> build keeps those factors and shortens the outdoor
 * base from eight days to one.
 */
public class CompFertilizerDissolution : ThingComp
{
    /// Ticks accumulated toward the next single pack. Reset after each one.
    int dissolveTicks;

    CompProperties_FertilizerDissolution Props => (CompProperties_FertilizerDissolution)props;

    float Days
    {
        get
        {
#if CBC_FAST_DECOMPOSITION
            return FertilizerDissolutionMath.FastDays;
#else
            return Props.dissolutionAfterDays;
#endif
        }
    }

    /// True while the pack's room uses the outdoor temperature.
    public bool IsOutdoors
    {
        get
        {
            Map map = parent.MapHeld;
            if (map == null)
            {
                return false;
            }

            Room room = parent.PositionHeld.GetRoom(map);
            return room != null && room.UsesOutdoorTemperature;
        }
    }

    bool IsFrozen => parent.AmbientTemperature <= 0f;

    /// True when a grower can spread this pack, and when the passive clock may run.
    public bool CanBeSpread => SitsOnWorkingGround;

    /// True when this pack is on ground a patch can grow from, and not on a shelf.
    bool SitsOnWorkingGround
    {
        get
        {
            if (KeptOnShelf())
            {
                return false;
            }

            Map map = parent.MapHeld;
            if (map == null)
            {
                return false;
            }

            TerrainDef terrain = parent.PositionHeld.GetTerrain(map);
            return terrain != null && FertilizerTerrainRules.AnchorsSpread(terrain.defName);
        }
    }

    /// A shelf or similar building holds the pack in storage even over cave stone.
    bool KeptOnShelf()
    {
        IThingHolder holder = parent.ParentHolder;
        for (int i = 0; i < 8 && holder != null; i++)
        {
            if (holder is Thing thing && thing.def?.building?.preventDeteriorationOnTop == true)
            {
                return true;
            }

            holder = holder.ParentHolder;
        }

        Map map = parent.Map;
        if (map == null || !parent.Spawned)
        {
            return false;
        }

        Building edifice = parent.Position.GetEdifice(map);
        return edifice != null
            && edifice.def.building != null
            && edifice.def.building.preventDeteriorationOnTop;
    }

    /// Rain or snow on an outdoor pack. A roof does not matter; the room does.
    bool IsBeingRainedOn
    {
        get
        {
            if (!IsOutdoors)
            {
                return false;
            }

            WeatherDef weather = parent.MapHeld?.weatherManager?.curWeather;
            return weather != null && FertilizerDissolutionMath.CountsAsRain(weather.rainRate);
        }
    }

    int IntervalTicks => FertilizerDissolutionMath.IntervalTicks(
        Days,
        IsBeingRainedOn,
        Props.dissolutionFactorRain);

    public override void CompTick()
    {
        base.CompTick();

        if (parent == null || parent.Destroyed || parent.MapHeld == null
            || IsFrozen || !IsOutdoors || !SitsOnWorkingGround)
        {
            return;
        }

        dissolveTicks++;
        if (dissolveTicks >= IntervalTicks)
        {
            TriggerDissolution(1);
        }
    }

    /// Improves the ground for up to <paramref name="amount"/> packs and
    /// removes them. Also used by dev tools. Returns how many cells were struck.
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

        int improved = ImproveGround(toDissolve);
        parent.stackCount -= toDissolve;
        dissolveTicks = 0;

        if (parent.stackCount <= 0 && !parent.Destroyed)
        {
            parent.Destroy();
        }

        return improved;
    }

    /// Strikes ground for packs that are about to be destroyed by damage.
    /// The stack is left in place so the damage itself can remove it.
    int ImproveGround(int packs)
    {
        if (packs <= 0 || parent?.MapHeld == null)
        {
            return 0;
        }

        return FertilizerSpread.GrowAt(
            parent.PositionHeld,
            parent.MapHeld,
            CageWasteMath.CellsImprovedByPacks(packs));
    }

    public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
    {
        base.PostPreApplyDamage(ref dinfo, out absorbed);

        if (absorbed || parent == null || parent.Destroyed || parent.MapHeld == null)
        {
            return;
        }

        if (dinfo.Def != null && dinfo.Def.harmsHealth && dinfo.Amount >= parent.HitPoints)
        {
            ImproveGround(parent.stackCount);
        }
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref dissolveTicks, "dissolveTicks", 0);
    }

    public override string CompInspectStringExtra()
    {
        if (parent == null || parent.Destroyed)
        {
            return null;
        }

        if (!IsOutdoors)
        {
            return "ChickenBatteryCage.Fertilizer.Indoors".Translate();
        }

        if (!SitsOnWorkingGround)
        {
            return "ChickenBatteryCage.Fertilizer.Stored".Translate();
        }

        if (IsFrozen)
        {
            return "ChickenBatteryCage.Fertilizer.Frozen".Translate();
        }

        string line = "ChickenBatteryCage.Fertilizer.DissolvesEvery".Translate(
            IntervalTicks.ToStringTicksToPeriod());

        var reasons = new List<string>();
        if (IsBeingRainedOn)
        {
            reasons.Add(
                "ChickenBatteryCage.Fertilizer.Rain".Translate()
                + " x" + Props.dissolutionFactorRain.ToStringPercent());
        }

        if (reasons.Count == 0)
        {
            return line;
        }

        return line + " (" + reasons.ToLineList().CapitalizeFirst() + ")";
    }

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        if (!DebugSettings.ShowDevGizmos)
        {
            yield break;
        }

        yield return new Command_Action
        {
            defaultLabel = "DEV: Fertilizer dissolve one pack",
            action = delegate { TriggerDissolution(1); },
        };

        yield return new Command_Action
        {
            defaultLabel = "DEV: Fertilizer dissolve stack now",
            action = delegate { TriggerDissolution(parent.stackCount); },
        };
    }
}
