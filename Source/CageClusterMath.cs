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
using System.Collections.Generic;

namespace BetterRimworlds.ChickenBatteryCage;

/**
 * The Verse-free arithmetic behind a cluster of touching battery cages.
 *
 * Touching cages act as one giant cage: they share a single feed pool even
 * though every cage still owns its own persisted store. This class only
 * spreads an aggregate amount back over the members' own stores, so the save
 * format stays per-cage and no shared pool has to be serialized.
 */
public static class CageClusterMath
{
    /**
     * Spreads <paramref name="total"/> across the members, proportionally to
     * each member's storage capacity, writing the per-member amounts into
     * <paramref name="result"/> (one entry per capacity). No member is ever
     * given more than its own capacity, and the sum of the result equals
     * <paramref name="total"/> whenever that total fit within the capacities.
     *
     * Spreading proportionally rather than filling the first cage first keeps
     * the feed sensibly distributed, so an unequal load or a cage leaving the
     * cluster never strands or duplicates the whole store on one member.
     */
    public static void Distribute(
        float total,
        IReadOnlyList<float> capacities,
        IList<float> result)
    {
        int count = capacities?.Count ?? 0;
        if (result == null || count == 0)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            result[i] = 0f;
        }

        if (total <= 0f)
        {
            return;
        }

        float sumCapacity = 0f;
        for (int i = 0; i < count; i++)
        {
            float capacity = capacities[i];
            if (capacity > 0f)
            {
                sumCapacity += capacity;
            }
        }

        if (sumCapacity <= 0f)
        {
            return;
        }

        // A total at or above capacity simply fills every member.
        if (total >= sumCapacity)
        {
            for (int i = 0; i < count; i++)
            {
                result[i] = capacities[i] > 0f ? capacities[i] : 0f;
            }
            return;
        }

        float assigned = 0f;
        for (int i = 0; i < count; i++)
        {
            float capacity = capacities[i];
            if (capacity <= 0f)
            {
                continue;
            }

            float share = total * (capacity / sumCapacity);
            float amount = share < capacity ? share : capacity;
            result[i] = amount;
            assigned += amount;
        }

        // Proportional shares rarely sum bit-exactly to the total; hand the
        // tiny remainder to whichever members still have room.
        float leftover = total - assigned;
        for (int i = 0; i < count && leftover > 0f; i++)
        {
            float room = capacities[i] - result[i];
            if (room <= 0f)
            {
                continue;
            }

            float amount = room < leftover ? room : leftover;
            result[i] += amount;
            leftover -= amount;
        }
    }
}
