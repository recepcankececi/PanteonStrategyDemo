using UnityEngine;

namespace StrategyDemo.Gameplay.Board
{
    /// <summary>
    /// Anything that holds cells in the <see cref="StrategyDemo.Core.GridMap"/>: buildings and soldiers.
    /// Selection, the selection highlight and attacks work with this, not with the concrete type.
    /// </summary>
    public interface IBoardOccupant
    {
        /// <summary>Id written into every grid cell this occupant covers; key in the <see cref="OccupantRegistry"/>.</summary>
        int OccupantId { get; }

        /// <summary>Bottom-left covered cell.</summary>
        Vector2Int Origin { get; }

        /// <summary>Covered cells in columns × rows; 1×1 for a soldier.</summary>
        Vector2Int Footprint { get; }

        /// <summary>
        /// True for soldiers, false for buildings. A mobile occupant can step aside, so searches for free cells may
        /// look past it; a building is a wall.
        /// </summary>
        bool IsMobile { get; }

        /// <summary>
        /// World position of the occupant's centre (its sprite pivot). Follows a walking soldier smoothly between
        /// cells, so markers drawn on it (the selection highlight) move with it.
        /// </summary>
        Vector3 WorldCenter { get; }
    }
}
