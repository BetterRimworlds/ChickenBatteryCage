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
 * Remembers how many fertilizer packs have struck each cell since its terrain
 * last changed.
 *
 * The spread still picks cells the way pollution does, but a picked cell only
 * changes terrain on its third strike. The count has to live on the map,
 * because the cell looks unchanged after the first two packs and a later pack
 * has to find that progress still there. RimWorld adds every MapComponent
 * subclass to every map, so no def registration is needed.
 */
public class MapComponent_FertilizerCharge : MapComponent
{
    Dictionary<IntVec3, int> charges = new Dictionary<IntVec3, int>();

    public MapComponent_FertilizerCharge(Map map)
        : base(map)
    {
    }

    /// Strikes already stored on this cell since its last transformation.
    public int ChargeAt(IntVec3 cell)
    {
        if (charges != null && charges.TryGetValue(cell, out int charge))
        {
            return charge;
        }

        return 0;
    }

    /**
     * Records one pack landing on <paramref name="cell"/>. Returns true when
     * the cell has now been struck enough times to transform, and clears the
     * stored charge so the next step starts over.
     */
    public bool RecordHit(IntVec3 cell)
    {
        if (charges == null)
        {
            charges = new Dictionary<IntVec3, int>();
        }

        bool transforms = FertilizerChargeMath.ApplyHit(ChargeAt(cell), out int next);
        if (next <= 0)
        {
            charges.Remove(cell);
        }
        else
        {
            charges[cell] = next;
        }

        return transforms;
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref charges, "fertilizerCharges", LookMode.Value, LookMode.Value);
        if (Scribe.mode == LoadSaveMode.PostLoadInit && charges == null)
        {
            charges = new Dictionary<IntVec3, int>();
        }
    }
}
