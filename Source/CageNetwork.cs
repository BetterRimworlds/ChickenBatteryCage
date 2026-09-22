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
 * have to be shared (settling feed, refill requests, toggles, unload marks)
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
            || CagedChickenMath.IsFutureTick(cachedAt, now)
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

    public static float LayingRatePerDay(IReadOnlyList<Building_ChickenBatteryCage> cluster)
    {
        float total = 0f;
        foreach (Building_ChickenBatteryCage cage in cluster)
        {
            if (Alive(cage))
            {
                total += cage.EggLayingRatePerDay;
            }
        }
        return total;
    }

    /// The number of eggs in one released stack: a whole day of the network's
    /// combined laying, floored to whole eggs and never below
    /// <paramref name="minimum"/>. Every member of a cluster reports the same
    /// size because the box they fill is shared.
    public static int EggStackSize(IReadOnlyList<Building_ChickenBatteryCage> cluster, int minimum)
    {
        return CageEggMath.EggsPerStack(LayingRatePerDay(cluster), minimum);
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

    /// Summed daily demand of every live member of the cluster, since the feed
    /// store — and therefore the fed interval — is shared.
    public static float DemandPerDay(IReadOnlyList<Building_ChickenBatteryCage> cluster)
    {
        float total = 0f;
        foreach (Building_ChickenBatteryCage cage in cluster)
        {
            if (Alive(cage))
            {
                total += cage.NutritionDemandPerDay;
            }
        }
        return total;
    }

    /**
     * How many ticks of an elapsed interval the flock was actually fed.
     *
     * Laying is gated by access to feed, and the store is shared across the
     * cluster, so an interval only earns eggs up to the point the pooled store
     * covers the whole flock's demand. A store that runs dry partway through
     * the interval stops earning there, instead of paying for the whole span.
     * Returns the whole interval when feed is ample (or the flock has no
     * demand).
     *
     * The result is a cluster aggregate, identical for every member. A caller
     * crediting a whole cluster for one interval must resolve it once here
     * rather than rescanning the cluster per cage.
     */
    public static int FedTicksWithin(IReadOnlyList<Building_ChickenBatteryCage> cluster, int elapsed)
    {
        float demandPerDay = DemandPerDay(cluster);
        if (demandPerDay <= 0f)
        {
            return elapsed;
        }

        float stored = StoredNutrition(cluster);
        if (stored <= 0f)
        {
            return 0;
        }

        double fedDays = stored / demandPerDay;
        long covered = (long)(fedDays * CagedChickenMath.TicksPerDay);
        if (covered >= elapsed)
        {
            return elapsed;
        }

        return (int)covered;
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

    public static int EggsHeld(IReadOnlyList<Building_ChickenBatteryCage> cluster)
    {
        int total = 0;
        foreach (Building_ChickenBatteryCage cage in cluster)
        {
            if (Alive(cage))
            {
                total += cage.EggsHeld;
            }
        }
        return total;
    }

    public static float EggProgress(IReadOnlyList<Building_ChickenBatteryCage> cluster)
    {
        float total = 0f;
        foreach (Building_ChickenBatteryCage cage in cluster)
        {
            if (Alive(cage))
            {
                total += cage.EggProgress;
            }
        }
        return total;
    }

    /**
     * Removes <paramref name="amount"/> of eggs from the cluster's shared box
     * after a stack has been placed. Survivors scale their own fractional
     * progress down in proportion, so the remainder is spread across the
     * network rather than stranded on the cage that happened to place the
     * stack. The amount is clamped so the box never goes negative.
     */
    public static void WithdrawEggs(IReadOnlyList<Building_ChickenBatteryCage> cluster, float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        int count = cluster.Count;
        var stored = new float[count];
        var result = new float[count];
        for (int i = 0; i < count; i++)
        {
            stored[i] = Alive(cluster[i]) ? cluster[i].EggProgress : 0f;
        }

        float remaining = EggProgress(cluster) - amount;
        if (remaining < 0f)
        {
            remaining = 0f;
        }

        CageClusterMath.ScaleToTotal(remaining, stored, result);

        for (int i = 0; i < count; i++)
        {
            if (Alive(cluster[i]))
            {
                cluster[i].SetEggProgress(result[i]);
            }
        }
    }

    /**
     * Settles the whole cluster's feed once, however many members ask. The
     * elapsed time is measured from the most recent settle by any member, so
     * the first member to tick does the work and the rest see no time pass.
     * Accrues each bird's mortality exposure before writing the shared
     * starving clock and per-member stores.
     * Every live member contributes its birds' demand; the cage runs whether
     * or not it is roofed.
     */
    public static void SettleCluster(IReadOnlyList<Building_ChickenBatteryCage> cluster, int now)
    {
        int last = 0;
        bool anySettled = false;
        foreach (Building_ChickenBatteryCage cage in cluster)
        {
            if (!Alive(cage))
            {
                continue;
            }

            // Compare with a wrapping subtraction so the most recent settle is
            // found correctly after the game-tick counter wraps negative.
            int settledAt = cage.NutritionSettledAtTick;
            if (!anySettled || CagedChickenMath.IsAfter(settledAt, last))
            {
                last = settledAt;
                anySettled = true;
            }
        }

        if (!anySettled || last == 0 || CagedChickenMath.IsImpossibleTick(last, now))
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
        // A negative starting duration represents the feed still available.
        // Once that duration reaches zero, the empty-store clock begins.
        double starvingDaysAtStart = CageMortalityMath.StarvingDaysAtStart(
            WorstStarvingTicks(cluster), stored, birds,
            CageNutritionMath.DefaultNutritionPerChickenPerDay);

        foreach (Building_ChickenBatteryCage cage in cluster)
        {
            if (Alive(cage))
            {
                cage.AccumulateMortality(last, now, starvingDaysAtStart);
            }
        }

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

        // Preserve mortality exposure before incoming feed resets starvation.
        SettleCluster(cluster, GenTicks.TicksAbs);

        float space = NutritionSpace(cluster);
        if (space <= 0f)
        {
            return 0f;
        }

        float accepted = amount < space ? amount : space;
        WriteStored(cluster, StoredNutrition(cluster) + accepted);
        ClearStarving(cluster);
        return accepted;
    }

    /// Clears the cluster's refill request so haulers stop topping up a pool
    /// that is effectively full. The low-water mark re-arms it on the next dip.
    public static void ClearFeedRequest(IReadOnlyList<Building_ChickenBatteryCage> cluster)
    {
        foreach (Building_ChickenBatteryCage cage in cluster)
        {
            if (Alive(cage))
            {
                cage.FeedTopUpRequested = false;
            }
        }
    }

    /// Feed has arrived, so the flock is no longer starving. Restart the shared
    /// clock instead of carrying the old deficit into the next settlement.
    static void ClearStarving(IReadOnlyList<Building_ChickenBatteryCage> cluster)
    {
        foreach (Building_ChickenBatteryCage cage in cluster)
        {
            if (Alive(cage))
            {
                cage.StarvingTicks = 0;
            }
        }
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

        // Refill hysteresis: once the pool dips below the low-water mark the
        // cluster keeps asking for feed until it is full, so haulers make one
        // burst of deliveries instead of trickling in every time a unit is
        // eaten. The request is written to every member so each can answer the
        // feeding work giver from its own cheap persisted flag.
        float capacity = NutritionCapacity(cluster);
        bool requested = FeedTopUpRequested(cluster);
        if (capacity <= 0f || totalStored >= capacity)
        {
            requested = false;
        }
        else if (totalStored < capacity * Building_ChickenBatteryCage.FeedLowWaterFraction)
        {
            requested = true;
        }

        foreach (Building_ChickenBatteryCage cage in cluster)
        {
            if (Alive(cage))
            {
                cage.StarvingTicks = starvingTicks;
                cage.FeedTopUpRequested = requested;
            }
        }
    }

    /// True while any member still carries an outstanding refill request.
    static bool FeedTopUpRequested(IReadOnlyList<Building_ChickenBatteryCage> cluster)
    {
        foreach (Building_ChickenBatteryCage cage in cluster)
        {
            if (Alive(cage) && cage.FeedTopUpRequested)
            {
                return true;
            }
        }
        return false;
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

    /**
     * Cuts off automatic intake for every live member of a cluster that just
     * put live birds back on the map, so handlers do not rope the freed
     * chickens straight back into a cage. The player turns intake back on with
     * the "Pen system" gizmo. Every live-release path funnels through here:
     * manual unloads, cage destruction, and the deferred rescue releases.
     *
     * Only the releasing cluster is affected. If that network is gone and an
     * unrelated cage is still accepting, the freed bird can still be roped
     * there; that is a deliberate limit of a cluster-level cutoff, kept instead
     * of per-bird memory or disabling every cage on the map.
     */
    public static void DisableIntakeOnRelease(IReadOnlyList<Building_ChickenBatteryCage> cluster)
    {
        SetPenSystemEnabled(cluster, false);
    }
}
