# ChickenBatteryCage

RimWorld chicken battery cages that keep huge chicken flocks without game lag. World hunger solved??

Unlock confined poultry husbandry in the medieval era, then build a four-tier 2×3 cage
from wood, steel, or glitterworld metals. Housed chickens are managed as a flock.

Part of the [Better Rimworlds](https://github.com/BetterRimworlds) collection.

*Schrodinger's Chickens produce Schrodinger Eggs ^_^*

## Supported RimWorld Versions

- RimWorld 1.6

## Egg production by hen age

A caged hen has no individual egg-laying component: output is a pure function of
her exact biological age, summed across the flock. She lays nothing before
adulthood (0.2 years), about one egg a day through age 2, then declines gradually
without ever quite stopping. Touching cages pool their output and release one
haulable stack sized to a single day of the whole network's laying (minimum ten
eggs).

This age decline is **on by default** and can be turned off in the mod settings.
With it off, every adult hen lays one egg a day for the rest of her life, as in
vanilla RimWorld.

| Age (years) | Eggs/day | Eggs per RimWorld year* | Eggs laid since adulthood |
|---:|---:|---:|---:|
| 0.2 (adult) | 1.0000 | 60.0 | 0 |
| 0.5 | 1.0000 | 60.0 | 18 |
| 1 | 1.0000 | 60.0 | 48 |
| 2 | 1.0000 | 60.0 | 108 |
| 3 | 0.7500 | 45.0 | 160 |
| 4 | 0.6250 | 37.5 | 200 |
| 5 | 0.5500 | 33.0 | 236 |
| 6 (life expectancy) | 0.5000 | 30.0 | 267 |
| 7 | 0.4643 | 27.9 | 296 |
| 8 | 0.4375 | 26.3 | 323 |
| 9 | 0.4167 | 25.0 | 348 |
| 10 | 0.4000 | 24.0 | 373 |

\*A RimWorld year is 60 days. The decline softens to a floor of 0.325 eggs/day
(about 19.5 per year) around age 20, so even an old hen still lays.

## Chicken manure, fertilizer, and sanitation

A caged flock produces manure the same way it eats and lays: as a pure function
of hen count and elapsed game time, never as a spawned waste `Thing` or a
per-bird need.

The abstraction is grounded in real numbers. A laying hen drops roughly **0.1 kg
of fresh manure a day**, so fifty hens make about 5 kg a day. One **fertilizer
pack** stands for roughly that 5 kg of collected, compacted manure, which makes
the production rate **one pack per fifty hen-days**:

| Hens | Packs/day |
|---:|---:|
| 1 | 0.02 |
| 10 | 0.2 |
| 50 | 1 |
| 100 | 2 |
| 200 | 4 |

A standard 100-hen installation therefore makes about **2 packs a day**, or
about **120 packs a RimWorld year**.

### Cleaning

Manure accumulates in the cage, and the flock is serviced in batches rather than
continuously. Once a **touching cage cluster** has stored roughly five days of
output — about **10 packs for 100 hens** — a handler cleans the cage. One
service empties every whole pack the cluster holds, drops them beside the cage
as ordinary haulable **fertilizer packs**, and leaves the fractional remainder
behind, so no manure is lost between visits.

### Fertilizer packs

Fertilizer packs are the beneficial inverse of vanilla toxic wastepacks. They
stack five to a tile and **spoil like food**: rot progress runs on temperature,
so a pack keeps roughly ten days in the warm open, lingers longer in the cold,
and stops outright below freezing — a freezer preserves fertilizer as well as it
preserves corn. They produce no pollution, no tox gas, and no infestations.

When a stack rots away the **whole stack dissolves at once**, exactly as a stack
of raw corn is destroyed all at once. Each pack it held improves **six cells of
natural ground**, using the same irregular, outward-creeping cell selection that
vanilla pollution uses. Repeated dissolution grows the fertile patch outward the
same way repeated wastepack dissolution grows a polluted one. Fertilizer works
a progression up toward rich **soil**: soft sand becomes sand, sand and rough
natural cave stone become stony soil, stony soil becomes lichen-covered soil,
marshy soil becomes stony soil, lichen-covered soil becomes soil, and soil
becomes rich soil. Constructed floors, bridges, roads, water, and buildings are
never touched, and natural rock walls block the spread exactly as they block
pollution.

For scale, a hundred hens make enough fertilizer in a year to improve roughly
**720 cells**: a genuine terraforming project for desert, sandy, underground, or
otherwise poor colonies.

### Dirty cages and toxic buildup

Neglect has a cost. Below about ten days of accumulated manure the flock is
merely dirty; past that the cage accumulates vanilla **toxic buildup** at the
same rate a pawn does on polluted ground, and it recovers at vanilla's natural
rate once the cage is cleaned. A flock can be pushed through the full range of
Toxic Buildup stages and will begin losing birds if the cage is left long
enough.

The condition stays virtual while the birds do — no Pawn is spawned to carry a
Hediff. But when a hen is unloaded and materialized, she leaves with the real
`Toxic Buildup` Hediff she accumulated, so the condition becomes visible.

### Inspecting a cage

Selecting a cage reports, for the whole touching cluster:

```text
Waste:         {stored} / {capacity} packs
Cleaning:      clean / needs cleaning / critical
Fertilizer:    {n} packs if cleaned now
Toxic buildup: {severity} (stable / increasing / recovering)
```


## Changelog

For more, see [CHANGELOG.md](CHANGELOG.md).
