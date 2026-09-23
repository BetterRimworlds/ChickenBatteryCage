# PLAN.md — RimWorld 1.6 Chicken Battery Cage Mod

## Purpose

Create a RimWorld **1.6-only** mod that provides a high-performance battery-cage system for chickens.

The core goal is not merely to add a new livestock building. The mod must remove the major runtime and savegame costs caused by very large chicken populations by **serializing the biological concept of each chicken while it is inside a cage**, rather than keeping that chicken as a spawned `Pawn`.

When a chicken enters a battery cage:

1. Record only the minimum biological data needed to reconstruct an equivalent chicken later.
2. Record the exact game tick when it entered the cage.
3. Aggressively remove the original Pawn and its unnecessary historical/world references without treating entry as death.
4. Simulate feeding, aging, egg production, starvation, and mortality mathematically while the chicken remains serialized.
5. Generate a **new chicken Pawn** only when a chicken is released back onto the map.

The original Pawn identity does not need to survive.

---

## Peer Review — Pull Request Titles

Every pull request below is stacked on the one before it, so the review diff
for a given PR is simply `<base>..<branch>`. PRs 1–3 are merged into `trunk`;
PRs 4–12 are open and local.

| PR | Title | Branch | Base | Commits | Status |
|---:|-------|--------|------|--------:|--------|
| 1 | Established the RimWorld 1.6 poultry-cage foundation | `battery-cage-foundation` | — | 9 | Merged |
| 2 | Virtualized chickens inside battery cages | `loading-and-unloading` | `battery-cage-foundation` | 5 | Merged |
| 3 | Deconstructed and destroyed battery cages safely | `deconstructing-and-destroying` | `loading-and-unloading` | 3 | Merged |
| 4 | Created the battery cage nutrition system | `pr4-cage-nutrition` | `trunk` | 5 | Open |
| 5 | Added statistical mortality for caged chickens | `pr5-statistical-mortality` | `pr4-cage-nutrition` | 4 | Open |
| 6 | Virtualized egg production inside battery cages | `pr6-virtualized-eggs` | `pr5-statistical-mortality` | 6 | Open |
| 7 | Added cage-only poultry modules | `pr7-cage-modules` | `pr6-virtualized-eggs` | 5 | Open |
| 9 | Hardened savegames and chicken-history cleanup | `pr9-savegame-hardening` | `pr7-cage-modules` | 4 | Open |
| 10 | Profiled large industrial poultry colonies | `pr10-profiling` | `pr9-savegame-hardening` | 4 | Open |
| 11 | Finished the RimWorld 1.6 battery-poultry release | `pr11-release` | `pr10-profiling` | 5 | Open |
| 12 | Added a per-chicken picker window for unloading battery cages | `pr12-chicken-picker` | `pr11-release` | 5 | Open |

---

# Non-Negotiable Architecture

## Target Version

- RimWorld **1.6 only**.
- Do not implement backward compatibility with RimWorld 1.5 or earlier.
- Avoid compatibility abstractions that exist only to support previous versions.

## Core Performance Rule

> Never instantiate a Pawn or individual egg merely to simulate something that happened while it was inside the battery-cage system.

Examples of forbidden implementation patterns:

```text
virtual chicken starved
→ instantiate Pawn
→ kill Pawn
→ create corpse/history
→ clean it up
```

and:

```text
hen produced 10 eggs
→ instantiate 10 vanilla Egg Things
→ merge them into a custom item
```

Instead:

```text
serialized chicken
→ mathematical state update
→ serialized chicken removed if dead
```

and:

```text
egg-production calculation
→ increment abstract egg count
→ materialize a stack only once the box fills
```

---

# Initial Gameplay Design

| Feature | Initial Design |
|---|---|
| RimWorld version | 1.6 |
| Technology | Medieval |
| Cage footprint | 2×3 cells |
| Capacity | 10 chickens |
| Vertical tiers | 4 |
| Roof requirement | Required |
| Water requirement | None, matching vanilla RimWorld animals |
| Chicken identity | Not preserved |
| Biological age | Preserved exactly |
| Gender | Preserved |
| Growth/development | Preserved or exactly reconstructable |
| Feeding | Collective cage-level feeding |
| Eggs | Unfertilized |
| Egg packaging | Egg box, released as stacks of ten |
| Mortality | Statistical and age/starvation dependent |
| Modules | Visible only from selected cage |
| Module Architect visibility | Hidden from normal Build menu |

