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

/**
 * The Verse-free terrain table the fertilizer spread consults.
 *
 * Fertilizer stands for both nutrients and accumulated organic material, so a
 * pack builds a richer layer over poorer natural ground. The progression runs
 * from the barren grounds up toward rich soil:
 *
 *     SoftSand -> Sand -> Gravel -> MossyTerrain -> Soil -> SoilRich
 *     MarshyTerrain -> Gravel
 *     every rough natural stone -> Gravel
 *
 * Any other natural ground also qualifies. A terrain with its own driesTo
 * target follows that (mud dries to soil); anything else becomes stony soil and
 * joins the normal progression. Only floors — smoothed or built — water,
 * foundations, and buildings are left alone. Natural rock walls and player
 * structures still block the spread.
 *
 * The table keys on defName so it is easy to extend with future progressions
 * without touching the spread algorithm. A rough stone terrain is recognized by
 * the suffixed names RimWorld's own TerrainDefGenerator_Stone builds for every
 * natural rock, so a modded stone type works without being listed.
 */
public static class FertilizerTerrainRules
{
    /// The ordinary soil the natural progression passes through.
    public const string Soil = "Soil";

    /// Rich soil, the final step the progression ends at.
    public const string SoilRich = "SoilRich";

    /// Stony soil, the target of sand and of every rough natural stone.
    public const string Gravel = "Gravel";

    /// Lichen-covered soil, the step between stony ground and ordinary soil.
    public const string MossyTerrain = "MossyTerrain";

    /// Sand, the step above soft sand.
    public const string Sand = "Sand";

    /// Terrains that already count as finished: the spread floods through them
    /// to reach the poor ground beyond, and never tries to improve them again.
    static readonly string[] ImprovedDefNames =
    {
        SoilRich,
        "Riverbank",
    };

    /// Source terrains that can be improved, and the terrain each becomes.
    static readonly Dictionary<string, string> TransformTargets =
        new Dictionary<string, string>
        {
            { "SoftSand", Sand },
            { Sand, Gravel },
            { Gravel, MossyTerrain },
            { MossyTerrain, Soil },
            { Soil, SoilRich },
            { "MarshyTerrain", Gravel },
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

        string target;
        if (TransformTargets.TryGetValue(defName, out target))
        {
            return target;
        }

        return IsRoughStone(defName) ? Gravel : null;
    }

    /**
     * The terrain a convertible cell becomes. Unlisted natural ground follows
     * its own driesTo target when it has one; otherwise it becomes stony soil
     * and joins the normal progression.
     */
    public static string TargetForConvertible(string defName, string driesTo)
    {
        string target = TargetFor(defName);
        if (target != null)
        {
            return target;
        }

        if (IsImproved(defName))
        {
            return null;
        }

        return string.IsNullOrEmpty(driesTo) ? Gravel : driesTo;
    }

    /**
     * True when the spread may replace this terrain.
     *
     * All natural ground qualifies, plus the game's generated rough-hewn cave
     * stone: RimWorld leaves TerrainDef.natural unset on every *_RoughHewn
     * terrain even though it is the stone floor a mined-out wall leaves behind,
     * so testing that flag alone would reject the most common underground
     * floor. Floors — smoothed or built — water, and foundations never qualify,
     * so a pack stored on them waits.
     */
    public static bool CanConvert(
        string defName,
        bool natural,
        bool isWater,
        bool isFoundation,
        bool isFloor)
    {
        if (isWater || isFoundation || isFloor || IsImproved(defName))
        {
            return false;
        }

        return natural || IsRoughStone(defName);
    }

    /**
     * True when a pack sitting on this terrain can start or continue a fertile
     * patch. Finished ground anchors a patch too, so the spread can flood
     * through it to reach poorer ground beyond.
     */
    public static bool AnchorsSpread(
        string defName,
        bool natural,
        bool isWater,
        bool isFoundation,
        bool isFloor)
    {
        return IsImproved(defName)
            || CanConvert(defName, natural, isWater, isFoundation, isFloor);
    }

    /// True when a terrain defName is one of the game's generated rough-stone
    /// cave-floor terrains (for example "Granite_Rough").
    public static bool IsRoughStone(string defName)
    {
        return defName != null
            && (defName.EndsWith("_Rough") || defName.EndsWith("_RoughHewn"));
    }
}
