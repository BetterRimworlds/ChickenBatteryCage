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
using UnityEngine;
using Verse;

namespace BetterRimworlds.ChickenBatteryCage;

public class Settings : ModSettings
{
    /// When on (the default) a housed hen's laying rate declines with age.
    /// Turned off, every adult hen lays one egg a day for the rest of her life,
    /// matching vanilla RimWorld.
    public bool ageDeclineEnabled = true;

    public bool debugMode = false;

    /**
     * When set, battery cages never accept dairy as feed. Chickens are not
     * mammals and cannot digest lactose, so milk is not feed. The player may
     * lift the ban, but it is on by default.
     */
    public bool forbidDairyInFeed = true;

    /// Extra def names treated as dairy, for modded solid dairy (cheese,
    /// butter, yogurt) that carries no fluid food type. Free text, parsed by
    /// <see cref="DairyFeedRules.ParseDefNames"/>.
    public string extraForbiddenFeedDefNames = "";

    List<string> parsedExtraForbiddenFeedDefNames;
    string parsedExtraForbiddenFeedDefNamesSource;

    /// The parsed form of <see cref="extraForbiddenFeedDefNames"/>, cached
    /// because the feeder asks for it for every stack it considers.
    public List<string> ExtraForbiddenFeedDefNames
    {
        get
        {
            if (parsedExtraForbiddenFeedDefNames == null
                || parsedExtraForbiddenFeedDefNamesSource != extraForbiddenFeedDefNames)
            {
                parsedExtraForbiddenFeedDefNames =
                    DairyFeedRules.ParseDefNames(extraForbiddenFeedDefNames);
                parsedExtraForbiddenFeedDefNamesSource = extraForbiddenFeedDefNames;
            }

            return parsedExtraForbiddenFeedDefNames;
        }
    }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref ageDeclineEnabled, "brw.BetterRimworlds.ChickenBatteryCage.ageDeclineEnabled", true);
        Scribe_Values.Look(ref debugMode, "brw.BetterRimworlds.ChickenBatteryCage.debugMode", false);
        Scribe_Values.Look(ref forbidDairyInFeed, "brw.BetterRimworlds.ChickenBatteryCage.forbidDairyInFeed", true);
        Scribe_Values.Look(ref extraForbiddenFeedDefNames, "brw.BetterRimworlds.ChickenBatteryCage.extraForbiddenFeedDefNames", "");
    }

    public void DoSettingsWindowContents(Rect inRect)
    {
        Listing_Standard listing_Standard = new Listing_Standard();
        listing_Standard.Begin(inRect);

        listing_Standard.CheckboxLabeled(
            "ChickenBatteryCage.Settings.AgeDecline".Translate(),
            ref ageDeclineEnabled,
            "ChickenBatteryCage.Settings.AgeDeclineTip".Translate());

        listing_Standard.Gap();
        listing_Standard.CheckboxLabeled(
            "ChickenBatteryCage.Settings.ForbidDairyInFeed".Translate(), ref forbidDairyInFeed,
            "ChickenBatteryCage.Settings.ForbidDairyInFeedTip".Translate());

        listing_Standard.Gap();
        listing_Standard.Label("ChickenBatteryCage.Settings.ExtraForbiddenFeedDefNames".Translate());
        extraForbiddenFeedDefNames = listing_Standard.TextEntry(extraForbiddenFeedDefNames);

        listing_Standard.GapLine();
        listing_Standard.CheckboxLabeled("Print debug messages?", ref debugMode);

        listing_Standard.End();

        ChickenBatteryCage.Settings.ageDeclineEnabled = ageDeclineEnabled;
        ChickenBatteryCage.Settings.debugMode = debugMode;
        ChickenBatteryCage.Settings.forbidDairyInFeed = forbidDairyInFeed;
        ChickenBatteryCage.Settings.extraForbiddenFeedDefNames = extraForbiddenFeedDefNames;
    }
}
