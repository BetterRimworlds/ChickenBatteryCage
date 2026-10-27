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

/**
 * The storage-style feed selector for a battery-cage cluster.
 *
 * Touching cages share one feed store, so they also share one list of allowed
 * foods. The window edits the cluster's effective selector and uses RimWorld's
 * own thing-filter tree, which is the same control a stockpile or shelf uses.
 * Only foods are shown; the selector narrows what haulers may bring, it cannot
 * turn an inedible item into feed.
 */
public class Window_CageFeedFilter : Window
{
    const float FooterHeight = 40f;

    readonly Building_ChickenBatteryCage cage;

    /// The foods the tree may offer. Built once per window, so opening the
    /// selector never rescans every def each frame.
    readonly ThingFilter selectable;

    /// Scroll and search state for RimWorld's filter tree. The UI keeps its own
    /// state object in 1.6 instead of a bare scroll vector.
    readonly ThingFilterUI.UIState state = new ThingFilterUI.UIState();

    public Window_CageFeedFilter(Building_ChickenBatteryCage cage)
    {
        this.cage = cage;
        selectable = CageFeed.CreateSelectableFilter();

        doCloseX = true;
        doCloseButton = false;
        closeOnCancel = true;
        closeOnClickedOutside = false;
        absorbInputAroundWindow = true;
        draggable = true;
        resizeable = true;
        onlyOneOfTypeAllowed = true;
        optionalTitle = "ChickenBatteryCage.FeedFilter.Title".Translate();
    }

    public override Vector2 InitialSize => new Vector2(620f, 620f);

    public override void DoWindowContents(Rect inRect)
    {
        if (cage == null || cage.Destroyed || cage.Map == null)
        {
            Close();
            return;
        }

        Rect content = inRect;
        content.height -= FooterHeight;

        ThingFilterUI.DoThingFilterConfigWindow(
            content,
            state,
            cage.ClusterFeedFilter,
            selectable,
            openMask: 8);

        Rect close = new Rect(inRect.xMax - 140f, inRect.yMax - 32f, 140f, 32f);
        if (Widgets.ButtonText(close, "CloseButton".Translate()))
        {
            Close();
        }
    }
}
