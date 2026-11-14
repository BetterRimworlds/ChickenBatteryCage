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
using RimWorld;
using Verse;
using Verse.Noise;

namespace BetterRimworlds.ChickenBatteryCage;

/**
 * Grows fertile terrain outward from a dissolving Fertilizer Pack.
 *
 * This uses vanilla-inspired noise with distance-and-fertility selection. The
 * cell-selection mechanics live in <see cref="FertilizerSpreadMath"/>; this
 * class supplies the three things that need a live map:
 *
 *   - the improved/can-improve predicates, taken from the terrain table and the
 *     block on natural rock roofs;
 *   - the Perlin-noise score, copied from PollutionUtility.PollutionCellComparer
 *     so the patch is irregular exactly the way players recognize from
 *     pollution; and
 *   - the terrain change itself, which a cell earns only on its third strike.
 *
 * Unlike pollution growth, the search crosses unfinished ground too, so nearby
 * soil does not hide poorer terrain. Each terrain step adds effective distance,
 * producing a fertile center and progressively poorer ground toward the edge.
 */
public static class FertilizerSpread
{
    /// Vanilla-inspired noise frequency and bounded edge variation.
    const float PerlinFrequency = 0.015f;
    const float PerlinNoiseFactor = 2f;

    /**
     * Strikes up to <paramref name="cellCount"/> cells, growing outward from
     * <paramref name="root"/>, and returns how many cells were struck. A cell's
     * terrain changes only on its third strike.
     */
    public static int GrowAt(IntVec3 root, Map map, int cellCount)
    {
        return GrowStack(root, map, cellCount, 1);
    }

    /// Reuses connected-ground discovery and noise for every pack in a stack.
    public static int GrowStack(IntVec3 root, Map map, int cellCount, int packs)
    {
        if (map == null || cellCount <= 0 || packs <= 0)
        {
            return 0;
        }

        // One noise field per event, seeded like vanilla's comparer so the
        // same pack placement produces the same ragged shape.
        var perlin = new Perlin(PerlinFrequency, 2.0, 0.5, 6, map.uniqueID, QualityMode.Medium);
        ModuleBase noise = new ScaleBias(0.5, 0.5, perlin);

        var chosen = FertilizerSpreadMath.GrowMany(
            new FertilizerSpreadMath.Cell(root.x, root.z),
            cellCount,
            packs,
            (x, z) => new IntVec3(x, 0, z).InBounds(map),
            (x, z) => IsImprovedCell(new IntVec3(x, 0, z), map),
            (x, z) => CanImproveCell(new IntVec3(x, 0, z), map),
            (x, z) => Score(root, map, noise, new IntVec3(x, 0, z)),
            cell => ApplyImprovement(new IntVec3(cell.X, 0, cell.Z), map));

        return chosen.Count;
    }

    /// Improves a single cell if the rules allow it. Used by the developer
    /// tools; returns true when the terrain changed.
    public static bool ImproveCell(IntVec3 cell, Map map)
    {
        if (map == null || !CanImproveCell(cell, map))
        {
            return false;
        }

        return ApplyImprovement(cell, map);
    }

    /// True when a cell already carries fertile ground. The spread floods
    /// through these cells to reach the poor ground beyond them.
    public static bool IsImprovedCell(IntVec3 cell, Map map)
    {
        if (map == null || !cell.InBounds(map) || HasNaturalRoof(cell, map))
        {
            return false;
        }

        TerrainDef terrain = GroundAt(cell, map);
        return terrain != null && FertilizerTerrainRules.IsImproved(terrain.defName);
    }

    /**
     * True when a cell has ground fertilizer may improve. Floors, buildings,
     * and constructed roofs do not block treatment of the ground underneath.
     * Natural rock roofs block both treatment and traversal, even on rich soil.
     */
    public static bool CanImproveCell(IntVec3 cell, Map map)
    {
        if (map == null || !cell.InBounds(map) || HasNaturalRoof(cell, map))
        {
            return false;
        }

        TerrainDef terrain = GroundAt(cell, map);
        if (terrain == null
            || !FertilizerTerrainRules.CanConvert(
                terrain.defName,
                terrain.natural,
                terrain.IsWater,
                terrain.isFoundation,
                terrain.IsFloor))
        {
            return false;
        }

        return true;
    }

    /// Uses buried ground when a floor or other terrain layer covers it.
    static TerrainDef GroundAt(IntVec3 cell, Map map)
    {
        return map.terrainGrid.UnderTerrainAt(cell) ?? cell.GetTerrain(map);
    }

    static bool HasNaturalRoof(IntVec3 cell, Map map)
    {
        RoofDef roof = map.roofGrid.RoofAt(cell);
        return roof != null && roof.isNatural;
    }

    /// The terrain a cell would become, or null when it cannot be improved.
    static TerrainDef TerrainTargetAt(IntVec3 cell, Map map)
    {
        TerrainDef terrain = GroundAt(cell, map);
        string targetName = terrain == null
            ? null
            : FertilizerTerrainRules.TargetForConvertible(
                terrain.defName,
                terrain.driesTo?.defName);
        if (targetName == null)
        {
            return null;
        }

        TerrainDef target = DefDatabase<TerrainDef>.GetNamedSilentFail(targetName);
        return target ?? TerrainDefOf.Soil;
    }

    /// Records one pack striking a chosen cell. The terrain changes only when
    /// this is the third strike since the cell last transformed.
    static bool ApplyImprovement(IntVec3 cell, Map map)
    {
        if (!CanImproveCell(cell, map))
        {
            return false;
        }

        MapComponent_FertilizerCharge charges = map.GetComponent<MapComponent_FertilizerCharge>();
        if (charges == null || !charges.RecordHit(cell))
        {
            return false;
        }

        TerrainDef target = TerrainTargetAt(cell, map);
        if (target == null)
        {
            return false;
        }

        if (map.terrainGrid.UnderTerrainAt(cell) != null)
        {
            map.terrainGrid.SetUnderTerrain(cell, target);
        }
        else
        {
            map.terrainGrid.SetTerrain(cell, target);
        }
        return true;
    }

    /**
     * Distance dominates globally; poorer terrain wins locally. Bounded noise
     * gives the gradient an irregular edge without overpowering proximity.
     */
    static float Score(IntVec3 root, Map map, ModuleBase noise, IntVec3 cell)
    {
        float variation = Math.Max(0f, Math.Min(1f,
            (float)noise.GetValue(cell.x, cell.y, cell.z)));
        float value = FertilizerTerrainRules.SpreadScore(
            Distance(root, cell) + variation * PerlinNoiseFactor,
            GroundAt(cell, map).defName);

        if (MapGenerator.mapBeingGenerated == map)
        {
            value *= Distance(cell, MapGenerator.PlayerStartSpot) / map.Size.LengthHorizontal;
        }

        return value;
    }

    /// Horizontal distance between two cells. Computed directly so the spread
    /// does not depend on a grid helper.
    static float Distance(IntVec3 a, IntVec3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return (float)Math.Sqrt(dx * dx + dz * dz);
    }
}
