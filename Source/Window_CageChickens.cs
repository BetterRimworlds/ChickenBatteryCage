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
 * The per-chicken picker for the whole battery-cage network.
 *
 * A cage's flock is a list of anonymous biological records, so this window is
 * the only way a player can see the birds individually and choose which ones a
 * handler should unload. Every cage that touches the one the picker was opened
 * from contributes to one list: touching cages behave as a single giant cage,
 * and the cluster's gizmos are identical on every member, so opening the
 * picker from any cage shows the whole cluster's flock.
 *
 * Rows are grouped by life stage — chicks, then juveniles, then adults — and
 * sorted by age within each group, because age and stage are the only facts a
 * record carries. Every bird is simply labelled "chicken"; identity is not
 * stored and is deliberately not invented.
 */
public class Window_CageChickens : Window
{
    const float RowHeight = 26f;
    const float HeaderHeight = 26f;
    const float StageHeaderHeight = 24f;
    const float FooterHeight = 34f;
    const float AgeColumnWidth = 120f;
    const float StageColumnWidth = 90f;
    const float StageButtonWidth = 80f;
    const float StageButtonHeight = 20f;

    readonly Building_ChickenBatteryCage cage;
    readonly Map map;

    /// The records the player has ticked, each mapped to the cage that houses
    /// it. Held by object reference and owning cage so neither a changing flock
    /// nor a shifting list can move a tick onto a different bird.
    readonly Dictionary<CagedChickenRecord, Building_ChickenBatteryCage> selected =
        new Dictionary<CagedChickenRecord, Building_ChickenBatteryCage>();

    /// The rows of the current snapshot, in display order, each with the
    /// stage it was grouped under.
    List<Row> rows;

    Vector2 scroll;

    public Window_CageChickens(Building_ChickenBatteryCage cage)
    {
        this.cage = cage;
        this.map = cage?.Map;

        doCloseX = true;
        doCloseButton = false;
        forcePause = false;
        absorbInputAroundWindow = false;
        closeOnClickedOutside = false;
        preventCameraMotion = false;
        draggable = true;
        resizeable = true;
        // One picker at a time: it always shows a whole cluster, so a second
        // window would be a duplicate view of the same flock.
        onlyOneOfTypeAllowed = true;
        optionalTitle = "ChickenBatteryCage.Picker.Title".Translate();
    }

    public override Vector2 InitialSize => new Vector2(520f, 560f);

    /// One displayed bird: the record, the cage that houses it, and the facts
    /// derived for its row.
    struct Row
    {
        public CagedChickenRecord record;
        public Building_ChickenBatteryCage cage;
        public CagedChickenStage stage;

        /// RimWorld's own name for this bird's life stage, e.g. "Chick" or
        /// "Hen", used as the row's label instead of a flat "chicken".
        public string name;
        public float ageYears;
        public bool alreadyMarked;

        /// The bird's label number, 1-based within its life-stage group.
        /// Records carry no identity, so this is only a display handle: it is
        /// assigned in the order the birds are listed, not stored.
        public int number;
    }

    public override void PreOpen()
    {
        base.PreOpen();
        RebuildRows();
    }

