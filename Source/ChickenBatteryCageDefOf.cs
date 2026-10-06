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

using RimWorld;
using Verse;

namespace BetterRimworlds.ChickenBatteryCage;

[DefOf]
public static class ChickenBatteryCageDefOf
{
    public static JobDef CBC_RopeHenToBatteryCage;

    public static JobDef CBC_UnloadBatteryCage;

    public static JobDef CBC_FeedBatteryCage;

    /// The vanilla chicken pawn kind, used both to recognize birds and to
    /// regenerate them on release.
    public static PawnKindDef Chicken;

    /// The vanilla unfertilized chicken egg, the cage's output unit. The cage
    /// releases eggs in stacks of ten; this is the exact Thing a released
    /// stack is made of.
    public static ThingDef EggChickenUnfertilized;

    static ChickenBatteryCageDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(ChickenBatteryCageDefOf));
    }
}
