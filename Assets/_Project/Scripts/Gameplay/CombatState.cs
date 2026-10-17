namespace StrategyDemo.Gameplay
{
    /// <summary>What a soldier does in combat. Exactly one at a time, so no flag combination is invalid.</summary>
    public enum CombatState
    {
        /// <summary>No attack order: standing, or walking where the player sent it.</summary>
        Idle,

        /// <summary>Walking to a cell within attack range of its target (or waiting near it for one to free up).</summary>
        Chasing,

        /// <summary>Standing within range and hitting its target once every attack interval.</summary>
        Attacking,
    }
}
