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

using RimWorld;
using Verse;
using Verse.AI;

namespace BetterRimworlds.ChickenBatteryCage;

/**
 * The feed rules for battery cages.
 *
 * Feeding uses ordinary colony hauling: a hauler carries an approved feed
 * stack to the cage and tips its nutrition straight into the collective store.
 * The birds stay serialized throughout, and the food itself is consumed on the
 * spot — nothing is ever left sitting on the cage for an animal to eat, and
 * nothing lingers for a hopper-filling job to top back up.
 */
public static class CageFeed
{
    /// Vanilla food defs that make unambiguous animal feed even though their
    /// food category is not a raw plant.
    static readonly string[] NamedFeedDefs = { "Hay", "Kibble" };

    /**
     * Whether a thing is suitable chicken feed. Prepared meals and meat are
     * rejected: a confined flock eats raw plant food, hay, and kibble, and
     * footing a lavish meal into the cage would be an expensive mistake.
     */
    public static bool Accepts(Thing thing)
    {
        if (thing == null || thing.Destroyed || thing.stackCount <= 0)
        {
            return false;
        }

        ThingDef def = thing.def;
        if (def == null || !def.IsNutritionGivingIngestible || def.ingestible == null)
        {
            return false;
        }

        foreach (string name in NamedFeedDefs)
        {
            if (def.defName == name)
            {
                return true;
            }
        }

        return (def.ingestible.foodType & FoodTypeFlags.VegetableOrFruit) != 0;
    }

    public static float NutritionPerUnit(Thing thing)
    {
        if (thing?.def == null)
        {
            return 0f;
        }

        return thing.GetStatValue(StatDefOf.Nutrition);
    }

    /// Nearest reachable, unreserved feed stack that the given pawn may haul
    /// and of which the cage's remaining pool can absorb at least one unit.
    public static Thing FindFeed(Pawn pawn, Building_ChickenBatteryCage cage)
    {
        if (pawn?.Map == null || cage == null)
        {
            return null;
        }

        return GenClosest.ClosestThingReachable(
            pawn.Position,
            pawn.Map,
            ThingRequest.ForGroup(ThingRequestGroup.HaulableEver),
            PathEndMode.ClosestTouch,
            TraverseParms.For(pawn),
            maxDistance: 9999f,
            validator: thing => Accepts(thing)
                && !thing.IsForbidden(pawn)
                && pawn.CanReserve(thing)
                && CageFeedMath.ConsumeUnits(
                    cage.ClusterNutritionSpace,
                    NutritionPerUnit(thing),
                    thing.stackCount) > 0);
    }

    /**
     * Pours as much of a feed stack into a cage's shared pool as fits, then
     * consumes the used units. A stack that is only partly needed keeps the
     * remainder, so no food is ever destroyed above the cage's capacity.
     */
    public static void Feed(Building_ChickenBatteryCage cage, Thing stack)
    {
        if (cage == null || stack == null || stack.Destroyed)
        {
            return;
        }

        float perUnit = NutritionPerUnit(stack);
        if (perUnit <= 0f)
        {
            return;
        }

        cage.SettleNutrition();
        float space = cage.ClusterNutritionSpace;
        int take = CageFeedMath.ConsumeUnits(space, perUnit, stack.stackCount);
        if (take > 0)
        {
            Thing consumed = stack.SplitOff(take);
            cage.AddNutrition(perUnit * take);
            consumed.Destroy();
        }

        // Whether or not this pour took anything, a pool with room for no
        // whole unit must stop asking for feed. Whole-unit pours leave a gap
        // smaller than one unit, so without this the refill request stays set
        // forever and haulers loop on an effectively full cage, dropping food.
        if (CageFeedMath.ConsumeUnits(cage.ClusterNutritionSpace, perUnit, 1) <= 0)
        {
            cage.ClearFeedRequest();
        }
    }
}
