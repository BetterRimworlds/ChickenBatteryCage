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
using UnityEngine;
using Verse;

namespace BetterRimworlds.ChickenBatteryCage;

public class Window_PenIntakeAge : Window
{
    readonly CompAnimalPenMarker marker;
    float age = 2f;
    string buffer = "2";

    public Window_PenIntakeAge(CompAnimalPenMarker marker)
    {
        this.marker = marker;
        float maximum = PenIntakeAge.For(marker).Maximum;
        age = maximum >= 0f ? maximum : 2f;
        buffer = age.ToString();
        optionalTitle = "ChickenBatteryCage.PenIntake.Title".Translate();
        doCloseX = true;
        closeOnCancel = true;
        absorbInputAroundWindow = true;
        draggable = true;
        onlyOneOfTypeAllowed = true;
    }

    public override Vector2 InitialSize => new Vector2(480f, 300f);

    public override void DoWindowContents(Rect inRect)
    {
        if (!marker.parent.Spawned)
        {
            Close();
            return;
        }
        var settings = PenIntakeAge.For(marker);
        bool limited = settings.Maximum >= 0f;
        Widgets.Label(new Rect(0f, 0f, inRect.width, 30f), "ChickenBatteryCage.Intake.Maximum".Translate());
        if (Widgets.RadioButtonLabeled(new Rect(0f, 35f, inRect.width, 30f),
            "ChickenBatteryCage.Intake.Any".Translate(), !limited))
        {
            limited = false;
            settings.SetMaximum(-1f);
        }
        if (Widgets.RadioButtonLabeled(new Rect(0f, 70f, 125f, 30f),
            "ChickenBatteryCage.Intake.AtMost".Translate(), limited))
        {
            limited = true;
        }
        Widgets.TextFieldNumeric(new Rect(135f, 70f, 90f, 30f), ref age, ref buffer, 0f, 100000f);
        Widgets.Label(new Rect(235f, 70f, 150f, 30f), "ChickenBatteryCage.Intake.Years".Translate());
        if (limited)
        {
            settings.SetMaximum(age);
        }
        Widgets.Label(new Rect(0f, 115f, inRect.width, 80f), "ChickenBatteryCage.PenIntake.Description".Translate());
        if (Widgets.ButtonText(new Rect(inRect.xMax - 140f, inRect.yMax - 32f, 140f, 32f), "CloseButton".Translate()))
        {
            Close();
        }
    }
}
