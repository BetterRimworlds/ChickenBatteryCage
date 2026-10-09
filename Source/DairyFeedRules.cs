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

using System;
using System.Collections.Generic;

namespace BetterRimworlds.ChickenBatteryCage;

/**
 * Which foods a battery cage refuses to feed its flock.
 *
 * Real chickens are not mammals and never evolved to digest milk: poultry lack
 * intestinal lactase, so lactose passes through undigested and causes osmotic
 * diarrhea, dehydration, and poor growth. Dairy is therefore not feed, and the
 * cage must never accept it even when a player stockpiles it.
 *
 * RimWorld has no "dairy" category, so the rule is built from two signals:
 *
 * - the food type. A raw food that is both an animal product and a fluid is
 *   milk-like by definition. In vanilla only Milk carries that pair.
 * - a def-name list, which covers solid dairy (cheese, butter, yogurt) from
 *   vanilla-adjacent mods that carry no fluid flag. Players can extend it.
 *
 * The decision itself is primitive-only so it can be unit tested headlessly,
 * alongside the other pure cage math; CageFeed adapts ThingDefs onto it.
 */
public static class DairyFeedRules
{
    // FoodTypeFlags bits, mirrored here so the pure rules do not take a
    // dependency on Assembly-CSharp. See RimWorld.FoodTypeFlags.
    public const int AnimalProductFlag = 0x20;
    public const int FluidFlag = 0x04;

    /// Def names that count as dairy regardless of their food type. Vanilla
    /// Milk is the canonical member; the entry is defensive in case another
    /// mod retags it away from the animal-product/fluid pair.
    public static readonly IReadOnlyList<string> DefaultForbiddenDefNames =
        new List<string> { "Milk" };

    /// A raw food is dairy when it is both an animal product and a fluid. This
    /// is the vanilla signature of milk, and it ignores beer (fluid, not an
    /// animal product) and eggs or meat (animal products, not fluid).
    public static bool IsDairy(bool isAnimalProduct, bool isFluid)
    {
        return isAnimalProduct && isFluid;
    }

    /// True when <paramref name="defName"/> is dairy and the cage must refuse
    /// it. <paramref name="extraDefNames"/> may be null.
    public static bool IsForbidden(
        string defName, bool isAnimalProduct, bool isFluid,
        IEnumerable<string> extraDefNames = null)
    {
        if (MatchesAny(defName, DefaultForbiddenDefNames))
        {
            return true;
        }

        if (MatchesAny(defName, extraDefNames))
        {
            return true;
        }

        return IsDairy(isAnimalProduct, isFluid);
    }

    /// Case-insensitive membership test over a def-name list.
    public static bool MatchesAny(string defName, IEnumerable<string> defNames)
    {
        if (string.IsNullOrEmpty(defName) || defNames == null)
        {
            return false;
        }

        foreach (string candidate in defNames)
        {
            if (!string.IsNullOrEmpty(candidate)
                && string.Equals(candidate.Trim(), defName.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// Splits a free-text settings field into distinct def names. Commas,
    /// semicolons, and any whitespace separate entries; blanks are dropped.
    public static List<string> ParseDefNames(string raw)
    {
        var names = new List<string>();
        if (string.IsNullOrEmpty(raw))
        {
            return names;
        }

        string[] parts = raw.Split(
            new[] { ',', ';', ' ', '\t', '\r', '\n' },
            StringSplitOptions.RemoveEmptyEntries);

        foreach (string part in parts)
        {
            string trimmed = part.Trim();
            if (trimmed.Length == 0 || MatchesAny(trimmed, names))
            {
                continue;
            }

            names.Add(trimmed);
        }

        return names;
    }
}
