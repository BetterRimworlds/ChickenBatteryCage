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

namespace BetterRimworlds.ChickenBatteryCage;

/// Verse-free reconciliation of kind-selector requests with a surviving flock.
public static class CageUnloadMath
{
    /**
     * Prunes requests that can no longer be satisfied. When a parallel
     * <paramref name="targets"/> list is supplied, the entry aligned with a
     * dropped request is dropped too, so a specific mark never loses the
     * record it points at. A specific mark survives only while the record it
     * names is still in <paramref name="surviving"/>; a dead bird's stale mark
     * must be dropped before the capacity test, or it would consume a slot
     * that a living mark needs.
     */
    public static void Reconcile(
        IList<ChickenReleaseFilter> requests,
        int birds,
        int adultHens,
        int juveniles,
        System.Collections.IList targets = null,
        System.Collections.IList surviving = null)
    {
        int kept = 0;
        int adultsRequested = 0;
        int juvenilesRequested = 0;
        for (int i = 0; i < requests.Count;)
        {
            ChickenReleaseFilter filter = requests[i];
            bool available = kept < birds;
            switch (filter)
            {
                case ChickenReleaseFilter.AdultHen:
                    available &= adultsRequested < adultHens;
                    break;
                case ChickenReleaseFilter.Juvenile:
                    available &= juvenilesRequested < juveniles;
                    break;
                case ChickenReleaseFilter.Random:
                case ChickenReleaseFilter.Youngest:
                case ChickenReleaseFilter.Oldest:
                case ChickenReleaseFilter.All:
                    break;
                case ChickenReleaseFilter.Specific:
                    // Validate the exact bird before the capacity test: a
                    // target that died (or is missing) must not keep a slot.
                    available &= targets != null
                        && surviving != null
                        && i < targets.Count
                        && surviving.Contains(targets[i]);
                    break;
                default:
                    available = false;
                    break;
            }

            if (!available)
            {
                requests.RemoveAt(i);
                if (targets != null && i < targets.Count)
                {
                    targets.RemoveAt(i);
                }
                continue;
            }

            if (filter == ChickenReleaseFilter.AdultHen) adultsRequested++;
            if (filter == ChickenReleaseFilter.Juvenile) juvenilesRequested++;
            kept++;
            i++;
        }
    }
}
