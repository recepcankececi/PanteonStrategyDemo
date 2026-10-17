# Edge cases

Every row names the EditMode test(s) that prove the handling (class `.` method, all in
`Assets/_Project/Tests/EditMode/`), or says **accepted limit** when the case is deliberately not handled.
Run them with `Window > General > Test Runner > EditMode > Run All`.

## Grid and coordinates

| Case | Handling | Test(s) |
|---|---|---|
| Footprint partly or wholly off the board | Not free, so not placeable | `GridMapTests.IsAreaFree_FootprintOutsideBounds_ReturnsFalse` |
| Footprint touching the top-right corner | Still inside, placeable | `GridMapTests.IsAreaFree_FootprintTouchingTopRightCorner_ReturnsTrue` |
| Zero or negative size | Never "inside" or "free" | `GridMapTests.IsAreaFree_EmptyOrNegativeSize_ReturnsFalse` |
| Occupying an occupied area | Throws, first occupant kept (a logic bug must fail loudly) | `GridMapTests.Occupy_AlreadyOccupiedArea_ThrowsAndKeepsFirstOccupant` |
| Releasing cells another object holds | Throws, nothing freed | `GridMapTests.Release_AreaOverlappingAnotherOccupant_ThrowsAndFreesNothing` |
| Pointer left of / below the board | Negative cell, never wraps to cell 0 | `GridCoordinatesTests.WorldToCell_PointJustLeftAndBelowOrigin_ReturnsNegativeCell` |
| Even-sized footprint under the cursor | Snaps to the nearest cell corner | `GridCoordinatesTests.FootprintOriginAt_EvenFootprint_SnapsToNearestCellCorner` |
| Distance to a large building | Measured to its nearest cell, not its centre | `GridDistanceTests.ToArea_NextToCornerOfLargeBuilding_IsOne` |
| Occupant ids run out | Ids are `int` and never reused; 2³¹ placements or productions in one session is out of reach | **accepted limit** |

## Building placement

| Case | Handling | Test(s) |
|---|---|---|
| Overlap by a single cell | Invalid, shown red | `BuildingPlacerTests.MoveTo_PartialOverlapByOneCell_IsInvalid`, `PlacementGhostTests.InvalidCandidate_ShowsRedTint` |
| Building placed on a soldier | Impossible: every cell a soldier stands in is held in the grid by a soldier in it | `GridMapTests.IsAreaFree_OverlapsOccupiedCellByOne_ReturnsFalse`, `UnitMovementTests.Tick_WalkingThroughStandingSoldier_HoldsNoCellUntilPastIt`, `UnitMovementTests.Remove_StandingSoldierWhileAnotherWalksThroughIt_WalkerTakesTheCellAtOnce` |
| Grid changes under a still pointer (building destroyed, spawn side taken) | Validity and spawn cell re-evaluated on every grid change | `BuildingPlacerTests.GridFreedUnderStillPreview_PreviewTurnsValid`, `BuildingPlacerTests.GridChangedOnSpawnSideUnderStillPreview_MovesSpawnCellAndNotifies` |
| Barracks destroyed under a still preview covering its spawn cell | Spawn reservation released before the grid event, so the preview turns valid at once | `OccupantDespawnerTests.Despawn_BarracksUnderStillPreview_PreviewOverItsSpawnCellTurnsValid` |
| Only a reservation changes under a still pointer (no grid event) | The preview colour may be stale until the pointer moves; the click always evaluates again | **accepted limit**; click: `BuildingPlacerTests.TryPlace_SpawnCellReservedWhilePointerStill_TakesNextSide` |
| Grid events raised by the placement itself | Ignored while placing; one notification per click | `BuildingPlacerTests.TryPlace_Producer_NotifiesExactlyOnce` |
| Spot became invalid without a grid event before the click | Click evaluates again; never places on a stale "valid" | `BuildingPlacerTests.TryPlace_AreaOccupiedAfterMove_ReturnsFalseAndTurnsInvalid`, `BuildingPlacerTests.TryPlace_SpawnCellReservedWhilePointerStill_TakesNextSide` |
| Scene object creation throws | Cells (and no reservation) given back, exception passed on | `BuildingPlacerTests.TryPlace_FactoryThrows_GivesCellsBackAndRethrows`, `BuildingPlacerTests.TryPlace_ProducerFactoryThrows_ReservesNothing` |
| Click on the UI while placing | Never places, ghost hidden | `PlacementInputHandlerTests.Select_OverUi_NeverPlacesAndHidesGhost` |
| Cancel and click in the same frame | Cancel wins, nothing placed | `PlacementInputHandlerTests.Cancel_WithSelectInSameFrame_PlacesNothing` |
| Menu clicked again while placing | Switches building, drops the old candidate | `BuildingPlacerTests.Begin_WhilePlacing_SwitchesBuildingAndDropsCandidate` |
| Shift-placing many in a row | Unlimited, distinct ids, same spot turns invalid | `BuildingPlacerTests.TryPlace_KeepPlacing_UnlimitedBuildingsGetDistinctIds`, `BuildingPlacerTests.TryPlace_KeepPlacing_StaysInModeAndSameSpotTurnsInvalid` |

