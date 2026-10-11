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

using Verse;

namespace BetterRimworlds.ChickenBatteryCage;

/**
 * Compile-time switch for fertilizer dissolution.
 *
 * <c>./build.sh fast</c> passes <c>-p:FastDecomposition=true</c>, and the
 * project turns that into <c>CBC_FAST_DECOMPOSITION</c>. A normal build leaves
 * the constant false. The clock itself is the wastepack dissolution timer;
 * fast only shortens the outdoor base from eight days to one and keeps the
 * indoor and rain factors.
 */
public static class FertilizerFastDecomposition
{
#if CBC_FAST_DECOMPOSITION
    public const bool Enabled = true;
#else
    public const bool Enabled = false;
#endif

    public const string FertilizerDefName = "CBC_FertilizerPack";
}

#if CBC_FAST_DECOMPOSITION
/// Says, once defs exist, that this build is not the eight-day release behaviour.
[StaticConstructorOnStartup]
public static class FertilizerFastDecompositionStartup
{
    static FertilizerFastDecompositionStartup()
    {
        ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(FertilizerFastDecomposition.FertilizerDefName);
        if (def == null)
        {
            return;
        }

        def.description +=
            "\n\nThis development build dissolves one pack per day outdoors, or every twelve hours while rain or snow falls outdoors. Indoors takes two days. Freezing stops it.";
    }
}
#endif
