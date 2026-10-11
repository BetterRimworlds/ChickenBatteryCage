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

using HarmonyLib;
using RimWorld;
using Verse;

namespace BetterRimworlds.ChickenBatteryCage;

/**
 * Compile-time switch for fertilizer spoilage.
 *
 * <c>./build.sh fast</c> passes <c>-p:FastDecomposition=true</c>, and the
 * project turns that into <c>CBC_FAST_DECOMPOSITION</c>. A normal build leaves
 * the constant false and does not patch vanilla rot.
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
/**
 * Keeps vanilla rot from also running on a fast build. The ten-day food timer
 * would otherwise outlast the one-day clock and still show a refrigerated
 * countdown. Skipping it here, instead of setting <c>CompRottable.disabled</c>,
 * leaves that flag unset so a save written by this build still rots normally
 * when loaded in a normal build.
 */
[HarmonyPatch(typeof(CompRottable), "TickInterval")]
public static class Patch_FertilizerFastRotTick
{
    static bool Prefix(CompRottable __instance)
    {
        return !IsFertilizer(__instance);
    }

    internal static bool IsFertilizer(CompRottable rottable)
    {
        return rottable?.parent?.def?.defName == FertilizerFastDecomposition.FertilizerDefName;
    }
}

/// Hides the ten-day rot line so the fast clock's own inspect string is the one
/// a player reads.
[HarmonyPatch(typeof(CompRottable), nameof(CompRottable.CompInspectStringExtra))]
public static class Patch_FertilizerFastRotInspect
{
    static bool Prefix(CompRottable __instance, ref string __result)
    {
        if (!Patch_FertilizerFastRotTick.IsFertilizer(__instance))
        {
            return true;
        }

        __result = null;
        return false;
    }
}

/// Says, once defs exist, that this build is not the ten-day release behaviour.
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
            "\n\nThis development build dissolves a whole stack in one day. Bad weather — rain, fog, snow, or a thunderstorm — dissolves an unroofed stack four times as fast. A roof or fair weather keeps the one-day clock, and a freezer does not hold it back.";
    }
}
#endif
