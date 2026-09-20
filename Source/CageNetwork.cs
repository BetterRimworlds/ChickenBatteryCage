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

using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace BetterRimworlds.ChickenBatteryCage;

/**
 * The flock-level view of every battery cage on a map.
 *
 * Players never micromanage individual cages: all cages form one network
 * whose gizmos are identical on every member (so multi-select merges them
 * cleanly), report aggregate capacity, toggle the pen system network-wide,
 * and place unload marks against whichever cage actually houses a matching
 * bird. Individual cages still own their own records; this class only
 * aggregates and routes.
 */
public static class CageNetwork
{
    public static IEnumerable<Building_ChickenBatteryCage> Cages(Map map)
    {
        if (map == null)
        {
            yield break;
        }

        foreach (Building_ChickenBatteryCage cage in map.listerBuildings.AllBuildingsColonistOfClass<Building_ChickenBatteryCage>())
        {
            yield return cage;
        }
    }

    public static int TotalCapacity(Map map)
    {
        int total = 0;
        foreach (Building_ChickenBatteryCage cage in Cages(map))
        {
            total += Building_ChickenBatteryCage.ChickenCapacity;
        }
        return total;
    }

    public static int ChickenCount(Map map)
    {
        int total = 0;
        foreach (Building_ChickenBatteryCage cage in Cages(map))
        {
            total += cage.ChickenCount;
        }
        return total;
    }

    public static int AdultHenCount(Map map)
    {
        int total = 0;
        foreach (Building_ChickenBatteryCage cage in Cages(map))
        {
            total += cage.AdultHenCount;
        }
        return total;
    }

    public static int JuvenileCount(Map map)
    {
        int total = 0;
        foreach (Building_ChickenBatteryCage cage in Cages(map))
        {
            total += cage.JuvenileCount;
        }
        return total;
    }

    public static int PendingUnloadCount(Map map)
    {
        int total = 0;
        foreach (Building_ChickenBatteryCage cage in Cages(map))
        {
            total += cage.PendingUnloadCount;
        }
        return total;
    }

    public static bool HasPendingUnload(Map map)
    {
        return PendingUnloadCount(map) > 0;
    }

    /// True only when every cage has its pen system on; the gizmo reads this
    /// so a partial network shows as "off" and one click re-enables everything.
    public static bool PenSystemEnabled(Map map)
    {
        foreach (Building_ChickenBatteryCage cage in Cages(map))
        {
            if (!cage.PenSystemEnabled)
            {
                return false;
            }
        }
        return true;
    }

    public static void SetPenSystemEnabled(Map map, bool enabled)
    {
        foreach (Building_ChickenBatteryCage cage in Cages(map))
        {
            cage.SetPenSystemEnabled(enabled);
        }
    }

    public static void ClearPendingUnloads(Map map)
    {
        foreach (Building_ChickenBatteryCage cage in Cages(map))
        {
            cage.ClearPendingUnloads();
        }
    }

    /**
     * Marks one bird of the chosen kind for unloading. The mark is placed on
     * the cage that actually houses a matching bird — for Youngest/Oldest the
     * whole network is scanned so the true extreme of the flock is picked.
     * Returns false when no cage houses a match, so a mark is never queued
     * that would later be silently discarded.
     */
    public static bool RequestUnload(Map map, ChickenReleaseFilter filter)
    {
        Building_ChickenBatteryCage target = FindCageWithRecord(map, filter);
        return target != null && target.RequestUnload(filter);
    }

    /// A cage can only hold as many marks as it has birds; routing past a
    /// saturated cage lets available room in another cage take the mark.
    static bool HasMarkRoom(Building_ChickenBatteryCage cage)
    {
        return cage.PendingUnloadCount < cage.ChickenCount;
    }

