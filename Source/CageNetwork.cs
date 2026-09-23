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
using RimWorld;
using Verse;

namespace BetterRimworlds.ChickenBatteryCage;

/**
 * The flock-level view of the battery cages on a map.
 *
 * Players never micromanage individual cages. Cages that touch — sharing an
 * edge or a corner — form one cluster that behaves as a single giant cage:
 * they report one shared hen count, one shared picker window, one shared feed
 * pool, and one shared set of toggle gizmos. Clusters that do not touch are
 * independent, so a player can run several separate houses.
 *
 * Each cage still owns its own persisted records and feed store; this class
 * discovers the clusters, aggregates them on read, and routes the writes that
 * have to be shared (settling feed, siphoning hoppers, toggles, unload marks)
 * back across the members.
 */
public static class CageNetwork
{
    /// How long a discovered cluster layout is reused before being rescanned.
    /// Adjacency rarely changes, so this is coarse on purpose: the expensive
    /// flood fill runs a few times a second at most, not per gizmo.
    const int ClusterCacheTicks = 250;

    static readonly Dictionary<Map, Dictionary<Building_ChickenBatteryCage, List<Building_ChickenBatteryCage>>>
        clusterCache = new Dictionary<Map, Dictionary<Building_ChickenBatteryCage, List<Building_ChickenBatteryCage>>>();

    static readonly Dictionary<Map, int> clusterCacheTick = new Dictionary<Map, int>();

    static readonly List<Building_ChickenBatteryCage> emptyCluster = new List<Building_ChickenBatteryCage>();

    /// Every battery cage on the map, regardless of adjacency. Used by the
    /// developer tools and profiler, which deliberately span the whole map.
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

    /// True while the building is still a live member of a cluster.
    static bool Alive(Building_ChickenBatteryCage cage)
    {
        return cage != null && !cage.Destroyed;
    }

    /**
     * Every cage connected to <paramref name="reference"/> by touching,
     * including the reference itself. The result is a cached, read-only view;
     * callers must not mutate it. A destroyed or mapless cage resolves to a
     * singleton so a caller never receives an empty cluster for itself.
     */
    public static IReadOnlyList<Building_ChickenBatteryCage> Cluster(Building_ChickenBatteryCage reference)
    {
        if (reference == null)
        {
            return emptyCluster;
        }

        Map map = reference.Map;
        if (map == null)
        {
            return new List<Building_ChickenBatteryCage> { reference };
        }

        int now = GenTicks.TicksAbs;
        if (!clusterCacheTick.TryGetValue(map, out int cachedAt)
            || now < cachedAt
            || now - cachedAt >= ClusterCacheTicks)
        {
            RebuildClusters(map, now);
        }

        if (clusterCache.TryGetValue(map, out var byCage)
            && byCage.TryGetValue(reference, out var cluster))
        {
            return cluster;
        }

        return new List<Building_ChickenBatteryCage> { reference };
    }

    /**
     * Floods the map's cages into connected components once, so every later
     * lookup is a dictionary hit instead of a fresh adjacency scan.
     */
    static void RebuildClusters(Map map, int now)
    {
        PruneDeadMaps();

        var byCage = new Dictionary<Building_ChickenBatteryCage, List<Building_ChickenBatteryCage>>();
        var all = new List<Building_ChickenBatteryCage>(Cages(map));
        var assigned = new HashSet<Building_ChickenBatteryCage>();
        var queue = new Queue<Building_ChickenBatteryCage>();

        foreach (Building_ChickenBatteryCage start in all)
        {
            if (assigned.Contains(start))
            {
                continue;
            }

            var component = new List<Building_ChickenBatteryCage>();
            queue.Clear();
            queue.Enqueue(start);
            assigned.Add(start);

            while (queue.Count > 0)
            {
                Building_ChickenBatteryCage cage = queue.Dequeue();
                component.Add(cage);

                foreach (IntVec3 cell in GenAdj.CellsAdjacent8Way(cage))
                {
                    if (!cell.InBounds(map))
                    {
                        continue;
                    }

                    foreach (Thing thing in cell.GetThingList(map))
                    {
                        if (thing is Building_ChickenBatteryCage other
                            && !assigned.Contains(other))
                        {
                            assigned.Add(other);
                            queue.Enqueue(other);
                        }
                    }
                }
            }

            foreach (Building_ChickenBatteryCage cage in component)
            {
                byCage[cage] = component;
            }
        }

        clusterCache[map] = byCage;
        clusterCacheTick[map] = now;
    }

    /// Drops the cached cluster layout for a map, so a cage just built or
    /// removed is reflected at once instead of after the cache window.
    public static void Invalidate(Map map)
    {
        if (map == null)
        {
            return;
        }

        clusterCache.Remove(map);
        clusterCacheTick.Remove(map);
    }

