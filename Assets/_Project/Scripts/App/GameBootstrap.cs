using System;
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

        private void Awake()
        {
            // Fail at start-up with a clear message instead of a NullReferenceException deep in a system.
            if (_gridConfig == null || _buildingCatalog == null || _gridView == null || _cameraFitter == null || _productionMenu == null)
            {
                Debug.LogError($"{nameof(GameBootstrap)} has unassigned references; the game cannot start.", this);
                enabled = false;
                return;
            }

            try
            {
                _gridView.Init(_gridConfig);
                _cameraFitter.Init(_gridView.WorldBounds);
                _productionMenu.Init(_buildingCatalog);
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
            // Placeholder until placement exists (M3): makes the menu's event visible in the console.
            Debug.Log($"Build requested: {building.DisplayName}", this);
        }
    }
}
