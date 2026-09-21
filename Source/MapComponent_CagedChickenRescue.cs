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
using RimWorld;
using Verse;

namespace BetterRimworlds.ChickenBatteryCage;

/**
 * Keeps caged-chicken records safe when their cage is destroyed before they
 * can be turned back into pawns.
 *
 * A record is the only copy of a housed bird, so it must never be deleted just
 * because the building holding it is gone. When <see cref="CageChickenFactory"/>
 * cannot materialize a bird at destruction time — a packed map with no free
 * cell, or a generation failure — the cage hands the record here instead. The
 * component carries the record across save/load and retries the release in the
 * background until the bird is back on the map. RimWorld adds every
 * MapComponent subclass to every map automatically, so no def registration is
 * needed.
 */
public class MapComponent_CagedChickenRescue : MapComponent
{
    // A stranded bird can wait. Retry often enough to free it quickly once
    // space appears, rarely enough that a full map costs nothing per tick.
    const int RetryIntervalTicks = 250;

    List<StrandedCagedChicken> stranded = new List<StrandedCagedChicken>();

    int ticksSinceRetry;

    public MapComponent_CagedChickenRescue(Map map)
        : base(map)
    {
    }

    /// Takes ownership of a record that could not be released, so it survives
    /// the destruction of the building that held it.
    public void Preserve(CagedChickenRecord record, IntVec3 near, bool injured)
    {
        if (record == null)
        {
            return;
        }

        stranded.Add(new StrandedCagedChicken(record, near, injured));
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref stranded, "strandedCagedChickens", LookMode.Deep);

        if (Scribe.mode == LoadSaveMode.PostLoadInit && stranded == null)
        {
            stranded = new List<StrandedCagedChicken>();
        }
    }

    public override void MapComponentTick()
    {
        if (stranded.Count == 0)
        {
            ticksSinceRetry = 0;
            return;
        }

        // Counting ticks rather than comparing absolute tick stamps keeps this
        // correct across RimWorld's wrapping game-tick counter.
        if (++ticksSinceRetry < RetryIntervalTicks)
        {
            return;
        }

        ticksSinceRetry = 0;
        ReleaseStranded();
    }

    void ReleaseStranded()
    {
        int released = 0;
        for (int i = stranded.Count - 1; i >= 0; i--)
        {
            StrandedCagedChicken entry = stranded[i];
            Pawn chicken = CageChickenFactory.Generate(entry.record, map, entry.near);
            if (chicken == null)
            {
                // Still stranded; keep the record and try again next interval.
                continue;
            }

            stranded.RemoveAt(i);
            CageHenReleaseMemory.Mark(chicken);
            if (entry.injured)
            {
                CageChickenInjuries.Injure(chicken);
            }

            released++;
        }

        if (released > 0)
        {
            Messages.Message(
                "ChickenBatteryCage.Message.StrandedReleased".Translate(released),
                MessageTypeDefOf.NeutralEvent,
                historical: false);
        }
    }
}
