# QA checklist — PR 4 → PR 11

Test on RimWorld 1.6 with only Harmony + BetterRimworlds.ChickenBatteryCage
enabled. PRs 1–3 are merged into `trunk`; the open chain is stacked, so test
each PR on a save made from the previous PR's build (or test the full
`pr11-feed-hopper-dairy` stack and tick items in order). After every PR step,
watch the player log for red errors, failed Harmony patch lines, and XML parse
warnings.

**Global pre-flight (run once per build):**

- [ ] Mod loads on RimWorld 1.6 with zero red errors.
- [ ] Log contains no "patch failed" lines from `BetterRimworlds.ChickenBatteryCage`.
- [ ] All mod XML files load without parse warnings (Defs, Jobs, Research, WorkGivers, Patches).
- [ ] `dotnet test` passes 119/119; `bash build.sh 1` runs green.

---

## PR 4 — Created the battery cage nutrition system (`pr4-cage-nutrition`, 6 commits)

*Collective feed store, elapsed-time consumption, starvation state, direct hauling of feed, and touching cages behaving as one giant cage.*

- [x] Cage inspection shows a **nutrition/feed** figure that drains over in-game time, with the estimated days of feed remaining; an empty store reads **starving** instead of a day estimate.
- [x] Drain scales with flock size (more hens = faster drain) and pauses while the game is paused.
- [ ] **Feeding is by direct hauling:** colonists carry raw plant food, hay, or kibble straight to a cage with the ordinary hauling pipeline, and the cage converts it immediately into the shared feed store. No hopper or nutrient paste is required.
- [ ] Haulers only start when the cluster's pool falls below half its capacity, then keep topping up until full, so flocks are fed in infrequent bursts rather than a continuous trickle.
- [ ] A **cluster with no reachable food stockpile** never gains feed and eventually starves; inspection explains the feed state.
- [ ] Food is destroyed on delivery and is never left lying on the cage; the cage stores only abstract nutrition.
- [ ] The store caps at **3.3 nutrition per bird (33 for a full ten-hen cage)** across the whole cluster; surplus food stays in the stockpile rather than being wasted.
- [ ] When nutrition hits zero, hens pass through **hungry** and **starving** states — no starving pawns are spawned.
- [ ] Starving birds recover once feed is hauled in.
- [ ] **Unroofed cages keep running — then fall apart.** If a roof is removed after construction, feed still drains and the flock still cycles through hungry/starving, but the inspection panel now warns that the cage is unroofed and deteriorating. The exposed cage loses hit points and collapses after about **30 days if made of wood or steel**, or **60 days for any other material**. It rots whether or not the sky is clear, but **bad weather — rain, fog, or snow — makes it rot four times as fast**.
- [ ] Re-roofing an unroofed cage before it collapses stops the damage; already-lost hit points are **not** restored automatically and must be repaired.
- [ ] Save + reload preserves the cage's nutrition value exactly.

**Touching cages — one giant cage:**

- [ ] Feeding any cage in a touching pair feeds **both**; a cage with an empty store still eats while a neighbour in its cluster has feed.
- [ ] Two cages sharing an edge **or a corner** share one hen count, one feed figure, one Allow toggle, and one unload/picker list.
- [ ] Cages that do **not** touch stay independent: separate hen counts, separate feed pools, and releasing one cluster's intake does not disable the other.
- [ ] Building a cage against an existing one folds it into the cluster immediately (no reload); deconstructing one splits the cluster and each survivor keeps its own share of feed.
- [ ] Each member still persists its own feed store; a save/load round trip preserves the shared pool split across members with no migration.
- [ ] The inspection **"Linked cages"** line appears only when the cluster holds more than one cage, and its totals match the sum of the members.

## PR 5 — Added statistical mortality for caged chickens (`pr5-statistical-mortality`, 6 commits)

*Periodic death rolls, age curves, starvation deaths, corpses dropped outside the cage.*

