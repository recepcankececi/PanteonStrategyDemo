# Movement — guidance

- `UnitMovement` is the only public movement API (`UnitMovementDriver` just ticks it). It owns the move orders and
  decides *when* to plan;
  `MovePlanner` (path + stop cell + reservation), `MoveStepper` (one frame of walking) and `HeldCellTracker`
  (grid cell follows the soldier's centre) are internal helpers. New callers go through `UnitMovement`.
- Invariant: after every `Tick` / `Remove`, every cell a soldier stands in is held in the grid by a soldier in it
  (`HeldCellTracker.Settle`). Break it and buildings can be placed on soldiers.
- `MovePlanner.Plan` requires the soldier to hold **no** reservation: release first (`CellReservations.Release`), or
  call `MovePlanner.Replan`, which does both.
- Zero GC per frame: predicates are cached delegates, `MoveOrder` is pooled and keeps its path capacity. Never pass a
  method group or lambda in a per-frame path.
- Tests: `Tests/EditMode/Gameplay/Movement/UnitMovementTests.cs` (behaviour through the public API only).
