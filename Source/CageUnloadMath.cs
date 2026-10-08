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

using System.Collections;

namespace BetterRimworlds.ChickenBatteryCage;

/// Verse-free pruning of the exact-record unload queue.
public static class CageUnloadMath
{
    /**
     * Drops marks whose bird is no longer in the surviving flock. Every mark
     * names one exact record, so a mark survives only while that record is
     * still housed; a dead bird's stale mark must be dropped before any
     * capacity test, or it would consume a slot a living mark needs.
     */
    public static void Reconcile(IList marks, IList surviving)
    {
        for (int i = marks.Count - 1; i >= 0; i--)
        {
            if (!surviving.Contains(marks[i]))
            {
                marks.RemoveAt(i);
            }
        }
    }
}
