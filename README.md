# ChickenBatteryCage

RimWorld chicken battery cages that keep huge chicken flocks without game lag. World hunger solved??

Unlock confined poultry husbandry in the medieval era, then build a four-tier 2×3 cage
from wood, steel, or glitterworld metals. Housed chickens are managed as a flock.

Part of the [Better Rimworlds](https://github.com/BetterRimworlds) collection.

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

## Changelog

For more, see [CHANGELOG.md](CHANGELOG.md).