---

# Chicken Serialization Model

The cage must store lightweight chicken records rather than full Pawn graphs.

Suggested conceptual structure:

```csharp
public struct CagedChickenRecord
{
    public long biologicalAgeTicksAtEntry;
    public int enteredAtGameTick;
    public Gender gender;

    // Add only if needed to reconstruct vanilla behavior accurately.
    public float growthAtEntry;
}
```

Before adding any field, ask:

> Will losing this information materially change the reconstructed chicken's biological/gameplay state?

If the answer is no, do not serialize it.

Do not preserve by default:

- `ThingID`
- name
- jobs
- needs trackers
- memories
- pathing state
- map position
- records
- corpse state
- dead-pawn state
- WorldPawns bookkeeping
- social relationships
- history/tales

Exceptions must be justified by observed gameplay requirements.

---

# Exact Aging Model

Do not update every chicken's age each tick, RareTick, or LongTick.

Store:

```text
biologicalAgeTicksAtEntry
enteredAtGameTick
```

Then derive exact current age only when needed:

```text
currentBiologicalAgeTicks =
    biologicalAgeTicksAtEntry
    + (currentGameTick - enteredAtGameTick)
```

This ensures:

- exact biological aging;
- zero per-chicken age ticking;
- correct age after save/load;
- correct age after long periods in storage;
- accurate release age;
- accurate breeding-age and mortality calculations.

RimWorld uses approximately **60,000 ticks per in-game day**. The implementation must use the game's actual tick system rather than hardcoding assumptions where a direct API is available.

---

# Pull Request 1 — Established the RimWorld 1.6 poultry-cage foundation

## Goal

Created a loadable RimWorld 1.6 mod containing the basic four-tier battery cage, research, placement requirements, and inspection UI.

No chicken virtualization was implemented yet.

## Commits

### Commit 1 — `Created the RimWorld 1.6 mod skeleton`

Implemented:

- `About/`
- `Defs/`
- `Languages/`
- `Source/`
- `Textures/`
- RimWorld 1.6 metadata
- assembly/project references
- namespace structure
- mod bootstrap
- Harmony bootstrap only if actually required

Acceptance criteria:

- Mod loaded without errors on RimWorld 1.6.
- No compatibility code existed for older RimWorld versions.

---

### Commit 2 — `Added the medieval battery-cage research project`

Implemented a Medieval research project for organized confined poultry husbandry.

Acceptance criteria:

- Research appeared in the appropriate tech progression.
- Research wording described the husbandry system rather than implying that cages or chickens had just been invented.

---

### Commit 3 — `Added the four-tier battery chicken cage`

Implemented the base cage:

```text
Footprint: 2×3
Capacity:  10 chickens
Tiers:     4
Tech:      Medieval
```

Acceptance criteria:

- Cage could be built normally after research.
- Graphics clearly communicated a vertically stacked four-level cage system.

---

### Commit 4 — `Required battery cages to be constructed under roofs`

Implemented roof-placement validation.

Acceptance criteria:

- Cage placement failed if required cells were not roofed.
- Existing cages became inoperable rather than destroyed if roofing was later removed.
- Inspection UI explained the roof requirement.

---

### Commit 5 — `Added battery-cage inspection and capacity gizmos`

Initial inspection UI displayed:

```text
Chickens:       0 / 10
Adult hens:     0
Adult roosters: 0
Juveniles:      0
Feed:           —
Eggs:           —
```

Acceptance criteria:

- Cage status was visible without opening a debug menu.
- UI architecture was reusable by later PRs.

---

# Pull Request 2 — Virtualized chickens inside battery cages

## Goal

Implemented the central chicken-serialization architecture.

## Commits

### Commit 1 — `Added lightweight serialized chicken records`

Implemented a minimal persisted chicken record.

Acceptance criteria:

