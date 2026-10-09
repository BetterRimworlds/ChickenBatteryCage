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
using RimWorld;
using UnityEngine;
using Verse;

namespace BetterRimworlds.ChickenBatteryCage;

/**
 * Empties a dirty cage and turns the collected manure into Fertilizer Packs.
 *
 * A cleaner performs one service operation: every whole pack-equivalent the
 * flock has accumulated is removed and dropped beside the cage as an ordinary
 * haulable stack, and the fractional remainder stays in the cage so no manure
 * is lost between visits. Only whole packs change hands, exactly as only whole
 * eggs leave an egg box.
 */
public static class CageCleaning
{
    /// Cleans a cage's whole touching cluster once. Returns how many packs were
    /// produced and dropped.
    public static int Clean(Building_ChickenBatteryCage cage)
    {
        if (cage == null || !cage.Spawned || cage.Map == null)
        {
            return 0;
        }

        IReadOnlyList<Building_ChickenBatteryCage> cluster = CageNetwork.Cluster(cage);

        // Bring every member's waste up to now before measuring, exactly as
        // feeding settles the shared store before it changes.
        cage.SettleNutrition();

        float waste = CageNetwork.WastePacks(cluster);
        int whole = CageWasteMath.WholePacks(waste);
        if (whole <= 0)
        {
            return 0;
        }

        int placed = PlacePacks(cage, whole);
        if (placed > 0)
        {
            // Remove only what actually landed, so a crowded map never swallows
            // manure. The fractional remainder is preserved.
            CageNetwork.WithdrawWaste(cluster, placed);
        }

        return placed;
    }

    /// Drops <paramref name="count"/> packs beside the cage in stacks no larger
    /// than the def's stack limit. Returns how many actually landed.
    static int PlacePacks(Building_ChickenBatteryCage cage, int count)
    {
        ThingDef def = ChickenBatteryCageDefOf.CBC_FertilizerPack;
        if (def == null)
        {
            return 0;
        }

        int limit = Mathf.Max(1, def.stackLimit);
        IntVec3 cell = cage.FindDropCell();
        Map map = cage.Map;

        int remaining = count;
        int placed = 0;
        while (remaining > 0)
        {
            int size = Mathf.Min(limit, remaining);
            Thing pack = ThingMaker.MakeThing(def);
            pack.stackCount = size;

            int landed = 0;
            GenPlace.TryPlaceThing(
                pack,
                cell,
                map,
                ThingPlaceMode.Near,
                (thing, amount) => landed += amount);

            // Only an unplaced transient remainder lingers here; anything the
            // map accepted is now spawned output and must not be destroyed.
            if (!pack.Destroyed && !pack.Spawned)
            {
                pack.Destroy();
            }

            if (landed <= 0)
            {
                break;
            }

            placed += landed;
            remaining -= landed;

            if (landed < size)
            {
                // The map could not take the whole stack; stop rather than
                // looping on a full cell. What is left stays in the cage.
                break;
            }
        }

        return placed;
    }
}
