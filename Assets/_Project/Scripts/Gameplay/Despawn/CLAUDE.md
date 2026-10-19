# Despawn — guidance

- `OccupantDespawner.Despawn` is the **only** way a building or soldier leaves the board (R10). Its step order (board,
  movement, registry, selection, `Despawned` event, pool) is documented on the class; each step needs the object still
  intact. Do not reorder.
- A new occupant type implements `IDespawnable` (`LeaveBoard`, `ReturnToPool`); the despawner does not change.
  If it needs another service to leave the board, add a property to `DespawnContext`.
- Other systems react to a death by listening to `OccupantDespawner.Despawned`, never by deactivating objects themselves.
- Tests: `Tests/EditMode/Gameplay/Despawn/OccupantDespawnerTests.cs`.
