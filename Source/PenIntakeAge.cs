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

using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace BetterRimworlds.ChickenBatteryCage;

// Keep settings on the marker without replacing vanilla's component or its animal filter.
public class PenIntakeAge
{
    static readonly ConditionalWeakTable<CompAnimalPenMarker, PenIntakeAge> settings = new();
    float maximumYears = -1f;

    public static PenIntakeAge For(CompAnimalPenMarker marker) => settings.GetOrCreateValue(marker);

    public float Maximum => maximumYears;

    public void SetMaximum(float years)
    {
        if (!float.IsNaN(years) && !float.IsInfinity(years))
        {
            maximumYears = years < 0f ? -1f : years;
        }
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref maximumYears, "CBC_penIntakeMaximumAgeYears", -1f);
    }
}

[HarmonyPatch(typeof(CompAnimalPenMarker), nameof(CompAnimalPenMarker.PostExposeData))]
public static class Patch_PenIntakeAgeSave
{
    static void Postfix(CompAnimalPenMarker __instance) => PenIntakeAge.For(__instance).ExposeData();
}

// Only the handler's candidate-destination check changes. In particular, do not
// patch AcceptsToPen: vanilla also uses that to recognize an animal's current pen.
[HarmonyPatch(typeof(AnimalPenUtility), "CheckUseAndReach")]
public static class Patch_PenIntakeAgeDestination
{
    static void Postfix(Pawn animal, CompAnimalPenMarker penMarker, Pawn roper, ref bool __result)
    {
        if (!__result || roper == null || penMarker.PenState.ContainsConnectedRegion(animal.GetRegion()))
        {
            return;
        }
        __result = CageIntakeMath.AcceptsAge(animal.ageTracker.AgeBiologicalTicks,
            PenIntakeAge.For(penMarker).Maximum, GenDate.TicksPerYear);
    }
}

[HarmonyPatch(typeof(ITab_PenAnimals), "FillTab")]
public static class Patch_PenIntakeAgeUI
{
    static void Postfix()
    {
        // Vanilla leaves an 18px header above its animal-filter tree.
        var marker = Find.Selector.SingleSelectedThing?.TryGetComp<CompAnimalPenMarker>();
        if (marker != null && Widgets.ButtonText(new Rect(10f, 10f, 280f, 18f),
            "ChickenBatteryCage.PenIntake.Button".Translate()))
        {
            Find.WindowStack.Add(new Window_PenIntakeAge(marker));
        }
    }
}
