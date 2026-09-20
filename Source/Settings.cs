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

using UnityEngine;
using Verse;

namespace BetterRimworlds.ChickenBatteryCage;

public class Settings : ModSettings
{
    public bool debugMode = false;

    public override void ExposeData()
    {
        Scribe_Values.Look(ref debugMode, "brw.BetterRimworlds.ChickenBatteryCage.debugMode", false);
    }

    public void DoSettingsWindowContents(Rect inRect)
    {
        Listing_Standard listing_Standard = new Listing_Standard();
        listing_Standard.Begin(inRect);

        listing_Standard.CheckboxLabeled("Print debug messages?", ref debugMode);

        listing_Standard.End();

        ChickenBatteryCage.Settings.debugMode = debugMode;
    }
}
