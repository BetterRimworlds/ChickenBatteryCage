/*
 * This file is part of BetterRimworlds.ChickenBatteryCage, a Better Rimworlds Project.
 *
 * Copyright © 2026 Theodore R. Smith
 * Author: Theodore R. Smith <hopeseekr@gmail.com>
 *   GPG Fingerprint: D8EA 6E4D 5952 159D 7759  2BB4 EEB6 CE72 F441 EC41
 *   https://github.com/BetterRimworlds/BetterRimworlds.ChickenBatteryCage
 *
 * This file is licensed under the MIT License.
 *
 * =============================================================================
 * HARMONY PATCH LOADING — DO NOT REGRESS
 * =============================================================================
 *
 * Never call harmony.PatchAll(Assembly) for the whole assembly when any
 * TargetMethod is version-sensitive. One failed patch aborts the rest.
 *
 * Apply each [HarmonyPatch] type with CreateClassProcessor(type).Patch()
 * inside its own try/catch (see ctor).
 *
 * When resolving vanilla methods by signature, prefer ordered
 * AccessTools.Method fallbacks.
 *
 * If a float-menu / job feature "does nothing" on one game version only, check
 * the player log first for Harmony load failures before rewriting gameplay
 * conditions.
 *
 * After changing any TargetMethod / patch attribute, boot RimWorld 1.6 and
 * confirm no Harmony patch failed lines at startup.
 * =============================================================================
 */

using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace BetterRimworlds.ChickenBatteryCage;

public class ChickenBatteryCage : Mod
{
    public static Settings Settings;

    public ChickenBatteryCage(ModContentPack content) : base(content)
    {
        Settings = GetSettings<Settings>() ?? new Settings();

        var harmony = new Harmony("BetterRimworlds.ChickenBatteryCage");

        foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
        {
            try
            {
                if (!type.IsDefined(typeof(HarmonyPatch), inherit: true))
                    continue;

                harmony.CreateClassProcessor(type).Patch();
            }
            catch (Exception ex)
            {
                Log.Error(
                    $"[ChickenBatteryCage] Harmony patch failed on {type.FullName}: {ex}");
            }
        }
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        base.DoSettingsWindowContents(inRect);
        Settings.DoSettingsWindowContents(inRect);
    }

    public override string SettingsCategory()
    {
        return "ChickenBatteryCage";
    }
}
