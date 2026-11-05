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

using System;
using System.Collections.Generic;

namespace BetterRimworlds.ChickenBatteryCage;

/**
 * The Verse-free core of the fertilizer spread.
 *
 * Searches connected natural ground for cells needing rehabilitation.
 * The caller supplies one cell, the number of cells to improve, and three
 * predicates: whether a cell is already improved, whether it can be improved,
 * and how attractive it is. They are the exact analogues of the vanilla
 * pollution grid's IsPolluted, CanPollute, and PollutionCellComparer.
 *
 * Floods through both finished and improvable ground, respecting barriers.
 * A queue serves the caller's highest distance-and-fertility scores first.
 * Stack applications reuse this discovery, updating each
 * struck cell's priority as its terrain changes.
 *
 * Keeping the mechanics here lets the terrain rule be exercised against a
 * synthetic grid without a running game, while the map-facing caller supplies
 * the vanilla Perlin-noise score and applies the terrain change.
 */
public static class FertilizerSpreadMath
{
    /// A map cell's horizontal coordinates. Coordinates are enough for the
    /// spread; the caller maps them back to its own cell type.
    public readonly struct Cell : IEquatable<Cell>
    {
        public readonly int X;
        public readonly int Z;

        public Cell(int x, int z)
        {
            X = x;
            Z = z;
        }

        public bool Equals(Cell other)
        {
            return X == other.X && Z == other.Z;
        }

        public override bool Equals(object obj)
        {
            return obj is Cell other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (X * 397) ^ Z;
        }
    }

    /// Four-way adjacency, matching both the vanilla flood filler and
    /// PollutionUtility's neighbour scan. Order matches GenAdj's tables, though
    /// the exact order only affects tie-breaking.
    static readonly (int dx, int dz)[] Cardinal =
    {
        (0, -1),
        (-1, 0),
        (0, 1),
        (1, 0),
    };

    /**
     * Improves up to <paramref name="count"/> cells growing outward from the
     * root and returns them in the order they were chosen.
     *
     * <paramref name="isImproved"/> tells whether a cell already counts as
     * improved and can therefore be flooded through.
     * <paramref name="canImprove"/> tells whether a cell can be improved.
     * <paramref name="score"/> ranks improvable cells; higher is chosen first.
     * <paramref name="onImproved"/>, when supplied, is invoked for the root and
     * for every cell the moment it is chosen, so the caller can apply the
     * terrain change immediately and let the score see the growing patch, just
     * as vanilla sets the pollution flag as it goes.
     */
    public static List<Cell> GrowAt(
        Cell root,
        int count,
        Func<int, int, bool> inBounds,
        Func<int, int, bool> isImproved,
        Func<int, int, bool> canImprove,
        Func<int, int, float> score,
        Action<Cell> onImproved = null)
    {
        return GrowMany(root, count, 1, inBounds, isImproved, canImprove, score, onImproved);
    }

