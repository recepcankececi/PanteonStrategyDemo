# ArtGen

Generates every sprite of the game from code: buildings, soldiers (standing + two-frame walk cycle), the ground
tile, the cell highlight and the UI panels. Lives outside `Assets/` so Unity never imports the Node project.

```
npm ci
node generate.js             # SVG + PNG into out/<group>/{svg,png}
node generate.js --install   # also copy the PNGs into Assets/_Project/Art/Generated/<group>/
```

Unity applies the import settings by folder (`StrategyDemo.EditorTools.GeneratedArtImporter`).

| File | What it draws |
|---|---|
| `src/palette.js` | the single colour palette every sprite uses |
| `src/svg.js` | SVG string helpers and `CELL` (64 px per grid cell) |
| `src/buildings.js` | six buildings; each canvas is exactly its footprint |
| `src/units.js` | Assault, Rifleman, Scout; facing up, inside the cell's inscribed circle |
| `src/board.js` | ground tile, board edge, white cell highlight, health bar strip, Barracks spawn marker, tracer, destruction burst |
| `src/ui.js` | 9-slice panel, row and button (16 px border) |

`generate.js` refuses to write a sprite whose rendered size is not its footprint × 64 px, or a soldier with a
pixel outside its safe circle, so the art invariants hold before Unity ever sees a file. Unity's EditMode tests
check them again on the imported assets.

Renderer: `@resvg/resvg-js` 2.6.2 (MPL-2.0, npm), pinned in `package.json`; a dev tool only, nothing of it ships
with the game.