    /// Drops cluster caches for maps that have gone away, so static state does
    /// not accumulate across saves and new games.
    static void PruneDeadMaps()
    {
        if (clusterCache.Count == 0)
        {
            return;
        }

        List<Map> stale = null;
        foreach (Map map in clusterCache.Keys)
        {
            if (!Find.Maps.Contains(map))
            {
                (stale ??= new List<Map>()).Add(map);
            }
        }

        if (stale == null)
        {
            return;
        }

        foreach (Map map in stale)
        {
            clusterCache.Remove(map);
            clusterCacheTick.Remove(map);
        }
    }

    // ---- Aggregates over one cluster -------------------------------------

    public static int TotalCapacity(IReadOnlyList<Building_ChickenBatteryCage> cluster)
    {
        int total = 0;
        foreach (Building_ChickenBatteryCage cage in cluster)
        {
            if (Alive(cage))
            {
                total += Building_ChickenBatteryCage.ChickenCapacity;
            }
        }
        return total;
    }

    public static int ChickenCount(IReadOnlyList<Building_ChickenBatteryCage> cluster)
    {
        int total = 0;
        foreach (Building_ChickenBatteryCage cage in cluster)
        {
            if (Alive(cage))
            {
                total += cage.ChickenCount;
            }
        }
        return total;
    }

    public static int AdultHenCount(IReadOnlyList<Building_ChickenBatteryCage> cluster)
    {
        int total = 0;
        foreach (Building_ChickenBatteryCage cage in cluster)
        {
            if (Alive(cage))
            {
                total += cage.AdultHenCount;
            }
        }
        return total;
    }

    public static int JuvenileCount(IReadOnlyList<Building_ChickenBatteryCage> cluster)
    {
        int total = 0;
        foreach (Building_ChickenBatteryCage cage in cluster)
        {
            if (Alive(cage))
            {
                total += cage.JuvenileCount;
            }
        }
        return total;
    }

    public static int PendingUnloadCount(IReadOnlyList<Building_ChickenBatteryCage> cluster)
    {
        int total = 0;
        foreach (Building_ChickenBatteryCage cage in cluster)
        {
            if (Alive(cage))
            {
                total += cage.PendingUnloadCount;
            }
        }
        return total;
    }

    public static bool HasPendingUnload(IReadOnlyList<Building_ChickenBatteryCage> cluster)
    {
        return PendingUnloadCount(cluster) > 0;
    }

    // ---- The shared feed pool --------------------------------------------

    /// Feed held across the whole cluster, treated as one pool.
    public static float StoredNutrition(IReadOnlyList<Building_ChickenBatteryCage> cluster)
    {
        float total = 0f;
        foreach (Building_ChickenBatteryCage cage in cluster)
        {
            if (Alive(cage))
            {
                total += cage.StoredNutrition;
            }
        }
        return total;
    }

    /// Combined storage of every member, so a cluster buffers more feed than
    /// any single cage without changing the per-cage capacity rule.
    public static float NutritionCapacity(IReadOnlyList<Building_ChickenBatteryCage> cluster)
    {
        float total = 0f;
        foreach (Building_ChickenBatteryCage cage in cluster)
        {
            if (Alive(cage))
            {
                total += cage.NutritionCapacity;
            }
        }
        return total;
    }

    public static float NutritionSpace(IReadOnlyList<Building_ChickenBatteryCage> cluster)
    {
        float stored = StoredNutrition(cluster);
        float capacity = NutritionCapacity(cluster);
        return stored >= capacity ? 0f : capacity - stored;
    }

    /// Longest any member has gone without feed. Members settle together, so
    /// this is normally the single shared starvation clock.
    public static float StarvingDays(IReadOnlyList<Building_ChickenBatteryCage> cluster)
    {
        int worst = 0;
        foreach (Building_ChickenBatteryCage cage in cluster)
        {
            if (Alive(cage) && cage.StarvingTicks > worst)
            {
                worst = cage.StarvingTicks;
            }
        }
        return worst / (float)CagedChickenMath.TicksPerDay;
    }

    public static CageNutritionState NutritionState(IReadOnlyList<Building_ChickenBatteryCage> cluster)
    {
        return CageNutritionMath.Classify(
            StoredNutrition(cluster),
            ChickenCount(cluster),
            CageNutritionMath.DefaultNutritionPerChickenPerDay,
            StarvingDays(cluster));
    }

