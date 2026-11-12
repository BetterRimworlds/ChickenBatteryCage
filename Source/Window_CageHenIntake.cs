using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace BetterRimworlds.ChickenBatteryCage;

public class Window_CageHenIntake : Window
{
    readonly Building_ChickenBatteryCage cage;
    float selectedAge = 2f;
    string ageBuffer = "2";
    IReadOnlyList<Building_ChickenBatteryCage> observedCluster;
    float observedMaximum;

    public Window_CageHenIntake(Building_ChickenBatteryCage cage)
    {
        this.cage = cage;
        RefreshSelection(CageNetwork.Cluster(cage));
        doCloseX = true;
        closeOnCancel = true;
        absorbInputAroundWindow = true;
        draggable = true;
        onlyOneOfTypeAllowed = true;
        optionalTitle = "ChickenBatteryCage.Intake.Title".Translate();
    }

    public override Vector2 InitialSize => new Vector2(460f, 310f);

    // Reading a merged network must never overwrite its strictest limit.
    void RefreshSelection(IReadOnlyList<Building_ChickenBatteryCage> cluster)
    {
        float maximum = CageNetwork.MaximumIntakeAgeYears(cluster);
        bool membershipChanged = !ReferenceEquals(observedCluster, cluster)
            && (observedCluster == null
                || !new HashSet<Building_ChickenBatteryCage>(observedCluster).SetEquals(cluster));
        observedCluster = cluster;
        if (membershipChanged || observedMaximum != maximum)
        {
            observedMaximum = maximum;
            selectedAge = maximum >= 0f ? maximum : 2f;
            ageBuffer = selectedAge.ToString();
        }
    }

    public override void DoWindowContents(Rect inRect)
    {
        if (cage.Destroyed || cage.Map == null)
        {
            Close();
            return;
        }

        var cluster = CageNetwork.Cluster(cage);
        RefreshSelection(cluster);
        bool limited = observedMaximum >= 0f;
        bool changed = false;
        Widgets.Label(new Rect(0f, 0f, inRect.width, 30f), "ChickenBatteryCage.Intake.Network".Translate());
        Widgets.Label(new Rect(0f, 38f, inRect.width, 30f), "ChickenBatteryCage.Intake.Maximum".Translate());
        if (Widgets.RadioButtonLabeled(new Rect(0f, 72f, inRect.width, 30f),
            "ChickenBatteryCage.Intake.Any".Translate(), !limited) && limited)
        {
            changed = true;
            limited = false;
        }
        if (Widgets.RadioButtonLabeled(new Rect(0f, 108f, 125f, 30f),
            "ChickenBatteryCage.Intake.AtMost".Translate(), limited) && !limited)
        {
            limited = true;
            changed = true;
        }
        float previousAge = selectedAge;
        Widgets.TextFieldNumeric(new Rect(135f, 108f, 90f, 30f), ref selectedAge, ref ageBuffer, 0f, 100000f);
        Widgets.Label(new Rect(235f, 108f, 150f, 30f), "ChickenBatteryCage.Intake.Years".Translate());
        if (changed || (limited && selectedAge != previousAge))
        {
            observedMaximum = limited ? selectedAge : -1f;
            CageNetwork.SetMaximumIntakeAgeYears(cluster, observedMaximum);
        }
        Widgets.Label(new Rect(0f, 150f, inRect.width, 55f), "ChickenBatteryCage.Intake.Description".Translate());
        if (Widgets.ButtonText(new Rect(inRect.xMax - 140f, inRect.yMax - 32f, 140f, 32f), "CloseButton".Translate()))
        {
            Close();
        }
    }
}
