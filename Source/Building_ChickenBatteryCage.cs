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

    /// When false the cage's pen system is inactive. Handlers stop roping hens
    /// in, but the player can still release hens by hand and the hens already
    /// housed stay exactly as they are.
    protected bool penSystemEnabled = true;

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

    /// Fractional eggs held inside the cage's egg box, on their way to the next
    /// whole egg. Once the box holds a full batch it spits the eggs out as one
    /// haulable stack and starts filling again.
    protected float eggProgress;

    /// Absolute tick at which egg output was last settled.
    protected int eggCheckedAtTick;

    /// The smallest egg stack a cage releases. A lone cage holds at most ten
    /// hens, each laying no more than one egg a day at her prime, so this floor
    /// keeps a single cage's box at its familiar size. Cages that touch pool
    /// their output and release a full day of the whole network instead.
    public const int MinEggsPerStack = 10;

    /**
     * The size of the next egg stack this cage releases: one whole day of the
     * whole network's laying, floored to whole eggs and never below
     * <see cref="MinEggsPerStack"/>. The network box is shared, so this is the
     * same figure on every member. Feeding penalties slow how quickly the box
     * fills, not how large one day's stack is.
     */
    public int EggStackSize => CageNetwork.EggStackSize(
        CageNetwork.Cluster(this),
        MinEggsPerStack);

    /// Whole eggs currently waiting in the box for the next release.
    public int EggsHeld => CageEggMath.WholeEggsInBox(eggProgress);

    /// The raw fractional egg count, including the partial egg on the way.
    public float EggProgress => eggProgress;

    /// Fractional progress toward the next whole egg, in [0, 1). Drives the
    /// inspection readout so a player can see an egg coming.
    public float ProgressToNextEgg => CageEggMath.ProgressToNextEgg(eggProgress);
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

    /// True while this cage should be offered to haulers as a feeding target.
    /// The decision is shared across the cluster's pool: a member is only a
    /// target while the flock needs feed and the pool is still filling.
    public bool NeedsFeeding => chickens.Count > 0
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

        // The cage runs whether or not it is roofed: an unroofed cage still
        // eats, ages, and dies exactly as a roofed one does.
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

    /// Internal hook the network uses to rebalance the shared egg box back over
    /// the members after a stack has been released.
    internal void SetEggProgress(float value)
    {
        eggProgress = value;
    }

    /// Summed daily demand of every bird currently housed.
    public float NutritionDemandPerDay => CageNutritionMath.DemandPerDay(
        chickens.Count,
        CageNutritionMath.DefaultNutritionPerChickenPerDay);

    /// Summed daily laying rate of the housed hens, derived from their exact
    /// biological age. The age decline is dropped when the player turns it off
    /// in the mod settings, leaving every adult hen at one egg a day. No hen
    /// owns a ticking egg-production component either way.
    public float EggLayingRatePerDay
    {
        get
        {
            if (chickens.Count == 0)
            {
                return 0f;
            }

            float adultYears = AdultMinAgeYears;
            int now = GenTicks.TicksAbs;
            float rate = 0f;
            foreach (CagedChickenRecord record in chickens)
            {
                if (record.gender != Gender.Female)
                {
                    continue;
                }

                rate += CageEggMath.EggsPerHenPerDay(
                    record.BiologicalAgeYearsAt(now),
                    adultYears,
                    ChickenBatteryCage.Settings?.ageDeclineEnabled ?? true);
            }

            return rate;
        }
    }

    static float AdultMinAgeYears => EnsureLifeStageTicks()
        ? adultMinAgeTicks / (float)GenDate.TicksPerYear
        : 0.2f;

    /**
     * How many ticks of an elapsed interval the flock was actually fed.
     *
     * Laying is gated by access to feed, and the store is shared across the
     * cluster, so an interval only earns eggs up to the point the pooled store
     * covers the whole flock's demand. A store that runs dry partway through
     * the interval stops earning there, instead of paying for the whole span.
     * Returns the whole interval when feed is ample (or the flock has no
     * demand).
     */
    int FedTicksWithin(int elapsed)
    {
        float demandPerDay = ClusterDemandPerDay;
        if (demandPerDay <= 0f)
        {
            return elapsed;
        }

        float stored = ClusterStoredNutrition;
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

    /// Summed daily demand of every live member of this cage's cluster, since
    /// the feed store — and therefore the fed interval — is shared.
    float ClusterDemandPerDay
    {
        get
        {
            float total = 0f;
            foreach (Building_ChickenBatteryCage cage in CageNetwork.Cluster(this))
            {
                if (cage != null && !cage.Destroyed)
                {
                    total += cage.NutritionDemandPerDay;
                }
            }

            return total;
        }
    }

    /**
     * Settles accrued laying into this cage's own box at a state transition.
     * Exposed so a feed delivery can bound an interval at the moment the store
     * changes, instead of letting the next rare tick judge the whole span
     * against the refilled store.
     */
    internal void SettleEggProduction()
    {
        AccrueEggProduction();
    }

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

    protected virtual string EggsInspectValue
    {
        get
        {
            // Pooled on purpose. The network box is shared, so the whole-egg
            // count is the floor of the pooled progress — not the sum of each
            // cage's floored share — because that is exactly what
            // ReleaseEggBatches releases. Keep the two in step.
            float raw = CageNetwork.EggProgress(CageNetwork.Cluster(this));
            int held = CageEggMath.WholeEggsInBox(raw);
            string batch = "ChickenBatteryCage.Eggs.Held".Translate(
                held,
                EggStackSize);
            string progress = "ChickenBatteryCage.Eggs.Progress".Translate(
                (CageEggMath.ProgressToNextEgg(raw) * 100f).ToString("0"));
            return batch + " " + progress;
        }
    }

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

        // A new cage changes which cages touch; rebuild the clusters now so the
        // just-built cage immediately joins its neighbours.
        CageNetwork.Invalidate(map);
    }

    public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
    {
        Map map = Map;
        if (map != null)
        {
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
        // Capture the network's surviving cages while this cage is still a live
        // member: after base.Destroy the building is gone and its cluster can no
        // longer be discovered from here. Every bird released below must not be
        // roped straight back in, so those survivors' intake is cut off in turn.
        IReadOnlyList<Building_ChickenBatteryCage> origin = SurvivingNetwork();

        // Credit laying accrued since the last rare tick before the flock can
        // leave. This runs before ReleaseFlock's SettleNutrition so a fed
        // interval is not evaluated against a pool that is already drained.
        AccrueEggProduction();

        // Spit out the box's eggs: whole batches, then any partial remainder,
        // so a destroyed cage never swallows eggs it already laid.
        ReleaseEggBatches();
        ReleasePartialEggs();

        if (chickens.Count > 0)
        {
            bool injured = mode != DestroyMode.Deconstruct;
            ReleaseFlock(injured, origin);
            PreserveUnreleasedFlock(injured, origin);
        }

        base.Destroy(mode);
    }

    /// The connected cages that will outlive this one, excluding the cage being
    /// destroyed. Captured before base.Destroy so the cluster is still
    /// discoverable; used to cut intake off after a release.
    IReadOnlyList<Building_ChickenBatteryCage> SurvivingNetwork()
    {
        var survivors = new List<Building_ChickenBatteryCage>();
        if (Map == null)
        {
            return survivors;
        }

        foreach (Building_ChickenBatteryCage cage in CageNetwork.Cluster(this))
        {
            if (cage != null && cage != this && !cage.Destroyed)
            {
                survivors.Add(cage);
            }
        }

        return survivors;
    }

    /// Materializes and releases every housed chicken when the cage itself is
    /// going away. A deconstructed cage is taken apart carefully, so its birds
    /// come out unharmed; a cage wrecked by force spits them out wounded.
    /// Flock-level, so it neither consults nor keeps the pending unload queue.
    /// <paramref name="origin"/> is the releasing network, captured while this
    /// cage was still spawned, and is cut off so the freed birds are not roped
    /// straight back in.
    void ReleaseFlock(bool injured, IReadOnlyList<Building_ChickenBatteryCage> origin)
    {
        if (!Spawned || Map == null || chickens.Count == 0)
        {
            return;
        }

        SettleNutrition();

        Map map = Map;
        EvaluateMortality(force: true);
        IntVec3 near = FindDropCell();
        int released = 0;
        for (int i = chickens.Count - 1; i >= 0; i--)
        {
            Pawn chicken = CageChickenFactory.Generate(chickens[i], map, near);
            if (chicken == null)
            {
                continue;
            }

            chickens.RemoveAt(i);
            if (injured)
            {
                CageChickenInjuries.Injure(chicken);
            }

            released++;
        }

        pendingUnloads.Clear();

        if (released > 0)
        {
            // The flock just spilled onto the map; shut the network's intake so
            // handlers cannot immediately rope it back into a cage.
            CageNetwork.DisableIntakeOnRelease(origin);

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
    /// be deleted just because the cage that held it is gone. The releasing
    /// network is recorded alongside each record so the eventual rescue release
    /// can cut its intake too.
    void PreserveUnreleasedFlock(bool injured, IReadOnlyList<Building_ChickenBatteryCage> origin)
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

        IntVec3 near = FindDropCell();
        int preserved = chickens.Count;
        foreach (CagedChickenRecord record in chickens)
        {
            rescue.Preserve(record, near, injured, origin);
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
        Scribe_Values.Look(ref eggProgress, "eggProgress", 0f);
        Scribe_Values.Look(ref eggCheckedAtTick, "eggCheckedAtTick", 0);
        Scribe_Values.Look(ref mortalityCheckedAtTick, "mortalityCheckedAtTick", 0);
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

        if (!penSystemEnabled)
        {
            sb.AppendLine("ChickenBatteryCage.Inspect.PenSystemOff".Translate());
        }

        int clusterChickens = CageNetwork.ChickenCount(cluster);
        sb.AppendLine("ChickenBatteryCage.Inspect.Chickens".Translate(
            clusterChickens,
            CageNetwork.TotalCapacity(cluster)));

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
        int pendingUnloadCount = CageNetwork.PendingUnloadCount(cluster);
        bool penSystemEnabled = CageNetwork.PenSystemEnabled(cluster);

        Command_Action capacityGizmo = new Command_Action
        {
            defaultLabel = "ChickenBatteryCage.Gizmo.Capacity".Translate(chickenCount, totalCapacity),
            defaultDesc = "ChickenBatteryCage.Gizmo.CapacityDesc".Translate(
                chickenCount,
                totalCapacity),
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
            && penSystemEnabled
            && !IsFull;
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
            if (cage.penSystemEnabled && !cage.IsFull)
            {
                return true;
            }
        }

        return false;
    }

    public static string NoAcceptingCageReason(Map map)
    {
        bool any = false;
        bool anyEnabled = false;
        foreach (Building_ChickenBatteryCage cage in map.listerBuildings.AllBuildingsColonistOfClass<Building_ChickenBatteryCage>())
        {
            any = true;
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
        // Credit the current flock's laying before the newcomer joins, so she
        // is not paid for time she spent outside the cage. Accrual runs before
        // the feed is billed for the same reason as in TickRare.
        AccrueEggProduction();

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

            // Remove this request before settlement reconciles the remaining
            // queue. A death during settlement can satisfy this request too.
            pendingUnloads.RemoveAt(0);
            if (!ReleaseRecordAt(index))
            {
                // Generation failed; keep both the record and the mark.
                pendingUnloads.Insert(0, filter);
                ReconcilePendingUnloads();
                return false;
            }

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

        // Credit the departing bird's laying before she is removed, then settle
        // so her share of the store is billed.
        CagedChickenRecord selected = chickens[index];
        AccrueEggProduction();
        SettleNutrition();
        EvaluateMortality(force: true);
        index = chickens.IndexOf(selected);
        if (index < 0)
        {
            // The selected bird died while settling her accrued exposure.
            return true;
        }

        CagedChickenRecord record = chickens[index];
        IntVec3 near = FindDropCell();
        Pawn released = CageChickenFactory.Generate(record, Map, near);
        if (released == null)
        {
            // The record is retained so the bird is not lost.
            return false;
        }

        chickens.RemoveAt(index);

        // Turn the cluster's intake off: the bird just set down must not be
        // roped straight back into a cage. The player turns it back on with
        // the "Pen system" gizmo once the released flock has cleared.
        CageNetwork.DisableIntakeOnRelease(CageNetwork.Cluster(this));

        ReconcilePendingUnloads();
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
            case ChickenReleaseFilter.All:
                return 0;

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
                    "[ChickenBatteryCage] Chicken pawn kind is unavailable; caged-bird adult/juvenile filters treat every bird as a juvenile until the def resolves.",
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

    public override void TickRare()
    {
        base.TickRare();

        // Accrue laying before nutrition settles: settlement can drain the
        // shared store to zero, and evaluating production against that emptied
        // pool would erase an interval the flock was really fed. Destroy uses
        // the same ordering.
        EvaluateEggProduction();
        SettleNutrition();
        EvaluateMortality();
    }

    /// Accrues exposure before nutrition settlement or population changes.
    internal void AccumulateMortality(int fromTick, int now, double starvingDaysAtStart)
    {
        float lifeExpectancy = ResolveLifeExpectancyYears();
        foreach (CagedChickenRecord record in chickens)
        {
            int start = CageMortalityMath.ExposureStartTick(fromTick, record.enteredAtGameTick);
            record.mortalityExposure += CageMortalityMath.CombinedExposureOverTicks(
                record.BiologicalAgeTicksAt(start), lifeExpectancy,
                starvingDaysAtStart + (start - fromTick) / (double)CagedChickenMath.TicksPerDay,
                now - start);
        }
    }

    /**
     * Adds the eggs laid since the last settlement. Output is a pure function
     * of elapsed time, the age-derived laying rate, and how well fed the flock
     * is, so no hen ever ticks an egg-production component of her own.
     */
    void EvaluateEggProduction()
    {
        AccrueEggProduction();

        // The box is shared across the network, so any member — even one with
        // no birds of its own — may be the one to notice a whole day's stack
        // is ready. Release after every accrual, regardless of whether this
        // tick credited new time: a batch retained by a failed placement must
        // still be retried on the first settlement, a same-tick pass, or a
        // rewound clock. New production stays gated inside accrual.
        ReleaseEggBatches();
    }

    /**
     * Credits the eggs laid since the last settlement into this cage's own box
     * without placing a stack. Accrual is independent of stack release: the
     * guards below only decide whether new output is credited, and a caller
     * that also wants to release a ready stack does so separately, so a
     * retained batch is never gated behind production guards. Separated from
     * <see cref="EvaluateEggProduction"/> so a terminal change such as
     * destruction can settle accrued output exactly once before spilling it.
     */
    void AccrueEggProduction()
    {
        int now = GenTicks.TicksAbs;
        if (eggCheckedAtTick <= 0 || eggCheckedAtTick > now)
        {
            // First settlement, or a clock that moved backwards: seed the
            // marker without crediting a bogus interval.
            eggCheckedAtTick = now;
            return;
        }

        int elapsed = now - eggCheckedAtTick;
        if (elapsed <= 0)
        {
            return;
        }

        eggCheckedAtTick = now;
        if (chickens != null && chickens.Count > 0)
        {
            int fedTicks = FedTicksWithin(elapsed);
            if (fedTicks > 0)
            {
                eggProgress += CageEggMath.EggsOverTicks(EggLayingRatePerDay, fedTicks);
            }
        }
    }

    /**
     * Spits out the eggs the network box has collected, one whole day's stack
     * at a time. The stack is sized to the network's combined daily laying
     * (see <see cref="EggStackSize"/>), so touching cages release a single
     * haulable stack per day rather than one small stack each. Only whole
     * stacks leave the box; the remainder stays and shows up as progress in
     * the inspection readout. If a stack cannot be placed the eggs stay held,
     * so no output is ever lost to a busy map.
     */
    void ReleaseEggBatches()
    {
        IReadOnlyList<Building_ChickenBatteryCage> cluster = CageNetwork.Cluster(this);
        int stack = CageNetwork.EggStackSize(cluster, MinEggsPerStack);
        int batches = CageEggMath.FullBatches(CageNetwork.EggProgress(cluster), stack);
        int released = 0;
        for (int i = 0; i < batches; i++)
        {
            int placed = TryReleaseEggBatch(stack);
            released += placed;
            if (placed < stack)
            {
                // Map was too crowded to place the rest of this batch; keep the
                // eggs in the box and try again on the next settlement.
                break;
            }
        }

        if (released > 0)
        {
            CageNetwork.WithdrawEggs(cluster, released);
        }
    }

    /// Releases one stack of the given size onto the map as a haulable stack.
    /// Returns how many eggs actually landed; the map may take only part of a
    /// split or merged stack. Once placed, any hauler can carry it to a
    /// stockpile like any other egg.
    int TryReleaseEggBatch(int eggs)
    {
        return TryPlaceEggStack(eggs);
    }

    /// Spills whatever whole eggs remain in the box as a smaller stack. Only
    /// used when the cage is going away, so a partial box is not lost.
    void ReleasePartialEggs()
    {
        int held = EggsHeld;
        if (held <= 0)
        {
            return;
        }

        int placed = TryPlaceEggStack(held);
        if (placed > 0)
        {
            eggProgress -= placed;
        }
    }

    /**
     * Where a released egg stack or bird lands. A standable cell the colony
     * can actually reach, so haulers are never sent to an egg on a walled-off
     * or occupied face. The eight neighbours are tried first, then a short
     * radial search.
     */
    public IntVec3 FindDropCell()
    {
        if (!Spawned || Map == null)
        {
            return Position;
        }

        foreach (IntVec3 cell in GenAdj.CellsAdjacent8Way(this))
        {
            if (IsReachableDropCell(cell))
            {
                return cell;
            }
        }

        if (CellFinder.TryFindRandomCellNear(
                Position, Map, 8, IsReachableDropCell, out IntVec3 found))
        {
            return found;
        }

        return Position;
    }

    bool IsReachableDropCell(IntVec3 cell)
    {
        return cell.InBounds(Map)
            && cell.Standable(Map)
            && Map.reachability.CanReachColony(cell);
    }

    /**
     * Places up to <paramref name="eggs"/> onto the map and returns how many
     * actually landed. GenPlace can split an oversized stack or merge part of
     * one into an existing pile and still report failure, so its return value
     * alone cannot be trusted for accounting. The placedAction callback reports
     * each amount that made it onto the map; the caller deducts exactly that
     * many virtual eggs, and the rest stays in the box for the next settlement.
     */
    int TryPlaceEggStack(int eggs)
    {
        if (eggs <= 0 || !Spawned || Map == null || ChickenBatteryCageDefOf.EggChickenUnfertilized == null)
        {
            return 0;
        }

        Thing eggsThing = ThingMaker.MakeThing(ChickenBatteryCageDefOf.EggChickenUnfertilized);
        eggsThing.stackCount = eggs;
        IntVec3 cell = FindDropCell();

        int placed = 0;
        GenPlace.TryPlaceThing(
            eggsThing,
            cell,
            Map,
            ThingPlaceMode.Near,
            (thing, count) => placed += count);

        // Only the unplaced transient remainder lingers here; anything the map
        // accepted is now spawned output and must not be destroyed.
        if (!eggsThing.Destroyed && !eggsThing.Spawned)
        {
            eggsThing.Destroy();
        }

        return placed;
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
            DropCorpse(record);
            died++;
        }

        if (died == 0)
        {
            return;
        }

        // A death can leave an unload mark with no bird left to satisfy it.
        // Prune those here so a stale mark cannot keep reporting an
        // unavailable bird, send a handler to an emptied cage, or hold a
        // request slot the survivors could still use.
        ReconcilePendingUnloads();

        // One aggregated notice per evaluation, however many birds were lost,
        // so a bad die-off never floods the message log.
        Messages.Message(
            "ChickenBatteryCage.Message.Mortality".Translate(died),
            MessageTypeDefOf.NegativeEvent,
            historical: false);
    }

    /// Materializes a freshly dead chicken from her record and drops the body
    /// on the ground outside the cage, so a virtual flock still leaves real
    /// corpses behind. Failed generation or placement is handed to the map's
    /// recovery component, which retries without reviving the dead bird.
    void DropCorpse(CagedChickenRecord record)
    {
        if (!Spawned || Map == null)
        {
            return;
        }

        IntVec3 near = InteractionCell.IsValid ? InteractionCell : Position;
        int diedAtTick = GenTicks.TicksAbs;
        Corpse corpse = CageChickenFactory.GenerateCorpse(record, Map, diedAtTick);
        if (corpse != null && GenPlace.TryPlaceThing(corpse, near, Map, ThingPlaceMode.Near))
        {
            return;
        }

        Map.GetComponent<MapComponent_CagedChickenRescue>().PreserveCorpse(
            record, corpse, near, diedAtTick);
    }

    /**
     * Reconciles the unload queue with the surviving flock after one or more
     * deaths or releases. Marks are kind selectors, not per-bird handles.
     * Keep the earliest jointly satisfiable requests, allowing unrestricted
     * selectors to use whichever birds the restricted requests do not need.
     */
    void ReconcilePendingUnloads()
    {
        int now = GenTicks.TicksAbs;
        int adultHens = 0;
        int juveniles = 0;
        foreach (CagedChickenRecord record in chickens)
        {
            if (!IsAdult(record, now))
            {
                juveniles++;
            }
            else if (record.gender == Gender.Female)
            {
                adultHens++;
            }
        }

        CageUnloadMath.Reconcile(pendingUnloads, chickens.Count, adultHens, juveniles);
    }
}
