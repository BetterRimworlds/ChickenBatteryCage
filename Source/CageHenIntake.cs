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
     * The caller must deliver a spawnable, unowned hen. A small hen may still be
     * carried in a handler's hands when the rope job reports her as arrived,
     * so she is first set down out of whatever container holds her; only a
     * holder that refuses to let go, or a bird that cannot reach the map, is
     * refused with an error and left untouched. Returns false when the cage
     * cannot accept her or she could not be freed; true means the record was
     * captured and the Pawn unmade.
     */
    public static bool PutHenInCage(Building_ChickenBatteryCage cage, Pawn hen)
    {
        if (cage == null || hen == null || !cage.CanAcceptChicken(hen))
        {
            return false;
        }

        if (hen.holdingOwner != null && !TryFreeHeldHen(hen, cage))
        {
            Log.Error("[ChickenBatteryCage] Refused to put a hen in a cage: she is still held by " +
                hen.holdingOwner + " and could not be set down. The hen was left untouched.");
            return false;
        }

        if (!hen.Spawned)
        {
            Log.Error("[ChickenBatteryCage] Refused to put a hen in a cage: she is not on the map. " +
                "The hen was left untouched.");
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
     * Sets a held hen down on her holder's cell so she can be un-made like any
     * other delivered bird. Returns true once nothing holds her any more.
     */
    static bool TryFreeHeldHen(Pawn hen, Building_ChickenBatteryCage cage)
    {
        ThingOwner holder = hen.holdingOwner;
        if (holder == null)
        {
            return true;
        }

        Thing owner = holder.Owner as Thing;
        Map map = owner != null ? owner.MapHeld : hen.MapHeld;
        if (map == null)
        {
            return false;
        }

        IntVec3 dropCell = owner != null ? owner.PositionHeld : hen.PositionHeld;
        if (!dropCell.IsValid || !dropCell.InBounds(map))
        {
            dropCell = cage.Position;
        }

        if (owner is Pawn carrier && carrier.carryTracker != null && carrier.carryTracker.CarriedThing == hen)
        {
            carrier.carryTracker.TryDropCarriedThing(dropCell, ThingPlaceMode.Near, out Thing _);
        }
        else
        {
            holder.TryDrop(hen, dropCell, map, ThingPlaceMode.Near, out Thing _);
        }

        return hen.holdingOwner == null;
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

        // Scrub the references other pawns and colony systems keep to this one,
        // so no stale relationship or ownership artifact survives a cage entry
        // and quietly accumulates across repeated loading cycles.
        try
        {
            pawn.relations?.ClearAllRelations();
            pawn.ownership?.UnclaimAll();
        }
        catch (Exception ex)
        {
            Log.Warning("[ChickenBatteryCage] Could not fully scrub a caged chicken's " +
                "references before discarding her: " + ex);
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