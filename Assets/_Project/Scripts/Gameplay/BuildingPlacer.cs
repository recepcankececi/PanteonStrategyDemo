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
        private readonly IBuildingFactory _factory;
        // Last id handed out; 0 is GridMap.EmptyId, so the first building gets 1.
        private int _lastOccupantId = GridMap.EmptyId;

        /// <exception cref="ArgumentNullException">Any argument is null.</exception>
        public BuildingPlacer(GridMap grid, IBuildingFactory factory)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
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
        /// Places the building at the candidate when it is valid: occupies the grid and creates the scene
        /// object. Afterwards placement ends, unless <paramref name="keepPlacing"/> is true (Shift held).
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
            // still (from M6 buildings are destroyed). Occupy would throw on a stale "valid".
            if (!_grid.IsAreaFree(CandidateOrigin, placed.Footprint))
            {
                SetValidity(false);
                return false;
            }

            int occupantId = ++_lastOccupantId;
            // Grid first: it is the source of truth. If the scene object cannot be created, give the cells back,
            // or the spot would stay blocked forever with nothing standing on it.
            _grid.Occupy(CandidateOrigin, placed.Footprint, occupantId);
            try
            {
                _factory.Create(placed, CandidateOrigin, occupantId);
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
