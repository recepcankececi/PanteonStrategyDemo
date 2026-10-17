namespace StrategyDemo.Gameplay
{
    /// <summary>
    /// The case's rule: there is no enemy side, so a soldier may attack any building or soldier except itself
    /// (ADR-001 scope decision).
    /// </summary>
    public sealed class FreeForAllAttackRule : IAttackRule
    {
        /// <inheritdoc/>
        public bool CanAttack(Unit attacker, IDamageable target)
        {
            return attacker != null && target != null && !ReferenceEquals(attacker, target);
        }
    }
}
