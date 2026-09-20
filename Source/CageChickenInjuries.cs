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
using UnityEngine;
using Verse;

namespace BetterRimworlds.ChickenBatteryCage;

/**
 * Wounds a chicken that survived the destruction of its battery cage.
 *
 * Injuries go through the normal damage pipeline, so the bird ends up with
 * real wounds, bleeding, and pain rather than an abstract "damaged" flag. The
 * total is deliberately capped below what the bird can take: a wrecked cage
 * should maim and scatter its flock, not quietly wipe it out.
 */
public static class CageChickenInjuries
{
    // Physical trauma only. Fire and burn types are left out on purpose so a
    // freed bird is not set alight by the very damage meant to wound it.
    static readonly DamageDef[] HarmfulDamageDefs =
    {
        DamageDefOf.Cut,
        DamageDefOf.Scratch,
        DamageDefOf.Blunt,
        DamageDefOf.Stab,
        DamageDefOf.Bite,
        DamageDefOf.Bullet,
    };

    const int MinInjuries = 1;
    const int MaxInjuries = 3;

    const float MinDamagePerInjury = 1.5f;
    const float MaxDamagePerInjury = 4.5f;

    // Share of the bird's maximum health that all of its injuries together may
    // consume. The ceiling keeps the bird alive even when the rolls are unkind.
    const float DamageBudgetFraction = 0.35f;

    // Never carry a single wound past this share of the body part's health, so
    // an injury stays a wound instead of destroying the part outright.
    const float MaxPartDamageFraction = 0.6f;

    public static void Injure(Pawn chicken)
    {
        if (chicken == null || chicken.Dead || chicken.health == null)
        {
            return;
        }

        List<BodyPartRecord> parts = new List<BodyPartRecord>(
            chicken.health.hediffSet.GetNotMissingParts(depth: BodyPartDepth.Outside));
        if (parts.Count == 0)
        {
            return;
        }

        float budget = Mathf.Max(MinDamagePerInjury, chicken.HealthScale * 100f * DamageBudgetFraction);

        int injuries = Rand.RangeInclusive(MinInjuries, MaxInjuries);
        for (int i = 0; i < injuries && parts.Count > 0; i++)
        {
            // Each wound lands on a different part, so a run of bad luck cannot
            // concentrate every injury into one limb.
            BodyPartRecord part = parts[Rand.Range(0, parts.Count)];
            parts.Remove(part);

            float partCeiling = part.def.GetMaxHealth(chicken) * MaxPartDamageFraction;
            float amount = Mathf.Min(Rand.Range(MinDamagePerInjury, MaxDamagePerInjury), budget, partCeiling);
            if (amount <= 0f)
            {
                return;
            }

            budget -= amount;
            chicken.TakeDamage(new DamageInfo(HarmfulDamageDefs.RandomElement(), amount, 0f, -1f, null, part));

            if (chicken.Dead)
            {
                return;
            }
        }
    }
}
