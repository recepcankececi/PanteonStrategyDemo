using System;
using StrategyDemo.Data;
using UnityEngine;

namespace StrategyDemo.Gameplay
{
    /// <summary>
    /// Marks the cells of the selected building or soldier with the tiled cell sprite, in its own colour so it
    /// never reads as the green/red placement preview. One renderer in the scene, moved and resized on change.
    /// </summary>
    /// <remarks>
    /// The colour is a vertex colour (<see cref="SpriteRenderer.color"/>): the shared material is untouched and
    /// the highlight batches with the world atlas (ADR-001 Karar 9).
    /// </remarks>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SelectionHighlight : MonoBehaviour
    {
        [SerializeField] private Color _tint = new Color(1f, 0.85f, 0.2f, 0.5f);

        private SpriteRenderer _renderer;
        private SelectionService _selection;
        private GridView _gridView;
        private bool _isSubscribed;

        /// <summary>Starts following <paramref name="selection"/>; hidden while nothing is selected.</summary>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="InvalidOperationException">Already initialised.</exception>
        public void Init(SelectionService selection, GridView gridView)
        {
            if (_selection != null)
            {
                throw new InvalidOperationException($"{nameof(SelectionHighlight)} on '{name}' is already initialised.");
            }

            _selection = selection ?? throw new ArgumentNullException(nameof(selection));
            _gridView = gridView != null ? gridView : throw new ArgumentNullException(nameof(gridView));
            _renderer = GetComponent<SpriteRenderer>();
            // Init runs after OnEnable on a scene object, so subscribe here too; the flag prevents doubling.
            if (isActiveAndEnabled)
            {
                Subscribe();
            }

            Refresh();
        }

        /// <summary>Matches the highlight to the current selection; internal so EditMode tests can call it.</summary>
        internal void Refresh()
        {
            IBoardOccupant selected = _selection.Selected;
            _renderer.enabled = selected != null;
            if (selected == null)
            {
                return;
            }

            // Pivot is centred: put it in the middle of the covered area, then size the tiling to the footprint.
            Vector2 areaSize = (Vector2)selected.Footprint * GridConfig.CellWorldSize;
            Vector3 baseCenter = _gridView.FootprintBaseCenter(selected.Origin, selected.Footprint);
            transform.position = baseCenter + new Vector3(0f, areaSize.y * 0.5f, 0f);
            _renderer.size = areaSize;
            _renderer.color = _tint;
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
        }

        private void Subscribe()
        {
            if (!_isSubscribed)
            {
                _selection.SelectionChanged += Refresh;
                _isSubscribed = true;
            }
        }
    }
}
