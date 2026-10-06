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
 * Migration guards for caged chicken records.
 *
 * A record loaded from an old or damaged save must never brick the colony. The
 * cheap problems are clamped back into range; a genuinely impossible record
 * (an absurd age, say) is reported so the caller can drop just that bird and
 * keep the rest of the save.
 */
public static class CagedChickenValidation
{
    /// Ages beyond this are treated as corrupt rather than merely old. It is
    /// far past any plausible RimWorld colony, so no real bird is ever lost.
    public const long MaxPlausibleAgeTicks = 200L * CagedChickenMath.TicksPerYear;

    /**
     * Repairs what can be repaired and reports whether the record survives.
     *
     * A future entry tick or a negative value is clamped; only an impossible
     * age causes a drop.
     */
    public static bool TryRepair(ref long ageTicksAtEntry, ref int enteredAtGameTick, int now)
    {
        if (enteredAtGameTick < 0 && now >= 0)
        {
            // A negative tick before the counter has wrapped is corrupt.
            enteredAtGameTick = 0;
        }
        else if (CagedChickenMath.IsFutureTick(enteredAtGameTick, now))
        {
            // A future tick is impossible; pull it back to now. The wrapping
            // comparison keeps a legitimate pre-wrap tick from being reset
            // once the counter has wrapped negative, which would erase the
            // bird's elapsed caged aging.
            enteredAtGameTick = now;
        }

        if (ageTicksAtEntry < 0)
        {
            ageTicksAtEntry = 0;
        }

        return ageTicksAtEntry <= MaxPlausibleAgeTicks;
    }
}
