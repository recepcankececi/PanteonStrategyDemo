# Board — guidance

- Base layer of `Gameplay`: `GridView` (cell ↔ world), `IBoardOccupant` + `OccupantRegistry` (grid id → object),
  `PointerInput` (one frame of input), `BoardCameraFitter`. Every feature uses these.
- Keep it that way: no code here may use another `Gameplay` feature namespace (Movement, Selection, ...); an XML-doc
  link to one is written fully qualified, so it needs no `using`. Code that
  coordinates several features belongs in that feature's folder (e.g. the despawn path is in `Despawn/`).
- Occupant ids are never reused, so a stale id simply resolves to nothing in the registry.
