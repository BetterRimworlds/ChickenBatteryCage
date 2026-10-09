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
 * Hands a caged flock's accumulated virtual Toxic Buildup to a real Pawn.
 *
 * While a chicken is confined the condition is only a number on the cage: no
 * Pawn exists to carry a Hediff. When she is unloaded and materialized, that
 * number becomes the vanilla ToxicBuildup Hediff she would have contracted by
 * living in the filth, so the accumulated condition is real and visible.
 *
 * The severity band is vanilla's own: ToxicBuildup.lethalSeverity, which the
 * mod clamps to when the def is readable.
 */
public static class CageToxicBuildup
{
    /// The def's lethal severity, or the arithmetic fallback before defs load.
    public static float LethalSeverity =>
        HediffDefOf.ToxicBuildup != null && HediffDefOf.ToxicBuildup.lethalSeverity > 0f
            ? HediffDefOf.ToxicBuildup.lethalSeverity
            : CageWasteMath.LethalSeverity;

    /**
     * Applies the accumulated severity to a freshly materialized bird. A
     * severity at or below zero does nothing. A value at or above lethal would
     * kill the bird, so it is clamped just below lethal: the flock's mortality
     * has already been dealing with poisoned birds, and a bird leaving a dirty
     * cage should emerge gravely ill, not drop dead on the spot.
     */
    public static void Apply(Pawn chicken, float severity)
    {
        if (chicken == null || chicken.Dead || chicken.health == null)
        {
            return;
        }

        float lethal = LethalSeverity;
        float clamped = CageWasteMath.ClampSeverity(severity, lethal);
        if (clamped <= 0f)
        {
            return;
        }

        if (clamped >= lethal)
        {
            clamped = lethal * 0.99f;
        }

        HediffDef def = HediffDefOf.ToxicBuildup;
        if (def == null)
        {
            return;
        }

        Hediff hediff = HediffMaker.MakeHediff(def, chicken);
        if (hediff == null)
        {
            return;
        }

        hediff.Severity = clamped;
        chicken.health.AddHediff(hediff);
    }
}
