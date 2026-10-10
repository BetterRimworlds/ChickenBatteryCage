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
 * How many dissolving packs must strike one cell before its terrain takes a
 * step.
 *
 * A pack still strikes six cells, the same footprint as one toxic wastepack.
 * The cell itself does not change until three packs have struck it, so
 * terraforming costs three times what a single strike would. The charge
 * clears when the terrain steps, and the next step — sand into stony soil,
 * soil into rich soil — costs another three packs.
 */
public static class FertilizerChargeMath
{
    /// Packs that must strike a cell before it transforms once.
    public const int PacksToTransform = 3;

    /**
     * Records one pack striking a cell that already holds <paramref name="charge"/>
     * earlier strikes. Returns true when this strike is the one that transforms
     * the cell, and writes the charge to keep afterwards. A transforming strike
     * leaves the charge at zero.
     */
    public static bool ApplyHit(int charge, out int nextCharge)
    {
        if (charge < 0)
        {
            charge = 0;
        }

        int next = charge + 1;
        if (next >= PacksToTransform)
        {
            nextCharge = 0;
            return true;
        }

        nextCharge = next;
        return false;
    }
}