    static Building_ChickenBatteryCage FindCageWithRecord(Map map, ChickenReleaseFilter filter)
    {
        switch (filter)
        {
            case ChickenReleaseFilter.Youngest:
            case ChickenReleaseFilter.Oldest:
                return ExtremeByAgeCage(map, filter == ChickenReleaseFilter.Youngest);

            case ChickenReleaseFilter.Random:
                return RandomCageWithRecord(map);

            default:
                foreach (Building_ChickenBatteryCage cage in Cages(map))
                {
                    if (HasMarkRoom(cage) && cage.FindRecordIndex(filter) >= 0)
                    {
                        return cage;
                    }
                }
                return null;
        }
    }

    static Building_ChickenBatteryCage ExtremeByAgeCage(Map map, bool youngest)
    {
        int now = GenTicks.TicksAbs;
        Building_ChickenBatteryCage bestCage = null;
        long bestAge = 0;
        foreach (Building_ChickenBatteryCage cage in Cages(map))
        {
            if (!HasMarkRoom(cage))
            {
                continue;
            }

            int index = cage.FindRecordIndex(youngest ? ChickenReleaseFilter.Youngest : ChickenReleaseFilter.Oldest);
            if (index < 0)
            {
                continue;
            }

            long age = cage.RecordAt(index).BiologicalAgeTicksAt(now);
            if (bestCage == null || (youngest ? age < bestAge : age > bestAge))
            {
                bestCage = cage;
                bestAge = age;
            }
        }
        return bestCage;
    }

    /**
     * Samples uniformly across all eligible records in the network, not across
     * cages: picking a cage first would give a lone bird in one cage the same
     * chance of release as each bird in a crowded cage. Each cage is weighted
     * by its population instead. Any cage that reaches this point houses at
     * least one bird, since HasMarkRoom excludes empty ones.
     */
    static Building_ChickenBatteryCage RandomCageWithRecord(Map map)
    {
        int totalBirds = 0;
        foreach (Building_ChickenBatteryCage cage in Cages(map))
        {
            if (HasMarkRoom(cage))
            {
                totalBirds += cage.ChickenCount;
            }
        }
        if (totalBirds == 0)
        {
            return null;
        }

        int pick = Rand.Range(0, totalBirds);
        foreach (Building_ChickenBatteryCage cage in Cages(map))
        {
            if (!HasMarkRoom(cage))
            {
                continue;
            }
            if (pick < cage.ChickenCount)
            {
                return cage;
            }
            pick -= cage.ChickenCount;
        }

        return null;
    }

    public static List<FloatMenuOption> BuildUnloadMenu(Map map)
    {
        var options = new List<FloatMenuOption>();
        int total = ChickenCount(map);
        int pending = PendingUnloadCount(map);
        foreach (ChickenReleaseFilter filter in (ChickenReleaseFilter[])Enum.GetValues(typeof(ChickenReleaseFilter)))
        {
            ChickenReleaseFilter local = filter;
            bool available = FindCageWithRecord(map, local) != null && pending < total;
            FloatMenuOption option = new FloatMenuOption(
                ReleaseFilterLabel(local),
                available ? (Action)(() => RequestUnload(map, local)) : null);
            if (!available)
            {
                option.Disabled = true;
            }
            options.Add(option);
        }

        if (HasPendingUnload(map))
        {
            options.Add(new FloatMenuOption(
                "ChickenBatteryCage.Gizmo.CancelUnload".Translate(),
                delegate { ClearPendingUnloads(map); }));
        }

        return options;
    }

    static string ReleaseFilterLabel(ChickenReleaseFilter filter)
    {
        switch (filter)
        {
            case ChickenReleaseFilter.Youngest:
                return "ChickenBatteryCage.Release.Youngest".Translate();
            case ChickenReleaseFilter.Oldest:
                return "ChickenBatteryCage.Release.Oldest".Translate();
            case ChickenReleaseFilter.Random:
                return "ChickenBatteryCage.Release.Random".Translate();
            case ChickenReleaseFilter.AdultHen:
                return "ChickenBatteryCage.Release.AdultHen".Translate();
            case ChickenReleaseFilter.Juvenile:
                return "ChickenBatteryCage.Release.Juvenile".Translate();
            default:
                return filter.ToString();
        }
    }
}