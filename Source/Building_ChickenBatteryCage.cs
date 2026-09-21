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
    /// describe and (later) to roll starvation mortality; it never spawns a
    /// starving Pawn or applies a malnutrition Hediff.
    protected int starvingTicks;

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

    public bool IsOperational => roofedOverOccupiedCells;

    /**
     * Drains the collective store for the time that has passed since the last
     * settlement. Aging-style: no per-chicken food tick exists, the flock's
     * summed demand is applied in one calculation whenever a value is needed.
     */
    public void SettleNutrition()
    {
        int now = GenTicks.TicksAbs;

        // A fresh or pre-fix save has no stored clock; start it now instead of
        // charging the whole game's elapsed time against an empty store.
        if (nutritionSettledAtTick <= 0 || nutritionSettledAtTick > now)
        {
            nutritionSettledAtTick = now;
            return;
        }

        int elapsed = now - nutritionSettledAtTick;
        if (elapsed <= 0 || chickens == null)
        {
            return;
        }

        starvingTicks = CageNutritionMath.StarvingTicksAfter(
            starvingTicks,
            nutritionStored,
            chickens.Count,
            CageNutritionMath.DefaultNutritionPerChickenPerDay,
            elapsed);

        nutritionStored = CageNutritionMath.RemainingAfter(
            nutritionStored,
            chickens.Count,
            CageNutritionMath.DefaultNutritionPerChickenPerDay,
            elapsed);
        nutritionSettledAtTick = now;
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

    /// Summed daily demand of every bird currently housed.
    public float NutritionDemandPerDay => CageNutritionMath.DemandPerDay(
        chickens.Count,
        CageNutritionMath.DefaultNutritionPerChickenPerDay);

    /// Days the flock has spent with an empty store.
    public float StarvingDays => starvingTicks / (float)CagedChickenMath.TicksPerDay;

    /// Fed, hungry, or starving, derived from the collective store alone.
    public CageNutritionState NutritionState => CageNutritionMath.Classify(
        nutritionStored,
        chickens.Count,
        CageNutritionMath.DefaultNutritionPerChickenPerDay,
        StarvingDays);

    /// Adds feed to the collective store and returns how much was accepted.
    /// Anything above capacity is refused rather than silently wasted. Only
    /// <see cref="DrawFromHoppers"/> may call this: hoppers are the sole way
    /// food enters a cage.
    public float AddNutrition(float amount)
    {
        if (amount <= 0f)
        {
            return 0f;
        }

        float accepted = amount < NutritionSpace ? amount : NutritionSpace;
        nutritionStored += accepted;
        return accepted;
    }

    protected virtual string FeedInspectValue => "ChickenBatteryCage.Feed.Status".Translate(
        nutritionStored.ToString("0.#"),
        NutritionCapacity.ToString("0.#"),
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
    }

    public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
    {
        Map map = Map;
        if (map != null)
        {
            map.events.RoofChanged -= OnRoofChanged;
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
        if (this.OccupiedRect().Contains(cell))
        {
            RecheckRoofing();
        }
    }

    public override string GetInspectString()
    {
        SettleNutrition();

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

        sb.AppendLine("ChickenBatteryCage.Inspect.Chickens".Translate(ChickenCount, ChickenCapacity));
        sb.AppendLine("ChickenBatteryCage.Inspect.AdultHens".Translate(AdultHenCount));
        sb.AppendLine("ChickenBatteryCage.Inspect.Juveniles".Translate(JuvenileCount));

        int cageCount = 0;
        foreach (Building_ChickenBatteryCage cage in CageNetwork.Cages(Map))
        {
            cageCount++;
        }

        if (cageCount > 1)
        {
            sb.AppendLine("ChickenBatteryCage.Inspect.Network".Translate(
                CageNetwork.ChickenCount(Map),
                CageNetwork.TotalCapacity(Map),
                cageCount));
        }

        if (HasPendingUnload)
        {
            sb.AppendLine("ChickenBatteryCage.Inspect.PendingUnload".Translate(PendingUnloadCount));
        }

        if (chickens.Count > 0 && !HasAdjacentHopper())
        {
            sb.AppendLine("ChickenBatteryCage.Inspect.NoHopper".Translate());
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

        // Every gizmo below reads from the map-wide cage network, so all cages
        // present identical labels and merge into one control on multi-select.
        Map map = Map;

        // The CageNetwork aggregates each rescan the map's colonist buildings,
        // so gather everything this method displays in a single pass instead of
        // calling ChickenCount/TotalCapacity/... repeatedly for one frame.
        int chickenCount = 0;
        int totalCapacity = 0;
        int adultHenCount = 0;
        int juvenileCount = 0;
        int pendingUnloadCount = 0;
        bool penSystemEnabled = true;
        foreach (Building_ChickenBatteryCage cage in CageNetwork.Cages(map))
        {
            chickenCount += cage.ChickenCount;
            totalCapacity += ChickenCapacity;
            adultHenCount += cage.AdultHenCount;
            juvenileCount += cage.JuvenileCount;
            pendingUnloadCount += cage.PendingUnloadCount;
            if (!cage.PenSystemEnabled)
            {
                penSystemEnabled = false;
            }
        }

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
            isActive = () => CageNetwork.PenSystemEnabled(Map),
            toggleAction = delegate
            {
                CageNetwork.SetPenSystemEnabled(map, !CageNetwork.PenSystemEnabled(map));
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
                FloatMenu menu = new FloatMenu(CageNetwork.BuildUnloadMenu(map));
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
        SettleNutrition();

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
        DrawFromHoppers();
    }

    /// Every vanilla feed hopper touching this cage's edge. The cage network
    /// shares them in the sense that whichever cage a hopper touches, that
    /// hopper feeds the network's flock through the cage it is bolted to.
    public IEnumerable<Building_Storage> AdjacentHoppers()
    {
        if (!Spawned || Map == null)
        {
            yield break;
        }

        foreach (IntVec3 cell in GenAdj.CellsAdjacentCardinal(this))
        {
            if (HopperFeeding.IsHopper(Map.edificeGrid[cell])
                && Map.edificeGrid[cell] is Building_Storage hopper)
            {
                yield return hopper;
            }
        }
    }

    public bool HasAdjacentHopper()
    {
        foreach (Building_Storage _ in AdjacentHoppers())
        {
            return true;
        }
        return false;
    }

    /**
     * Siphons feed out of the attached hoppers into the collective store.
     * This is the only path by which food ever enters a cage: colonists fill
     * the vanilla hoppers with the ordinary hauling pipeline, and the machine
     * does the rest.
     */
    void DrawFromHoppers()
    {
        float space = NutritionSpace;
        if (space <= 0f)
        {
            return;
        }

        foreach (Building_Storage hopper in AdjacentHoppers())
        {
            if (space <= 0f)
            {
                break;
            }

            float delivered = HopperFeeding.TryConsume(hopper, space);
            if (delivered <= 0f)
            {
                continue;
            }

            AddNutrition(delivered);
            space = NutritionSpace;
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
