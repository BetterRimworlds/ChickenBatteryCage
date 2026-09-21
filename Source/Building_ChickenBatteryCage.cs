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
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace BetterRimworlds.ChickenBatteryCage;

public class Building_ChickenBatteryCage : Building
{
    public const int ChickenCapacity = 10;

    bool roofedOverOccupiedCells = true;

    /// When false the cage's pen system is inactive. Handlers stop roping hens
    /// in, but the player can still release hens by hand and the hens already
    /// housed stay exactly as they are.
    protected bool penSystemEnabled = true;

    readonly List<IntVec3> unroofedCellsScratch = new List<IntVec3>();

    /// The entire confined flock, stored as compact biological records. No
    /// spawned Pawn is kept here.
    protected List<CagedChickenRecord> chickens = new List<CagedChickenRecord>();

    /// Collective nutrition held for the whole flock and drained by the summed
    /// demand of its members. No caged bird owns a personal food need, so feed
    /// is stored, consumed, and displayed at the cage level only.
    protected float nutritionStored;

    /// Absolute tick at which <see cref="nutritionStored"/> was last settled
    /// against elapsed time, so consumption is computed lazily instead of on
    /// a per-chicken schedule.
    protected int nutritionSettledAtTick;

    /// Accumulated ticks the flock has spent with an empty store. Only used to
    /// describe and to roll starvation mortality; it never spawns a starving
    /// Pawn or applies a malnutrition Hediff.
    protected int starvingTicks;

    /// Refill hysteresis. Set while the cluster's pool is below the low-water
    /// mark and cleared only once the pool is full, so one feeding burst tops
    /// the flock up instead of sipping at it every time a unit is eaten.
    protected bool feedTopUpRequested;

    /// Fraction of capacity below which a cluster asks to be refilled.
    public const float FeedLowWaterFraction = 0.5f;

    /// How often the flock is rolled for mortality. Coarse on purpose: a caged
    /// flock never pays a per-bird death check on every tick.
    protected const int MortalityEvaluationIntervalTicks = 2500;

    /// Absolute tick at which mortality was last rolled.
    protected int mortalityCheckedAtTick;

    /// Lifetime tallies. Deaths are counted here, never materialized as Pawns.
    protected int deathsNatural;

    protected int deathsStarvation;

    public int DeathsNatural => deathsNatural;

    public int DeathsStarvation => deathsStarvation;

    public int TotalDeaths => deathsNatural + deathsStarvation;

    /// Chickens the player has marked for unloading, one filter per bird. An
    /// animal handler resolves the front of the queue when the unload job
    /// reaches the cage. Persisted so marks survive save/load.
    protected List<ChickenReleaseFilter> pendingUnloads = new List<ChickenReleaseFilter>();

    /// Sentinel kept at long.MaxValue while unresolved so that, even if a
    /// caller somehow bypasses the guard in IsAdult, no bird is misread as
    /// an adult before the chicken def's life stages are known.
    static long adultMinAgeTicks = long.MaxValue;

    static bool adultMinAgeTicksResolved;
    static bool warnedMissingChickenDef;

    /// Species life expectancy, resolved from the chicken def once available.
    static float lifeExpectancyYears = CageMortalityMath.DefaultLifeExpectancyYears;
    static bool lifeExpectancyResolved;

    public bool IsOperational => roofedOverOccupiedCells;

    /// True while this cage should be offered to haulers as a feeding target.
    /// The decision is shared across the cluster's pool: a member is only a
    /// target while the flock needs feed and the pool is still filling.
    public bool NeedsFeeding => IsOperational
        && chickens.Count > 0
        && feedTopUpRequested;

    /// The persisted per-member copy of the cluster's refill request. The
    /// cluster settle keeps every member's copy in step.
    internal bool FeedTopUpRequested
    {
        get => feedTopUpRequested;
        set => feedTopUpRequested = value;
    }

    /**
     * Drains the shared store for the time that has passed since the cluster
     * last settled. Aging-style: no per-chicken food tick exists, the cluster's
     * summed demand is applied in one calculation whenever a value is needed.
     * Touching cages are one giant cage, so whichever member settles bills the
     * whole pool exactly once.
     */
    public void SettleNutrition()
    {
        IReadOnlyList<Building_ChickenBatteryCage> cluster = CageNetwork.Cluster(this);
        int now = GenTicks.TicksAbs;

        // An unroofed cage is inoperable, so its simulation pauses: the settle
        // clock advances without eating, and time passed that way is not
        // billed to the flock once the roof is restored.
        if (!IsOperational)
        {
            CageNetwork.PauseCluster(cluster, now);
            return;
        }

        CageNetwork.SettleCluster(cluster, now);
    }

