# Case Requirements Checklist (source: Panteon "Strategy Game Demo" PDF)

Every item must be traceable to code/tests/scene. `/verify` checks each ID.

## Platform & delivery
- P1 Unity 6.3 LTS, 2D platform.
- P2 Playable **Windows build** included in submission.
- P3 Full project shared via GitHub (link emailed to hr@panteon.games).

## Gameplay
- R1 Various building types besides Barracks, Power Plant; Soldier units listed in Production Menu.
- R2 Buildings placed on map at user-selected suitable locations; **invalid placement shown to user** (e.g. red preview). Buildings have **name, image, dimensions**.
- R3 Unlimited production, **no production time** (buildings and units).
- R4 Selecting a building on the board shows its image in the Information Panel; if it can produce units, their images are listed too.
- R5 Only Barracks produce units. Power Plant has no production menu.
- R6 Soldiers: left-click select, right-click move to target via **shortest path**, routing **around buildings**. Each Barracks has a **spawn point** where soldiers emerge.
- R7 Barracks produce 3 soldier types, all HP 10; damage: Soldier1 = 10, Soldier2 = 5, Soldier3 = 2.
- R8 All buildings have HP: Barracks 100, Power Plant 50 (new building types define their own).
- R9 Selected soldiers attack a unit or building with right-click.
- R10 Soldiers and buildings are destroyed at HP 0.

## Technical
- T1 Batching/instancing so that **SetPass calls < 20**.
- T2 Camera and UI layout work across aspect ratios/resolutions; **window resizable** in Windows build.
- T3 Clear, readable code; standard naming; scalable; commented.
- T4 Scene structure, GameObject names and folder structure are reviewed.
- T5 All edge cases considered.
- T6 Developer understands every line (AI use allowed).

## Interface
- I1 Production Menu: **infinite scroll view** listing producible buildings.
- I2 Game Board: shows produced buildings and soldiers.
- I3 Information Panel: info for the selected building/soldier.
- I4 Layout reference: Production Menu (left) | Game Board (center, grid) | Information (right).

## UX / Algorithm
- U1 Infinite scroll view implemented with **object pooling**.
- A1 Pathfinding with **A\***.
