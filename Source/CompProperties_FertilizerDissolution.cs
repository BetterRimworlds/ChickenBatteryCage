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
 * Def properties for a Fertilizer Pack's dissolution.
 *
 * The numbers match a toxic wastepack's <c>CompProperties_Dissolution</c>:
 * one pack every eight days outdoors, half that speed indoors, and twice
 * that speed in rain or snow. Cells struck per pack is the beneficial
 * opposite of the wastepack's six polluted cells.
 */
public class CompProperties_FertilizerDissolution : CompProperties
{
    /// Days between dissolving one pack outdoors in fair weather.
    public float dissolutionAfterDays = FertilizerDissolutionMath.DaysToDissolve;

    /// Divides the interval while the pack is not in an outdoor-temperature room.
    public float dissolutionFactorIndoors = FertilizerDissolutionMath.IndoorFactor;

    /// Divides the interval while rain or snow is falling on an outdoor pack.
    public float dissolutionFactorRain = FertilizerDissolutionMath.RainFactor;

    /// Cells struck per pack that dissolves. A cell transforms on its third strike.
    public int cellsImprovedPerPack = CageWasteMath.CellsImprovedPerPack;

    public CompProperties_FertilizerDissolution()
    {
        compClass = typeof(CompFertilizerDissolution);
    }
}
