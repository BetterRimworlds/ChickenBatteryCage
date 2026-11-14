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
using UnityEngine;
using Verse;

namespace BetterRimworlds.ChickenBatteryCage;

public class PlaceWorker_MustBeRoofed : PlaceWorker
{
    static readonly List<IntVec3> FootprintCells = new List<IntVec3>();
    static readonly List<IntVec3> UnroofedCells = new List<IntVec3>();

    static readonly Color UnroofedColor = new Color(1f, 0.55f, 0.15f, 0.85f);

    public override AcceptanceReport AllowsPlacing(
        BuildableDef checkingDef,
        IntVec3 loc,
        Rot4 rot,
        Map map,
        Thing thingToIgnore = null,
        Thing thing = null)
    {
        foreach (IntVec3 cell in GenAdj.OccupiedRect(loc, rot, checkingDef.Size))
        {
            if (!cell.InBounds(map))
            {
                // Let core placement rules handle out-of-bounds as an invalid location.
                return false;
            }

            if (!map.roofGrid.Roofed(cell))
            {
                return new AcceptanceReport("ChickenBatteryCage.MustBeRoofed".Translate());
            }
        }

        return true;
    }

    public override void DrawGhost(
        ThingDef def,
        IntVec3 center,
        Rot4 rot,
        Color ghostCol,
        Thing thing = null)
    {
        Map map = Find.CurrentMap;
        if (map == null || def == null)
        {
            return;
        }

        FootprintCells.Clear();
        UnroofedCells.Clear();
        foreach (IntVec3 cell in GenAdj.OccupiedRect(center, rot, def.Size))
        {
            FootprintCells.Add(cell);
            if (cell.InBounds(map) && !map.roofGrid.Roofed(cell))
            {
                UnroofedCells.Add(cell);
            }
        }

        // The 4-tier sprite ghost is a holey silhouette, so the occupied cells
        // are traced as field edges. Skip that box once the cage is built; the
        // selection brackets already mark it. Unroofed cells stay highlighted
        // after construction so a missing roof is still visible.
        if (thing == null && FootprintCells.Count > 0)
        {
            Color outline = ghostCol;
            if (outline.a < 0.8f)
            {
                outline.a = 0.8f;
            }

            GenDraw.DrawFieldEdges(FootprintCells, outline);
        }

        if (UnroofedCells.Count > 0)
        {
            GenDraw.DrawFieldEdges(UnroofedCells, UnroofedColor);
        }
    }
}
