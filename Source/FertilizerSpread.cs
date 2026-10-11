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
 * This is the map-facing twin of vanilla PollutionUtility.GrowPollutionAt. The
 * cell-selection mechanics live in <see cref="FertilizerSpreadMath"/>; this
 * class supplies the three things that need a live map:
 *
 *   - the improved/can-improve predicates, taken from the terrain table and the
 *     block on any building edifice;
 *   - the Perlin-noise score, copied from PollutionUtility.PollutionCellComparer
 *     so the patch is irregular exactly the way players recognize from
 *     pollution; and
 *   - the terrain change itself, which a cell earns only on its third strike.
 *
 * Why the algorithm is reproduced rather than reused: GrowPollutionAt is hard
 * wired to map.pollutionGrid and sets a pollution flag on every cell it visits.
 * There is no hook to make it write terrain instead, and fertilizer must never
 * create pollution. The selection behaviour is therefore reproduced as
 * narrowly as possible — same flood-through-saturated step, same best-first
 * frontier, same Perlin score — with the flag swapped for a terrain
 * transformation.
 */
public static class FertilizerSpread
{
    /// Vanilla's noise frequency and shape constants, copied verbatim so the
    /// fertilizer blobs look like pollution blobs.
    const float PerlinFrequency = 0.015f;
    const float NoisyEdgeFactor = 0.25f;
    const float PerlinNoiseFactor = 2f;

    /**
     * Strikes up to <paramref name="cellCount"/> cells, growing outward from
     * <paramref name="root"/>, and returns how many cells were struck. A cell's
     * terrain changes only on its third strike.
     */
    public static int GrowAt(IntVec3 root, Map map, int cellCount)
    {
        if (map == null || cellCount <= 0)
        {
            return 0;
        }

        // One noise field per event, seeded like vanilla's comparer so the
        // same pack placement produces the same ragged shape.
        var perlin = new Perlin(PerlinFrequency, 2.0, 0.5, 6, map.uniqueID, QualityMode.Medium);
        ModuleBase noise = new ScaleBias(0.5, 0.5, perlin);

        var chosen = FertilizerSpreadMath.GrowAt(
            new FertilizerSpreadMath.Cell(root.x, root.z),
            cellCount,
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
        TerrainDef terrain = cell.GetTerrain(map);
        return terrain != null && FertilizerTerrainRules.IsImproved(terrain.defName);
    }

    /**
     * True when a cell is natural, unconstructed ground the table knows how to
     * improve. Constructed floors, bridges, roads, water, and every building —
     * including natural rock walls — fail this test, so the spread can neither
     * see nor replace them, mirroring how a natural rock wall blocks vanilla
     * pollution.
     */
    public static bool CanImproveCell(IntVec3 cell, Map map)
    {
        if (!cell.InBounds(map))
        {
            return false;
        }

        TerrainDef terrain = cell.GetTerrain(map);
        if (terrain == null || !terrain.natural || terrain.IsWater || terrain.isFoundation)
        {
            return false;
        }

        if (FertilizerTerrainRules.TargetFor(terrain.defName) == null)
        {
            return false;
        }

        // Any building on the cell stops the spread: natural rock walls and
        // player-built structures are both boundaries.
        return cell.GetEdifice(map) == null;
    }

    /// The terrain a cell would become, or null when it cannot be improved.
    static TerrainDef TerrainTargetAt(IntVec3 cell, Map map)
    {
        TerrainDef terrain = cell.GetTerrain(map);
        string targetName = terrain == null ? null : FertilizerTerrainRules.TargetFor(terrain.defName);
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

        map.terrainGrid.SetTerrain(cell, target);
        return true;
    }

    /**
     * The vanilla PollutionCellComparer score, with "polluted" read as
     * "improved": nearer cells, cells the Perlin field favours, and cells that
     * already have improved neighbours rank higher, which is what gives the
     * patch its ragged, cohesive edge. Higher is served first.
     */
    static float Score(IntVec3 root, Map map, ModuleBase noise, IntVec3 cell)
    {
        float distance = Distance(root, cell);
        if (distance <= 0.0001f)
        {
            distance = 0.0001f;
        }

        float value = 1f / distance;
        value *= 1f + (float)noise.GetValue(cell.x, cell.y, cell.z) * PerlinNoiseFactor;

        if (MapGenerator.mapBeingGenerated == map)
        {
            value *= Distance(cell, MapGenerator.PlayerStartSpot) / map.Size.LengthHorizontal;
        }

        return value * (1f + AdjacentImprovedCount(cell, map) / 8f * NoisyEdgeFactor);
    }

    static int AdjacentImprovedCount(IntVec3 cell, Map map)
    {
        int count = 0;
        foreach (IntVec3 offset in GenAdj.AdjacentCells)
        {
            IntVec3 neighbour = cell + offset;
            if (neighbour.InBounds(map) && IsImprovedCell(neighbour, map))
            {
                count++;
            }
        }

        return count;
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