- No complete Pawn objects were stored inside the cage.
- The serialized structure contained only biologically meaningful reconstruction data.

---

### Commit 2 — `Derived caged chicken ages from elapsed game ticks`

Implemented lazy age calculation using entry age and entry tick.

Acceptance criteria:

- A chicken stored for one in-game day emerged exactly one in-game day older.
- Saving and loading did not alter age calculations.
- No scheduled task incremented individual chicken ages.

---

### Commit 3 — `Serialized chickens when they entered battery cages`

Implemented the loading job:

```text
Colonist reserved chicken
→ Colonist carried chicken to cage
→ Cage validated capacity
→ Biological state was serialized
→ Original Pawn was aggressively removed
→ Cage count increased
```

Acceptance criteria:

- Interrupted jobs did not duplicate chickens.
- Interrupted jobs did not delete chickens.
- Full cages rejected additional chickens.

---

### Commit 4 — `Removed caged chickens without recording deaths`

Reused or adapted aggressive Pawn-removal logic similar to the project's Stargate behavior.

The implementation audited:

- `WorldPawns`
- dead-pawn collections
- tales/story records
- corpse generation
- death notifications
- stale map/world references
- any chicken-specific history retained by RimWorld

Acceptance criteria:

- Entering a cage did not count as death.
- No corpse was created.
- No dead-pawn history entry remained merely because a chicken had entered a cage.
- Repeated cage entry did not cause savegame growth from historical Pawn artifacts.

---

### Commit 5 — `Regenerated equivalent chicken pawns when cages released birds`

Implemented release behavior:

1. Selected one serialized chicken record.
2. Calculated its exact current biological age.
3. Generated a new chicken Pawn.
4. Applied gender.
5. Applied age and development state.
6. Spawned the new Pawn near the cage.
7. Removed the serialized record.

Acceptance criteria:

- No old `ThingID` was preserved or required.
- Released chickens matched the biological state represented by their stored records.

---

### Commit 6 — `Added individual chicken selection for cage releases`

Implemented useful release filters:

- Youngest
- Oldest
- Random
- Adult hen
- Adult rooster
- Juvenile

Acceptance criteria:

- Biological-age-sensitive flock management was possible without materializing every chicken.

---

### Commit 7 — `Added serialization round-trip tests for virtualized chickens`

Covered:

```text
chicken
→ cage
→ save
→ load
→ release
```

Verified:

- age accuracy
- gender accuracy
- growth/development accuracy
- chicken counts
- absence of the old Pawn
- absence of duplicate historical references

---

# Pull Request 3 — Deconstructed and destroyed battery cages safely

## Goal

Guaranteed that a caged flock survived the loss of its cage: birds were released
unharmed on deconstruction, wounded when the cage was destroyed by force, and any
record that could not be released immediately was preserved and retried rather
than silently deleted.

## Commits

### Commit 1 — `Released caged chickens unharmed when a battery cage was deconstructed`

### Commit 2 — `Injured caged chickens when a battery cage is destroyed by force`

### Commit 3 — `Preserved caged chickens that cannot be released when a cage is destroyed`

---

# Pull Request 4 — Created the battery cage nutrition system

## Goal

Removed food searching, reservation, and pathfinding from serialized chickens.

## Commits

### Commit 1 — `Documented the peer-review plan and QA checklist`

Added the peer-review table of every pull request — number, title, branch,
base, commit count, and status — renumbered the sections to match the actual
sequence, and rewrote `QA_CHECKLIST.md` for the same PR numbers and branches.

---

### Commit 2 — `Added collective nutrition tracking to battery cages`

Implemented collective nutrition demand.

Acceptance criteria:

- Contained chickens never searched for food.
- Contained chickens never reserved food.
- Contained chickens never generated ingestion jobs.
- Contained chickens never performed food-related pathfinding.

---

### Commit 3 — `Consumed cage nutrition according to elapsed simulation time`

Implemented mathematical food usage:

```text
nutritionUsed =
    summedVirtualChickenDemand
    × elapsedTime
```

Acceptance criteria:

- No per-chicken food tick existed.
- Save/load and fast-forwarding produced equivalent results.

