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
 * Def properties for a Fertilizer Pack's decomposition.
 *
 * Spoilage is vanilla CompRottable now, so the temperature curve, the frozen
 * pause, and the inspect hints all come from the game. This comp only carries
 * the one fertilizer-specific number: how much ground a pack improves when the
 * stack finally rots away.
 */
public class CompProperties_FertilizerDissolution : CompProperties
{
    /// Cells struck per pack that dissolves. A cell transforms on its third strike.
    public int cellsImprovedPerPack = CageWasteMath.CellsImprovedPerPack;

    public CompProperties_FertilizerDissolution()
    {
        compClass = typeof(CompFertilizerDissolution);
    }
}
