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
 * This reproduces the shape of RimWorld's own PollutionUtility.GrowPollutionAt.
 * The caller supplies one cell, the number of cells to improve, and three
 * predicates: whether a cell is already improved, whether it can be improved,
 * and how attractive it is. They are the exact analogues of the vanilla
 * pollution grid's IsPolluted, CanPollute, and PollutionCellComparer.
 *
 * The algorithm is deliberately the same, not an approximation:
 *
 *   1. If the root cell can be improved, improve it first.
 *   2. Flood-fill the connected region of already-improved cells reachable
 *      from the root. Vanilla does this with map.floodFiller over polluted
 *      cells; the passable set here is the improved set.
 *   3. Seed a best-first queue with the improvable neighbours of that region.
 *   4. Repeatedly take the most attractive cell, mark it improved, spend one
 *      unit, and push its improvable neighbours. This is what makes the patch
 *      irregular and makes repeated dissolution grow outward instead of
 *      re-selecting cells it has already improved.
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
        var chosen = new List<Cell>();
        if (count <= 0 || inBounds == null || isImproved == null || canImprove == null)
        {
            return chosen;
        }

        var chosenSet = new HashSet<Cell>();
        bool InB(Cell c) => inBounds(c.X, c.Z);
        bool Improved(Cell c) => chosenSet.Contains(c) || isImproved(c.X, c.Z);
        bool CanImp(Cell c) => !chosenSet.Contains(c) && canImprove(c.X, c.Z);

        if (InB(root) && CanImp(root))
        {
            Choose(root);
            if (count <= 0)
            {
                return chosen;
            }
        }

        // Flood-fill the improved region reachable from the root.
        var improved = new List<Cell>();
        var visited = new HashSet<Cell>();
        var open = new Queue<Cell>();
        if (InB(root) && Improved(root))
        {
            visited.Add(root);
            open.Enqueue(root);
        }

        while (open.Count > 0)
        {
            Cell c = open.Dequeue();
            improved.Add(c);
            foreach ((int dx, int dz) in Cardinal)
            {
                var n = new Cell(c.X + dx, c.Z + dz);
                if (InB(n) && !visited.Contains(n) && Improved(n))
                {
                    visited.Add(n);
                    open.Enqueue(n);
                }
            }
        }

        if (improved.Count == 0)
        {
            return chosen;
        }

        var queue = new ScoreQueue(score);
        var queued = new HashSet<Cell>();

        void PushNeighbours(Cell c)
        {
            foreach ((int dx, int dz) in Cardinal)
            {
                var n = new Cell(c.X + dx, c.Z + dz);
                if (InB(n) && CanImp(n) && queued.Add(n))
                {
                    queue.Push(n);
                }
            }
        }

        foreach (Cell c in improved)
        {
            PushNeighbours(c);
        }

        while (count > 0 && queue.Count > 0)
        {
            Cell c = queue.Pop();
            queued.Remove(c);
            if (!CanImp(c))
            {
                continue;
            }

            Choose(c);
            PushNeighbours(c);
        }

        return chosen;

        void Choose(Cell c)
        {
            chosen.Add(c);
            chosenSet.Add(c);
            count--;
            onImproved?.Invoke(c);
        }
    }

    /**
     * A binary max-heap keyed by the caller's score. It mirrors vanilla's
     * FastPriorityQueue<IntVec3> with the PollutionCellComparer, where a higher
     * score is served first. Ties keep the earlier-pushed cell ahead, which
     * keeps a synthetic grid deterministic.
     */
    sealed class ScoreQueue
    {
        readonly List<(Cell cell, float score, long order)> heap =
            new List<(Cell, float, long)>();

        readonly Func<int, int, float> score;
        long nextOrder;

        public ScoreQueue(Func<int, int, float> score)
        {
            this.score = score ?? ((_, _) => 0f);
        }

        public int Count => heap.Count;

        public void Push(Cell cell)
        {
            float value = score(cell.X, cell.Z);
            heap.Add((cell, value, nextOrder++));
            SiftUp(heap.Count - 1);
        }

        public Cell Pop()
        {
            (Cell cell, float value, long order) top = heap[0];
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
        int Rank((Cell cell, float score, long order) a, (Cell cell, float score, long order) b)
        {
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