## Spawn point and production

| Case | Handling | Test(s) |
|---|---|---|
| Spawn side choice | Middle of bottom, then right, top, left; first usable one, fixed for life | `SpawnSidesTests.Candidate_ThreeByThree_MiddleOfBottomRightTopLeftInThatOrder`, `BuildingPlacerTests.MoveTo_ProducerInOpenField_TakesCellBelowBottomMiddle` |
| Barracks on the bottom edge / in a corner | Next side on the board is taken | `BuildingPlacerTests.MoveTo_ProducerOnBottomEdge_TakesRightSide`, `BuildingPlacerTests.MoveTo_ProducerInBottomRightCorner_TakesTopSide`, `BuildingPlacerTests.MoveTo_ProducerWithOnlyLeftSideOpen_TakesLeftSide` |
| No usable side at all | Placement invalid (red), not a silent useless Barracks | `BuildingPlacerTests.MoveTo_ProducerWithNoUsableSide_IsInvalid` |
| Side cell is a standing soldier's cell or another Barracks' spawn cell | Skipped | `BuildingPlacerTests.MoveTo_ProducerSpawnSideIsStandingSoldiersCell_SkipsThatSide`, `BuildingPlacerTests.MoveTo_ProducerWhoseSpawnSideIsAnotherProducersSpawnCell_SkipsThatSide` |
| Soldier merely walking through the side cell | Still usable; it walks on | `BuildingPlacerTests.MoveTo_ProducerSpawnSideHasSoldierWalkingThrough_KeepsThatSide` |
| Building placed on a Barracks' spawn cell | Invalid (a soldier's stop cell, by contrast, may be built on) | `BuildingPlacerTests.MoveTo_FootprintOverAnotherProducersSpawnCell_IsInvalid`, `BuildingPlacerTests.MoveTo_FootprintOverSoldiersStopCell_IsValid` |
| Soldier ordered onto a spawn cell | Stops on the nearest free cell instead | `UnitProducerTests.MoveOrder_ToTheSpawnCell_StopsBesideIt` |
| New soldier | Always appears on the spawn cell, then walks to the nearest free cell | `UnitProducerTests.TryProduce_FreeBoard_SoldierAppearsOnSpawnCellAndHoldsIt`, `UnitProducerTests.TryProduce_Success_SoldierWalksOffAndSpawnCellClears` |
| Many clicks in one frame (no production time) | All appear on the spawn cell; each ends on its own cell | `UnitProducerTests.TryProduce_ManyInOneFrame_AllAppearOnSpawnCellThenStandOnDistinctCells`, `UnitProducerTests.TryProduce_WhileFirstStillOnSpawnCell_SecondHoldsNoCellUntilItLeaves` |
| Soldiers crowd the spawn cell | Search looks past them to the next free cell | `UnitProducerTests.TryProduce_RingAroundSpawnFullOfSoldiers_LooksPastThem` |
| Board full, or spawn cell walled in by buildings | Production refused; buttons disabled with "No free cell for a new soldier." | `UnitProducerTests.TryProduce_BoardFull_ReturnsFalseAndChangesNothing`, `UnitProducerTests.CanProduce_SpawnCellWalledInByOtherBuildings_IsFalse`, `InfoPanelTests.Refresh_BarracksOnFullBoard_DisablesRowsAndShowsMessage` |
| Board fills / frees while the Barracks stays selected | Buttons follow (polled each frame while a producer is shown, allocation-free) | `InfoPanelTests.FollowProductionAvailability_BoardFillsWhileSelected_DisablesRows`, `InfoPanelTests.FollowProductionAvailability_CellFreesUp_EnablesRowsAndClearsMessage`, `InfoPanelTests.FollowProductionAvailability_EveryFrameWhileBarracksSelected_AllocatesNothing` |
| Disabled button clicked through code | Ignored | `InfoPanelTests.ClickingDisabledRow_ProducesNothing` |
| Pooled row reused after being disabled | Comes back enabled | `InfoPanelTests.Refresh_RowsOfDisabledBarracksReleased_GoBackToPoolEnabled` |
| Power Plant asked to produce (UI bypassed) | Throws; rule enforced in the model, not only the UI | `UnitProducerTests.TryProduce_BuildingThatProducesNothing_Throws`, `InfoPanelTests.Refresh_PowerPlantSelected_HasNoProductionSection` |
| Soldier creation throws | Spawn cell given back, nothing registered | `UnitProducerTests.TryProduce_FactoryThrows_GivesCellBackAndRethrows` |
| Barracks destroyed | Its spawn cell becomes an ordinary cell | `OccupantDespawnerTests.Despawn_Barracks_GivesBackItsSpawnCell` |
| Code tries to move a spawn cell or give a building a second one | Refused loudly: a spawn cell is a *permanent* reservation, a soldier's stop cell a *temporary* one, and the API keeps them apart (`ReserveSpawnCell` vs `ReserveStopCell`) | `CellReservationsTests.ReserveStopCell_OwnerHoldsSpawnCell_ThrowsAndKeepsIt`, `CellReservationsTests.ReserveSpawnCell_OwnerAlreadyHoldsACell_Throws` |