    /// Controls whether handlers may rope hens into this cage automatically.
    /// Defaults to true and persists across saves. Manual release is unaffected.
    public bool PenSystemEnabled => penSystemEnabled;

    /// True while at least one chicken is marked to be unloaded by a handler.
    public bool HasPendingUnload => pendingUnloads.Count > 0;

    public int PendingUnloadCount => pendingUnloads.Count;

    public bool IsFull => chickens.Count >= ChickenCapacity;

    public virtual int ChickenCount => chickens.Count;

    public virtual int AdultHenCount => CountMatching(Gender.Female, adult: true);

    public virtual int JuvenileCount => CountMatching(Gender.None, adult: false, anyGender: true);

    public CagedChickenRecord RecordAt(int index)
    {
        return (index >= 0 && index < chickens.Count) ? chickens[index] : null;
    }

    /// Collective nutrition currently held for the flock.
    public float StoredNutrition => nutritionStored;

    /// Largest collective store the cage can hold, scaled by bird capacity.
    public float NutritionCapacity => CageNutritionMath.MaxNutrition(ChickenCapacity);

    /// Free room left in the collective store.
    public float NutritionSpace =>
        nutritionStored >= NutritionCapacity ? 0f : NutritionCapacity - nutritionStored;

    /// Feed held across this cage's whole cluster — the shared pool.
    public float ClusterStoredNutrition => CageNetwork.StoredNutrition(CageNetwork.Cluster(this));

    /// Combined storage of every cage in the cluster.
    public float ClusterNutritionCapacity => CageNetwork.NutritionCapacity(CageNetwork.Cluster(this));

    /// Free room left in the cluster's shared pool.
    public float ClusterNutritionSpace => CageNetwork.NutritionSpace(CageNetwork.Cluster(this));

    /// Fed, hungry, or starving, derived from the cluster's shared pool.
    public CageNutritionState ClusterNutritionState => CageNetwork.NutritionState(CageNetwork.Cluster(this));

    /**
     * Internal hooks the cluster settlement uses to write its single result
     * back into each member's own persisted store. A cage's store therefore
     * stays per-cage on disk even though the pool is shared at runtime.
     */
    internal int NutritionSettledAtTick
    {
        get => nutritionSettledAtTick;
        set => nutritionSettledAtTick = value;
    }

    internal int StarvingTicks
    {
        get => starvingTicks;
        set => starvingTicks = value;
    }

    internal void SetStoredNutrition(float value)
    {
        nutritionStored = value;
    }

    /// Summed daily demand of every bird currently housed.
    public float NutritionDemandPerDay => CageNutritionMath.DemandPerDay(
        chickens.Count,
        CageNutritionMath.DefaultNutritionPerChickenPerDay);

    /// Days the flock has spent with an empty store.
    public float StarvingDays => starvingTicks / (float)CagedChickenMath.TicksPerDay;

    /// Fed, hungry, or starving, derived from the cluster's shared pool alone.
    public CageNutritionState NutritionState => ClusterNutritionState;

    /// Adds feed to the cluster's shared pool and returns how much was
    /// accepted. Anything above the cluster's combined capacity is refused
    /// rather than silently wasted. Only <see cref="CageFeed.Feed"/> may call
    /// this: hauling food straight to a cage is the sole way feed enters it.
    public float AddNutrition(float amount)
    {
        return CageNetwork.AddNutrition(CageNetwork.Cluster(this), amount);
    }

    /// Stops the cluster asking for feed once no further whole unit fits, so a
    /// sub-unit gap cannot leave the feeding work running indefinitely. The
    /// low-water mark re-arms the request on the next dip.
    public void ClearFeedRequest()
    {
        CageNetwork.ClearFeedRequest(CageNetwork.Cluster(this));
    }

    protected virtual string FeedInspectValue => "ChickenBatteryCage.Feed.Status".Translate(
        ClusterStoredNutrition.ToString("0.#"),
        ClusterNutritionCapacity.ToString("0.#"),
        NutritionStateLabel);

    string NutritionStateLabel
    {
        get
        {
            switch (NutritionState)
            {
                case CageNutritionState.Starving:
                    return "ChickenBatteryCage.Feed.StateStarving".Translate();
                case CageNutritionState.Hungry:
                    return "ChickenBatteryCage.Feed.StateHungry".Translate();
                default:
                    return "ChickenBatteryCage.Feed.StateFed".Translate();
            }
        }
    }

    protected virtual string EggsInspectValue => "ChickenBatteryCage.Inspect.Empty".Translate();

