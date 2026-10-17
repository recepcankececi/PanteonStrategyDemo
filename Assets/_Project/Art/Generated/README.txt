Generated art - created for this project
=========================================

Source : created for this project by Tools/ArtGen (sprites written as SVG in code, rendered to PNG with
         @resvg/resvg-js). No third-party art, no licence restrictions.
Style  : flat vector, seen from straight above, modern RTS; one palette (Tools/ArtGen/src/palette.js).
Rebuild: cd Tools/ArtGen && npm ci && node generate.js --install
         Do not edit these PNGs by hand; change the generator and run it again.
Import : applied automatically by StrategyDemo.EditorTools.GeneratedArtImporter (by folder).

Buildings/  one sprite per building, exactly footprint x 64 px, pivot centred:
            Barracks 3x3, PowerPlant 2x2, CommandCenter 4x4, SupplyDepot 2x1, Radar 2x2, GuardTower 1x1.
Units/      <Type>_Stand, <Type>_Walk_0, <Type>_Walk_1 for Assault, Rifleman, Scout. 64x64, facing up, drawn
            inside the cell's inscribed circle so a rotated soldier never leaves its cell.
Board/      Ground.png - seamless one-cell tile (its edge line draws the grid), wraps (Repeat), not atlased.
            CellHighlight.png - white cell (2 px frame, 55 % fill), tinted for placement and selection.
UI/         Panel, Row, Button - 64x64 9-slice sprites, 16 px border, 100 PPU (= Canvas reference).
