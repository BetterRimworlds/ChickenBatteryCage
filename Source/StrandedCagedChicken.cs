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
 * A caged chicken record that outlived the cage it was housed in.
 *
 * The record is the only copy of the bird, so when the cage is destroyed
 * before the bird can be materialized the record is not deleted. It is paired
 * here with the cell it should be released near and whether the destruction
 * that stranded it was violent, so the delayed release can still wound the
 * bird the way the original destruction would have.
 */
public class StrandedCagedChicken : IExposable
{
    public CagedChickenRecord record;
    public IntVec3 near;
    public bool injured;

    // Scribe needs a parameterless constructor.
    public StrandedCagedChicken()
    {
    }

    public StrandedCagedChicken(CagedChickenRecord record, IntVec3 near, bool injured)
    {
        this.record = record;
        this.near = near;
        this.injured = injured;
    }

    public void ExposeData()
    {
        Scribe_Deep.Look(ref record, "record");
        Scribe_Values.Look(ref near, "near");
        Scribe_Values.Look(ref injured, "injured", false);
    }
}
