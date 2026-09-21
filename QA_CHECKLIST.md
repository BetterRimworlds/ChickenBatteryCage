# QA checklist — PR 4 → PR 12

Test on RimWorld 1.6 with only Harmony + BetterRimworlds.ChickenBatteryCage
enabled. PRs 1–3 are merged into `trunk`; the open chain is stacked, so test
each PR on a save made from the previous PR's build (or test the full
`pr12-chicken-picker` stack and tick items in order). After every PR step,
watch the player log for red errors, failed Harmony patch lines, and XML parse
warnings.

**Global pre-flight (run once per build):**

- [ ] Mod loads on RimWorld 1.6 with zero red errors.
- [ ] Log contains no "patch failed" lines from `BetterRimworlds.ChickenBatteryCage`.
- [ ] All mod XML files load without parse warnings (Defs, Jobs, Research, WorkGivers, Patches).
- [ ] `dotnet test` passes 70/70; `bash build.sh 1` runs green.

---

## PR 4 — Created the battery cage nutrition system (`pr4-cage-nutrition`, 5 commits)

*Collective feed store, elapsed-time consumption, starvation state, vanilla-hopper feeding.*

- [ ] Cage inspection shows a **nutrition/feed** figure that drains over in-game time.
- [ ] Drain scales with flock size (more hens = faster drain) and pauses while the game is paused.
- [ ] **Feeding is via an adjacent vanilla hopper:** colonists haul hay, kibble, or plants into a hopper touching the cage with the ordinary hauling pipeline, and the cage siphons nutrition from every hopper on its rare tick.
- [ ] The hopper is unlocked by the battery-cage research (`Patches/Research_VanillaHopper.xml` adds `ChickenBatteryCage` to the Hopper's `researchPrerequisites`).
- [ ] A cage with **no adjacent hopper** never gains feed and eventually starves; inspection explains the feed state.
- [ ] Feed stacks stay in the hopper and are not duplicated into the cage; the cage stores only abstract nutrition.
- [ ] The store caps at six days of feed per bird; extra feed stays in the hopper rather than being wasted.
- [ ] When nutrition hits zero, hens pass through **hungry** and **starving** states — no starving pawns are spawned.
- [ ] Starving birds recover once feed is added.
- [ ] Unroofed/inoperable cage: laying and the other simulation steps stop, and the panel explains the roof requirement.
- [ ] Save + reload preserves the cage's nutrition value exactly.

## PR 5 — Added statistical mortality for caged chickens (`pr5-statistical-mortality`, 4 commits)

*Periodic death rolls, age curves, starvation deaths, death tallies in inspection.*

- [ ] Caged chickens die statistically — no corpse, blood, or death tale is spawned per death; the bird is removed and tallied.
- [ ] Flock count declines slowly over in-game years even when well fed (natural mortality).
- [ ] Mortality rate rises sharply for elderly birds (age curve visible over a long run).
- [ ] Starving cages lose birds much faster than fed cages.
- [ ] Cage inspection shows a **mortality summary** (deaths by cause: natural vs. starvation) with correct running totals.
- [ ] Death tallies persist across save/load.
- [ ] Long-session check: the log shows one aggregated notice per evaluation, not one per death.

## PR 6 — Virtualized egg production inside battery cages (`pr6-virtualized-eggs`, 6 commits)

*Serialized hens, age-dependent laying, elapsed-time output, ten-egg boxes.*

> This PR also carries two unloading fixes (`Replaced the per-hen release memory with a manual network intake cutoff`, `Fixed the newborn-and-other-stages error when unloading a chicken`); exercise unloading alongside the egg checks.

- [ ] Only **hens** produce eggs; roosters/juveniles contribute nothing.
- [ ] Laying rate depends on hen age (young adults peak, very old hens lay little or none).
- [ ] Egg output is computed from **elapsed cage time** — a cage left alone overnight has a full accrual on return; output accrues while the game is unpaused, not on inspection.
- [ ] Output is **unfertilized eggs only** — no fertilized or chick-hatching eggs ever leave the cage.
- [ ] The cage behaves as an **egg box**: it collects eggs internally and, each time ten have accrued, releases one haulable stack of ten regular chicken eggs.
- [ ] Inspection shows **progress toward the next egg** as a percentage.
- [ ] Released eggs are ordinary vanilla chicken eggs: they stack, haul, trade, and satisfy "eggs" cooking bills like any other.
- [ ] No individual egg `Thing` is created while the eggs are still inside the box.
- [ ] Unloading a hen works for every life stage without the "newborn and other developmental stages" error appearing in the log.
- [ ] Releasing a hen switches the network's intake off (manual cutoff); no per-hen release memory is retained.
- [ ] Save + reload preserves the box's partial progress and accrued egg counts.

## PR 7 — Added cage-only poultry modules (`pr7-cage-modules`, 5 commits)

*Install-from-cage gizmos, Architect removal, cage-bound placement, climate controller.*

- [ ] Selecting a battery cage shows **module installation gizmos** (including the climate controller).
- [ ] Modules are **absent from the Architect/build menu** — the only way to install is via the cage.
- [ ] Installing a module enters placement mode restricted to the **selected cage's** attachment cells; placement elsewhere is refused.
- [ ] Climate controller installs, connects to power, and its status shows in cage inspection (laying-rate/climate readouts respond to it).
- [ ] Climate status cycles correctly: **active** when powered → **unpowered** when the cable is cut → **broken** after degradation/failure.
- [ ] With the controller broken or unpowered, cage output/comfort degrades and the inspection panel says why.
- [ ] Repairing or re-powering restores active status without reinstalling.
- [ ] Deconstructing a module (or the cage) detaches it cleanly; no ghost module entries remain.
- [ ] Save + reload keeps module assignment and its state.

## PR 9 — Hardened savegames and chicken-history cleanup (`pr9-savegame-hardening`, 4 commits)

*Save-size audit, trimmed serialization, reference cleanup, migration guards.*

- [ ] Build a colony with five or more full cages, save, and compare against a pre-PR-9 build if available — saves should be **equal or smaller**, not larger.
- [ ] Save + reload round trip: flock counts, ages, feed, eggs, egg-box progress, module states, and death tallies all survive exactly.
- [ ] **No orphaned references:** caged chickens never appear in world-pawn lists, wild-animal counts, wildlife tabs, relations, or animal-management windows while virtualized.
- [ ] Map load with a large flock shows no "could not resolve cross-reference" warnings.
- [ ] **Migration guards:** a malformed or partial cage record loads without a hard crash; the bad record is dropped and logged with a clear warning.
- [ ] A save from PR 7 still loads under this build without data loss.
- [ ] Repeated save/load cycles do not grow the file size.

## PR 10 — Profiled large industrial poultry colonies (`pr10-profiling`, 4 commits)

*Dev stress-test tools, benchmarks, reduced ticking, multi-year stability.*

- [ ] Dev mode exposes the **poultry stress-test tools** (spawn/fill commands per `Docs/Profiling.md`).
- [ ] Fill cages to capacity and spawn 2,000 free-range birds: FPS and tick time stay playable (caged-bird ticking should be measurably lighter than free-range).
- [ ] Benchmark run for free-range vs. virtualized populations produces numbers and completes without hitching.
- [ ] Battery cages with no flock or full feed **tick minimally**.
- [ ] Simulate **five in-game years** of feeding, starvation, mortality, and laying: no runaway slowdown, unbounded log growth, or repeated exception spam.
- [ ] Save file size after five years is not pathological.
- [ ] Memory usage does not creep upward across the multi-year run.

## PR 11 — Finished the RimWorld 1.6 battery-poultry release (`pr11-release`, 5 commits)

*Final textures, 4-tier graphics, sounds, strings, player-facing explanations, docs, build.*

- [ ] All four cage tiers have distinct, correct textures at normal/outline/selected sizes; no missing-texture (pink) states, including during the upgrade transition.
- [ ] Building/placement, feeding, and module interactions have **sound effects**; no "missing SoundDef" warnings.
- [ ] All UI strings render (inspection panel, gizmo labels, alerts); no raw `TranslationKey` placeholders.
- [ ] In-game **explanations of virtualization** read clearly in inspection and info cards.
- [ ] Mod info screen: About.xml name, author, packageId `HopeSeekr.BetterRimworlds.ChickenBatteryCage`, supported version 1.6, Harmony dependency listed.
- [ ] `Docs/Release.md` and the system documentation match actual behavior.
- [ ] `bash build.sh 1` green; `release.sh` zip contains `About/`, `Defs/`, `Assemblies/`, `Textures/`, `Languages/`, `Patches/` and nothing extraneous.
- [ ] Fresh-install test: drop the zip into a clean Mods folder → mod loads, and all the PR 4–10 smoke items pass at least superficially.

## PR 12 — Added a per-chicken picker window (`pr12-chicken-picker`, 5 commits)

*Per-chicken picker window, life-stage grouping, exact-record marks, network-wide flock.*

- [ ] Selecting a cage's **"Hens: N / M"** capacity gizmo opens the picker window; the old per-kind unload menu stays reachable as a "Bulk..." shortcut.
- [ ] The window lists **every chicken across the whole cage network**, grouped as chicks, juveniles, and adults, oldest-first within each group.
- [ ] Each row shows the def's life-stage/sex label plus a 1-based number; the number is display-only and renumbers as the flock changes.
- [ ] Ticking rows marks the **exact records**; "Select all/none" and per-stage selection work.
- [ ] Unloading releases exactly the ticked birds, even as records shift; the handler is sent to the owning cage.
- [ ] With the chicken def's life stages unresolved (unusual load order), rows fall back to Adult rather than crashing.
- [ ] Save + reload preserves pending unload marks, then completes the release.
- [ ] The picker does not materialize the rest of the flock — caged birds stay serialized while the window is open.

---

## Cross-cutting regression sweep (final build, `pr12-chicken-picker`)

- [ ] Full PR 4→12 flow in one continuous colony: research → build → fill → feed → starve → recover → lay → upgrade → modules → pick birds → 5-year run.
- [ ] Combined save/load at every stage; one save carried through the entire session.
- [ ] Load order / mod-compat smoke test with Harmony only, then with two or three common animal mods (no duplicate defNames, no patches double-applying).
- [ ] Every commit GPG-signed (`git log --format='%h %G?'` all `G`), 42 commits ahead of `trunk`.
- [ ] No `obj/`, `bin/`, `*.dll`, or `*.zip` staged for merge.
