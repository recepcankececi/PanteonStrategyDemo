# Combat — guidance

- `UnitCombat` is pure C#; `UnitCombatDriver` ticks it **after** movement (`[DefaultExecutionOrder(10)]`). Keep that
  order: range is judged on this frame's positions, and cells freed by a death are free before input (100) runs.
- Chasing uses `UnitMovement.Approach`; combat never moves a soldier itself.
- Deaths are resolved after every soldier has acted in the frame, and only through `OccupantDespawner`. Never
  deactivate or pool a dead object from here; listen to `OccupantDespawner.Despawned` instead.
- Who may attack whom is `IAttackRule` (`FreeForAllAttackRule` today); a faction rule is a new class, not an `if`.
- Damage, range, interval and HP come from `UnitDefinition` / `BuildingDefinition` ScriptableObjects.
