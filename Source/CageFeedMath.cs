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

namespace BetterRimworlds.ChickenBatteryCage;

/**
 * The pure consumption rules for cage feeding.
 *
 * A cage converts hauled food into its collective nutrition store, so the
 * arithmetic that decides how much of a stack a single pour may take lives
 * here where it can be unit tested headlessly.
 */
public static class CageFeedMath
{
    /**
     * How many whole units a single pour may take from one stack.
     *
     * The cage absorbs only what fits (floored to whole units), and never
     * more than the stack holds. A unit whose nutrition exceeds the remaining
     * space is refused rather than wasted.
     */
    public static int ConsumeUnits(float nutritionSpace, float perUnit, int available)
    {
        if (nutritionSpace <= 0f || perUnit <= 0f || available <= 0)
        {
            return 0;
        }

        int wanted = (int)(nutritionSpace / perUnit);
        return wanted < available ? wanted : available;
    }
}
