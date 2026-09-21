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

/**
 * A simple before/after benchmark for comparing free-range and virtualized
 * flocks.
 *
 * Begin a benchmark, run the colony for a fixed real-time window, then end it.
 * The log line reports ticks per second plus the population and thing counts,
 * so a 500- or 2,000-bird colony can be measured with and without cages.
 */
public static class CageProfiler
{
    struct Snapshot
    {
        public int ticks;
        public float realtime;
        public int pawns;
        public int things;
        public int caged;
        public int cages;
    }

    static Snapshot baseline;

    static bool hasBaseline;

    public static void Begin(Map map)
    {
        if (map == null)
        {
            return;
        }

        baseline = Capture(map);
        hasBaseline = true;
        Log.Message("[ChickenBatteryCage] Benchmark started: spawned pawns=" +
            baseline.pawns + ", things=" + baseline.things + ", caged=" + baseline.caged + ".");
    }

    public static void End(Map map, string label)
    {
        if (map == null)
        {
            return;
        }

        if (!hasBaseline)
        {
            Log.Warning("[ChickenBatteryCage] Benchmark ended without a baseline; call Begin first.");
            return;
        }

        Snapshot now = Capture(map);
        hasBaseline = false;

        float seconds = now.realtime - baseline.realtime;
        int ticks = now.ticks - baseline.ticks;
        float tps = seconds > 0.01f ? ticks / seconds : 0f;

        Log.Message(string.Format(
            "[ChickenBatteryCage] Benchmark '{0}': {1:0.0} TPS over {2:0.0}s; " +
            "spawned pawns {3}->{4}, things {5}->{6}, caged {7}->{8}, cages {9}.",
            label,
            tps,
            seconds,
            baseline.pawns,
            now.pawns,
            baseline.things,
            now.things,
            baseline.caged,
            now.caged,
            now.cages));
    }

    static Snapshot Capture(Map map)
    {
        Snapshot snapshot = new Snapshot();
        snapshot.realtime = Time.realtimeSinceStartup;
        snapshot.ticks = Find.TickManager.TicksGame;
        snapshot.pawns = map.mapPawns.AllPawnsSpawned.Count;
        snapshot.things = map.listerThings.AllThings.Count;

        foreach (Building_ChickenBatteryCage cage in CageNetwork.Cages(map))
        {
            snapshot.cages++;
            snapshot.caged += cage.ChickenCount;
        }

        return snapshot;
    }
}
