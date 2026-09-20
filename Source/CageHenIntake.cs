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

using RimWorld.Planet;
using Verse;

namespace BetterRimworlds.ChickenBatteryCage;

/**
 * The intake act of the battery cage: a delivered hen's biology is serialized
 * into a cage record and her Pawn form ceases to exist — with no death
 * bookkeeping, because nothing died. The bird was filed away.
 */
public static class CageHenIntake
{
    /**
     * Puts a delivered hen into her cage.
     *
     * The caller must deliver a spawned, unowned, roped hen — that is the
     * contract, and it is enforced here: anything else is refused with an
     * error and left untouched rather than being silently fixed up.
     * Returns false when the cage cannot accept her or the contract was
     * violated; true means the record was captured and the Pawn unmade.
     */
    public static bool PutHenInCage(Building_ChickenBatteryCage cage, Pawn hen)
    {
        if (cage == null || hen == null || !cage.CanAcceptChicken(hen))
        {
            return false;
        }

        if (hen.holdingOwner != null || !hen.Spawned)
        {
            Log.Error("[ChickenBatteryCage] Refused to put a hen in a cage: the handler delivered her " +
                (hen.holdingOwner != null
                    ? "while still held by " + hen.holdingOwner + "."
                    : "while she was not on the map.") +
                " The hen was left untouched.");
            return false;
        }

        // Record first: if the unmaking below ever threw, the bird would at
        // worst be duplicated, never silently deleted.
        CagedChickenRecord record = CagedChickenRecord.Capture(hen, GenTicks.TicksAbs);
        cage.AddRecord(record);
        UnmakeWithoutDeath(hen);
        return true;
    }

    /**
     * A Pawn ceases to exist without the game recording a death: no corpse,
     * no death tale, no dead-pawn entry, no mourning. This mirrors the
     * destroy-and-discard path RimWorld itself uses when garbage collecting
     * world pawns. The pawn must not be held by any container — being
     * destroyed while owned is refused by the engine, so that is treated as a
     * contract violation here too.
     */
    internal static void UnmakeWithoutDeath(Pawn pawn)
    {
        if (pawn == null || pawn.Destroyed)
        {
            return;
        }

        if (pawn.holdingOwner != null)
        {
            Log.Error("[ChickenBatteryCage] Refused to unmake " + pawn + ": it is still held by " + pawn.holdingOwner + ".");
            return;
        }

        if (pawn.Spawned)
        {
            pawn.DeSpawn(DestroyMode.Vanish);
        }

        if (!pawn.Destroyed)
        {
            // PassToWorld(Discard) internally destroys and then discards the
            // pawn while marking it as "being discarded", so the destroy step
            // skips the normal pass-to-world handling and no dead entry remains.
            Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.Discard);
        }
        else if (!pawn.Discarded)
        {
            pawn.Discard();
        }
    }
}