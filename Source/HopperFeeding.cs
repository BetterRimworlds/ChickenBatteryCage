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
 * Feed extraction from vanilla hoppers.
 *
 * The cage reuses the vanilla hopper (ThingDef "Hopper", Building_Storage with
 * <building><isHopper>true</isHopper>) — the same building the nutrient paste
 * dispenser draws from. Colonists fill it with the ordinary hauling pipeline
 * ("filling hopper" work, just like with the dispenser); the player chooses
 * what it accepts through the standard storage tab. A battery cage draws its
 * nutrition only from hoppers physically adjacent to it.
 */
public static class HopperFeeding
{
    /// True when the edifice on <paramref name="cell"/> is a vanilla hopper.
    public static bool IsHopper(Thing edifice)
    {
        return edifice is Building_Storage storage
            && storage.Spawned
            && storage.def != null
            && storage.def.building != null
            && storage.def.building.isHopper;
    }

    /// Every nutrition-giving stack currently sitting on the hopper.
    public static IEnumerable<Thing> HeldFeed(Building_Storage hopper)
    {
        if (hopper == null || !hopper.Spawned || hopper.Map == null)
        {
            yield break;
        }

        foreach (Thing thing in hopper.Map.thingGrid.ThingsAt(hopper.Position))
        {
            if (thing != hopper && !thing.Destroyed && thing.def != null
                && thing.def.EverHaulable
                && thing.def.IsNutritionGivingIngestible)
            {
                yield return thing;
            }
        }
    }

    /// Total nutrition the hopper currently holds.
    public static float NutritionStored(Building_Storage hopper)
    {
        float total = 0f;
        foreach (Thing stack in HeldFeed(hopper))
        {
            total += PerUnitNutrition(stack) * stack.stackCount;
        }
        return total;
    }

    static float PerUnitNutrition(Thing stack)
    {
        return stack.GetStatValue(StatDefOf.Nutrition);
    }

    /**
     * Consumes up to <paramref name="nutritionSpace"/> worth of feed from the
     * hopper and returns the nutrition actually delivered. The cage calls this
     * from its own rare tick; nothing else may move food out of a hopper.
     */
    public static float TryConsume(Building_Storage hopper, float nutritionSpace)
    {
        if (hopper == null || nutritionSpace <= 0f || !hopper.Spawned)
        {
            return 0f;
        }

        float delivered = 0f;
        foreach (Thing stack in HeldFeed(hopper))
        {
            if (delivered >= nutritionSpace)
            {
                break;
            }

            float perUnit = PerUnitNutrition(stack);
            int take = FeedHopperMath.ConsumeUnits(
                nutritionSpace - delivered, perUnit, stack.stackCount);
            if (take <= 0)
            {
                continue;
            }

            Thing consumed = stack.SplitOff(take);
            consumed.Destroy();
            delivered += perUnit * take;
        }

        return delivered;
    }
}