    public static bool IsChicken(Pawn pawn)
    {
        return pawn != null
            && !pawn.Dead
            && pawn.RaceProps != null
            && pawn.RaceProps.Animal
            && pawn.kindDef == ChickenBatteryCageDefOf.Chicken;
    }

    public static bool IsHen(Pawn pawn)
    {
        return IsChicken(pawn) && pawn.gender == Gender.Female;
    }

    public override void SpawnSetup(Map map, bool respawningAfterLoad)
    {
        base.SpawnSetup(map, respawningAfterLoad);

        if (chickens == null)
        {
            chickens = new List<CagedChickenRecord>();
        }

        // RoofGrid.SetRoof notifies MapEvents.RoofChanged, so react to roof changes
        // immediately instead of polling on TickRare and leaving IsOperational stale.
        map.events.RoofChanged -= OnRoofChanged;
        map.events.RoofChanged += OnRoofChanged;
        RecheckRoofing();

        // A new cage changes which cages touch; rebuild the clusters now so the
        // just-built cage immediately joins its neighbours.
        CageNetwork.Invalidate(map);
    }

    public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
    {
        Map map = Map;
        if (map != null)
        {
            map.events.RoofChanged -= OnRoofChanged;
            // Removing a cage can split or shrink a cluster; rebuild at once.
            CageNetwork.Invalidate(map);
        }

        base.DeSpawn(mode);
    }

    /// A cage that goes away hands its whole flock back to the map, the records
    /// being the only copy of those birds. Peaceful deconstruction by a
    /// colonist frees them unharmed; any other destruction, such as combat
    /// damage or fire, leaves the flock wounded.
    ///
    /// A bird that cannot be materialized at that instant is not abandoned: the
    /// record is handed to <see cref="MapComponent_CagedChickenRescue"/>, which
    /// holds it past the building's destruction and retries the release.
    public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
    {
        if (chickens.Count > 0)
        {
            bool injured = mode != DestroyMode.Deconstruct;
            ReleaseFlock(injured);
            PreserveUnreleasedFlock(injured);
        }

        base.Destroy(mode);
    }

    /// Materializes and releases every housed chicken when the cage itself is
    /// going away. A deconstructed cage is taken apart carefully, so its birds
    /// come out unharmed; a cage wrecked by force spits them out wounded.
    /// Flock-level, so it neither consults nor keeps the pending unload queue.
    void ReleaseFlock(bool injured)
    {
        if (!Spawned || Map == null || chickens.Count == 0)
        {
            return;
        }

        SettleNutrition();

        Map map = Map;
        EvaluateMortality(force: true);
        IntVec3 near = InteractionCell.IsValid ? InteractionCell : Position;
        int released = 0;
        for (int i = chickens.Count - 1; i >= 0; i--)
        {
            Pawn chicken = CageChickenFactory.Generate(chickens[i], map, near);
            if (chicken == null)
            {
                continue;
            }

            chickens.RemoveAt(i);
            CageHenReleaseMemory.Mark(chicken);
            if (injured)
            {
                CageChickenInjuries.Injure(chicken);
            }

            released++;
        }

        pendingUnloads.Clear();

        if (released > 0)
        {
            string message = injured
                ? "ChickenBatteryCage.Message.ReleasedInjured"
                : "ChickenBatteryCage.Message.ReleasedOnDeconstruct";
            Messages.Message(
                message.Translate(released),
                MessageTypeDefOf.NeutralEvent,
                historical: false);
        }
    }

    /// Handles the records that <see cref="ReleaseFlock"/> could not turn into
    /// pawns. They outlive the building by moving to a map component, which
    /// keeps them across save/load and retries the release. A record must never
    /// be deleted just because the cage that held it is gone.
    void PreserveUnreleasedFlock(bool injured)
    {
        if (chickens.Count == 0)
        {
            return;
        }

        Map map = Map;
        MapComponent_CagedChickenRescue rescue =
            map?.GetComponent<MapComponent_CagedChickenRescue>();

        if (rescue == null)
        {
            // Nowhere to keep them. There is no map to attach the records to,
            // so their only copy disappears with the building. Say so loudly
            // instead of dropping them in silence.
            Log.Error("[ChickenBatteryCage] Destroying a battery cage with " +
                chickens.Count + " unreleased chicken record(s) but no map to " +
                "preserve them on; those chickens were lost.");
            chickens.Clear();
            pendingUnloads.Clear();
            return;
        }

        IntVec3 near = InteractionCell.IsValid ? InteractionCell : Position;
        int preserved = chickens.Count;
        foreach (CagedChickenRecord record in chickens)
        {
            rescue.Preserve(record, near, injured);
        }

        chickens.Clear();
        pendingUnloads.Clear();

        Log.Warning("[ChickenBatteryCage] " + preserved +
            " caged chicken record(s) could not be released when their cage was " +
            "destroyed; they were handed to the map's rescue component and will " +
            "be released as soon as a placement cell is free.");
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref penSystemEnabled, "penSystemEnabled", true);
        Scribe_Values.Look(ref nutritionStored, "nutritionStored", 0f);
        Scribe_Values.Look(ref nutritionSettledAtTick, "nutritionSettledAtTick", 0);
        Scribe_Values.Look(ref starvingTicks, "starvingTicks", 0);
        Scribe_Values.Look(ref feedTopUpRequested, "feedTopUpRequested", false);
        Scribe_Values.Look(ref mortalityCheckedAtTick, "mortalityCheckedAtTick", 0);
        Scribe_Values.Look(ref deathsNatural, "deathsNatural", 0);
        Scribe_Values.Look(ref deathsStarvation, "deathsStarvation", 0);
        Scribe_Collections.Look(ref chickens, "chickens", LookMode.Deep);
        Scribe_Collections.Look(ref pendingUnloads, "pendingUnloads", LookMode.Value);

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            if (chickens == null)
            {
                chickens = new List<CagedChickenRecord>();
            }

