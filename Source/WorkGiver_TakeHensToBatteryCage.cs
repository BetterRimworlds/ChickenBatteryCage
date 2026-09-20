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
using Verse.AI;

namespace BetterRimworlds.ChickenBatteryCage;

/**
 * Handling work that leads player hens into battery cages.
 *
 * The WorkGiverDef priority is above vanilla TakeToPen / TakeRoamingAnimalsToPen,
 * so a cage with space is preferred over an ordinary chicken pen.
 */
public class WorkGiver_TakeHensToBatteryCage : WorkGiver_InteractAnimal
{
    public WorkGiver_TakeHensToBatteryCage()
    {
        canInteractWhileSleeping = true;
    }

    public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
    {
        // Cheap pre-filter: exclude everything that can never be a caged hen
        // (colonists, males, non-animals) before JobOnThing's checks run.
        foreach (Pawn candidate in pawn.Map.mapPawns.SpawnedPawnsInFaction(pawn.Faction))
        {
            if (Building_ChickenBatteryCage.IsHen(candidate))
            {
                yield return candidate;
            }
        }
    }

    public override bool ShouldSkip(Pawn pawn, bool forced)
    {
        return !Building_ChickenBatteryCage.AnyAcceptingCage(pawn.Map);
    }

    public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        if (!(t is Pawn animal) || !Building_ChickenBatteryCage.IsHen(animal))
        {
            return null;
        }

        if (animal.Faction != pawn.Faction)
        {
            return null;
        }

        if (animal.MentalStateDef != null && animal.MentalStateDef != MentalStateDefOf.Roaming)
        {
            JobFailReason.Is("CantRopeAnimalMentalState".Translate(animal, animal.MentalStateDef.label));
            return null;
        }

        if (animal.Position.IsForbidden(pawn))
        {
            JobFailReason.Is(string.Format(
                "{0} ({1})",
                "ForbiddenOutsideAllowedAreaLower".Translate().CapitalizeFirst(),
                pawn.playerSettings.EffectiveAreaRestrictionInPawnCurrentMap.Label));
            return null;
        }

        if (t.Map.designationManager.DesignationOn(t, DesignationDefOf.ReleaseAnimalToWild) != null)
        {
            return null;
        }

        if (!WorkGiver_InteractAnimal.CanInteractWithAnimal(
                pawn,
                animal,
                out string interactFailReason,
                forced,
                canInteractWhileSleeping,
                ignoreSkillRequirements: true,
                canInteractWhileRoaming: true))
        {
            if (interactFailReason != null)
            {
                JobFailReason.Is(interactFailReason);
            }
            return null;
        }

        Building_ChickenBatteryCage cage = Building_ChickenBatteryCage.FindAcceptingCage(animal, pawn);
        if (cage == null)
        {
            JobFailReason.Is(Building_ChickenBatteryCage.NoAcceptingCageReason(animal.Map));
            return null;
        }

        IntVec3 standCell = cage.FindStandCellForHandler(pawn);
        if (!standCell.IsValid)
        {
            JobFailReason.Is("CantRopeAnimalNoSpace".Translate());
            return null;
        }

        return JobMaker.MakeJob(
            ChickenBatteryCageDefOf.CBC_RopeHenToBatteryCage,
            animal,
            standCell,
            cage);
    }
}