---

### Commit 4 — `Added starvation state without spawning starving pawns`

Implemented starvation progression at cage level.

Conceptual states:

```text
Fed
→ Hungry
→ Starving
→ Mortality risk
```

Acceptance criteria:

- No Pawn was generated merely to receive malnutrition Hediffs.
- Starvation state was visible in inspection UI.

---

### Commit 5 — `Fed battery cages from adjacent vanilla hoppers`

Colonists filled ordinary vanilla hoppers through the normal hauling pipeline,
and each cage siphoned nutrition from every hopper touching its edge on its rare
tick. The cage def declared `wantsHopperAdjacent` and a patch unlocked the
hopper with the cage research. This replaced the mod's own hopper building and
its direct feed-hauling job.

Acceptance criteria:

- Feeding used normal colony hauling into vanilla hoppers.
- Chickens remained virtual throughout feeding.

---

# Pull Request 5 — Added statistical mortality for caged chickens

## Goal

Allowed chickens to age and die naturally without ever existing as live Pawns during confinement.

## Commits

### Commit 1 — `Added periodic mortality evaluations for caged chickens`

Implemented coarse mortality evaluation using an appropriate RimWorld scheduling mechanism.

Preference:

- LongTick
- RareTick
- or a custom low-frequency scheduled evaluation

Acceptance criteria:

- Mortality was not checked every normal Tick.
- Profiling confirmed low runtime overhead.

---

### Commit 2 — `Added age-dependent natural mortality`

Implemented probability-based mortality that increased with derived biological age.

Acceptance criteria:

- Chickens did not die automatically at a fixed hard age.
- Old outliers such as 10-year-old chickens remained possible.
- Around RimWorld's nominal biological life expectancy, mortality became increasingly likely rather than deterministic.

---

### Commit 3 — `Added starvation-driven cage mortality`

Implemented mortality due to sustained nutrition deficits.

Correct behavior:

```text
serialized record
→ mortality roll succeeded
→ serialized record removed
```

Forbidden behavior:

```text
serialized record
→ generated Pawn
→ killed Pawn
→ created corpse/history
```

---

### Commit 4 — `Added mortality summaries to cage inspection`

Displayed mortality summaries without message spam.

Example:

```text
2 chickens died from prolonged starvation
during the last evaluation period.
```

Acceptance criteria:

- Large flock mortality did not generate hundreds of individual notices.

---

# Pull Request 6 — Virtualized egg production inside battery cages

## Goal

Removed individual egg Things and individual egg ticking from battery-cage production.

## Commits

### Commit 1 — `Added age-dependent egg production for serialized hens`

Implemented age-sensitive laying eligibility and output.

Acceptance criteria:

- Production derived from biological age.
- No individual chicken received a ticking egg-production component while caged.

---

### Commit 2 — `Restricted battery-cage production to unfertilized eggs`

Initial battery cages produced unfertilized eggs only.

Acceptance criteria:

- No embryo tracking existed.
- No fertilization state existed.
- No hatching component existed on virtual cage output.

---

### Commit 3 — `Calculated egg output from elapsed cage time`

Implemented aggregated production:

```text
elapsed time
× eligible hens
× age-dependent laying rate
× cage modifiers
=
egg production
```

Acceptance criteria:

- Fractional production accumulated internally.
- No per-hen egg Thing was created.

---

### Commit 4 — `Added ten-egg boxes as the cage egg output unit`

The cage collected laying progress internally and, each time ten eggs had
accrued, released one haulable stack of ten ordinary vanilla chicken eggs.
Haulers carried the stacks away like any other egg while the box kept filling,
and inspection reported both the eggs in the box and the percentage progress
toward the next egg.

Acceptance criteria:

- The box was one stack representing up to ten eggs, not ten vanilla `Thing`s.
- Hauling and cooking used ordinary vanilla egg stacks.

---

### Commit 5 — `Replaced the per-hen release memory with a manual network intake cutoff`

Releasing a bird no longer recorded it individually to keep it from being roped
straight back in. Releasing a hen turned the whole network's intake off until
the player turned it back on, keeping no per-bird state.

---

