namespace StrategyDemo.Gameplay
{
    /// <summary>
    /// Who may attack whom. The single place a game mode changes: teams or an enemy side would supply
    /// another rule; soldiers, buildings and pathfinding stay as they are (Strategy pattern).
    /// </summary>
    public interface IAttackRule
    {
        /// <summary>True when <paramref name="attacker"/> may attack <paramref name="target"/>.</summary>
        bool CanAttack(Unit attacker, IDamageable target);
    }
}
