# Profiling a poultry colony

The mod's reason to exist is performance: a caged chicken is a compact record,
not a spawned `Pawn`, so a huge flock must not drag the tick rate or the save
size down the way a free-range flock does.

## In-game stress tools

Select any battery cage with developer mode on and open **Dev: poultry stress
tools** from its gizmos. The benchmark profiler entries below only appear while
**God Mode** is also enabled:

| Entry | Effect |
|---|---|
| Fill this cage | Spawns hens and serializes them until the cage is full. |
| Fill all cages | Fills every cage on the map. |
| Spawn 500 free-range chickens | Floods the map with real `Pawn`s. |
| Spawn 2,000 free-range chickens | A deliberately punishing free-range load. |
| Generate 10,000 carton-equivalent eggs | Spawns the cartons a very large flock would produce. |
| Log population report | Prints spawned chickens, caged birds, spawned pawns, live Things, and the estimated cage save size. |
| Begin benchmark | Records a baseline of ticks, spawned pawns, and Things. |
| End benchmark | Logs TPS over the elapsed real time plus the population change. |

## Procedure

1. Start with an empty, roofed cage and a drained pen.
2. **Begin benchmark**, let the game run for a fixed real-time window (for
   example 60 seconds at normal speed), then **End benchmark**. This is the
   free-range baseline.
3. **Spawn 2,000 free-range chickens**, repeat the benchmark window, and note
   the TPS and pawn/Thing counts.
4. Remove the free-range birds, **Fill all cages** so the same flock is
   virtualized, and repeat the benchmark window.
5. Compare the two **End benchmark** log lines. The virtualized run should show
   a caged count in the thousands while spawned pawns and live Things stay
   flat, and TPS should not collapse the way the free-range run does.
6. Save and reload after the virtualized run and check the file size. Ten
   thousand caged birds are estimated at well under two megabytes; see
   `CageSaveSize` and `Tests/CageSaveAuditTests.cs` for the budget.

## Long-run simulation

The dev menu's simulation entry advances the cage math by whole game days
without waiting for real time, so multi-year behaviour (feeding, starvation,
mortality, laying, carton output) can be checked quickly. It does not create or
destroy pawns; it only runs the same arithmetic the cages run on `TickRare`.
