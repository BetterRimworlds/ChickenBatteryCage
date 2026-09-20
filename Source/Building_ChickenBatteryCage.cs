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

    readonly List<IntVec3> unroofedCellsScratch = new List<IntVec3>();

    /// The entire confined flock, stored as compact biological records. No
    /// spawned Pawn is kept here.
    protected List<CagedChickenRecord> chickens = new List<CagedChickenRecord>();

    /// Sentinel kept at long.MaxValue while unresolved so that, even if a
    /// caller somehow bypasses the guard in IsAdult, no bird is misread as
    /// an adult before the chicken def's life stages are known.
    static long adultMinAgeTicks = long.MaxValue;

    static bool adultMinAgeTicksResolved;
    static bool warnedMissingChickenDef;

    public bool IsOperational => roofedOverOccupiedCells;

    public bool IsFull => chickens.Count >= ChickenCapacity;

    public virtual int ChickenCount => chickens.Count;

    public virtual int AdultHenCount => CountMatching(Gender.Female, adult: true);

    public virtual int JuvenileCount => CountMatching(Gender.None, adult: false, anyGender: true);

    protected virtual string FeedInspectValue => "ChickenBatteryCage.Inspect.Empty".Translate();

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

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref chickens, "chickens", LookMode.Deep);

        if (Scribe.mode == LoadSaveMode.PostLoadInit && chickens == null)
        {
            chickens = new List<CagedChickenRecord>();
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

        sb.AppendLine("ChickenBatteryCage.Inspect.Chickens".Translate(ChickenCount, ChickenCapacity));
        sb.AppendLine("ChickenBatteryCage.Inspect.AdultHens".Translate(AdultHenCount));
        sb.AppendLine("ChickenBatteryCage.Inspect.Juveniles".Translate(JuvenileCount));
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

        Command_Action capacityGizmo = new Command_Action
        {
            defaultLabel = "ChickenBatteryCage.Gizmo.Capacity".Translate(ChickenCount, ChickenCapacity),
            defaultDesc = "ChickenBatteryCage.Gizmo.CapacityDesc".Translate(
                ChickenCount,
                ChickenCapacity,
                AdultHenCount,
                JuvenileCount),
            icon = def.uiIcon,
            action = delegate { },
        };
        capacityGizmo.Disable("ChickenBatteryCage.Gizmo.CapacityDisabled".Translate());
        yield return capacityGizmo;

        Command_Action releaseGizmo = new Command_Action
        {
            defaultLabel = "ChickenBatteryCage.Gizmo.Release".Translate(),
            defaultDesc = "ChickenBatteryCage.Gizmo.ReleaseDesc".Translate(),
            icon = def.uiIcon,
            groupable = false,
            action = delegate
            {
                FloatMenu menu = new FloatMenu(BuildReleaseMenu());
                menu.vanishIfMouseDistant = false;
                Find.WindowStack.Add(menu);
            },
        };
        if (ChickenCount == 0)
        {
            releaseGizmo.Disable("ChickenBatteryCage.Gizmo.ReleaseEmpty".Translate());
        }
        yield return releaseGizmo;
    }

    List<FloatMenuOption> BuildReleaseMenu()
    {
        var options = new List<FloatMenuOption>();
        foreach (ChickenReleaseFilter filter in (ChickenReleaseFilter[])Enum.GetValues(typeof(ChickenReleaseFilter)))
        {
            ChickenReleaseFilter local = filter;
            bool available = FindRecordIndex(local) >= 0;
            FloatMenuOption option = new FloatMenuOption(
                ReleaseFilterLabel(local),
                available ? (Action)(() => TryReleaseChicken(local)) : null);
            if (!available)
            {
                option.Disabled = true;
            }
            options.Add(option);
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

    public bool CanAcceptChicken(Pawn chicken)
    {
        return IsHen(chicken)
            && IsOperational
            && !IsFull
            && !CageHenReleaseMemory.IsRecentlyReleased(chicken);
    }

    public static bool AnyAcceptingCage(Map map)
    {
        if (map == null)
        {
            return false;
        }

        foreach (Building_ChickenBatteryCage cage in map.listerBuildings.AllBuildingsColonistOfClass<Building_ChickenBatteryCage>())
        {
            if (cage.IsOperational && !cage.IsFull)
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
        foreach (Building_ChickenBatteryCage cage in map.listerBuildings.AllBuildingsColonistOfClass<Building_ChickenBatteryCage>())
        {
            any = true;
            if (!cage.IsOperational)
            {
                continue;
            }

            anyOperational = true;
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

            if (!handler.CanReach(cage, PathEndMode.InteractionCell, Danger.Deadly))
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
        // Check the primary interaction cell
        if (cage.InteractionCell.IsValid && cage.IsGoodStandCell(cage.InteractionCell, handler))
        {
            return true;
        }

        // Check adjacent cells
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

        if (IsGoodStandCell(InteractionCell, handler))
        {
            return InteractionCell;
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
        chickens.Add(record);
    }

    void TryReleaseChicken(ChickenReleaseFilter filter)
    {
        try
        {
            if (ReleaseChicken(filter))
            {
                return;
            }
        }
        catch (Exception ex)
        {
            Log.Error("[ChickenBatteryCage] Exception while releasing a chicken: " + ex);
        }

        Messages.Message(
            "ChickenBatteryCage.Message.ReleaseFailed".Translate(),
            this,
            MessageTypeDefOf.RejectInput,
            historical: false);
    }

    public bool ReleaseChicken(ChickenReleaseFilter filter)
    {
        int index = FindRecordIndex(filter);
        if (index < 0)
        {
            return false;
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

    int FindRecordIndex(ChickenReleaseFilter filter)
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
