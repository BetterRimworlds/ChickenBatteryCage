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
| Deterioration | hit points | unroofed cage takes damage so it fails in 30 days (wood/steel) or 60 (other materials); bad weather quadruples the rate |

Nutrition settles through `CageNetwork.SettleCluster`, which drains the cluster's
shared store and accrues starvation exposure in a single call. Laying and
mortality run through the cage's private `ApplyEggProductionElapsed` and
`ApplyMortalityElapsed` steps, whose elapsed-time inputs are separate from the
scheduled wrapper. `DebugSimulateTicks` drives all three in lockstep so the dev
simulator can advance years of behaviour in a moment.

Feed is hauled straight into the cage by ordinary colony work, never through a
hopper: a hauler carries an approved raw plant, seed, hay, or kibble stack to
the cage, tips its nutrition into the shared store, and the used food is
destroyed on the spot. Dairy is refused: chickens cannot digest lactose, so
milk is never accepted as feed. `CageFeed.IsForbiddenFeed` applies the ban,
backed by the pure classifier in `DairyFeedRules`, which keys on the
animal-product/fluid food type plus a player-extensible def list, and the ban
can be lifted in mod settings.

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
across the members proportionally (`CageClusterMath.Distribute`). Feeding any
one member tops up the whole cluster. Because the state stays per-cage
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

## Manure, fertilizer, and sanitation

Manure follows the same elapsed-time rule as feed, laying, and mortality:

| Subsystem | State | Rule |
|---|---|---|
| Manure | `wastePacks` | hen count × elapsed time × one pack per fifty hen-days |
| Cleaning | `NeedsCleaning` | cluster waste past its flock-scaled threshold yields whole packs |
| Toxicity | `toxicBuildup` | vanilla polluted-ground gain above the safe band, vanilla recovery below |

No per-chicken waste object exists. `CageNetwork.SettleCluster` bills each
member's own flock for the elapsed interval in the same call that drains feed
and accrues mortality, so waste is measured once per cluster and summing the
members never double counts. The persisted total is per cage, so the cluster is
an aggregate on read and cleaning, exactly like the feed pool and egg box.

Cleaning removes only whole packs and scales the remainder back across the
members (`CageNetwork.WithdrawWaste` via `CageClusterMath.ScaleToTotal`), so the
fraction survives a service on any member. Each whole pack becomes one
real `CBC_FertilizerPack` stack on the ground.

A Fertilizer Pack is a real spawned `Thing`, but a passive one: it carries
vanilla `CompRottable`, so it spoils with temperature exactly as food does —
full speed at ten degrees and above, proportionally slower in the cold, and
stopped below freezing, with vanilla's own Fresh/Rotting and refrigerated or
frozen hints. It also carries raw plant food's deterioration rate, so outdoors
it loses hit points over about ten fair days, and rain or snow on an unroofed
cell makes that five times as fast, the same 500% factor vanilla uses for raw
food. A small companion comp watches the rot
stage and, the moment the stack leaves Fresh — or the moment deterioration
destroys it — dissolves all of it at once, improving six cells per pack.
This is raw corn's one-shot spoilage with `CompDissolution`'s toxic behavior
left out.

### Fertilizer spread

The spread is a faithful reproduction of `PollutionUtility.GrowPollutionAt`,
because the vanilla routine is hard wired to `map.pollutionGrid` and cannot be
pointed at terrain. The mechanics live in the Verse-free
`FertilizerSpreadMath.GrowAt`: improve the root, flood-fill the already-improved
ground reachable from it, seed a best-first queue with the improvable frontier,
then repeatedly take the most attractive cell and push its neighbours. The
map-facing `FertilizerSpread` supplies the same Perlin-noise comparator vanilla
uses, so the blob is as ragged as a pollution patch, and the flood/patch are
encoded in the terrain itself rather than a per-cell dictionary: finished ground
counts as improved and is never re-selected.

`FertilizerTerrainRules` is the pure table. It runs a progression from the
barren grounds up toward rich soil — soft sand to sand to stony soil to
lichen-covered soil to soil to rich soil, marshy soil to stony soil, and the
game's own generated rough-stone terrains to stony soil. Constructed terrain
and water are simply absent. Any building edifice — natural rock wall or player
structure — blocks the spread.

### Toxic Buildup

While a chicken is confined the condition is a single cage-level severity.
`CageWasteMath.ToxicBuildupAfter` integrates it over the elapsed interval,
splitting exactly at the safe-waste crossing so a long settlement equals several
short ones. The gain (0.4/day) and recovery (0.08/day) are vanilla's own
numbers, and the lethal threshold is read from `ToxicBuildup.lethalSeverity`.
Above the onset severity the cage adds a capped daily death chance, folded into
the existing mortality roll, so a poisoned flock dies off gradually. When a hen
is materialized, `CageToxicBuildup.Apply` gives her the real `ToxicBuildup`
Hediff at the stored severity, so the virtual condition becomes visible on the
map.

## Safety

Loaded state is sanitized on `PostLoadInit`: cheap fields are clamped into
range and an impossible chicken record is dropped with a warning rather than
bricking the save. A record that cannot be released when its cage is destroyed
is handed to `MapComponent_CagedChickenRescue` and retried until a placement
cell is free.
