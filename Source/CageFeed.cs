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
     *
     * RimWorld 1.6 moved raw rice to the Seed food type (it doubles as
     * sowing stock), so seed-class plant food must be accepted too or a rice
     * farm cannot feed its cages at all.
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

        if (IsForbiddenFeed(def))
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

        // With the ban lifted, dairy (milk) is feed even though it is neither a
        // named feed nor a raw plant; otherwise the setting would do nothing.
        if (IsDairy(def))
        {
            return true;
        }

        const FoodTypeFlags plantFood =
            FoodTypeFlags.VegetableOrFruit | FoodTypeFlags.Seed;
        return (def.ingestible.foodType & plantFood) != 0;
    }

    /**
     * True when a food is banned from the cage's feed. Dairy is refused by
     * default: chickens are not mammals and cannot digest lactose, so milk
     * must never be accepted as feed. The ban can be lifted in mod settings.
     */
    public static bool IsForbiddenFeed(ThingDef def)
    {
        Settings settings = ChickenBatteryCage.Settings;
        if (settings != null && !settings.forbidDairyInFeed)
        {
            return false;
        }

        return MatchesDairySignature(def, settings);
    }

    /// True when a def carries the dairy signature (an animal-product fluid, or
    /// a listed name), whether or not the ban currently refuses it.
    public static bool IsDairy(ThingDef def)
    {
        return MatchesDairySignature(def, ChickenBatteryCage.Settings);
    }

    /// Whether a def matches the dairy classifier. Both <see cref="IsDairy"/>
    /// and <see cref="IsForbiddenFeed"/> share this so their food-type flags and
    /// def-list handling cannot drift apart.
    static bool MatchesDairySignature(ThingDef def, Settings settings)
    {
        if (def?.ingestible == null)
        {
            return false;
        }

        FoodTypeFlags foodType = def.ingestible.foodType;
        bool isAnimalProduct = (foodType & FoodTypeFlags.AnimalProduct) != 0;
        bool isFluid = (foodType & FoodTypeFlags.Fluid) != 0;

        return DairyFeedRules.IsForbidden(
            def.defName, isAnimalProduct, isFluid, settings?.ExtraForbiddenFeedDefNames);
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

        // The stack was chosen before the haul began, so re-check it: the feed
        // rules (the dairy ban) can change while the hauler is in transit.
        if (!Accepts(stack))
        {
            return;
        }

        float perUnit = NutritionPerUnit(stack);
        if (perUnit <= 0f)
        {
            return;
        }

        // Settle laying on every member before the shared store changes. The
        // pool is shared, so settling only the receiving cage would leave the
        // others holding an interval that spans the empty stretch, which they
        // would later credit as fed once the refill landed.
        foreach (Building_ChickenBatteryCage member in CageNetwork.Cluster(cage))
        {
            if (member != null && !member.Destroyed)
            {
                member.SettleEggProduction();
            }
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
