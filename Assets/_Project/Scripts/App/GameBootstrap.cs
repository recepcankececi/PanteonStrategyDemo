using System;
using StrategyDemo.Core;
using StrategyDemo.Data;
using StrategyDemo.Gameplay;
using StrategyDemo.UI;
using UnityEngine;

namespace StrategyDemo.App
{
    /// <summary>
    /// Composition root (ADR-001 Karar 3): the only place that creates services and wires scene
    /// objects together. Read top to bottom, it is the start-up order of the game.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private GridConfig _gridConfig;
        [SerializeField] private BuildingCatalog _buildingCatalog;
        [SerializeField] private GridView _gridView;
        [SerializeField] private BoardCameraFitter _cameraFitter;
        [SerializeField] private ProductionMenu _productionMenu;
        [SerializeField] private Building _buildingPrefab;
        [SerializeField, Tooltip("Board container placed buildings are parented under.")]
        private Transform _buildingsRoot;
        [SerializeField, Tooltip("Board container pooled (inactive) buildings wait under.")]
        private Transform _buildingPoolRoot;
        [SerializeField] private PlacementGhost _placementGhost;
        [SerializeField] private Unit _unitPrefab;
        [SerializeField, Tooltip("Board container produced soldiers are parented under.")]
        private Transform _unitsRoot;
        [SerializeField, Tooltip("Board container pooled (inactive) soldiers wait under.")]
        private Transform _unitPoolRoot;
        [SerializeField] private SelectionHighlight _selectionHighlight;
        [SerializeField] private SelectionBox _selectionBox;
        [SerializeField] private UnitMovementDriver _movementDriver;
        [SerializeField] private UnitCombatDriver _combatDriver;
        [SerializeField] private CombatEffects _combatEffects;
        [SerializeField] private InfoPanel _infoPanel;
        [SerializeField] private InputController _inputController;

        private BuildingPlacer _placer;

        private void Awake()
        {
            // Fail at start-up with a clear message instead of a NullReferenceException deep in a system.
            if (_gridConfig == null || _buildingCatalog == null || _gridView == null || _cameraFitter == null || _productionMenu == null
                || _buildingPrefab == null || _buildingsRoot == null || _buildingPoolRoot == null || _placementGhost == null
                || _unitPrefab == null || _unitsRoot == null || _unitPoolRoot == null || _selectionHighlight == null
                || _selectionBox == null || _movementDriver == null || _combatDriver == null || _combatEffects == null || _infoPanel == null
                || _inputController == null)
            {
                Debug.LogError($"{nameof(GameBootstrap)} has unassigned references; the game cannot start.", this);
                enabled = false;
                return;
            }

            try
            {
                var gridMap = new GridMap(_gridConfig.Width, _gridConfig.Height);
                _gridView.Init(_gridConfig);
                _cameraFitter.Init(_gridView.WorldBounds);
                _productionMenu.Init(_buildingCatalog);

                // One registry for buildings and soldiers: both hold grid cells, and a clicked cell must lead to either.
                var occupants = new OccupantRegistry();
                var buildingFactory = new BuildingFactory(_buildingPrefab, _buildingsRoot, _buildingPoolRoot, _gridView);
                _placer = new BuildingPlacer(gridMap, occupants, buildingFactory);
                _placementGhost.Init(_placer, _gridView);

                // Where every soldier will stand: shared by production (spawn cells) and movement (stop cells).
                var reservations = new CellReservations(_gridConfig.Width, _gridConfig.Height);
                var unitFactory = new UnitFactory(_unitPrefab, _unitsRoot, _unitPoolRoot, _gridView);
                var unitProducer = new UnitProducer(gridMap, occupants, reservations, unitFactory);
                var movement = new UnitMovement(gridMap, occupants, reservations, _gridView);
                _movementDriver.Init(movement);

                var selection = new SelectionService(gridMap, occupants);
                var selectionInput = new SelectionInputHandler(selection, _gridView);
                _selectionHighlight.Init(selection);
                _selectionBox.Init(selectionInput, _gridView);
                _infoPanel.Init(selection, unitProducer);

                // The one remove path for whatever dies; combat deals the damage and calls it (R9, R10).
                var despawner = new OccupantDespawner(gridMap, occupants, movement, selection);
                var combat = new UnitCombat(movement, new FreeForAllAttackRule(), despawner);
                _combatDriver.Init(combat);
                _combatEffects.Init(combat, despawner);

                // The fitter's camera is the one drawing the board, so it is the one that maps the pointer.
                _inputController.Init(
                    _cameraFitter.GetComponent<Camera>(),
                    new PlacementInputHandler(_placer, _gridView),
                    selectionInput,
                    new CommandInputHandler(selection, movement, combat, gridMap, occupants, _gridView));
            }
            catch (Exception exception)
            {
                // Init methods throw on misconfiguration; report it like a missing reference and stop.
                Debug.LogException(exception, this);
                enabled = false;
            }
        }

        // Disabling the component in Awake also skips OnEnable, so a half-wired game is never subscribed.
        private void OnEnable()
        {
            _productionMenu.BuildRequested += HandleBuildRequested;
        }

        private void OnDisable()
        {
            _productionMenu.BuildRequested -= HandleBuildRequested;
        }

        private void HandleBuildRequested(BuildingDefinition building)
        {
            // A click while already placing switches the ghost to the new building.
            _placer.Begin(building);
        }
    }
}
