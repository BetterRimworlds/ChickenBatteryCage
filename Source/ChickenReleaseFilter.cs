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

namespace BetterRimworlds.ChickenBatteryCage;

/**
 * How a player chooses which serialized chicken leaves a battery cage.
 *
 * Every option is evaluated against derived biological age, so the flock can
 * be managed without materializing all of its members.
 */
public enum ChickenReleaseFilter
{
    Youngest,
    Oldest,
    Random,
    AdultHen,
    Juvenile,
}