- [ ] Caged chickens die statistically — each death immediately drops a real chicken corpse on the ground outside the cage.
- [ ] If corpse generation or placement fails, the flock still loses the bird and the map retries delivery. Save/load retains the pending dead record or existing corpse; retrying never revives the bird or produces a second corpse.
- [ ] After deaths, repeated adult/juvenile unload requests are pruned to the surviving category counts. Mixed requests retain queue priority, and a death during unloading cannot consume the next request by mistake.
- [ ] Feeding between death rolls preserves prior starvation exposure. Newly admitted hens accrue mortality exposure only from entry; releasing a hen settles her outstanding exposure before materialization.
- [ ] Flock count declines slowly over in-game years even when well fed (natural mortality).
- [ ] Mortality rate rises sharply for elderly birds (age curve visible over a long run).
- [ ] Starving cages lose birds much faster than fed cages.
- [ ] Mortality rolls whether or not the cage is roofed; a roof change neither pauses nor advances the mortality clock.
- [ ] Cage inspection shows no deaths-by-cause summary.
- [ ] Long-session check: the log shows one aggregated notice per evaluation, not one per death.

## PR 6 — Virtualized egg production inside battery cages (`pr6-virtualized-eggs`, 6 commits)

*Serialized hens, age-dependent laying, elapsed-time output, network-sized egg stacks.*

> This PR also carries two unloading fixes (`Replaced the per-hen release memory with a manual network intake cutoff`, `Fixed the newborn-and-other-stages error when unloading a chicken`); exercise unloading alongside the egg checks.

- [ ] Only **hens** produce eggs; roosters/juveniles contribute nothing.
- [ ] Laying rate depends on hen age (young adults peak, very old hens lay little or none).
- [ ] Egg output is computed from **elapsed cage time** — a cage left alone overnight has a full accrual on return; output accrues while the game is unpaused, not on inspection.
- [ ] Output is **unfertilized eggs only** — no fertilized or chick-hatching eggs ever leave the cage.
- [ ] The cage behaves as an **egg box**: it collects eggs internally and releases one haulable stack of regular chicken eggs. A lone cage releases a ten-egg stack; touching cages share one box and release a single stack sized to one whole day of the whole network's laying (floored, minimum ten).
- [ ] Inspection shows **progress toward the next egg** as a percentage.
- [ ] Released eggs are ordinary vanilla chicken eggs: they stack, haul, trade, and satisfy "eggs" cooking bills like any other.
- [ ] No individual egg `Thing` is created while the eggs are still inside the box.
- [ ] Unloading a hen works for every life stage without the "newborn and other developmental stages" error appearing in the log.
- [ ] Releasing a hen switches its **cluster's** intake off (manual cutoff); no per-hen release memory is retained, and other clusters stay enabled.
- [ ] Save + reload preserves the box's partial progress and accrued egg counts.


## PR 7 — Hardened savegames and chicken-history cleanup (`pr7-savegame-hardening`, 4 commits)

*Save-size audit, trimmed serialization, reference cleanup, migration guards.*

- [ ] Build a colony with five or more full cages, save, and compare against a pre-PR-7 build if available — saves should be **equal or smaller**, not larger.
- [ ] Save + reload round trip: flock counts, ages, feed, eggs, egg-box progress, and death tallies all survive exactly.
- [ ] **No orphaned references:** caged chickens never appear in world-pawn lists, wild-animal counts, wildlife tabs, relations, or animal-management windows while virtualized.
- [ ] Map load with a large flock shows no "could not resolve cross-reference" warnings.
- [ ] **Migration guards:** a malformed or partial cage record loads without a hard crash; the bad record is dropped and logged with a clear warning.
- [ ] Repeated save/load cycles do not grow the file size.

## PR 8 — Profiled large industrial poultry colonies (`pr8-profiling`, 4 commits)

*Dev stress-test tools, benchmarks, reduced ticking, multi-year stability.*

- [ ] Dev mode exposes the **poultry stress-test tools** (spawn/fill commands per `Docs/Profiling.md`).
- [ ] Fill cages to capacity and spawn 2,000 free-range birds: FPS and tick time stay playable (caged-bird ticking should be measurably lighter than free-range).
- [ ] The **Begin/End benchmark** profiler entries appear only with God Mode on, and never in a normal game.
- [ ] Benchmark run for free-range vs. virtualized populations produces numbers and completes without hitching.
- [ ] Battery cages with no flock or full feed **tick minimally**.
- [ ] Simulate **five in-game years** of feeding, starvation, mortality, and laying: no runaway slowdown, unbounded log growth, or repeated exception spam.
- [ ] Save file size after five years is not pathological.
- [ ] Memory usage does not creep upward across the multi-year run.

## PR 9 — Finished the RimWorld 1.6 battery-poultry release (`pr9-release`, 5 commits)