    /// Takes a fresh snapshot of the whole cluster's flock, sorted by stage
    /// then age.
    void RebuildRows()
    {
        rows = new List<Row>();
        if (map == null || cage == null)
        {
            return;
        }

        foreach (Building_ChickenBatteryCage member in CageNetwork.Cluster(cage))
        {
            if (member == null || member.Destroyed)
            {
                continue;
            }

            int count = member.ChickenCount;
            for (int i = 0; i < count; i++)
            {
                CagedChickenRecord record = member.RecordAt(i);
                if (record == null)
                {
                    continue;
                }

                CagedChickenStage stage = member.StageOf(record);
                rows.Add(new Row
                {
                    record = record,
                    cage = member,
                    stage = stage,
                    name = member.LifeStageName(stage),
                    ageYears = member.BiologicalAgeYears(record),
                    alreadyMarked = member.IsMarkedForUnload(record),
                });
            }
        }

        // Oldest first within each stage: the stage rank is the primary key,
        // and age descends inside a group so the oldest bird of a stage sits at
        // the top of that group. Chicks come first, so the oldest chick is
        // numbered 1.
        rows.Sort((a, b) =>
        {
            int rank = CagedChickenStageMath.SortRank(a.stage)
                .CompareTo(CagedChickenStageMath.SortRank(b.stage));
            if (rank != 0)
            {
                return rank;
            }

            return b.ageYears.CompareTo(a.ageYears);
        });

        // Drop ticks whose bird left the network since the last snapshot.
        var stillHoused = new HashSet<CagedChickenRecord>();
        foreach (Row row in rows)
        {
            stillHoused.Add(row.record);
        }

        var stale = new List<CagedChickenRecord>();
        foreach (CagedChickenRecord record in selected.Keys)
        {
            if (!stillHoused.Contains(record))
            {
                stale.Add(record);
            }
        }

        foreach (CagedChickenRecord record in stale)
        {
            selected.Remove(record);
        }

        // Number the birds 1..N within each life-stage group, in the order
        // they are listed: because age descends inside a group, hen 1 is the
        // oldest hen, regardless of how many chicks or juveniles sit above
        // that group in the list.
        CagedChickenStage? numberedStage = null;
        int numberInStage = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            Row numbered = rows[i];
            if (numberedStage == null || numberedStage.Value != numbered.stage)
            {
                numberedStage = numbered.stage;
                numberInStage = 0;
            }

            numbered.number = ++numberInStage;
            rows[i] = numbered;
        }
    }

    public override void DoWindowContents(Rect inRect)
    {
        if (map == null)
        {
            Close();
            return;
        }

        // Take a fresh view each frame so a bird released by a handler while
        // this window is open disappears from the list instead of going stale.
        RebuildRows();

        Rect content = inRect;
        Rect footer = new Rect(content.x, content.yMax - FooterHeight, content.width, FooterHeight);
        content.height -= FooterHeight + 6f;

        DrawHeader(content);
        content.y += HeaderHeight + 4f;
        content.height -= HeaderHeight + 4f;

        DrawList(content);
        DrawFooter(footer);
    }

    void DrawHeader(Rect rect)
    {
        Text.Font = GameFont.Medium;
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(rect, "ChickenBatteryCage.Picker.Heading".Translate(
            CageNetwork.ChickenCount(CageNetwork.Cluster(cage)),
            CageNetwork.TotalCapacity(CageNetwork.Cluster(cage))));
        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;
    }

    void DrawList(Rect outRect)
    {
        if (rows.Count == 0)
        {
            Widgets.Label(outRect, "ChickenBatteryCage.Picker.Empty".Translate());
            return;
        }

        float totalHeight = 0f;
        CagedChickenStage? lastStage = null;
        foreach (Row row in rows)
        {
            if (lastStage == null || lastStage.Value != row.stage)
            {
                totalHeight += StageHeaderHeight;
                lastStage = row.stage;
            }
            totalHeight += RowHeight;
        }

        Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, Mathf.Max(totalHeight, outRect.height));
        Widgets.BeginScrollView(outRect, ref scroll, viewRect);

        float y = 0f;
        lastStage = null;
        foreach (Row row in rows)
        {
            if (lastStage == null || lastStage.Value != row.stage)
            {
                lastStage = row.stage;
                Rect stageRect = new Rect(0f, y, viewRect.width, StageHeaderHeight);
                DrawStageHeader(stageRect, row.stage, CountOfStage(row.stage));
                y += StageHeaderHeight;
            }

            Rect rowRect = new Rect(0f, y, viewRect.width, RowHeight);
            DrawRow(rowRect, row);
            y += RowHeight;
        }

        Widgets.EndScrollView();
    }

    int CountOfStage(CagedChickenStage stage)
    {
        int count = 0;
        foreach (Row row in rows)
        {
            if (row.stage == stage)
            {
                count++;
            }
        }
        return count;
    }

    void DrawStageHeader(Rect rect, CagedChickenStage stage, int count)
    {
        Rect label = new Rect(rect.x, rect.y, rect.width - StageButtonWidth - 8f, rect.height);

        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleLeft;
        GUI.color = new Color(1f, 1f, 1f, 0.85f);
        Widgets.Label(label, "ChickenBatteryCage.Picker.StageHeader".Translate(
            StageLabel(stage),
            count));
        GUI.color = Color.white;

        // A per-stage action so an entire life stage can be marked without
        // ticking every bird: select every unmarked bird of the stage, or clear
        // the stage's ticks once they are all ticked. Hidden when the stage has
        // nothing left to select (every bird already queued).
        if (CountSelectableInStage(stage) > 0)
        {
            Rect button = new Rect(
                rect.xMax - StageButtonWidth - 4f,
                rect.y + (StageHeaderHeight - StageButtonHeight) / 2f,
                StageButtonWidth,
                StageButtonHeight);
            string key = AllSelectableInStageSelected(stage)
                ? "ChickenBatteryCage.Picker.ClearStage"
                : "ChickenBatteryCage.Picker.SelectStage";
            if (Widgets.ButtonText(button, key.Translate()))
            {
                ToggleStageSelection(stage);
            }
        }

        Widgets.DrawLineHorizontal(rect.x, rect.yMax - 2f, rect.width);
    }

    int CountSelectableInStage(CagedChickenStage stage)
    {
        int count = 0;
        foreach (Row row in rows)
        {
            if (row.stage == stage && !row.alreadyMarked)
            {
                count++;
            }
        }
        return count;
    }

    /// True when every unmarked bird in the stage is already ticked, so the
    /// stage action can offer to clear instead of select. A stage with no
    /// selectable birds is never "all selected".
    bool AllSelectableInStageSelected(CagedChickenStage stage)
    {
        int selectable = 0;
        foreach (Row row in rows)
        {
            if (row.stage != stage || row.alreadyMarked)
            {
                continue;
            }

            selectable++;
            if (!selected.ContainsKey(row.record))
            {
                return false;
            }
        }

        return selectable > 0;
    }

    /// Ticks every unmarked bird in the stage, or clears the stage's ticks when
    /// they are already all ticked.
    void ToggleStageSelection(CagedChickenStage stage)
    {
        bool clear = AllSelectableInStageSelected(stage);
        foreach (Row row in rows)
        {
            if (row.stage != stage || row.alreadyMarked)
            {
                continue;
            }

            if (clear)
            {
                selected.Remove(row.record);
            }
            else
            {
                selected[row.record] = row.cage;
            }
        }
    }

    void DrawRow(Rect rect, Row row)
    {
        if (Mouse.IsOver(rect))
        {
            Widgets.DrawHighlight(rect);
        }

        // A bird already queued for unloading shows ticked and greyed, so the
        // player can see at a glance what is already on its way out.
        Rect checkRect = new Rect(rect.x + 4f, rect.y + 3f, 20f, 20f);
        if (row.alreadyMarked)
        {
            // Already queued: show it ticked but not editable, and never add it
            // to the local selection (its mark is already on its cage).
            bool marked = true;
            GUI.color = new Color(1f, 1f, 1f, 0.4f);
            Widgets.Checkbox(checkRect.x, checkRect.y, ref marked, 20f, disabled: true);
            GUI.color = Color.white;
        }
        else
        {
            bool ticked = selected.ContainsKey(row.record);
            Widgets.Checkbox(checkRect.x, checkRect.y, ref ticked, 20f);
            if (ticked)
            {
                selected[row.record] = row.cage;
            }
            else
            {
                selected.Remove(row.record);
            }
        }

        Rect labelRect = new Rect(rect.x + 30f, rect.y, rect.width - AgeColumnWidth - StageColumnWidth - 30f, rect.height);
        Rect stageRect = new Rect(labelRect.xMax, rect.y, StageColumnWidth, rect.height);
        Rect ageRect = new Rect(stageRect.xMax, rect.y, AgeColumnWidth - 8f, rect.height);

        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleLeft;

        string label = row.alreadyMarked
            ? "ChickenBatteryCage.Picker.RowMarked".Translate(row.name, row.number)
            : "ChickenBatteryCage.Picker.Row".Translate(row.name, row.number);
        Widgets.Label(labelRect, label);

        GUI.color = StageColor(row.stage);
        Widgets.Label(stageRect, StageLabel(row.stage));
        GUI.color = Color.white;

        Text.Anchor = TextAnchor.MiddleRight;
        Widgets.Label(ageRect, AgeLabel(row.ageYears));
        Text.Anchor = TextAnchor.UpperLeft;
    }

    void DrawFooter(Rect rect)
    {
        Rect selectAll = new Rect(rect.x, rect.y, 110f, 30f);
        if (Widgets.ButtonText(selectAll, "ChickenBatteryCage.Picker.SelectAll".Translate()))
        {
            foreach (Row row in rows)
            {
                if (!row.alreadyMarked)
                {
                    selected[row.record] = row.cage;
                }
            }
        }

        Rect selectNone = new Rect(selectAll.xMax + 4f, rect.y, 110f, 30f);
        if (Widgets.ButtonText(selectNone, "ChickenBatteryCage.Picker.SelectNone".Translate()))
        {
            selected.Clear();
        }

        int count = selected.Count;
        Rect release = new Rect(rect.xMax - 170f, rect.y, 170f, 30f);
        bool canRelease = count > 0;
        if (!canRelease)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.5f);
        }

        if (Widgets.ButtonText(release, "ChickenBatteryCage.Picker.Release".Translate(count))
            && canRelease)
        {
            ReleaseSelected();
        }

        GUI.color = Color.white;
    }

    void ReleaseSelected()
    {
        int requested = 0;
        foreach (KeyValuePair<CagedChickenRecord, Building_ChickenBatteryCage> mark in selected)
        {
            Building_ChickenBatteryCage cage = mark.Value;
            if (cage != null && !cage.Destroyed && cage.RequestUnloadSpecific(mark.Key))
            {
                requested++;
            }
        }

        selected.Clear();

        if (requested > 0)
        {
            Messages.Message(
                "ChickenBatteryCage.Message.Marked".Translate(requested),
                MessageTypeDefOf.TaskCompletion,
                historical: false);
        }
    }

    static string StageLabel(CagedChickenStage stage)
    {
        switch (stage)
        {
            case CagedChickenStage.Chick:
                return "ChickenBatteryCage.Picker.StageChick".Translate();
            case CagedChickenStage.Juvenile:
                return "ChickenBatteryCage.Picker.StageJuvenile".Translate();
            default:
                return "ChickenBatteryCage.Picker.StageAdult".Translate();
        }
    }

    static Color StageColor(CagedChickenStage stage)
    {
        switch (stage)
        {
            case CagedChickenStage.Chick:
                return new Color(0.85f, 0.85f, 0.45f);
            case CagedChickenStage.Juvenile:
                return new Color(0.75f, 0.85f, 0.6f);
            default:
                return new Color(0.8f, 0.9f, 1f);
        }
    }

    /// Exact age, phrased the way this mod's inspection strings phrase it:
    /// whole years with the leftover days beside them.
    static string AgeLabel(float ageYears)
    {
        int years = (int)ageYears;
        int days = (int)((ageYears - years) * GenDate.DaysPerYear);
        return "ChickenBatteryCage.Picker.Age".Translate(years, days);
    }
}
