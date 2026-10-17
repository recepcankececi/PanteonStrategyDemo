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
        [SerializeField] private PlacementGhost _placementGhost;
        [SerializeField] private InputController _inputController;

        private BuildingPlacer _placer;

        private void Awake()
        {
            // Fail at start-up with a clear message instead of a NullReferenceException deep in a system.
            if (_gridConfig == null || _buildingCatalog == null || _gridView == null || _cameraFitter == null || _productionMenu == null
                || _buildingPrefab == null || _buildingsRoot == null || _placementGhost == null || _inputController == null)
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

                var buildingFactory = new BuildingFactory(_buildingPrefab, _buildingsRoot, _gridView);
                _placer = new BuildingPlacer(gridMap, buildingFactory);
                _placementGhost.Init(_placer, _gridView);
                // The fitter's camera is the one drawing the board, so it is the one that maps the pointer.
                _inputController.Init(_cameraFitter.GetComponent<Camera>(), new PlacementInputHandler(_placer, _gridView));
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
