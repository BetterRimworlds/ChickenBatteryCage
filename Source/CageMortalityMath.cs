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

namespace BetterRimworlds.ChickenBatteryCage;

/**
 * The Verse-free survival arithmetic for caged chickens.
 *
 * Mortality is probabilistic, never a fixed expiration date, so a well-kept
 * flock can still produce rare long-lived birds. Nothing here materializes a
 * Pawn: a failed roll simply means the record is deleted.
 */
public static class CageMortalityMath
{
    /// Baseline daily death chance before age or starvation are considered.
    public const float BaselineDailyChance = 0.001f;

    /// No single daily roll may ever be a certainty, so old outliers survive.
    public const float MaxDailyChance = 0.5f;

    public static float CombinedDailyChance(float natural, float starvation)
    {
        float combined = natural + starvation;
        return combined > MaxDailyChance ? MaxDailyChance : combined;
    }

    /**
     * Converts a per-day hazard into the hazard for the ticks actually
     * evaluated. For a constant daily chance, compounding keeps a long
     * evaluation window equivalent to several short ones.
     */
    public static float ChanceOverTicks(float dailyChance, int ticks)
    {
        if (dailyChance <= 0f || ticks <= 0)
        {
            return 0f;
        }

        if (dailyChance >= 1f)
        {
            return 1f;
        }

        return ChanceFromExposure(ExposureOverTicks(dailyChance, ticks));
    }

    public static double ExposureOverTicks(float dailyChance, int ticks)
    {
        if (dailyChance <= 0f || ticks <= 0)
        {
            return 0.0;
        }

        return dailyChance >= 1f ? double.PositiveInfinity
            : -Math.Log(1.0 - dailyChance) * ticks / CagedChickenMath.TicksPerDay;
    }

    public static float ChanceFromExposure(double exposure)
    {
        return exposure <= 0.0 ? 0f : (float)(1.0 - Math.Exp(-exposure));
    }

    /// A newcomer owes no exposure for the part of a settlement before entry.
    public static int ExposureStartTick(int settledAtTick, int enteredAtTick)
    {
        return Math.Max(settledAtTick, enteredAtTick);
    }
}