    /// Searches connected working ground once, then applies separate pack strikes.
    /// Larger priority values identify poorer terrain; scores break ties only.
    public static List<Cell> GrowMany(
        Cell root,
        int count,
        int packs,
        Func<int, int, bool> inBounds,
        Func<int, int, bool> isImproved,
        Func<int, int, bool> canImprove,
        Func<int, int, float> score,
        Action<Cell> onImproved = null,
        Func<int, int, int> priority = null)
    {
        var chosen = new List<Cell>();
        if (count <= 0 || packs <= 0 || inBounds == null || isImproved == null || canImprove == null)
        {
            return chosen;
        }

        bool InB(Cell c) => inBounds(c.X, c.Z);
        bool CanImp(Cell c) => canImprove(c.X, c.Z);
        bool Passable(Cell c) => CanImp(c) || isImproved(c.X, c.Z);

        // Ordinary soil must not hide poorer ground beyond it.
        var queue = new ScoreQueue(score, priority);
        var visited = new HashSet<Cell>();
        var open = new Queue<Cell>();
        if (InB(root) && Passable(root))
        {
            visited.Add(root);
            open.Enqueue(root);
        }

        while (open.Count > 0)
        {
            Cell c = open.Dequeue();
            if (CanImp(c))
            {
                queue.Push(c);
            }
            foreach ((int dx, int dz) in Cardinal)
            {
                var n = new Cell(c.X + dx, c.Z + dz);
                if (InB(n) && visited.Add(n) && Passable(n))
                {
                    open.Enqueue(n);
                }
            }
        }

        var struck = new List<Cell>();
        for (int pack = 0; pack < packs && queue.Count > 0; pack++)
        {
            struck.Clear();
            while (struck.Count < count && queue.Count > 0)
            {
                Cell c = queue.Pop();
                if (!CanImp(c))
                {
                    continue;
                }

                chosen.Add(c);
                struck.Add(c);
                onImproved?.Invoke(c);
            }

            // Reinsert only after the pack: each cell receives at most one hit
            // per pack. Recompute priority after any terrain transformation.
            foreach (Cell c in struck)
            {
                if (CanImp(c))
                {
                    queue.Push(c);
                }
            }
        }

        return chosen;
    }

    /**
     * A binary max-heap keyed by the caller's score. It mirrors vanilla's
     * FastPriorityQueue<IntVec3> with the PollutionCellComparer, where a higher
     * score is served first. Ties keep the earlier-pushed cell ahead, which
     * keeps a synthetic grid deterministic.
     */
    sealed class ScoreQueue
    {
        readonly List<(Cell cell, float score, long order, int priority)> heap =
            new List<(Cell, float, long, int)>();

        readonly Func<int, int, float> score;
        readonly Func<int, int, int> priority;
        long nextOrder;

        public ScoreQueue(Func<int, int, float> score, Func<int, int, int> priority)
        {
            this.score = score ?? ((_, _) => 0f);
            this.priority = priority ?? ((_, _) => 0);
        }

        public int Count => heap.Count;

        public void Push(Cell cell)
        {
            float value = score(cell.X, cell.Z);
            heap.Add((cell, value, nextOrder++, priority(cell.X, cell.Z)));
            SiftUp(heap.Count - 1);
        }

        public Cell Pop()
        {
            var top = heap[0];
            int last = heap.Count - 1;
            heap[0] = heap[last];
            heap.RemoveAt(last);
            if (heap.Count > 0)
            {
                SiftDown(0);
            }

            return top.cell;
        }

        /// Positive when <paramref name="a"/> outranks <paramref name="b"/>.
        int Rank((Cell cell, float score, long order, int priority) a,
            (Cell cell, float score, long order, int priority) b)
        {
            if (a.priority != b.priority)
            {
                return a.priority.CompareTo(b.priority);
            }

            if (a.score > b.score)
            {
                return 1;
            }

            if (a.score < b.score)
            {
                return -1;
            }

            return a.order < b.order ? 1 : (a.order > b.order ? -1 : 0);
        }

        void SiftUp(int index)
        {
            while (index > 0)
            {
                int parent = (index - 1) / 2;
                if (Rank(heap[index], heap[parent]) <= 0)
                {
                    break;
                }

                (heap[index], heap[parent]) = (heap[parent], heap[index]);
                index = parent;
            }
        }

        void SiftDown(int index)
        {
            int count = heap.Count;
            while (true)
            {
                int best = index;
                int left = 2 * index + 1;
                int right = left + 1;
                if (left < count && Rank(heap[left], heap[best]) > 0)
                {
                    best = left;
                }

                if (right < count && Rank(heap[right], heap[best]) > 0)
                {
                    best = right;
                }

                if (best == index)
                {
                    break;
                }

                (heap[index], heap[best]) = (heap[best], heap[index]);
                index = best;
            }
        }
    }
}
