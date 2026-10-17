using System;
using StrategyDemo.Data;
using UnityEngine;

namespace StrategyDemo.Gameplay
{
    /// <summary>
    /// Draws the drag box of <see cref="SelectionInputHandler"/>: the cells it covers, with the tiled cell sprite in a
    /// neutral tint. It shows exactly the cells that will be searched for soldiers, so what you see is what you select.
    /// </summary>
    /// <remarks>
    /// Reads the handler in <c>LateUpdate</c> (after input ran this frame) and touches the renderer only when the area
    /// changes; resizing a tiled sprite rebuilds its mesh. Vertex colour only, so batching holds.
    /// </remarks>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SelectionBox : MonoBehaviour
    {
        [SerializeField] private Color _tint = new Color(1f, 1f, 1f, 0.35f);

        private SpriteRenderer _renderer;
        private SelectionInputHandler _input;
        private GridView _gridView;
        private RectInt _shownArea;

        /// <summary>Starts drawing the box of <paramref name="input"/>; hidden while no box is dragged.</summary>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="InvalidOperationException">Already initialised.</exception>
        public void Init(SelectionInputHandler input, GridView gridView)
        {
            if (_input != null)
            {
                throw new InvalidOperationException($"{nameof(SelectionBox)} on '{name}' is already initialised.");
            }

            _input = input ?? throw new ArgumentNullException(nameof(input));
            _gridView = gridView != null ? gridView : throw new ArgumentNullException(nameof(gridView));
            _renderer = GetComponent<SpriteRenderer>();
            _renderer.color = _tint;
            _renderer.enabled = false;
        }

        /// <summary>Matches the box to the handler's drag; internal so EditMode tests can call it.</summary>
        internal void Refresh()
        {
            RectInt area = _input.IsDragging ? _input.DragArea : default;
            if (area.Equals(_shownArea))
            {
                return;
            }

            _shownArea = area;
            // An empty area (no drag, or a box wholly off the board) has no cells to draw.
            _renderer.enabled = area.width > 0 && area.height > 0;
            if (_renderer.enabled)
            {
                transform.position = _gridView.FootprintCenter(area.position, area.size);
                _renderer.size = (Vector2)area.size * GridConfig.CellWorldSize;
            }
        }

        private void LateUpdate()
        {
            if (_input != null)
            {
                Refresh();
            }
        }
    }
}
