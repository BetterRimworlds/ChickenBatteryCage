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

/**
 * The lightweight persisted form of a chicken housed in a battery cage.
 *
 * A caged chicken is never a spawned Pawn. Only the biological facts needed to
 * reconstruct an equivalent bird later are recorded; identity, jobs, needs,
 * memories, history, and every other Pawn artefact are deliberately dropped.
 *
 * Biological age is stored as the age at entry plus the absolute tick of
 * entry. The current age is derived on demand, so no per-chicken aging work
 * ever runs while the bird is confined.
 */
public class CagedChickenRecord : IExposable
{
    public long biologicalAgeTicksAtEntry;
    public int enteredAtGameTick;
    public Gender gender;

    // Scribe needs a parameterless constructor; it is also the one used by Capture.
    public CagedChickenRecord()
    {
    }

    public CagedChickenRecord(long biologicalAgeTicksAtEntry, int enteredAtGameTick, Gender gender)
    {
        this.biologicalAgeTicksAtEntry = biologicalAgeTicksAtEntry;
        this.enteredAtGameTick = enteredAtGameTick;
        this.gender = gender;
    }

    /// Exact biological age at the given absolute tick. No scheduled task ever
    /// mutates the record; time alone supplies the aging.
    public long BiologicalAgeTicksAt(int absoluteTick)
    {
        return CagedChickenMath.BiologicalAgeTicksAt(
            biologicalAgeTicksAtEntry,
            enteredAtGameTick,
            absoluteTick);
    }

    public float BiologicalAgeYearsAt(int absoluteTick)
    {
        return BiologicalAgeTicksAt(absoluteTick) / (float)GenDate.TicksPerYear;
    }

    public static CagedChickenRecord Capture(Pawn chicken, int enteredAtGameTick)
    {
        return new CagedChickenRecord(
            chicken.ageTracker.AgeBiologicalTicks,
            enteredAtGameTick,
            chicken.gender);
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref biologicalAgeTicksAtEntry, "biologicalAgeTicksAtEntry", 0L);
        Scribe_Values.Look(ref enteredAtGameTick, "enteredAtGameTick", 0);
        Scribe_Values.Look(ref gender, "gender", Gender.Female);
    }
}