    /**
     * Settles the whole cluster's feed once, however many members ask. The
     * elapsed time is measured from the most recent settle by any member, so
     * the first member to tick does the work and the rest see no time pass.
     * Only the pool-level starving clock and per-member stores are written.
     */
    public static void SettleCluster(IReadOnlyList<Building_ChickenBatteryCage> cluster, int now)
    {
        int last = 0;
        foreach (Building_ChickenBatteryCage cage in cluster)
        {
            if (Alive(cage) && cage.NutritionSettledAtTick > last)
            {
                last = cage.NutritionSettledAtTick;
            }
        }

        if (last <= 0 || last > now)
        {
            SetSettledAt(cluster, now);
            return;
        }

        int elapsed = now - last;
        SetSettledAt(cluster, now);
        if (elapsed <= 0)
        {
            return;
        }

        float stored = StoredNutrition(cluster);
        int birds = ChickenCount(cluster);

        int starving = CageNutritionMath.StarvingTicksAfter(
            WorstStarvingTicks(cluster),
            stored,
            birds,
            CageNutritionMath.DefaultNutritionPerChickenPerDay,
            elapsed);

        float remaining = CageNutritionMath.RemainingAfter(
            stored,
            birds,
            CageNutritionMath.DefaultNutritionPerChickenPerDay,
            elapsed);

        WriteNutrition(cluster, remaining, starving);
    }

    static int WorstStarvingTicks(IReadOnlyList<Building_ChickenBatteryCage> cluster)
    {
        int worst = 0;
        foreach (Building_ChickenBatteryCage cage in cluster)
        {
            if (Alive(cage) && cage.StarvingTicks > worst)
            {
                worst = cage.StarvingTicks;
            }
        }
        return worst;
    }

    static void SetSettledAt(IReadOnlyList<Building_ChickenBatteryCage> cluster, int now)
    {
        foreach (Building_ChickenBatteryCage cage in cluster)
        {
            if (Alive(cage))
            {
                cage.NutritionSettledAtTick = now;
            }
        }
    }

    /**
     * Adds feed to the cluster's shared pool, refusing anything over the
     * combined capacity, and spreads the result across the members. Returns
     * how much was actually accepted.
     */
    public static float AddNutrition(IReadOnlyList<Building_ChickenBatteryCage> cluster, float amount)
    {
        if (amount <= 0f)
        {
            return 0f;
        }

        float space = NutritionSpace(cluster);
        if (space <= 0f)
        {
            return 0f;
        }

        float accepted = amount < space ? amount : space;
        WriteStored(cluster, StoredNutrition(cluster) + accepted);
        return accepted;
    }

    /// Spreads an aggregate store over the members' own stores, leaving each
    /// member's starvation clock untouched.
    static void WriteStored(IReadOnlyList<Building_ChickenBatteryCage> cluster, float totalStored)
    {
        int count = cluster.Count;
        if (count == 0)
        {
            return;
        }

        var capacities = new float[count];
        var result = new float[count];
        for (int i = 0; i < count; i++)
        {
            capacities[i] = Alive(cluster[i]) ? cluster[i].NutritionCapacity : 0f;
        }

        CageClusterMath.Distribute(totalStored, capacities, result);

        for (int i = 0; i < count; i++)
        {
            if (Alive(cluster[i]))
            {
                cluster[i].SetStoredNutrition(result[i]);
            }
        }
    }

    /// Spreads both the store and the shared starvation clock.
    static void WriteNutrition(IReadOnlyList<Building_ChickenBatteryCage> cluster, float totalStored, int starvingTicks)
    {
        WriteStored(cluster, totalStored);

        foreach (Building_ChickenBatteryCage cage in cluster)
        {
            if (Alive(cage))
            {
                cage.StarvingTicks = starvingTicks;
            }
        }
    }

    /// Every distinct hopper touching any cage in the cluster, so a hopper
    /// bolted to one member feeds the whole giant cage.
    public static IEnumerable<Building_Storage> ClusterHoppers(IReadOnlyList<Building_ChickenBatteryCage> cluster)
    {
        var seen = new HashSet<Building_Storage>();
        foreach (Building_ChickenBatteryCage cage in cluster)
        {
            if (!Alive(cage))
            {
                continue;
            }

            foreach (Building_Storage hopper in cage.AdjacentHoppers())
            {
                if (hopper != null && seen.Add(hopper))
                {
                    yield return hopper;
                }
            }
        }
    }

    // ---- Toggles, marks, and routing -------------------------------------

    /// True only when every cage in the cluster has its pen system on; the
    /// gizmo reads this so a partial cluster shows as off.
    public static bool PenSystemEnabled(IReadOnlyList<Building_ChickenBatteryCage> cluster)
    {
        bool any = false;
        foreach (Building_ChickenBatteryCage cage in cluster)
        {
            if (!Alive(cage))
            {
                continue;
            }

            any = true;
            if (!cage.PenSystemEnabled)
            {
                return false;
            }
        }
        return any;
    }

