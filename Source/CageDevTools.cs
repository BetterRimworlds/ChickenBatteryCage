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
using Verse;

namespace BetterRimworlds.ChickenBatteryCage;

/**
 * Developer-only stress tools, reachable from a cage's dev gizmos.
 *
 * These exist to make the performance claims testable: fill cages with virtual
 * birds, flood the map with free-range birds or egg stacks, and print the numbers
 * that matter (spawned pawns, live Things, and the estimated savegame cost of
 * the caged flock).
 */
public static class CageDevTools
{
    public static Pawn SpawnHen(Map map, IntVec3 near)
    {
        if (map == null || ChickenBatteryCageDefOf.Chicken == null)
        {
            return null;
        }

        Pawn hen = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
            ChickenBatteryCageDefOf.Chicken,
            Faction.OfPlayer,
            PawnGenerationContext.NonPlayer,
            tile: map.Tile,
            forceGenerateNewPawn: true,
            fixedGender: Gender.Female,
            forceNoIdeo: true));

        if (hen == null)
        {
            return null;
        }

        if (!GenPlace.TryPlaceThing(hen, near, map, ThingPlaceMode.Near))
        {
            hen.Destroy();
            return null;
        }

        return hen;
    }

    /// Fills one cage to capacity by spawning hens and serializing them.
    public static int FillCage(Building_ChickenBatteryCage cage)
    {
        if (cage == null || cage.Map == null)
        {
            return 0;
        }

        IntVec3 near = cage.Position;
        int added = 0;
        while (!cage.IsFull)
        {
            Pawn hen = SpawnHen(cage.Map, near);
            if (hen == null || !cage.TryAcceptChicken(hen))
            {
                if (hen != null && !hen.Destroyed)
                {
                    hen.Destroy();
                }

                break;
            }

            added++;
        }

        return added;
    }

    public static void FillAllCages(Map map)
    {
        if (map == null)
        {
            return;
        }

        int total = 0;
        foreach (Building_ChickenBatteryCage cage in CageNetwork.Cages(map))
        {
            total += FillCage(cage);
        }

        Log.Message("[ChickenBatteryCage] Dev: caged " + total + " additional hens.");
    }

    public static void SpawnFreeRangeChickens(Map map, int count)
    {
        if (map == null)
        {
            return;
        }

        IntVec3 near = Find.CameraDriver != null
            ? Find.CameraDriver.MapPosition
            : map.Center;

        int spawned = 0;
        for (int i = 0; i < count; i++)
        {
            if (SpawnHen(map, near) != null)
            {
                spawned++;
            }
        }

        Log.Message("[ChickenBatteryCage] Dev: spawned " + spawned + " free-range chickens.");
    }

    /// Spawns stacks of eggs representing the given number of eggs.
    public static void SpawnEggStacks(Map map, int eggs)
    {
        if (map == null || ChickenBatteryCageDefOf.EggChickenUnfertilized == null)
        {
            return;
        }

        IntVec3 near = Find.CameraDriver != null
            ? Find.CameraDriver.MapPosition
            : map.Center;

        int perStack = Building_ChickenBatteryCage.MinEggsPerStack;
        int stacks = eggs / perStack;
        int spawned = 0;
        for (int i = 0; i < stacks; i++)
        {
            Thing stack = ThingMaker.MakeThing(ChickenBatteryCageDefOf.EggChickenUnfertilized);
            stack.stackCount = perStack;
            if (GenPlace.TryPlaceThing(stack, near, map, ThingPlaceMode.Near))
            {
                spawned++;
            }
            else if (!stack.Destroyed)
            {
                stack.Destroy();
            }
        }

        Log.Message("[ChickenBatteryCage] Dev: spawned " + spawned + " egg stacks (" +
            spawned * perStack + " eggs).");
    }

    /**
     * Prints the population and save-cost figures used to compare free-range
     * and virtualized flocks. Run it before and after a stress spawn.
     */
    public static void LogPopulationReport(Map map, string label)
    {
        if (map == null)
        {
            return;
        }

        int spawnedChickens = 0;
        IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
        foreach (Pawn pawn in pawns)
        {
            if (Building_ChickenBatteryCage.IsChicken(pawn))
            {
                spawnedChickens++;
            }
        }

        int cages = 0;
        int caged = 0;
        foreach (Building_ChickenBatteryCage cage in CageNetwork.Cages(map))
        {
            cages++;
            caged += cage.ChickenCount;
        }

        long estimatedBytes = CageSaveSize.EstimatedBytes(cages, caged);

        Log.Message(string.Format(
            "[ChickenBatteryCage] {0}: spawned chickens={1}, caged={2}, cages={3}, " +
            "spawned pawns={4}, things={5}, estimated cage save={6} bytes.",
            label,
            spawnedChickens,
            caged,
            cages,
            pawns.Count,
            map.listerThings.AllThings.Count,
            estimatedBytes));
    }
}