## Selection

| Case | Handling | Test(s) |
|---|---|---|
| Click off the board or on an unregistered id | Clears selection, never throws | `SelectionServiceTests.SelectAt_OffBoard_ClearsSelectionWithoutThrowing`, `SelectionServiceTests.SelectAt_CellWithUnregisteredId_TreatedAsEmpty` |
| Click just above a building's art | Selects nothing: art never leaves its footprint | `SelectionInputHandlerTests.Select_JustAboveFootprint_SelectsNothing` |
| Drag box past the board edge or dragged down-left | Clamped; both corners spanned | `SelectionInputHandlerTests.Drag_PastBoardEdge_BoxIsClampedToBoard`, `SelectionInputHandlerTests.Drag_DownAndLeft_BoxStillSpansBothCorners` |
| Box over a soldier that shares a cell (spawn stack, walking through) | Selected: found by position, not by grid cell | `SelectionServiceTests.SelectSoldiersIn_SoldierSharingAnotherSoldiersCell_IsSelectedToo` |
| Press and release in one cell | A click, not a box | `SelectionInputHandlerTests.PressAndReleaseInOneCell_IsAClickNotABox` |
| Selected object dies | Leaves the selection; panel shows empty state | `UnitCombatTests.SelectedTargetDies_LeavesTheSelection`, `InfoPanelTests.Refresh_SelectedObjectRemoved_ShowsEmptyState` |
| One of a selected group dies | The rest stays selected | `UnitCombatTests.SelectedSoldierDiesInGroup_RestStaysSelected` |
| Click on a soldier that holds no cell (while it passes through another soldier's cell, or waits there for a free cell) | Selects the cell's holder; a box around it still finds it | **accepted limit**; box: `SelectionServiceTests.SelectSoldiersIn_SoldierSharingAnotherSoldiersCell_IsSelectedToo` |

## Pathfinding and movement

| Case | Handling | Test(s) |
|---|---|---|
| Unreachable goal (enclosed, or a building cell) | Ends at the closest reachable cell | `PathfinderTests.TryFindPath_GoalEnclosed_ReturnsFalseAndEndsAtClosestReachableCell`, `UnitMovementTests.MoveGroup_TargetIsBuilding_StopsNextToIt` |
| Diagonal past a building corner / through a diagonal gap | Not cut, not squeezed through | `PathfinderTests.TryFindPath_DiagonalPastBuildingCorner_IsNotCut`, `PathfinderTests.TryFindPath_DiagonalGapBetweenTwoBuildings_IsNotSqueezedThrough` |
| Start walled in | Path is just the start | `PathfinderTests.TryFindPath_StartWalledIn_PathIsJustStart` |
| Group to one cell | Each soldier stops on its own cell | `UnitMovementTests.MoveGroup_ThreeSoldiersToOneCell_EachStopsOnItsOwnCell` |
| Swaps and corridor trades | Soldiers walk through each other; all finish | `UnitMovementTests.MoveGroup_TwoSoldiersSwapCells_BothFinish`, `UnitMovementTests.MoveGroup_SoldiersTradePlacesInCorridor_AllFinish` |
| Building placed on the path, the stop cell or a diagonal's side cell | New plan from where the soldier is | `UnitMovementTests.Tick_BuildingPlacedOnPath_RepathsAroundIt`, `UnitMovementTests.Tick_BuildingPlacedOnStopCell_StopsNextToIt`, `UnitMovementTests.Tick_BuildingPlacedOnSideCellOfDiagonalStep_RepathsWithoutEnteringIt` |
| Every reachable cell reserved | Waits, plans again until a cell frees | `UnitMovementTests.MoveGroup_EveryReachableCellReserved_WaitsUntilACellFrees` |
| Long frame | Passes several cells, stops exactly on target | `UnitMovementTests.Tick_LongFrame_PassesSeveralCellsAndStopsExactlyOnTarget` |
| Redirected mid-step | Never steps back | `UnitMovementTests.MoveGroup_RedirectedOffCentreWithDiagonalFirstStep_NeverStepsBack` |
| Off-board move target | Ignored | `UnitMovementTests.MoveGroup_TargetOffBoard_IsIgnored`, `CommandInputHandlerTests.Command_OffBoardWhileAttacking_KeepsAttackOrder` |
| Garbage per frame | None after warm-up | `UnitMovementTests.Tick_AfterWarmUp_AllocatesNothing`, `PathfinderTests.TryFindPath_AfterWarmUp_AllocatesNothing` |

## Combat and destruction

| Case | Handling | Test(s) |
|---|---|---|
| Soldier ordered to attack itself / its own group | Never targets itself | `UnitCombatTests.Attack_SelectedSoldierClickedByItsGroup_NeverTargetsItself`, `UnitCombatTests.Attack_OnlyTargetItself_DoesNothing` |
| Already in range | Attacks without walking | `UnitCombatTests.Attack_AlreadyInRange_AttacksWithoutWalking` |
| Ranged soldier | Stops at its range, not adjacent | `UnitCombatTests.Attack_Scout_StopsAtItsRangeNotAdjacent` |
| No free cell in range | Waits nearby, attacks when one frees | `UnitCombatTests.NoFreeCellInRange_WaitsNearbyThenAttacksWhenACellFrees` |
| Target walks away / out of range | Followed and attacked again | `UnitCombatTests.MovingTarget_IsFollowedAndAttackedWhereItStops`, `UnitCombatTests.Attacking_TargetWalksOutOfRange_ChasesAgain` |
| Target dies mid-chase; attacker dies mid-attack | Chaser goes idle on its reserved cell; no further damage | `UnitCombatTests.TargetDiesMidChase_ChaserGoesIdleAndStopsOnItsReservedCell`, `UnitCombatTests.AttackerDiesMidAttack_TargetTakesNoMoreDamage` |
| Two soldiers kill each other in one frame | Both die, once each | `UnitCombatTests.MutualLethalHitsInOneFrame_BothDie`, `HealthTests.TakeDamage_WhenDead_IsIgnoredAndNeverReportsDeathTwice` |
| Re-ordering to dodge the attack interval | Cooldown kept across targets and moves | `UnitCombatTests.SwitchingTarget_KeepsCooldown`, `UnitCombatTests.MoveThenAttackAgain_StillWaitsForTheAttackInterval` |
| Same object despawned twice / stale reference to a pooled object | Second call changes nothing; pooled object fails loudly | `OccupantDespawnerTests.Despawn_Twice_SecondCallChangesNothing`, `BuildingFactoryTests.Footprint_OfReleasedBuilding_ThrowsClearError` |
| Barracks destroyed with soldiers around | Soldiers stay; its cells take a building or a path | `UnitCombatTests.BarracksDestroyedWithSoldiersAround_SoldiersStayAndItsCellsTakeABuildingOrAPath` |
| Shooting over buildings | Range is grid distance; there is no line of sight | **accepted limit** (common RTS simplification) |

## UI, scrolling, window

| Case | Handling | Test(s) |
|---|---|---|
| Fewer buildings than visible rows / empty catalog | Catalog repeats in order; empty is safe | `ProductionMenuTests.Init_FewerBuildingsThanRows_RepeatsCatalogInOrder`, `ProductionMenuTests.Init_EmptyCatalog_CreatesNoRowsAndScrollingIsSafe` |
| Fling across many rows; scroll above the start | Correct window, rebased, wraps backwards | `ProductionMenuTests.Refresh_FlingAcrossManyRows_ShowsCorrectWindowAndRebases`, `ProductionMenuTests.Refresh_ScrolledAboveStart_ShowsLastBuildingOnTop` |
| Viewport resized | Rows grow and shrink through the pool | `ProductionMenuTests.UpdateViewCount_ViewportResized_GrowsAndShrinksThroughThePool` |
| Reused row clicked | One event, never the previous owner's | `ProductionMenuTests.ReusedRow_Click_RaisesBuildRequestedExactlyOnce`, `ProductionMenuTests.ReleasedRow_IsResetAndRaisesNoEvents` |
| Missing sprite in data | Icon hidden instead of a white square | `ProductionMenuTests.Bind_BuildingWithoutSprite_HidesIconInsteadOfWhiteSquare` |
| Any aspect ratio; area collapsed to nothing | Board fits between the panels; finite frame | `CameraFramingTests.FitBoard_AnyScreen_BoardAndPaddingFitInsideArea`, `CameraFramingTests.FitBoard_AreaCollapsedToNothing_ReturnsFiniteFrame` |
| Window resized below 1024×576 | Grown back to the minimum | `WindowSizeRuleTests.TryGrowToMinimum_TooSmallBothWays_GrowsToMinimum` |
| Pool misuse (double release, foreign item, null) | Throws | `ObjectPoolTests.Release_SameItemTwice_Throws`, `ObjectPoolTests.Release_ItemNotFromThisPool_Throws` |
