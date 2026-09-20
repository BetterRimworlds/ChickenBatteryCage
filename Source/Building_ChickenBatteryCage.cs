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
using System.Text;
using UnityEngine;
using Verse;

namespace BetterRimworlds.ChickenBatteryCage;

public class Building_ChickenBatteryCage : Building
{
    public const int ChickenCapacity = 10;

    bool roofedOverOccupiedCells = true;

    readonly List<IntVec3> unroofedCellsScratch = new List<IntVec3>();

    public bool IsOperational => roofedOverOccupiedCells;

    public virtual int ChickenCount => 0;

    public virtual int AdultHenCount => 0;

    public virtual int AdultRoosterCount => 0;

    public virtual int JuvenileCount => 0;

    protected virtual string FeedInspectValue => "ChickenBatteryCage.Inspect.Empty".Translate();

    protected virtual string EggsInspectValue => "ChickenBatteryCage.Inspect.Empty".Translate();

    public override void SpawnSetup(Map map, bool respawningAfterLoad)
    {
        base.SpawnSetup(map, respawningAfterLoad);
        RecheckRoofing();
    }

    public override void TickRare()
    {
        base.TickRare();
        RecheckRoofing();
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

        sb.Append("ChickenBatteryCage.Inspect.RequiresRoof".Translate());
        if (!IsOperational)
        {
            sb.AppendLine();
            sb.Append("ChickenBatteryCage.Inspect.Unroofed".Translate());
        }

        sb.AppendLine();
        sb.AppendLine("ChickenBatteryCage.Inspect.Chickens".Translate(ChickenCount, ChickenCapacity));
        sb.AppendLine("ChickenBatteryCage.Inspect.AdultHens".Translate(AdultHenCount));
        sb.AppendLine("ChickenBatteryCage.Inspect.AdultRoosters".Translate(AdultRoosterCount));
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
                AdultRoosterCount,
                JuvenileCount),
            icon = def.uiIcon,
            action = delegate { },
        };
        capacityGizmo.Disable("ChickenBatteryCage.Gizmo.CapacityDisabled".Translate());
        yield return capacityGizmo;
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
