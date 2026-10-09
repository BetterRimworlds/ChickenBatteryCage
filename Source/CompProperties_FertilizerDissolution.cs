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

using Verse;

namespace BetterRimworlds.ChickenBatteryCage;

/**
 * Def properties for a Fertilizer Pack's slow decomposition.
 *
 * This mirrors the shape of vanilla CompProperties_Dissolution — a base number
 * of days and optional indoor and rain factors — but the defaults keep both
 * factors at one, so the plain ten-day-per-pack-per-stack baseline holds
 * wherever the pack rests.
 */
public class CompProperties_FertilizerDissolution : CompProperties
{
    /// Days one pack in a stack takes to decompose.
    public float dissolutionAfterDays = CageWasteMath.DecompositionDaysPerPack;

    /// Multiplier on the interval while indoors. One keeps the baseline.
    public float indoorsFactor = 1f;

    /// Multiplier on the interval while being rained on. One keeps the baseline.
    public float rainFactor = 1f;

    /// Terrain cells improved per pack that dissolves.
    public int cellsImprovedPerPack = CageWasteMath.CellsImprovedPerPack;

    public CompProperties_FertilizerDissolution()
    {
        compClass = typeof(CompFertilizerDissolution);
    }
}