### Commit 6 — `Fixed the newborn-and-other-stages error when unloading a chicken`

The release path asked `PawnGenerationRequest` for Newborn, Baby, Child, and
Adult at once, so RimWorld logged a "newborn and other developmental stages
simultaneously" error and distrusted the requested age. A chicken grows straight
from chick to adult, so the request now carries whichever single stage the
recorded age falls in, erring toward Adult when the threshold is not yet
resolved, and pins the exact age right after generation.

---

# Pull Request 7 — Added cage-only poultry modules

## Goal

Created upgrade modules that were visible and constructible only from a selected cage.

## Commits

### Commit 1 — `Added cage-specific module installation gizmos`

Selecting a cage exposed module-installation controls.

Example:

```text
Install module
```

with module choices underneath.

---

### Commit 2 — `Removed cage modules from the Architect menu`

Module `ThingDef`s were hidden from the normal global Build/Architect interface.

Acceptance criteria:

- Players could not browse directly to the module from the normal build menu.
- Module definitions remained valid and constructible through cage-specific controls.

---

### Commit 3 — `Restricted module placement to the selected battery cage`

Placement validation ensured modules:

- belonged to the cage that initiated the action;
- were placed only in valid positions;
- respected compatibility rules;
- rejected duplicates where appropriate.

---

### Commit 4 — `Added the climate-controller module`

Implemented the first cage module.

The climate controller should affect cage climate handling in a clearly documented way.

Do not overcomplicate climate simulation until profiling and gameplay testing justify it.

---

### Commit 5 — `Added climate-controller status and failure states`

Inspection UI showed states such as:

```text
Climate control: Active
Climate control: Unpowered
Climate control: Broken
Climate control: Not installed
```

---

# Pull Request 9 — Hardened savegames and chicken-history cleanup

## Goal

Verified that the mod solved the long-colony savegame problems that motivated the project.

## Commits

### Commit 1 — `Audited serialized cage data for savegame size`

Tested colonies containing:

- 10 virtual chickens
- 500 virtual chickens
- 2,000 virtual chickens
- 10,000 virtual chickens

Measured savegame growth.

---

### Commit 2 — `Removed unnecessary chicken serialization fields`

Removed fields found unnecessary after testing.

Goal:

> Ten thousand caged chickens serialized as compact biological records, not ten thousand pseudo-Pawns.

---

### Commit 3 — `Cleaned residual world references when chickens entered cages`

Stress-tested repeated cycles:

```text
generate 500 chickens
→ cage all
→ release all
→ cage all
→ repeat
```

Acceptance criteria:

- Savegame size did not grow continuously due to stale chicken history.
- World-history collections did not accumulate former cage occupants.

---

### Commit 4 — `Added migration guards for malformed cage records`

Handled corrupt or outdated cage records conservatively.

Allowed strategies:

- recover reasonable defaults where safe;
- drop an invalid record with a clear warning when reconstruction was impossible.

A single malformed chicken record must not brick the save.

---

# Pull Request 10 — Profiled large industrial poultry colonies

## Goal

Measured whether virtualization actually solved the runtime and savegame problems.

## Commits

### Commit 1 — `Added development tools for spawning poultry stress tests`

Added dev-only helpers such as:

```text
Fill cage
Fill all cages
Spawn 500 free-range chickens
Spawn 2,000 free-range chickens
Generate 10,000 carton-equivalent eggs
```

---

### Commit 2 — `Benchmarked free-range and virtualized chicken populations`

Compared at minimum:

```text
500 normal chickens
500 virtualized chickens

2,000 normal chickens
2,000 virtualized chickens
```

Recorded:

- TPS
- frame time
- save size
- save duration
- load duration
- active Pawn count
- active Thing count

---

### Commit 3 — `Reduced unnecessary battery-cage ticking`

Used profiler results to remove residual unnecessary work.

Desired architecture:

```text
Normal Tick():
    almost nothing

Rare/Long/custom scheduled work:
    nutrition
    mortality
    production

interaction/release:
    exact age reconstruction
```

---

### Commit 4 — `Verified multi-year poultry simulations without pathological save growth`

