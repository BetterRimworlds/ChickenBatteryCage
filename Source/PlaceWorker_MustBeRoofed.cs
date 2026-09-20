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

public class PlaceWorker_MustBeRoofed : PlaceWorker
{
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
            if (!cell.InBounds(map) || !map.roofGrid.Roofed(cell))
            {
                return new AcceptanceReport("ChickenBatteryCage.MustBeRoofed".Translate());
            }
        }

        return true;
    }
}
