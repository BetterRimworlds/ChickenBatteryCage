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
using Verse;

namespace BetterRimworlds.ChickenBatteryCage;

/**
 * A live chicken or dead body awaiting materialization or map placement.
 *
 * The record is the only copy of the bird, so when the cage is destroyed
 * before the bird can be materialized the record is not deleted. It is paired
 * here with the cell it should be released near and whether the destruction
 * that stranded it was violent, so the delayed release can still wound the
 * bird the way the original destruction would have. Dead entries instead
 * retain their death tick and, once generated, the same corpse across retries.
 *
 * A live entry also remembers the releasing network's surviving cages, so the
 * eventual rescue release can cut their intake off and stop handlers from
 * roping the freed bird straight back in. Those references are scribed, so the
 * cutoff survives save/load; references to cages that are gone by then simply
 * load as null and are ignored.
 */
public class StrandedCagedChicken : IExposable
{
    public CagedChickenRecord record;
    public IntVec3 near;
    public bool injured;

    /// The connected cages that housed this bird when its own cage was
    /// destroyed. Empty for a dead entry, where recapture does not apply.
    public List<Building_ChickenBatteryCage> originNetwork;

    /// The death tick for a dead bird, or <see cref="NotDead"/> for a live one.
    /// Its age is fixed at this tick while generation is pending; a generated
    /// body is retained across retries. Any real tick marks the entry dead,
    /// including a negative one after the game-tick counter wraps, so death is
    /// detected by inequality rather than by sign.
    public int diedAtTick = NotDead;
    public Corpse corpse;

    /// Sentinel recorded in <see cref="diedAtTick"/> for a live entry. -1 is
    /// also a real (if vanishingly unlikely) tick, so it is the one value the
    /// death check treats as "not yet dead".
    public const int NotDead = -1;

    public bool IsDead => diedAtTick != NotDead;

    // Scribe needs a parameterless constructor.
    public StrandedCagedChicken()
    {
    }

    public StrandedCagedChicken(CagedChickenRecord record, IntVec3 near, bool injured,
        IReadOnlyList<Building_ChickenBatteryCage> originNetwork = null)
    {
        this.record = record;
        this.near = near;
        this.injured = injured;
        CaptureOriginNetwork(originNetwork);
    }

    /**
     * Copies the releasing cluster's live members so a later rescue release can
     * cut their intake. The cluster a caller passes in is a cached, mutable
     * view, so it is copied rather than held.
     */
    public void CaptureOriginNetwork(IReadOnlyList<Building_ChickenBatteryCage> cluster)
    {
        if (cluster == null || cluster.Count == 0)
        {
            return;
        }

        originNetwork = new List<Building_ChickenBatteryCage>(cluster.Count);
        foreach (Building_ChickenBatteryCage cage in cluster)
        {
            if (cage != null && !cage.Destroyed)
            {
                originNetwork.Add(cage);
            }
        }
    }

    public void ExposeData()
    {
        Scribe_Deep.Look(ref record, "record");
        Scribe_Values.Look(ref near, "near");
        Scribe_Values.Look(ref injured, "injured", false);
        Scribe_Collections.Look(ref originNetwork, "originNetwork", LookMode.Reference);
        Scribe_Values.Look(ref diedAtTick, "diedAtTick", NotDead);
        Scribe_Deep.Look(ref corpse, "corpse");

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            // Dangling references to cages that no longer exist load as null
            // entries; drop them so the rescue release sees only live cages.
            originNetwork?.RemoveAll(cage => cage == null);
        }
    }
}