*Final textures, 4-tier graphics, sounds, strings, player-facing explanations, docs, build.*

- [ ] The four-tier cage graphic is correct at normal, outline, and selected sizes, with no missing-texture (pink) states.
- [ ] Building/placement and feeding have **sound effects**; no "missing SoundDef" warnings.
- [ ] All UI strings render (inspection panel, gizmo labels, alerts); no raw `TranslationKey` placeholders.
- [ ] In-game **explanations of virtualization** read clearly in inspection and info cards.
- [ ] Mod info screen: About.xml name, author, packageId `HopeSeekr.BetterRimworlds.ChickenBatteryCage`, supported version 1.6, Harmony dependency listed.
- [ ] `Docs/Release.md` and the system documentation match actual behavior, including the **"Touching cages are one giant cage"** section in `Docs/Architecture.md`.
- [ ] Cage inspection sums the laying rate across the cluster, not just the selected cage.
- [ ] `bash build.sh 1` green; `release.sh` zip contains `About/`, `Defs/`, `Assemblies/`, `Textures/`, `Languages/`, `Patches/` and nothing extraneous.
- [ ] Fresh-install test: drop the zip into a clean Mods folder → mod loads, and all the PR 4–8 smoke items pass at least superficially.

## PR 10 — Added a per-chicken picker window (`pr10-chicken-picker`, 5 commits)

*Per-chicken picker window, life-stage grouping, exact-record marks, cluster-wide flock.*

- [ ] Selecting a cage's **"Hens: N / M"** capacity gizmo opens the picker window; the old per-kind unload menu stays reachable as a "Bulk..." shortcut.
- [ ] The window lists **every chicken across the whole touching cluster**, grouped as chicks, juveniles, and adults, oldest-first within each group; opening it from any member shows the same flock.
- [ ] Hens in a cage that does **not** touch the opened cage are absent from the window, so separate clusters keep separate lists.
- [ ] Each row shows the def's life-stage/sex label plus a 1-based number; the number is display-only and renumbers as the flock changes.
- [ ] Ticking rows marks the **exact records**; "Select all/none" and per-stage selection work.
- [ ] Unloading releases exactly the ticked birds, even as records shift; the handler is sent to the owning cage.
- [ ] With the chicken def's life stages unresolved (unusual load order), rows fall back to Adult rather than crashing.
- [ ] Save + reload preserves pending unload marks, then completes the release.
- [ ] The picker does not materialize the rest of the flock — caged birds stay serialized while the window is open.

## PR 11 — Forbade dairy in battery-cage feed (`pr11-feed-hopper-dairy`, 3 commits)

*Pure dairy classifier, feed exclusion, mod setting.*

- [ ] The dairy rule is on by default: a dairy food that would otherwise qualify as feed is refused by the cage.
- [ ] Non-dairy feed (hay, kibble, raw plants) still feeds the cage normally.
- [ ] Fluids that are not animal products (beer, psychite tea) are **not** treated as dairy.
- [ ] The mod setting **"Forbid dairy in battery-cage feed?"** is on by default; turning it off lifts the refusal.
- [ ] Extra def names typed into the settings list (comma/space separated) are refused; blanks and unknown names are ignored.
- [ ] The dairy rule classifies by food type, so a modded animal-product/fluid milk is refused without being listed.
- [ ] Save + reload preserves the dairy toggle and the extra-name list.

---

## Realistic manure, fertilizer, and cage sanitation (`pr11-realism`)

*Virtual waste accumulation, batch cleaning into fertilizer packs, pollution-
style terrain improvement, and virtualized Toxic Buildup.*

**Production and accumulation**

- [ ] With 50 caged hens, waste reads about **1 pack a day**; 10 hens about 0.2
      packs a day; 100 hens about 2 packs a day; 200 hens about 4 packs a day.
- [ ] Waste accrues while the game is unpaused and from elapsed time, not from
      inspection: a cage left alone overnight shows the same total on return.
- [ ] No manure `Thing` and no per-hen waste object ever appears; the cage
      inspect panel simply reports a larger stored figure.
- [ ] Fractional progress accumulates across rare ticks and survives a
      save/reload without rounding to zero or duplicating.

**Cleaning**

- [ ] A clean cage offers no cleaning job.
- [ ] Once the cluster passes its threshold (about 10 packs for 100 hens), a
      handler cleans it; the job empties every whole pack and drops that many
      `fertilizer pack` stacks beside the cage.
