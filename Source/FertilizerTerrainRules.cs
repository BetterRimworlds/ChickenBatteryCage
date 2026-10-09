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
 * The Verse-free terrain table the fertilizer spread consults.
 *
 * Fertilizer stands for both nutrients and accumulated organic material, so a
 * pack can build a soil layer over otherwise infertile mineral ground. The
 * first implementation reclaims sand, gravel, and rough natural cave stone into
 * ordinary soil. Nothing constructed — floors, bridges, roads, water, or
 * buildings — is ever touched; those are simply absent from the table, so the
 * spread can neither see nor replace them.
 *
 * The table keys on defName so it is easy to extend with future progressions
 * (for example soil to rich soil) without touching the spread algorithm. A
 * rough stone terrain is recognized by the suffixed names RimWorld's own
 * TerrainDefGenerator_Stone builds for every natural rock, so a modded stone
 * type works without being listed.
 */
public static class FertilizerTerrainRules
{
    /// The ordinary soil every improvement ends at.
    public const string Soil = "Soil";

    /// Rich soil is already better than the target, so the spread treats it as
    /// done rather than overwriting it.
    public const string SoilRich = "SoilRich";

    /// Terrains that already count as improved: the spread floods through them
    /// to reach the poor ground beyond, and never tries to improve them again.
    static readonly string[] ImprovedDefNames =
    {
        Soil,
        SoilRich,
        "MossyTerrain",
        "MarshyTerrain",
        "Riverbank",
    };

    /// Source terrains that can be improved, and what they become.
    static readonly string[] TransformableDefNames =
    {
        "Sand",
        "SoftSand",
        "Gravel",
    };

    /// True when this terrain is already fertile enough to count as improved.
    public static bool IsImproved(string defName)
    {
        if (string.IsNullOrEmpty(defName))
        {
            return false;
        }

        foreach (string name in ImprovedDefNames)
        {
            if (defName == name)
            {
                return true;
            }
        }

        return false;
    }

    /**
     * The terrain this source becomes when a pack improves it, or null when it
     * cannot be improved. Rough natural stone is matched by the suffix the game
     * itself gives its generated rough-stone terrains.
     */
    public static string TargetFor(string defName)
    {
        if (string.IsNullOrEmpty(defName))
        {
            return null;
        }

        foreach (string name in TransformableDefNames)
        {
            if (defName == name)
            {
                return Soil;
            }
        }

        return IsRoughStone(defName) ? Soil : null;
    }

    /// True when a terrain defName is one of the game's generated rough-stone
    /// cave-floor terrains (for example "Granite_Rough").
    public static bool IsRoughStone(string defName)
    {
        return defName != null
            && (defName.EndsWith("_Rough") || defName.EndsWith("_RoughHewn"));
    }
}
