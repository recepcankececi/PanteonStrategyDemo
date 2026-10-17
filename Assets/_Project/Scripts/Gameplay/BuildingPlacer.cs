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
    /// <para>A spot is valid when the footprint is on the board and free, covers no other building's spawn cell, and a
    /// building that produces soldiers also finds its own spawn cell (<see cref="SpawnSides"/>): on the board, not under
    /// a building and not reserved. The spawn cell is chosen here, once, and becomes the building's permanent
    /// reservation in <see cref="CellReservations"/>: no soldier ever stops on it and no building is ever placed on it,
    /// yet soldiers still walk through it.</para>
    /// <para>States: Idle (no <see cref="Definition"/>) → Placing without a candidate (pointer not on the board yet)
    /// → Placing with a candidate (ghost visible). Placing a building ends the mode unless the player keeps
    /// placing; cancelling always ends it. Placement is unlimited and instant (R3).</para>
    /// </remarks>
    public sealed class BuildingPlacer
    {
        private readonly GridMap _grid;
        private readonly OccupantRegistry _occupants;
        private readonly CellReservations _reservations;
        private readonly IBuildingFactory _factory;
        // Cached once: converting a method group to a delegate on every call would allocate.
        private readonly Predicate<Vector2Int> _isUsableSpawnCell;
        // True while TryPlace writes the new building into the board; grid events it causes itself are ignored.
        private bool _isCommitting;

        /// <param name="grid">Board occupancy; placed buildings are written into it.</param>
        /// <param name="occupants">Hands out ids and records every placed building, so its cells lead back to it.</param>
        /// <param name="reservations">Temporary stop cells of soldiers and permanent spawn cells of buildings; a producer reserves its spawn cell here.</param>
        /// <param name="factory">Creates the scene object.</param>
        /// <exception cref="ArgumentNullException">Any argument is null.</exception>
        public BuildingPlacer(GridMap grid, OccupantRegistry occupants, CellReservations reservations, IBuildingFactory factory)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
            _occupants = occupants ?? throw new ArgumentNullException(nameof(occupants));
            _reservations = reservations ?? throw new ArgumentNullException(nameof(reservations));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _isUsableSpawnCell = IsUsableSpawnCell;
            // Cells change under a still pointer too (a building destroyed, a soldier walking in): keep the preview's
            // colour true. Both live as long as the game; nothing to unsubscribe from.
            _grid.AreaChanged += HandleAreaChanged;
        }

        /// <summary>
        /// Raised whenever anything observable changes (mode, building, candidate cell, validity or spawn cell).
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

        /// <summary>True when the building can be placed at the preview (see the class remarks).</summary>
        public bool IsCandidateValid { get; private set; }

        /// <summary>True when the preview is valid and its building produces soldiers: it has a spawn cell to show.</summary>
        public bool CandidateHasSpawnCell { get; private set; }

        /// <summary>Spawn cell the building would take. Meaningful only while <see cref="CandidateHasSpawnCell"/>.</summary>
        public Vector2Int CandidateSpawnCell { get; private set; }

        /// <summary>
        /// Starts placing <paramref name="definition"/>. While already placing, switches to it (the menu was
        /// clicked again); the candidate is dropped because it was computed for the old footprint.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="definition"/> is null.</exception>
        public void Begin(BuildingDefinition definition)
        {
            Definition = definition != null ? definition : throw new ArgumentNullException(nameof(definition));
            ClearCandidateState();
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
            Evaluate();
            Changed?.Invoke();
        }

        /// <summary>Hides the preview (e.g. pointer over the UI) without leaving placement mode.</summary>
        public void ClearCandidate()
        {
            if (!HasCandidate)
            {
                return;
            }

            ClearCandidateState();
            Changed?.Invoke();
        }

        /// <summary>
        /// Places the building at the candidate when it is valid: occupies the grid, creates the scene object, registers
        /// it and reserves its spawn cell. Afterwards placement ends, unless <paramref name="keepPlacing"/> is true (Shift held).
        /// </summary>
        /// <returns>True when a building was placed; false when not placing, no candidate, or the spot is not valid.</returns>
        /// <remarks>If the factory throws, the grid is restored and the exception is passed on.</remarks>
        public bool TryPlace(bool keepPlacing)
        {
            if (!IsPlacing || !HasCandidate)
            {
                return false;
            }

            // Validity is cached; evaluate again, because the board may have changed while the pointer stood still
            // (a building destroyed, a soldier reserving the spawn cell). Occupy would throw on a stale "valid".
            if (!RefreshValidity())
            {
                return false;
            }

            BuildingDefinition placed = Definition;
            bool hasSpawnCell = CandidateHasSpawnCell;
            Vector2Int spawnCell = CandidateSpawnCell;
            int occupantId = _occupants.NextId();
            // Occupy raises AreaChanged, which would re-evaluate the candidate halfway through placing (the building not
            // yet registered, its spawn cell not yet reserved). Ignore it; the state is evaluated once at the end.
            _isCommitting = true;
            try
            {
                // Grid first: it is the source of truth. If anything below fails, give everything back, or the spot
                // would stay blocked forever with nothing standing on it.
                _grid.Occupy(CandidateOrigin, placed.Footprint, occupantId);
                try
                {
                    if (hasSpawnCell)
                    {
                        // The building's permanent reservation, for its whole life; Building.LeaveBoard gives it back.
                        _reservations.ReserveSpawnCell(spawnCell, occupantId);
                    }

                    _occupants.Add(_factory.Create(placed, CandidateOrigin, occupantId, hasSpawnCell ? spawnCell : (Vector2Int?)null));
                }
                catch
                {
                    _reservations.Release(occupantId);
                    _grid.Release(CandidateOrigin, placed.Footprint, occupantId);
                    throw;
                }
            }
            finally
            {
                _isCommitting = false;
            }

            if (keepPlacing)
            {
                // The building now stands under the preview, so the same spot has become invalid.
                Evaluate();
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
            // Any change, not only one on the footprint: a spawn side may have been built on or freed. Evaluating is a
            // footprint scan plus four cells.
            if (HasCandidate && !_isCommitting)
            {
                RefreshValidity();
            }
        }

        /// <summary>Evaluates the candidate again and notifies listeners if the result changed. Returns its validity.</summary>
        private bool RefreshValidity()
        {
            bool wasValid = IsCandidateValid;
            bool hadSpawnCell = CandidateHasSpawnCell;
            Vector2Int oldSpawnCell = CandidateSpawnCell;
            Evaluate();
            if (wasValid != IsCandidateValid || hadSpawnCell != CandidateHasSpawnCell || oldSpawnCell != CandidateSpawnCell)
            {
                Changed?.Invoke();
            }

            return IsCandidateValid;
        }

        /// <summary>Sets the validity and spawn cell of the current candidate.</summary>
        private void Evaluate()
        {
            Vector2Int footprint = Definition.Footprint;
            CandidateHasSpawnCell = false;
            CandidateSpawnCell = default;
            if (!_grid.IsAreaFree(CandidateOrigin, footprint) || CoversSpawnCell(CandidateOrigin, footprint))
            {
                IsCandidateValid = false;
                return;
            }

            if (!Definition.CanProduceUnits)
            {
                IsCandidateValid = true;
                return;
            }

            // A producer without a spawn cell could never produce: such a spot is invalid, not silently useless.
            CandidateHasSpawnCell = SpawnSides.TryChoose(CandidateOrigin, footprint, _isUsableSpawnCell, out Vector2Int spawnCell);
            CandidateSpawnCell = spawnCell;
            IsCandidateValid = CandidateHasSpawnCell;
        }

        private bool CoversSpawnCell(Vector2Int origin, Vector2Int footprint)
        {
            for (int y = origin.y; y < origin.y + footprint.y; y++)
            {
                for (int x = origin.x; x < origin.x + footprint.x; x++)
                {
                    // A soldier's temporary stop cell may be built on (the soldier plans again); a building's
                    // permanent spawn cell may not.
                    if (_reservations.IsPermanent(new Vector2Int(x, y)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool IsUsableSpawnCell(Vector2Int cell)
        {
            if (!_grid.IsInside(cell) || _reservations.OwnerOf(cell) != GridMap.EmptyId)
            {
                return false;
            }

            // A soldier walking through is fine, it walks on; a building there would block the spawn for good.
            int occupant = _grid.GetOccupant(cell);
            return occupant == GridMap.EmptyId || IsSoldier(occupant);
        }

        private bool IsSoldier(int occupantId)
        {
            return _occupants.TryGet(occupantId, out IBoardOccupant occupant) && occupant.IsMobile;
        }

        private void ClearCandidateState()
        {
            HasCandidate = false;
            IsCandidateValid = false;
            CandidateHasSpawnCell = false;
            CandidateSpawnCell = default;
        }

        private void ResetToIdle()
        {
            Definition = null;
            ClearCandidateState();
        }
    }
}
