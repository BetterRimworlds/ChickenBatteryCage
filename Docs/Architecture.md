# Architecture

The invariant that justifies this mod:

> While a chicken is inside a battery cage it is not a spawned `Pawn`. It does
> not pathfind, seek food, tick a need, own an egg-production component, create
> individual egg `Thing`s, or accumulate Pawn history. Only crossing the
> boundary between the abstract cage and the physical map materializes a bird.

## Records, not pawns

`CagedChickenRecord` stores exactly what cannot be derived:

- `biologicalAgeTicksAtEntry`
- `enteredAtGameTick`

Current age is `ageAtEntry + (now - enteredAt)`, computed on demand, so no
scheduled task ages a caged bird and save/load cannot drift. Gender is not
stored because a cage only ever accepts hens.

`CageHenIntake.PutHenInCage` captures the record, then `UnmakeWithoutDeath`
despawns the bird and discards it through `WorldPawns` so no corpse, death
tale, or dead-pawn entry is produced. Relations and ownership are scrubbed
first so repeated loading cycles leave nothing behind.

`CageChickenFactory.Generate` is the only place a caged bird becomes a `Pawn`
again. It regenerates a fresh bird at the exact stored tick and places her near
the cage. No `ThingID`, name, or history carries over.

## Time-based simulation

All cage-level simulation hangs off `TickRare` and works from elapsed time, not
per-bird work:

| Subsystem | State | Rule |
|---|---|---|
| Nutrition | `nutritionStored`, `starvingTicks` | summed flock demand × elapsed time |
| Laying | `eggProgress` | eligible-hen age curve × elapsed time × feed modifiers |
| Mortality | death tallies | age hazard (hinged on species life expectancy) plus starvation hazard, rolled per evaluation interval |

Nutrition settles through `CageNetwork.SettleCluster`, which drains the cluster's
shared store and accrues starvation exposure in a single call. Laying and
mortality run through the cage's private `ApplyEggProductionElapsed` and
`ApplyMortalityElapsed` steps, whose elapsed-time inputs are separate from the
scheduled wrapper. `DebugSimulateTicks` drives all three in lockstep so the dev
simulator can advance years of behaviour in a moment.

Feed enters only through adjacent vanilla hoppers, and dairy is refused there:
chickens cannot digest lactose, so milk stored in a hopper is never counted as
feed or drawn by the cage. The pure classifier lives in `DairyFeedRules` and
keys on the animal-product/fluid food type plus a player-extensible def list.

## Touching cages are one giant cage

Cages that touch — sharing an edge or a corner — form a cluster that behaves as
a single cage. `CageNetwork` discovers the clusters once per cache window with
an 8-way flood fill (`GenAdj.CellsAdjacent8Way`), then aggregates each cluster
on read: one hen count, one picker window, one Allow toggle, one set of unload
marks. Clusters that do not touch are independent, so several separate houses
can coexist on one map.

The shared feed pool is the one piece of genuinely shared mutable state. Every
cage still persists its own `nutritionStored` and `starvingTicks`, but the
cluster settles them as a unit: whichever member ticks first computes the
cluster's summed demand and drains the combined store, writing the result back
across the members proportionally (`CageClusterMath.Distribute`). A hopper
bolted to any member feeds the whole cluster. Because the state stays per-cage
on disk, no save migration is needed and a cage leaving the cluster keeps only
its own share.

The egg box is pooled the same way. Each cage banks its own fractional
`eggProgress`, but release is decided on the cluster total: whenever the sum
reaches a whole stack — one full day of the whole network's laying, never fewer
than ten eggs — the cage that settles first places one haulable stack and the
remaining progress is scaled back proportionally across the members
(`CageNetwork.WithdrawEggs` via `CageClusterMath.ScaleToTotal`). Five unevenly
stocked cages therefore empty together however the hens are distributed,
instead of each cage having to reach the threshold on its own. A stack that
cannot be placed leaves its eggs in the pool and the attempt is retried on the
next settlement, so no output is lost to a crowded map.

## Output

Egg production accumulates internally and materializes a single vanilla
unfertilized-egg stack once the pooled box is full, so cooking draws whole eggs
from an ordinary stack and no individual egg `Thing` is ever created per hen.
The stack holds at least ten eggs (`MinEggsPerStack`) and grows to a full day of
the whole network's laying when the cluster's combined output is higher, matching
the release threshold described above. The pooled pull-out keeps every member's
fractions summing to the same total, so no fractional egg is lost at a cluster
boundary.

## Safety

Loaded state is sanitized on `PostLoadInit`: cheap fields are clamped into
range and an impossible chicken record is dropped with a warning rather than
bricking the save. A record that cannot be released when its cage is destroyed
is handed to `MapComponent_CagedChickenRescue` and retried until a placement
cell is free.
