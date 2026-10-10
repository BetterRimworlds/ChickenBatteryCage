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
 * The Verse-free arithmetic behind chicken manure, cage cleaning, and the
 * virtualized Toxic Buildup a neglected flock suffers.
 *
 * A caged chicken never becomes a waste Thing and never ticks a poison need.
 * Manure accumulates at the cage level as a fractional pack count derived from
 * hen count and elapsed game time, exactly as feed and eggs already are. The
 * accumulation constants come from a measured real-world figure: a laying hen
 * drops roughly 0.1 kg of fresh manure a day, so fifty hens make about 5 kg a
 * day, which is the mass one Fertilizer Pack represents. The whole unit is
 * therefore one pack per fifty hen-days.
 *
 * Toxic Buildup reuses vanilla's own numbers. A pawn on polluted terrain gains
 * 0.4 severity a day (ToxicUtility) and recovers 0.08 a day when clean
 * (HediffComp_ImmunizableToxic's severityPerDayNotImmune). A dirty cage is the
 * virtual analogue of polluted ground, so the same gain and recovery apply to
 * the flock, with a grace band of ordinary manure before real exposure begins.
 */
public static class CageWasteMath
{
    /// One Fertilizer Pack per fifty hen-days. A laying hen makes about 0.1 kg
    /// of manure a day, and a pack stands for roughly 5 kg of it.
    public const float PacksPerHenPerDay = 1f / 50f;

    /// Improved terrain cells produced when one pack decomposes. Deliberately
    /// symmetrical with vanilla pollution: one Toxic Wastepack pollutes six
    /// cells, so one Fertilizer Pack improves six.
    public const int CellsImprovedPerPack = 6;

    /// Days of accumulation at which haulers are asked to clean a cage. A
    /// hundred-hen installation makes two packs a day, so this is the intended
    /// five-day service interval. Exposed so it can be rebalanced after play.
    public const float CleaningIntervalDays = 5f;

    /// Days of accumulation the flock can carry with no serious effect. Below
    /// this the flock merely smells; above it, toxic exposure begins.
    public const float SafeAccumulationDays = 10f;

    /// Days of accumulation at which a cage is called critical. Past this the
    /// flock is gaining toxic buildup at the full vanilla rate.
    public const float CriticalAccumulationDays = 20f;

    /// Days of accumulation the cage's waste gauge is scaled to. Exposed as the
    /// denominator of the inspection readout, so the flock's condition is
    /// legible long before it reaches this point.
    public const float CapacityAccumulationDays = 30f;

    /// Severity gained per day while the cage is above its safe waste level.
    /// This is vanilla's polluted-ground rate.
    public const float ToxicGainPerDay = 0.4f;

    /// Severity recovered per day while the cage is at or below its safe waste
    /// level. This is vanilla's natural Toxic Buildup recovery.
    public const float ToxicRecoveryPerDay = 0.08f;

    /// Severity at which vanilla Toxic Buildup is lethal. Read from the def
    /// where possible; this is the fallback and the clamp.
    public const float LethalSeverity = 1f;

    /// Severity below which a dirty cage adds nothing to mortality. Vanilla's
    /// first visible Toxic Buildup stage begins at 0.04 and "minor" at 0.2.
    public const float ToxicMortalityOnsetSeverity = 0.2f;

    /// Daily death chance the flock reaches at lethal Toxic Buildup severity.
    /// This keeps a poisoned flock dying off gradually, as a poisoned pawn
    /// would, instead of every bird dropping in one tick.
    public const float MaxToxicDailyChance = 0.2f;

    /// A safely absurd ceiling for a persisted waste total. Real values are a
    /// few times the capacity gauge; anything past this is corrupt and clamped.
    public const float MaxWastePacks = 1000000f;

    /// How well cleaned a cage is, in ascending order of neglect.
    public enum CleaningStatus
    {
        Clean,
        NeedsCleaning,
        Critical,
    }

    /// Which way the flock's toxic exposure is moving.
    public enum ToxicTrend
    {
        Stable,
        Increasing,
        Recovering,
    }

    /// Pack-equivalents a flock of this size produces per day.
    public static float PacksPerDay(int hens)
    {
        return hens <= 0 ? 0f : hens * PacksPerHenPerDay;
    }

    /// Pack-equivalents accrued over an elapsed window, as a pure function of
    /// hen count and time. No per-hen tick ever runs.
    public static float PacksOverTicks(int hens, int ticks)
    {
        if (hens <= 0 || ticks <= 0)
        {
            return 0f;
        }

        return PacksPerDay(hens) * ticks / CagedChickenMath.TicksPerDay;
    }

    /// Days of manure accumulated at the flock's current production rate.
    public static float AccumulationDays(float wastePacks, int hens)
    {
        float perDay = PacksPerDay(hens);
        return perDay <= 0f ? 0f : wastePacks / perDay;
    }

    /// Pack-equivalents stored when a cage starts asking to be cleaned.
    public static float CleaningThresholdPacks(int hens)
    {
        return PacksPerDay(hens) * CleaningIntervalDays;
    }

    /// Pack-equivalents that still carry no serious toxic effect.
    public static float SafeWastePacks(int hens)
    {
        return PacksPerDay(hens) * SafeAccumulationDays;
    }

    /// Pack-equivalents at which a cage is critical.
    public static float CriticalWastePacks(int hens)
    {
        return PacksPerDay(hens) * CriticalAccumulationDays;
    }

    /// Pack-equivalents the waste gauge is scaled to.
    public static float CapacityPacks(int hens)
    {
        return PacksPerDay(hens) * CapacityAccumulationDays;
    }

    /// Whole packs that a stored total can yield when cleaned. Only whole packs
    /// are ever removed; the fractional remainder stays in the cage.
    public static int WholePacks(float wastePacks)
    {
        return wastePacks <= 0f ? 0 : (int)Math.Floor(wastePacks);
    }

    /// The fractional remainder preserved through a cleaning.
    public static float FractionalPacks(float wastePacks)
    {
        return wastePacks <= 0f ? 0f : wastePacks - (float)Math.Floor(wastePacks);
    }

    public static CleaningStatus ClassifyCleaning(float wastePacks, int hens)
    {
        if (wastePacks <= 0f)
        {
            return CleaningStatus.Clean;
        }

        // An emptied cage that still holds manure should still be cleanable,
        // even though its flock has no production rate left.
        if (hens <= 0)
        {
            return CleaningStatus.NeedsCleaning;
        }

        if (wastePacks >= SafeWastePacks(hens))
        {
            return CleaningStatus.Critical;
        }

        return wastePacks >= CleaningThresholdPacks(hens)
            ? CleaningStatus.NeedsCleaning
            : CleaningStatus.Clean;
    }

    /// True while a cage's waste is worth a service visit.
    public static bool NeedsCleaning(float wastePacks, int hens)
    {
        return ClassifyCleaning(wastePacks, hens) != CleaningStatus.Clean;
    }

    /// Clamps a virtual severity into the valid band. A non-finite value is
    /// corrupt and becomes zero.
    public static float ClampSeverity(float severity, float lethal = LethalSeverity)
    {
        if (float.IsNaN(severity) || float.IsInfinity(severity))
        {
            return 0f;
        }

        if (severity <= 0f)
        {
            return 0f;
        }

        return severity > lethal ? lethal : severity;
    }

    /**
     * The flock's virtual Toxic Buildup after an elapsed window.
     *
     * The cage's waste density rises linearly as manure accrues. While it stays
     * at or below the safe level the flock recovers at vanilla's rate; once it
     * passes the safe level the flock gains at vanilla's polluted-ground rate.
     * The window is split exactly at the crossing, so a long settlement gives
     * the same answer as several short ones.
     */
    public static float ToxicBuildupAfter(
        float severity,
        float wastePacksAtStart,
        int hens,
        int elapsedTicks,
        float lethal = LethalSeverity)
    {
        if (hens <= 0 || elapsedTicks <= 0)
        {
            return ClampSeverity(severity, lethal);
        }

        double elapsedDays = elapsedTicks / (double)CagedChickenMath.TicksPerDay;
        float perDay = PacksPerDay(hens);
        float safe = SafeWastePacks(hens);

        double daysAbove;
        if (perDay <= 0f)
        {
            daysAbove = wastePacksAtStart > safe ? elapsedDays : 0.0;
        }
        else
        {
            double daysToSafe = wastePacksAtStart >= safe
                ? 0.0
                : (safe - wastePacksAtStart) / (double)perDay;
            daysAbove = elapsedDays - daysToSafe;
        }

        if (daysAbove < 0.0)
        {
            daysAbove = 0.0;
        }
        else if (daysAbove > elapsedDays)
        {
            daysAbove = elapsedDays;
        }

        double daysBelow = elapsedDays - daysAbove;
        double change = ToxicGainPerDay * daysAbove - ToxicRecoveryPerDay * daysBelow;
        return ClampSeverity((float)(severity + change), lethal);
    }

    /// Which way the flock's exposure is currently heading, for the inspection
    /// readout. Above the safe level it is climbing; a flock that still carries
    /// exposure while clean is recovering; an untouched clean flock is stable.
    public static ToxicTrend ClassifyTrend(float severity, float wastePacks, int hens)
    {
        if (hens > 0 && wastePacks >= SafeWastePacks(hens))
        {
            return ToxicTrend.Increasing;
        }

        return severity > 0.0001f ? ToxicTrend.Recovering : ToxicTrend.Stable;
    }

    /// Daily death chance from virtual Toxic Buildup. Nothing below the onset
    /// severity; then it climbs to a capped rate at lethal severity.
    public static float ToxicDailyChance(float severity)
    {
        if (severity <= ToxicMortalityOnsetSeverity)
        {
            return 0f;
        }

        float span = LethalSeverity - ToxicMortalityOnsetSeverity;
        if (span <= 0f)
        {
            return MaxToxicDailyChance;
        }

        float fraction = (severity - ToxicMortalityOnsetSeverity) / span;
        if (fraction > 1f)
        {
            fraction = 1f;
        }

        return MaxToxicDailyChance * fraction;
    }

    /// Terrain cells a dissolving stack improves: six per pack it holds. The
    /// whole stack rots away at once, exactly as a stack of food does.
    public static int CellsImprovedByPacks(int packs)
    {
        return packs <= 0 ? 0 : packs * CellsImprovedPerPack;
    }

    /// Days until the next whole pack accrues, for the inspection readout.
    public static float DaysUntilNextPack(float wastePacks, int hens)
    {
        float perDay = PacksPerDay(hens);
        if (perDay <= 0f)
        {
            return float.PositiveInfinity;
        }

        return (1f - FractionalPacks(wastePacks)) / perDay;
    }

    /// Days until the cage reaches its cleaning threshold, for the readout.
    public static float DaysUntilCleaningThreshold(float wastePacks, int hens)
    {
        float perDay = PacksPerDay(hens);
        if (perDay <= 0f)
        {
            return float.PositiveInfinity;
        }

        float remaining = CleaningThresholdPacks(hens) - wastePacks;
        return remaining <= 0f ? 0f : remaining / perDay;
    }

    /// Sums the per-cage waste of a cluster. Kept here, Verse-free, so the
    /// no-double-counting rule can be unit tested.
    public static float TotalWaste(IReadOnlyList<float> perCageWaste)
    {
        if (perCageWaste == null)
        {
            return 0f;
        }

        float total = 0f;
        for (int i = 0; i < perCageWaste.Count; i++)
        {
            if (perCageWaste[i] > 0f)
            {
                total += perCageWaste[i];
            }
        }

        return total;
    }
}
