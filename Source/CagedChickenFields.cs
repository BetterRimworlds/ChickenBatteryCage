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
 * The persisted schema of a single CagedChickenRecord.
 *
 * The Scribe element names live here as constants so the record's
 * serialization and CageSaveSize's budget cannot drift apart: the record
 * scribes these exact names, and the estimator sums these exact fields.
 * Adding a persisted field means adding it here, which grows the estimated
 * save cost instead of leaving a hand-picked constant behind.
 */
public static class CagedChickenFields
{
    public const string BiologicalAgeTicksAtEntry = "biologicalAgeTicksAtEntry";
    public const string EnteredAtGameTick = "enteredAtGameTick";
    public const string MortalityExposure = "mortalityExposure";

    /// One entry per persisted field, in serialization order, paired with the
    /// widest decimal text its value can occupy.
    public static readonly PersistedField[] All =
    {
        new PersistedField(BiologicalAgeTicksAtEntry, 20), // long
        new PersistedField(EnteredAtGameTick, 11),          // int
        new PersistedField(MortalityExposure, 24),          // double
    };
}

/// A persisted XML element's name and the widest value text it can hold.
public readonly struct PersistedField
{
    public readonly string Name;
    public readonly int MaxValueWidth;

    public PersistedField(string name, int maxValueWidth)
    {
        Name = name;
        MaxValueWidth = maxValueWidth;
    }
}