- [ ] The fractional remainder stays in the cage after a service.
- [ ] Touching cages are cleaned as one cluster: the pooled waste is drained
      once, and the fractional remainder is spread across members.

**Fertilizer packs**

- [ ] A `fertilizer pack` stacks five to a tile, is haulable and stockpilable,
      and uses a distinct custom texture rather than vanilla art.
- [ ] Outdoors, a pack dissolves one at a time on natural ground: every
      **eight days**, or every **four days** in rain or snow. Indoors it does
      not dissolve on its own, even on rough cave stone. On a constructed floor
      or a shelf it does not dissolve. Freezing stops the outdoor clock.
- [ ] A grower can spread one pack immediately, including on an underground
      cave floor. Right-click a pack on natural ground and choose **spread
      fertilizer**, or leave Growing enabled and a grower will do it after
      sowing. Packs on a floor or a shelf are not offered. The work takes a
      few seconds and improves the ground the same way the outdoor clock does.
- [ ] Deterioration matches a wastepack (rate 4, 100 hit points). Destroying
      the stack by wear or any other damage fertilizes the ground for every
      pack still in it, then the stack is gone.
- [ ] The inspect line says how long until the next pack, and notes rain when
      it applies. An indoor pack says it will not dissolve on its own. A pack
      on a floor or shelf says it is stored. A frozen outdoor pack on natural
      ground says it will not dissolve.
- [ ] Each pack the stack held improves **six** nearby natural cells, growing an
      irregular patch outward from the pack, not six cells chosen independently.
- [ ] Fertilizer works a progression toward **rich soil**: soft sand → sand, sand
      and rough natural cave stone → stony soil, stony soil → lichen-covered
      soil, marshy soil → stony soil, lichen-covered soil → soil, and soil →
      rich soil; the resulting patch is comparable in size and raggedness to the
      pollution patch the same number of wastepacks would make.
- [ ] Constructed floors, bridges, roads, water, and buildings are never
      replaced; natural rock walls block the spread exactly as they block
      pollution.
- [ ] Ground that is already rich soil or riverbank is left alone rather than
      re-improved.
- [ ] Fertilizer never creates pollution, tox gas, or wastepack infestations.

**Toxic buildup**

- [ ] A cage below its safe waste band adds no toxic buildup.
- [ ] Neglecting a cage past the safe band raises the flock's virtual Toxic
      Buildup at vanilla's polluted-ground rate; cleaning lets it recover at
      vanilla's natural rate instead of resetting instantly.
- [ ] Prolonged neglect produces the full range of vanilla Toxic Buildup
      severity stages and eventually costs birds.
- [ ] Unloading a hen from a dirty cage gives her the real `Toxic Buildup`
      Hediff at the stored severity, without spawning pawns to hold it while she
      was caged.

**Inspection and dev tools**

- [ ] A cage's inspect panel shows waste stored / capacity, cleaning status
      (clean / needs cleaning / critical), fertilizer yield if cleaned now, and
      Toxic Buildup severity with a stable / increasing / recovering trend.
- [ ] For a touching cluster, the panel shows cluster totals.
- [ ] Dev mode offers: add 1 / 5 / 45 fertilizer packs, advance fertilizer
      decomposition, fill a cage's waste, clean a cage instantly, force toxic
      progression / recovery, and simulate 10 / 60 / 600 days.

---

## Cross-cutting regression sweep (final build, `pr11-feed-hopper-dairy`)

- [ ] Full PR 4→11 flow in one continuous colony: research → build → fill → feed → starve → recover → lay → build a touching cage and watch it join the cluster → pick birds → refuse dairy → 5-year run.
- [ ] **Cluster topology sweep:** build a row of three touching cages plus one detached cage; the three share hen count/feed/picker/toggle, the detached one stays independent; deconstruct the middle cage and confirm the survivors split without losing feed or birds.
- [ ] Combined save/load at every stage; one save carried through the entire session.
- [ ] Load order / mod-compat smoke test with Harmony only, then with two or three common animal mods (no duplicate defNames, no patches double-applying).
- [ ] Every commit GPG-signed (`git log --format='%h %G?'` all `G`) across the `pr4` → `pr11` stack ahead of `trunk`.
- [ ] No `obj/`, `bin/`, `*.dll`, or `*.zip` staged for merge.
