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
using Verse;

namespace BetterRimworlds.ChickenBatteryCage;

/**
 * Remembers hens that were just released so handlers do not immediately rope
 * them back into a battery cage.
 *
 * The table lives only for the current game: thingIDNumbers are reused across
 * saves, so a memory that outlived the game could wrongly block a fresh hen.
 * Whenever a different Game becomes current (a save was loaded or a new game
 * was started) the table is wiped.
 */
public static class CageHenReleaseMemory
{
    const int CooldownTicks = 4 * 2500;

    const int PruneThreshold = 64;

    static readonly Dictionary<int, int> untilTickById = new Dictionary<int, int>();

    static Game rememberedGame;

    public static void Clear()
    {
        untilTickById.Clear();
    }

    public static void Mark(Pawn pawn)
    {
        if (pawn == null)
        {
            return;
        }

        ForgetIfNewGame();

        if (untilTickById.Count >= PruneThreshold)
        {
            PruneExpired();
        }

        untilTickById[pawn.thingIDNumber] = Find.TickManager.TicksGame + CooldownTicks;
    }

    public static bool IsRecentlyReleased(Pawn pawn)
    {
        if (pawn == null)
        {
            return false;
        }

        ForgetIfNewGame();

        if (!untilTickById.TryGetValue(pawn.thingIDNumber, out int untilTick))
        {
            return false;
        }

        if (!IsPending(Find.TickManager.TicksGame, untilTick))
        {
            untilTickById.Remove(pawn.thingIDNumber);
            return false;
        }

        return true;
    }

    /**
     * True while the deadline is still ahead. TicksGame is an int that wraps,
     * so the plain "now >= untilTick" test fails on a deadline stored across
     * the boundary; the unsigned time left keeps counting down instead.
     */
    static bool IsPending(int now, int untilTick)
    {
        uint remaining = unchecked((uint)(untilTick - now));
        return remaining > 0 && remaining <= (uint)CooldownTicks;
    }

    static void ForgetIfNewGame()
    {
        if (!ReferenceEquals(rememberedGame, Current.Game))
        {
            rememberedGame = Current.Game;
            untilTickById.Clear();
        }
    }

    static void PruneExpired()
    {
        int now = Find.TickManager.TicksGame;
        List<int> expired = null;
        foreach (KeyValuePair<int, int> entry in untilTickById)
        {
            if (!IsPending(now, entry.Value))
            {
                (expired ??= new List<int>()).Add(entry.Key);
            }
        }

        if (expired != null)
        {
            foreach (int id in expired)
            {
                untilTickById.Remove(id);
            }
        }
    }
}