Ran multi-year colony simulations including:

- feeding
- starvation
- mortality
- cage loading
- cage release
- egg production
- carton consumption
- save/load cycles

Acceptance criterion:

> The chicken system no longer behaved like a pathological Pawn, Thing, corpse, egg, or world-history generator.

---

# Pull Request 11 — Finished the RimWorld 1.6 battery-poultry release

## Goal

Prepared the mod for normal player use.

## Commits

### Commit 1 — `Added final battery-cage textures and four-tier graphics`

Final graphics clearly communicated:

- four vertical cage tiers
- very large RimWorld chickens
- feeding equipment
- egg collection
- substantial structural framing

---

### Commit 2 — `Added final sounds, strings, and inspection text`

Standardized terminology and player-facing UI.

---

### Commit 3 — `Added player-facing explanations of chicken virtualization`

Do not expose implementation jargon such as:

```text
Pawn despawned and serialized
```

Prefer gameplay-facing language such as:

```text
Chickens housed in battery cages are managed collectively,
greatly reducing colony-management overhead.
```

The mod description may explicitly mention the performance benefits.

---

### Commit 4 — `Documented the RimWorld 1.6 battery-poultry system`

Documentation covered:

- 2×3 footprint
- 10-bird initial capacity
- four vertical tiers
- Medieval research
- roof requirement
- feeding
- exact biological aging
- mortality
- egg cartons
- modules
- release behavior
- performance architecture

---

### Commit 5 — `Completed the RimWorld 1.6 release build`

Prepared the final release and Workshop/package contents.

Acceptance criteria:

- Clean RimWorld 1.6 load.
- No known save corruption.
- No major debug-log spam.
- Stress tests passed.
- User-facing documentation completed.

---

# Pull Request 12 — Added a per-chicken picker window for unloading battery cages

## Goal

Let the player pick exactly which caged birds leave a cage, by life stage, and
unload several at once, without ever materializing the rest of the flock.

## Commits

### Commit 1 — `Added a per-chicken picker window for unloading battery cages`

The cage's unload controls opened a window that listed its serialized flock as
chicks, juveniles, and adults, each row markable for unloading, with actions to
unload one bird, one life stage, or the whole cage. The picks were held as a
per-record mark list so the release job could honor them, and the displayed
stage came from the same exact biological age used by the rest of the mod.

### Commit 2 — `Numbered picker rows within each life-stage group instead of across the whole flock`

### Commit 3 — `Skipped already-reserved cages when selecting a destination for hen roping, preventing two handlers from being assigned the same cage`

### Commit 4 — `Renamed the pen system gizmo to a vanilla-style Allow toggle and bound it to the F hotkey`

### Commit 5 — `Fixed caging a spawned hen logging that she was already spawned`

---

# Implementation Rules for LLM Agents

## Work One Pull Request at a Time

Do not implement future PRs opportunistically unless a minimal internal hook is necessary.

Every PR should:

1. build;
2. load in RimWorld 1.6;
3. preserve existing save compatibility where practical;
4. include targeted testing;
5. avoid unrelated refactors.

---

## Keep Commit Messages and Pull Request Titles in Past Tense

All commits and PR titles in this plan intentionally use past-tense wording.

Examples:

```text
GOOD:
Added collective nutrition tracking to battery cages
Virtualized chickens inside battery cages
Hardened savegames and chicken-history cleanup

BAD:
Add collective nutrition tracking to battery cages
Virtualize chickens inside battery cages
Harden savegames and chicken-history cleanup
```

Do not silently convert titles back to imperative Git style.

---

# Final Architectural Invariant

While a chicken is inside a battery cage:

- it is not a spawned Pawn;
- it does not pathfind;
- it does not seek food;
- it does not perform Pawn ticks;
- it does not own an individual egg-production component;
- it does not create individual egg Things;
- it does not accumulate normal Pawn-history artifacts;
- exact biological age remains recoverable;
- mortality can occur without materializing the Pawn;
- egg production can occur without materializing individual eggs.

Only crossing the boundary between the abstract cage system and the physical map should materialize a new chicken Pawn.

That invariant is the primary reason this mod exists.
