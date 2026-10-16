using StrategyDemo.Data;
using StrategyDemo.Gameplay;
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
        [SerializeField] private GridView _gridView;
        [SerializeField] private BoardCameraFitter _cameraFitter;

        private void Awake()
        {
            // Fail at start-up with a clear message instead of a NullReferenceException deep in a system.
            if (_gridConfig == null || _gridView == null || _cameraFitter == null)
            {
                Debug.LogError($"{nameof(GameBootstrap)} has unassigned references; the game cannot start.", this);
                enabled = false;
                return;
            }

            _gridView.Init(_gridConfig);
            _cameraFitter.Init(_gridView.WorldBounds);
        }
    }
}
