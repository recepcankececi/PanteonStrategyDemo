using System;
using StrategyDemo.Core;
using StrategyDemo.Data;
using UnityEngine;

namespace StrategyDemo.Gameplay
{
    /// <summary>
    /// Placement rules (R2, R3): which building is being placed, where the preview stands, whether that
    /// spot is valid, and placing it. Pure C# — it knows cells, not the mouse or the screen, so every
    /// edge case is tested without a scene. Input decides where the candidate is; the ghost draws it.
    /// </summary>
    /// <remarks>
    /// States: Idle (no <see cref="Definition"/>) → Placing without a candidate (pointer not on the board yet)
    /// → Placing with a candidate (ghost visible). Placing a building ends the mode unless the player keeps
    /// placing; cancelling always ends it. Placement is unlimited and instant (R3).
    /// </remarks>
    public sealed class BuildingPlacer
    {
        private readonly GridMap _grid;
        private readonly OccupantRegistry _occupants;
        private readonly IBuildingFactory _factory;

        /// <param name="grid">Board occupancy; placed buildings are written into it.</param>
        /// <param name="occupants">Hands out ids and records every placed building, so its cells lead back to it.</param>
        /// <param name="factory">Creates the scene object.</param>
        /// <exception cref="ArgumentNullException">Any argument is null.</exception>
        public BuildingPlacer(GridMap grid, OccupantRegistry occupants, IBuildingFactory factory)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _occupants = occupants ?? throw new ArgumentNullException(nameof(occupants));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            // Cells change under a still pointer too (a building destroyed, a soldier walking in): keep the preview's
            // colour true. Both live as long as the game; nothing to unsubscribe from.
            _grid.AreaChanged += HandleAreaChanged;
        }

        /// <summary>
        /// Raised whenever anything observable changes (mode, building, candidate cell or validity).
        /// Listeners read the properties; one event keeps the ghost unable to miss a combination.
        /// </summary>
        public event Action Changed;

        /// <summary>Building being placed; null when not placing.</summary>
        public BuildingDefinition Definition { get; private set; }

        /// <summary>True while the player is placing a building.</summary>
        public bool IsPlacing => Definition != null;

        /// <summary>True when the preview has a position on (or near) the board.</summary>
        public bool HasCandidate { get; private set; }

        /// <summary>Bottom-left cell of the preview. Meaningful only while <see cref="HasCandidate"/>.</summary>
        public Vector2Int CandidateOrigin { get; private set; }

        /// <summary>True when the preview's whole footprint is on the board and free.</summary>
        public bool IsCandidateValid { get; private set; }

        /// <summary>
        /// Starts placing <paramref name="definition"/>. While already placing, switches to it (the menu was
        /// clicked again); the candidate is dropped because it was computed for the old footprint.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="definition"/> is null.</exception>
        public void Begin(BuildingDefinition definition)
        {
            Definition = definition != null ? definition : throw new ArgumentNullException(nameof(definition));
            HasCandidate = false;
            IsCandidateValid = false;
            Changed?.Invoke();
        }

        /// <summary>Moves the preview so its bottom-left cell is <paramref name="origin"/>. Ignored when not placing.</summary>
        public void MoveTo(Vector2Int origin)
        {
            // Called every frame the pointer is on the board; only a real change notifies listeners.
            if (!IsPlacing || (HasCandidate && origin == CandidateOrigin))
            {
                return;
            }

            HasCandidate = true;
            CandidateOrigin = origin;
            IsCandidateValid = _grid.IsAreaFree(origin, Definition.Footprint);
            Changed?.Invoke();
        }

        /// <summary>Hides the preview (e.g. pointer over the UI) without leaving placement mode.</summary>
        public void ClearCandidate()
        {
            if (!HasCandidate)
            {
                return;
            }

            HasCandidate = false;
            IsCandidateValid = false;
            Changed?.Invoke();
        }

        /// <summary>
        /// Places the building at the candidate when it is valid: occupies the grid, creates the scene
        /// object and registers it. Afterwards placement ends, unless <paramref name="keepPlacing"/> is true (Shift held).
        /// </summary>
        /// <returns>True when a building was placed; false when not placing, no candidate, or the spot is not free.</returns>
        /// <remarks>If the factory throws, the grid is restored and the exception is passed on.</remarks>
        public bool TryPlace(bool keepPlacing)
        {
            if (!IsPlacing || !HasCandidate)
            {
                return false;
            }

            BuildingDefinition placed = Definition;
            // Validity is cached per cell; re-check, because the grid may have changed while the pointer stood
            // still (a building may have been destroyed). Occupy would throw on a stale "valid".
            if (!_grid.IsAreaFree(CandidateOrigin, placed.Footprint))
            {
                SetValidity(false);
                return false;
            }

            int occupantId = _occupants.NextId();
            // Grid first: it is the source of truth. If the scene object cannot be created, give the cells back,
            // or the spot would stay blocked forever with nothing standing on it.
            _grid.Occupy(CandidateOrigin, placed.Footprint, occupantId);
            try
            {
                _occupants.Add(_factory.Create(placed, CandidateOrigin, occupantId));
            }
            catch
            {
                _grid.Release(CandidateOrigin, placed.Footprint, occupantId);
                throw;
            }

            if (keepPlacing)
            {
                // The building now stands under the preview, so the same spot has become invalid.
                IsCandidateValid = _grid.IsAreaFree(CandidateOrigin, placed.Footprint);
            }
            else
            {
                ResetToIdle();
            }

            Changed?.Invoke();
            return true;
        }

        /// <summary>Leaves placement mode without placing. Ignored when not placing.</summary>
        public void Cancel()
        {
            if (!IsPlacing)
            {
                return;
            }

            ResetToIdle();
            Changed?.Invoke();
        }

        private void HandleAreaChanged(RectInt area)
        {
            if (HasCandidate && area.Overlaps(new RectInt(CandidateOrigin, Definition.Footprint)))
            {
                SetValidity(_grid.IsAreaFree(CandidateOrigin, Definition.Footprint));
            }
        }

        private void SetValidity(bool isValid)
        {
            if (IsCandidateValid != isValid)
            {
                IsCandidateValid = isValid;
                Changed?.Invoke();
            }
        }

        private void ResetToIdle()
        {
            Definition = null;
            HasCandidate = false;
            IsCandidateValid = false;
        }
    }
}