            if (pendingUnloads == null)
            {
                pendingUnloads = new List<ChickenReleaseFilter>();
            }
        }
    }

    void OnRoofChanged(IntVec3 cell)
    {
        if (!this.OccupiedRect().Contains(cell))
        {
            return;
        }

        // Settle the previous state before applying the new one. Settling
        // after the recheck would pause the cluster and silently reset the
        // settle clock, erasing the elapsed roofed time instead of billing the
        // flock for it. Restoring a roof owes nothing: pausing kept the clock
        // current, so only a loss of operability needs a final bill.
        if (IsOperational)
        {
            SettleNutrition();
        }

        RecheckRoofing();
    }

    public override string GetInspectString()
    {
        SettleNutrition();

        IReadOnlyList<Building_ChickenBatteryCage> cluster = CageNetwork.Cluster(this);

        var sb = new StringBuilder();
        string baseString = base.GetInspectString();
        if (!baseString.NullOrEmpty())
        {
            sb.Append(baseString);
        }

        if (sb.Length > 0)
        {
            sb.AppendLine();
        }

        if (!IsOperational)
        {
            sb.AppendLine("ChickenBatteryCage.Inspect.Unroofed".Translate());
        }

        if (!penSystemEnabled)
        {
            sb.AppendLine("ChickenBatteryCage.Inspect.PenSystemOff".Translate());
        }

        int clusterChickens = CageNetwork.ChickenCount(cluster);
        sb.AppendLine("ChickenBatteryCage.Inspect.Chickens".Translate(
            clusterChickens,
            CageNetwork.TotalCapacity(cluster)));
        sb.AppendLine("ChickenBatteryCage.Inspect.AdultHens".Translate(CageNetwork.AdultHenCount(cluster)));
        sb.AppendLine("ChickenBatteryCage.Inspect.Juveniles".Translate(CageNetwork.JuvenileCount(cluster)));

        int cageCount = 0;
        foreach (Building_ChickenBatteryCage cage in cluster)
        {
            if (cage != null && !cage.Destroyed)
            {
                cageCount++;
            }
        }

        if (cageCount > 1)
        {
            sb.AppendLine("ChickenBatteryCage.Inspect.Network".Translate(
                clusterChickens,
                CageNetwork.TotalCapacity(cluster),
                cageCount));
        }

        if (CageNetwork.HasPendingUnload(cluster))
        {
            sb.AppendLine("ChickenBatteryCage.Inspect.PendingUnload".Translate(
                CageNetwork.PendingUnloadCount(cluster)));
        }

        sb.AppendLine("ChickenBatteryCage.Inspect.Feed".Translate(FeedInspectValue));
        sb.Append("ChickenBatteryCage.Inspect.Eggs".Translate(EggsInspectValue));

        return sb.ToString().TrimEnd();
    }

    public override IEnumerable<Gizmo> GetGizmos()
    {
        foreach (Gizmo gizmo in base.GetGizmos())
        {
            yield return gizmo;
        }

        // Every gizmo below reads from this cage's cluster, so all cages that
        // touch present identical labels and merge into one control on
        // multi-select. Separate clusters stay independent.
        IReadOnlyList<Building_ChickenBatteryCage> cluster = CageNetwork.Cluster(this);

        int chickenCount = CageNetwork.ChickenCount(cluster);
        int totalCapacity = CageNetwork.TotalCapacity(cluster);
        int adultHenCount = CageNetwork.AdultHenCount(cluster);
        int juvenileCount = CageNetwork.JuvenileCount(cluster);
        int pendingUnloadCount = CageNetwork.PendingUnloadCount(cluster);
        bool penSystemEnabled = CageNetwork.PenSystemEnabled(cluster);

        Command_Action capacityGizmo = new Command_Action
        {
            defaultLabel = "ChickenBatteryCage.Gizmo.Capacity".Translate(chickenCount, totalCapacity),
            defaultDesc = "ChickenBatteryCage.Gizmo.CapacityDesc".Translate(
                chickenCount,
                totalCapacity,
                adultHenCount,
                juvenileCount),
            icon = def.uiIcon,
            action = delegate { },
        };
        capacityGizmo.Disable("ChickenBatteryCage.Gizmo.CapacityDisabled".Translate());
        yield return capacityGizmo;

        Command_Toggle penSystemGizmo = new Command_Toggle
        {
            defaultLabel = (penSystemEnabled
                ? "ChickenBatteryCage.Gizmo.PenSystemOn"
                : "ChickenBatteryCage.Gizmo.PenSystemOff").Translate(),
            defaultDesc = "ChickenBatteryCage.Gizmo.PenSystemDesc".Translate(),
            icon = def.uiIcon,
            isActive = () => CageNetwork.PenSystemEnabled(CageNetwork.Cluster(this)),
            toggleAction = delegate
            {
                IReadOnlyList<Building_ChickenBatteryCage> current = CageNetwork.Cluster(this);
                CageNetwork.SetPenSystemEnabled(current, !CageNetwork.PenSystemEnabled(current));
            },
        };
        yield return penSystemGizmo;

        Command_Action unloadGizmo = new Command_Action
        {
            defaultLabel = (pendingUnloadCount > 0
                ? "ChickenBatteryCage.Gizmo.UnloadPending"
                : "ChickenBatteryCage.Gizmo.Unload").Translate(pendingUnloadCount),
            defaultDesc = "ChickenBatteryCage.Gizmo.UnloadDesc".Translate(),
            icon = def.uiIcon,
            action = delegate
            {
                FloatMenu menu = new FloatMenu(CageNetwork.BuildUnloadMenu(CageNetwork.Cluster(this)));
                menu.vanishIfMouseDistant = false;
                Find.WindowStack.Add(menu);
            },
        };
        if (chickenCount == 0)
        {
            unloadGizmo.Disable("ChickenBatteryCage.Gizmo.UnloadEmpty".Translate());
        }
        yield return unloadGizmo;
    }

    public bool CanAcceptChicken(Pawn chicken)
    {
        return IsHen(chicken)
            && IsOperational
            && penSystemEnabled
            && !IsFull
            && !CageHenReleaseMemory.IsRecentlyReleased(chicken);
    }

    public static bool AnyCageWithPendingUnload(Map map)
    {
        if (map == null)
        {
            return false;
        }

        foreach (Building_ChickenBatteryCage cage in map.listerBuildings.AllBuildingsColonistOfClass<Building_ChickenBatteryCage>())
        {
            if (cage.HasPendingUnload)
            {
                return true;
            }
        }

        return false;
    }

    /// True while any cage on the map is below its cluster's low-water mark and
    /// should be offered to haulers, so the feeding work giver can skip the map
    /// entirely when every flock is settled.
    public static bool AnyCageNeedingFeed(Map map)
    {
        if (map == null)
        {
            return false;
        }

        foreach (Building_ChickenBatteryCage cage in map.listerBuildings.AllBuildingsColonistOfClass<Building_ChickenBatteryCage>())
        {
            if (cage.NeedsFeeding)
            {
                return true;
            }
        }

        return false;
    }

    public static bool AnyAcceptingCage(Map map)
    {
        if (map == null)
        {
            return false;
        }

        foreach (Building_ChickenBatteryCage cage in map.listerBuildings.AllBuildingsColonistOfClass<Building_ChickenBatteryCage>())
        {
            if (cage.IsOperational && cage.penSystemEnabled && !cage.IsFull)
            {
                return true;
            }
        }

        return false;
    }

    public static string NoAcceptingCageReason(Map map)
    {
        bool any = false;
        bool anyOperational = false;
        bool anyEnabled = false;
        foreach (Building_ChickenBatteryCage cage in map.listerBuildings.AllBuildingsColonistOfClass<Building_ChickenBatteryCage>())
        {
            any = true;
            if (!cage.IsOperational)
            {
                continue;
            }

            anyOperational = true;
            if (!cage.penSystemEnabled)
            {
                continue;
            }

            anyEnabled = true;
            if (!cage.IsFull)
            {
                return "ChickenBatteryCage.Job.NoReachableCage".Translate();
            }
        }

        if (!any)
        {
            return "ChickenBatteryCage.Job.NoCage".Translate();
        }

        if (!anyOperational)
        {
            return "ChickenBatteryCage.FloatMenu.Unroofed".Translate();
        }

        if (!anyEnabled)
        {
            return "ChickenBatteryCage.FloatMenu.Disabled".Translate();
        }

        return "ChickenBatteryCage.FloatMenu.Full".Translate();
    }

    public static Building_ChickenBatteryCage FindAcceptingCage(Pawn hen, Pawn handler)
    {
        if (hen?.Map == null || handler == null)
        {
            return null;
        }

        Building_ChickenBatteryCage best = null;
        int bestDist = int.MaxValue;
        foreach (Building_ChickenBatteryCage cage in hen.Map.listerBuildings.AllBuildingsColonistOfClass<Building_ChickenBatteryCage>())
        {
            if (!cage.CanAcceptChicken(hen) || cage.IsForbidden(handler))
            {
                continue;
            }

            if (!handler.CanReach(cage, PathEndMode.Touch, Danger.Deadly))
            {
                continue;
            }

            // Also check that the handler can reserve a stand cell in this cage.
            // Without this, a nearest cage may have all stand cells reserved,
            // causing the job to fail even when another cage has availability.
            if (!HasAvailableStandCell(cage, handler))
            {
                continue;
            }

            int dist = hen.Position.DistanceToSquared(cage.Position);
            if (dist < bestDist)
            {
                best = cage;
                bestDist = dist;
            }
        }

        return best;
    }

    private static bool HasAvailableStandCell(Building_ChickenBatteryCage cage, Pawn handler)
    {
        foreach (IntVec3 cell in GenAdj.CellsAdjacent8Way(cage))
        {
            if (cage.IsGoodStandCell(cell, handler))
            {
                return true;
            }
        }

        return false;
    }

    public IntVec3 FindStandCellForHandler(Pawn handler)
    {
        if (!Spawned || handler == null || Map == null)
        {
            return IntVec3.Invalid;
        }

        foreach (IntVec3 cell in GenAdj.CellsAdjacent8Way(this))
        {
            if (IsGoodStandCell(cell, handler))
            {
                return cell;
            }
        }

        return IntVec3.Invalid;
    }

    bool IsGoodStandCell(IntVec3 cell, Pawn handler)
    {
        if (!cell.InBounds(Map) || !cell.Standable(Map))
        {
            return false;
        }

        if (!handler.Map.pawnDestinationReservationManager.CanReserve(cell, handler))
        {
            return false;
        }

        return handler.CanReach(cell, PathEndMode.OnCell, Danger.Deadly);
    }

    /**
     * Serializes a delivered hen's biology and puts her in the cage; her Pawn
     * form is unmade without treating the event as a death. Called at the
     * instant a handler delivers the bird; before this point the bird is a
     * normal spawned Pawn.
     */
    public bool TryAcceptChicken(Pawn chicken)
    {
        return CageHenIntake.PutHenInCage(this, chicken);
    }

    /// Called by <see cref="CageHenIntake.PutHenInCage"/> after the hen's
    /// biology has been captured; appends the record to the housed flock.
    public void AddRecord(CagedChickenRecord record)
    {
        // Bill the outgoing population before the newcomer joins.
        SettleNutrition();
        chickens.Add(record);
    }

    /// Marks one chicken of the chosen kind to be unloaded by an animal
    /// handler. The mark is resolved when a handler reaches the cage.
    /// Returns false when the cage has no space left in its mark queue; the
    /// network only calls this after confirming a matching bird is housed here.
    public bool RequestUnload(ChickenReleaseFilter filter)
    {
        if (pendingUnloads.Count < chickens.Count)
        {
            pendingUnloads.Add(filter);
            return true;
        }

        return false;
    }

    public void SetPenSystemEnabled(bool enabled)
    {
        penSystemEnabled = enabled;
    }

    public void ClearPendingUnloads()
    {
        pendingUnloads.Clear();
    }

    /// Resolves the front of the unload queue. Discards marks whose kind of
    /// chicken is no longer present and stops without losing a mark if the
    /// bird could not be materialized.
    public bool TryUnloadNext()
    {
        while (pendingUnloads.Count > 0)
        {
            ChickenReleaseFilter filter = pendingUnloads[0];
            int index = FindRecordIndex(filter);
            if (index < 0)
            {
                pendingUnloads.RemoveAt(0);
                continue;
            }

            if (!ReleaseRecordAt(index))
            {
                // Generation failed; keep both the record and the mark.
                return false;
            }

            pendingUnloads.RemoveAt(0);
            return true;
        }

        return false;
    }

    /// Materializes and releases the record at the given index.
    public bool ReleaseRecordAt(int index)
    {
        if (index < 0 || index >= chickens.Count)
        {
            return false;
        }

        // Settle before the bird leaves so her share of the store is billed.
        CagedChickenRecord selected = chickens[index];
        SettleNutrition();
        EvaluateMortality(force: true);
        index = chickens.IndexOf(selected);
        if (index < 0)
        {
            // The selected bird died while settling her accrued exposure.
            return true;
        }

        CagedChickenRecord record = chickens[index];
        IntVec3 near = InteractionCell.IsValid ? InteractionCell : Position;
        Pawn released = CageChickenFactory.Generate(record, Map, near);
        if (released == null)
        {
            // The record is retained so the bird is not lost.
            return false;
        }

        chickens.RemoveAt(index);
        CageHenReleaseMemory.Mark(released);
        Messages.Message(
            "ChickenBatteryCage.Message.Released".Translate(released.LabelShortCap),
            released,
            MessageTypeDefOf.TaskCompletion,
            historical: false);
        return true;
    }

    /// The whole network scans this when routing unload marks, e.g. to find
    /// the true youngest hen across every cage on the map.
    public int FindRecordIndex(ChickenReleaseFilter filter)
    {
        if (chickens.Count == 0)
        {
            return -1;
        }

        int now = GenTicks.TicksAbs;

        switch (filter)
        {
            case ChickenReleaseFilter.Random:
                return Rand.Range(0, chickens.Count);

            case ChickenReleaseFilter.Youngest:
                return ExtremeByAge(now, youngest: true);

            case ChickenReleaseFilter.Oldest:
                return ExtremeByAge(now, youngest: false);

            case ChickenReleaseFilter.AdultHen:
                return FirstMatching(now, Gender.Female, adult: true);

            case ChickenReleaseFilter.Juvenile:
                return FirstMatching(now, Gender.None, adult: false, anyGender: true);

            default:
                return -1;
        }
    }

    int ExtremeByAge(int now, bool youngest)
    {
        int best = 0;
        long bestAge = chickens[0].BiologicalAgeTicksAt(now);
        for (int i = 1; i < chickens.Count; i++)
        {
            long age = chickens[i].BiologicalAgeTicksAt(now);
            if (youngest ? age < bestAge : age > bestAge)
            {
                best = i;
                bestAge = age;
            }
        }
        return best;
    }

    int FirstMatching(int now, Gender gender, bool adult, bool anyGender = false)
    {
        for (int i = 0; i < chickens.Count; i++)
        {
            CagedChickenRecord record = chickens[i];
            if (IsAdult(record, now) != adult)
            {
                continue;
            }
            if (!anyGender && record.gender != gender)
            {
                continue;
            }
            return i;
        }
        return -1;
    }

    int CountMatching(Gender gender, bool adult, bool anyGender = false)
    {
        int now = GenTicks.TicksAbs;
        int count = 0;
        foreach (CagedChickenRecord record in chickens)
        {
            if (IsAdult(record, now) != adult)
            {
                continue;
            }
            if (!anyGender && record.gender != gender)
            {
                continue;
            }
            count++;
        }
        return count;
    }

    static bool IsAdult(CagedChickenRecord record, int now)
    {
        if (!EnsureLifeStageTicks())
        {
            // Defs are not loaded yet, so the adult threshold is unknown.
            // Treat every bird as a juvenile rather than as an adult; the
            // next call re-resolves once the def is available.
            return false;
        }

        return CagedChickenMath.IsAdult(record.BiologicalAgeTicksAt(now), adultMinAgeTicks);
    }

    /// Returns false while the chicken def's life stages are unavailable and
    /// the threshold must still be treated as unknown. Callers retry on each
    /// evaluation; the resolved value is cached once found.
    static bool EnsureLifeStageTicks()
    {
        if (adultMinAgeTicksResolved)
        {
            return true;
        }

        long adult = 0;

        // The DefOf only binds once defs are loaded. Until then keep the
        // threshold unresolved and retry rather than caching a degenerate
        // value that would classify every caged bird as an adult.
        ThingDef chickenDef = ChickenBatteryCageDefOf.Chicken?.race;
        List<LifeStageAge> stages = chickenDef?.race?.lifeStageAges;

        if (stages == null)
        {
            if (!warnedMissingChickenDef)
            {
                warnedMissingChickenDef = true;
                Log.WarningOnce(
                    "[ChickenBatteryCage] Chicken pawn kind is unavailable; caged-bird adult/juvenile counts fall back to treating every bird as a juvenile until the def resolves.",
                    74129301);
            }
            return false;
        }

        foreach (LifeStageAge stage in stages)
        {
            long ticks = (long)(stage.minAge * GenDate.TicksPerYear);
            if (stage.def != null && stage.def.defName == "AnimalAdult")
            {
                adult = ticks;
            }
        }

        if (adult <= 0 && stages.Count > 0)
        {
            adult = (long)(stages[stages.Count - 1].minAge * GenDate.TicksPerYear);
        }

        adultMinAgeTicks = adult;
        adultMinAgeTicksResolved = true;
        return true;
    }

    /// The chicken's nominal life expectancy, used as the hinge of the natural
    /// mortality curve. Falls back to a sane default until defs are loaded.
    static float ResolveLifeExpectancyYears()
    {
        if (lifeExpectancyResolved)
        {
            return lifeExpectancyYears;
        }

        // The two "race" links are different members on different types:
        // PawnKindDef.race is the species ThingDef, and ThingDef.race is the
        // RaceProperties that actually carries lifeExpectancy. Null-check every
        // link: the DefOf stays unbound until defs finish loading.
        PawnKindDef chickenKind = ChickenBatteryCageDefOf.Chicken;
        ThingDef chickenDef = chickenKind?.race;
        RaceProperties chickenRace = chickenDef?.race;

        if (chickenRace == null || chickenRace.lifeExpectancy <= 0f)
        {
            // Defs are not loaded yet, or the species omits a value; retry on
            // the next evaluation rather than caching a degenerate value. Warn
            // once so the fallback is visible while debugging.
            Log.WarningOnce(
                "[ChickenBatteryCage] Chicken race life expectancy is unavailable; caged-bird mortality falls back to "
                    + CageMortalityMath.DefaultLifeExpectancyYears
                    + " years until the def resolves.",
                74129302);
            return CageMortalityMath.DefaultLifeExpectancyYears;
        }

        lifeExpectancyYears = chickenRace.lifeExpectancy;
        lifeExpectancyResolved = true;
        return lifeExpectancyYears;
    }

    public override void DrawExtraSelectionOverlays()
    {
        base.DrawExtraSelectionOverlays();
        if (!Spawned || IsOperational)
        {
            return;
        }

        unroofedCellsScratch.Clear();
        foreach (IntVec3 cell in this.OccupiedRect())
        {
            if (!Map.roofGrid.Roofed(cell))
            {
                unroofedCellsScratch.Add(cell);
            }
        }

        if (unroofedCellsScratch.Count > 0)
        {
            GenDraw.DrawFieldEdges(unroofedCellsScratch, Color.red);
        }
    }

    public override void TickRare()
    {
        base.TickRare();
        SettleNutrition();
        EvaluateMortality();
    }

    /// Accrues exposure before nutrition settlement or population changes.
    internal void AccumulateMortality(int fromTick, int now)
    {
        float lifeExpectancy = ResolveLifeExpectancyYears();
        foreach (CagedChickenRecord record in chickens)
        {
            int start = CageMortalityMath.ExposureStartTick(fromTick, record.enteredAtGameTick);
            record.mortalityExposure += CageMortalityMath.NaturalExposureOverTicks(
                record.BiologicalAgeTicksAt(start), lifeExpectancy, now - start);
        }
    }

    /// Rolls accumulated exposure coarsely, or before birds leave the cage.
    void EvaluateMortality(bool force = false)
    {
        int now = GenTicks.TicksAbs;
        if (mortalityCheckedAtTick <= 0 || mortalityCheckedAtTick > now)
        {
            mortalityCheckedAtTick = now;
            if (!force)
            {
                return;
            }
        }

        int elapsed = now - mortalityCheckedAtTick;
        if (!force && elapsed < MortalityEvaluationIntervalTicks)
        {
            return;
        }

        mortalityCheckedAtTick = now;
        if (chickens == null || chickens.Count == 0)
        {
            return;
        }

        int died = 0;
        for (int i = chickens.Count - 1; i >= 0; i--)
        {
            CagedChickenRecord record = chickens[i];
            float chance = CageMortalityMath.ChanceFromExposure(record.mortalityExposure);
            record.mortalityExposure = 0.0;
            if (!Rand.Chance(chance))
            {
                continue;
            }

            chickens.RemoveAt(i);
            died++;
        }

        if (died > 0)
        {
            deathsNatural += died;
        }
    }

    void RecheckRoofing()
    {
        if (!Spawned)
        {
            return;
        }

        roofedOverOccupiedCells = true;
        foreach (IntVec3 cell in this.OccupiedRect())
        {
            if (!Map.roofGrid.Roofed(cell))
            {
                roofedOverOccupiedCells = false;
                break;
            }
        }
    }
}
