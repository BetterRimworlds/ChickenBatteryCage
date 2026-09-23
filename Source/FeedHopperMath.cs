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
 * The pure consumption rules for a feed hopper.
 *
 * A cage can only be fed through an attached hopper, so the math that turns
 * hopper contents into cage nutrition lives here where it can be unit tested
 * headlessly.
 */
public static class FeedHopperMath
{
    /**
     * How many units a pour may take from one stack.
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
