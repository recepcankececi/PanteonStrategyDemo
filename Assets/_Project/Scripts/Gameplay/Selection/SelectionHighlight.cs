using System;
using System.Collections.Generic;
using StrategyDemo.Core;
using StrategyDemo.Data;
using StrategyDemo.Gameplay.Board;
using StrategyDemo.Gameplay.Combat;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StrategyDemo.Gameplay.Selection
{
    /// <summary>
    /// Marks every selected building or soldier with the tiled cell sprite, in its own colour so it never reads as the
    /// green/red placement preview. One pooled marker per selected object; markers follow walking soldiers. Selected
    /// objects also pin their health bar, so it shows even at full health.
    /// </summary>
    /// <remarks>
    /// The colour is a vertex colour (<see cref="SpriteRenderer.color"/>): the shared material is untouched and the
    /// markers batch with the world atlas. Markers are pooled because a box selection can mark many
    /// soldiers and is redrawn on every selection change.
    /// </remarks>
    public sealed class SelectionHighlight : MonoBehaviour
    {
        [SerializeField, Tooltip("Tiled cell-highlight renderer copied once per selected object; its pivot must be centred.")]
        private SpriteRenderer _markerPrefab;
        [SerializeField] private Color _tint = new Color(1f, 0.85f, 0.2f, 0.5f);

        private readonly List<SpriteRenderer> _markers = new List<SpriteRenderer>();
        // Objects whose health bar this highlight pinned; unpinned on the next refresh.
        private readonly List<IDamageable> _pinned = new List<IDamageable>();
        private ObjectPool<SpriteRenderer> _markerPool;
        private SelectionService _selection;
        private bool _isSubscribed;

        /// <summary>Markers currently shown, in selection order. Internal so EditMode tests can read them.</summary>
        internal IReadOnlyList<SpriteRenderer> Markers => _markers;

        /// <summary>Starts following <paramref name="selection"/>; shows nothing while nothing is selected.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="selection"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Already initialised, or the marker prefab is not assigned.</exception>
        public void Init(SelectionService selection)
        {
            if (_selection != null)
            {
                throw new InvalidOperationException($"{nameof(SelectionHighlight)} on '{name}' is already initialised.");
            }

            if (_markerPrefab == null)
            {
                throw new InvalidOperationException($"{nameof(SelectionHighlight)} on '{name}' has no marker prefab assigned.");
            }

            _selection = selection ?? throw new ArgumentNullException(nameof(selection));
            _markerPool = new ObjectPool<SpriteRenderer>(CreateMarker, OnMarkerTaken, OnMarkerReleased);
            // Init runs after OnEnable on a scene object, so subscribe here too; the flag prevents doubling.
            if (isActiveAndEnabled)
            {
                Subscribe();
            }

            Refresh();
        }

        /// <summary>One marker per selected object, sized to its footprint; internal so EditMode tests can call it.</summary>
        internal void Refresh()
        {
            for (int i = 0; i < _markers.Count; i++)
            {
                _markerPool.Release(_markers[i]);
            }

            _markers.Clear();
            // A destroyed object leaves the selection before it returns to its pool, so it is still safe to unpin here.
            UnpinAll();
            IReadOnlyList<IBoardOccupant> selected = _selection.All;
            for (int i = 0; i < selected.Count; i++)
            {
                SpriteRenderer marker = _markerPool.Get();
                marker.size = (Vector2)selected[i].Footprint * GridConfig.CellWorldSize;
                _markers.Add(marker);
                if (selected[i] is IDamageable damageable)
                {
                    damageable.PinHealthBar(true);
                    _pinned.Add(damageable);
                }
            }

            FollowSelection();
        }

        /// <summary>Moves each marker onto its object's centre; internal so EditMode tests can call it.</summary>
        internal void FollowSelection()
        {
            // Same order as the selection: Refresh builds the marker list from it, and both change only together.
            IReadOnlyList<IBoardOccupant> selected = _selection.All;
            for (int i = 0; i < _markers.Count; i++)
            {
                _markers[i].transform.position = selected[i].WorldCenter;
            }
        }

        private void LateUpdate()
        {
            // After movement ran this frame, so markers sit on the soldiers' final positions.
            if (_selection != null)
            {
                FollowSelection();
            }
        }

        private void OnEnable()
        {
            if (_selection != null)
            {
                Subscribe();
                Refresh();
            }
        }

        private void OnDisable()
        {
            if (_isSubscribed)
            {
                _selection.SelectionChanged -= Refresh;
                _isSubscribed = false;
            }

            // Not listening any more, so a pinned object could be destroyed and reused unseen: let go of them now.
            UnpinAll();
        }

        private void UnpinAll()
        {
            for (int i = 0; i < _pinned.Count; i++)
            {
                _pinned[i].PinHealthBar(false);
            }

            _pinned.Clear();
        }

        private void Subscribe()
        {
            if (!_isSubscribed)
            {
                _selection.SelectionChanged += Refresh;
                _isSubscribed = true;
            }
        }

        private SpriteRenderer CreateMarker()
        {
            SpriteRenderer marker = Object.Instantiate(_markerPrefab, transform);
            marker.name = _markerPrefab.name;
            marker.color = _tint;
            return marker;
        }

        private static void OnMarkerTaken(SpriteRenderer marker)
        {
            marker.gameObject.SetActive(true);
        }

        private static void OnMarkerReleased(SpriteRenderer marker)
        {
            marker.gameObject.SetActive(false);
        }
    }
}