    public static void SetPenSystemEnabled(IReadOnlyList<Building_ChickenBatteryCage> cluster, bool enabled)
    {
        foreach (Building_ChickenBatteryCage cage in cluster)
        {
            if (Alive(cage))
            {
                cage.SetPenSystemEnabled(enabled);
            }
        }
    }

    public static void ClearPendingUnloads(IReadOnlyList<Building_ChickenBatteryCage> cluster)
    {
        foreach (Building_ChickenBatteryCage cage in cluster)
        {
            if (Alive(cage))
            {
                cage.ClearPendingUnloads();
            }
        }
    }

    /**
     * Marks one bird of the chosen kind for unloading. The mark is placed on
     * the cage that actually houses a matching bird — for Youngest/Oldest the
     * whole cluster is scanned so the true extreme of the flock is picked.
     * Returns false when no cage houses a match, so a mark is never queued
     * that would later be silently discarded.
     */
    public static bool RequestUnload(IReadOnlyList<Building_ChickenBatteryCage> cluster, ChickenReleaseFilter filter)
    {
        Building_ChickenBatteryCage target = FindCageWithRecord(cluster, filter);
        return target != null && target.RequestUnload(filter);
    }

    /// A cage can only hold as many marks as it has birds; routing past a
    /// saturated cage lets available room in another cage take the mark.
    static bool HasMarkRoom(Building_ChickenBatteryCage cage)
    {
        return cage.PendingUnloadCount < cage.ChickenCount;
    }

    static Building_ChickenBatteryCage FindCageWithRecord(
        IReadOnlyList<Building_ChickenBatteryCage> cluster,
        ChickenReleaseFilter filter)
    {
        switch (filter)
        {
            case ChickenReleaseFilter.Youngest:
            case ChickenReleaseFilter.Oldest:
                return ExtremeByAgeCage(cluster, filter == ChickenReleaseFilter.Youngest);

            case ChickenReleaseFilter.Random:
                return RandomCageWithRecord(cluster);

            default:
                foreach (Building_ChickenBatteryCage cage in cluster)
                {
                    if (Alive(cage) && HasMarkRoom(cage) && cage.FindRecordIndex(filter) >= 0)
                    {
                        return cage;
                    }
                }
                return null;
        }
    }

    static Building_ChickenBatteryCage ExtremeByAgeCage(
        IReadOnlyList<Building_ChickenBatteryCage> cluster,
        bool youngest)
    {
        int now = GenTicks.TicksAbs;
        Building_ChickenBatteryCage bestCage = null;
        long bestAge = 0;
        foreach (Building_ChickenBatteryCage cage in cluster)
        {
            if (!Alive(cage) || !HasMarkRoom(cage))
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
     * Samples uniformly across all eligible records in the cluster, not across
     * cages: picking a cage first would give a lone bird in one cage the same
     * chance of release as each bird in a crowded cage. Each cage is weighted
     * by its population instead.
     */
    static Building_ChickenBatteryCage RandomCageWithRecord(IReadOnlyList<Building_ChickenBatteryCage> cluster)
    {
        int totalBirds = 0;
        foreach (Building_ChickenBatteryCage cage in cluster)
        {
            if (Alive(cage) && HasMarkRoom(cage))
            {
                totalBirds += cage.ChickenCount;
            }
        }
        if (totalBirds == 0)
        {
            return null;
        }

        int pick = Rand.Range(0, totalBirds);
        foreach (Building_ChickenBatteryCage cage in cluster)
        {
            if (!Alive(cage) || !HasMarkRoom(cage))
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

    public static List<FloatMenuOption> BuildUnloadMenu(IReadOnlyList<Building_ChickenBatteryCage> cluster)
    {
        var options = new List<FloatMenuOption>();
        int total = ChickenCount(cluster);
        int pending = PendingUnloadCount(cluster);
        foreach (ChickenReleaseFilter filter in (ChickenReleaseFilter[])Enum.GetValues(typeof(ChickenReleaseFilter)))
        {
            ChickenReleaseFilter local = filter;
            bool available = FindCageWithRecord(cluster, local) != null && pending < total;
            FloatMenuOption option = new FloatMenuOption(
                ReleaseFilterLabel(local),
                available ? (Action)(() => RequestUnload(cluster, local)) : null);
            if (!available)
            {
                option.Disabled = true;
            }
            options.Add(option);
        }

        if (HasPendingUnload(cluster))
        {
            options.Add(new FloatMenuOption(
                "ChickenBatteryCage.Gizmo.CancelUnload".Translate(),
                delegate { ClearPendingUnloads(cluster); }));
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
