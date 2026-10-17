using System;
using StrategyDemo.Data;
using UnityEngine;

namespace StrategyDemo.Gameplay
{
    /// <summary>
    /// Draws the building preview for a <see cref="BuildingPlacer"/> (R2): the building's sprite, plus a
    /// translucent overlay over the cells it would cover, both tinted green (valid) or red (invalid).
    /// One ghost lives in the scene and is reused for every building; it never gets instantiated again.
    /// </summary>
    /// <remarks>
    /// Tinting uses <see cref="SpriteRenderer.color"/>, a vertex colour: the shared material stays untouched,
    /// so the ghost batches with the rest of the world atlas.
    /// </remarks>
    public sealed class PlacementGhost : MonoBehaviour
    {
        [SerializeField, Tooltip("Shows the building sprite; its pivot is the footprint centre.")]
        private SpriteRenderer _buildingRenderer;
        [SerializeField, Tooltip("Tiled sprite sized to the footprint; one tile per cell, pivot centred.")]
        private SpriteRenderer _footprintRenderer;
        [SerializeField] private Color _validBuildingTint = new Color(0.7f, 1f, 0.7f, 0.85f);
        [SerializeField] private Color _invalidBuildingTint = new Color(1f, 0.5f, 0.5f, 0.85f);
        [SerializeField] private Color _validFootprintTint = new Color(0.3f, 1f, 0.3f, 0.45f);
        [SerializeField] private Color _invalidFootprintTint = new Color(1f, 0.2f, 0.2f, 0.45f);

        private BuildingPlacer _placer;
        private GridView _gridView;
        private bool _isSubscribed;

        /// <summary>Starts following <paramref name="placer"/>; hidden until it has a candidate.</summary>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="InvalidOperationException">A renderer is not assigned or the ghost was already initialised.</exception>
        public void Init(BuildingPlacer placer, GridView gridView)
        {
            if (_placer != null)
            {
                throw new InvalidOperationException($"{nameof(PlacementGhost)} on '{name}' is already initialised.");
            }

            if (_buildingRenderer == null || _footprintRenderer == null)
            {
                throw new InvalidOperationException($"{nameof(PlacementGhost)} on '{name}' has unassigned renderers.");
            }

            _placer = placer ?? throw new ArgumentNullException(nameof(placer));
            _gridView = gridView != null ? gridView : throw new ArgumentNullException(nameof(gridView));
            // Init runs after OnEnable on a scene object, so subscribe here too; the flag prevents doubling.
            if (isActiveAndEnabled)
            {
                Subscribe();
            }

            Refresh();
        }

        /// <summary>
        /// Matches the visuals to the placer's state. Runs on every placer change; internal so EditMode
        /// tests can check the result directly.
        /// </summary>
        internal void Refresh()
        {
            bool visible = _placer.IsPlacing && _placer.HasCandidate;
            _buildingRenderer.enabled = visible;
            _footprintRenderer.enabled = visible;
            if (!visible)
            {
                return;
            }

            Vector2Int footprint = _placer.Definition.Footprint;
            bool valid = _placer.IsCandidateValid;
            // The root sits where a placed building's pivot would: the centre of the footprint.
            transform.position = _gridView.FootprintCenter(_placer.CandidateOrigin, footprint);
            _buildingRenderer.sprite = _placer.Definition.Sprite;
            _buildingRenderer.color = valid ? _validBuildingTint : _invalidBuildingTint;
            // The overlay shares the building's centred pivot, so sizing it to the footprint covers the area exactly.
            _footprintRenderer.size = (Vector2)footprint * GridConfig.CellWorldSize;
            _footprintRenderer.color = valid ? _validFootprintTint : _invalidFootprintTint;
        }

        private void OnEnable()
        {
            if (_placer != null)
            {
                Subscribe();
                Refresh();
            }
        }

        private void OnDisable()
        {
            if (_isSubscribed)
            {
                _placer.Changed -= Refresh;
                _isSubscribed = false;
            }
        }

        private void Subscribe()
        {
            if (!_isSubscribed)
            {
                _placer.Changed += Refresh;
                _isSubscribed = true;
            }
        }
    }
}
