using System;
using System.Collections.Generic;
using StrategyDemo.Core;
using UnityEngine;

namespace StrategyDemo.Gameplay
{
    /// <summary>
    /// Soldiers attacking buildings and soldiers (R9, R10; ADR-001 Karar 10). Pure C#: <see cref="Tick"/> is driven once
    /// per frame by <see cref="UnitCombatDriver"/>, after movement, and stepped by hand in EditMode tests.
    /// </summary>
    /// <remarks>
    /// <para>Each attacking soldier has one order in one of two states (no order = <see cref="CombatState.Idle"/>):</para>
    /// <list type="bullet">
    /// <item><b>Chasing:</b> walking to its own cell within range (<see cref="UnitMovement.Approach(Unit, IBoardOccupant)"/>).
    /// Becomes Attacking once it stands within range. The chase is planned again at most every
    /// <see cref="RepathInterval"/> seconds, and only when that can help: the target stepped to another cell, or the
    /// soldier stopped out of range (no free cell in range when it planned).</item>
    /// <item><b>Attacking:</b> standing within range, one hit every attack interval. Back to Chasing when the target is
    /// out of range (a soldier walked off).</item>
    /// </list>
    /// <para>Range is the Chebyshev distance from the soldier's cell to the target's nearest footprint cell
    /// (<see cref="GridDistance"/>). Hits are instant (no projectile); the attack interval is timed on the soldier
    /// against this class's clock, so neither re-targeting nor a move in between can make a soldier hit faster. Deaths are resolved after every soldier has acted
    /// in the frame: two soldiers hitting each other in the same frame both land their hit, and the order list is never
    /// changed while it is being looped over. Removal itself goes through <see cref="OccupantDespawner"/>, whose
    /// <see cref="OccupantDespawner.Despawned"/> event drops the orders of and against the dead object here.</para>
    /// </remarks>
    public sealed class UnitCombat
    {
        /// <summary>
        /// Seconds between two path plans while chasing: often enough to follow a walking soldier (3 cells/s), rare
        /// enough that a dozen chasers do not run A* every frame.
        /// </summary>
        public const float RepathInterval = 0.5f;

        private readonly UnitMovement _movement;
        private readonly IAttackRule _rule;
        private readonly OccupantDespawner _despawner;
        private readonly ObjectPool<AttackOrder> _orderPool;
        private readonly List<AttackOrder> _orders = new List<AttackOrder>();
        // Scratch lists, kept to avoid allocating per command or per frame.
        private readonly List<Unit> _attackers = new List<Unit>();
        private readonly List<IDamageable> _killed = new List<IDamageable>();
        // Seconds of combat so far. Double: a float summing frame times loses precision over a long session.
        private double _clock;

        /// <param name="movement">Walks attackers into range.</param>
        /// <param name="rule">Decides who may attack whom.</param>
        /// <param name="despawner">Removes what dies; its event tells combat to drop orders.</param>
        /// <exception cref="ArgumentNullException">Any argument is null.</exception>
        public UnitCombat(UnitMovement movement, IAttackRule rule, OccupantDespawner despawner)
        {
            _movement = movement ?? throw new ArgumentNullException(nameof(movement));
            _rule = rule ?? throw new ArgumentNullException(nameof(rule));
            _despawner = despawner ?? throw new ArgumentNullException(nameof(despawner));
            _orderPool = new ObjectPool<AttackOrder>(() => new AttackOrder(), null, order => order.Clear());
            // Both live as long as the game; nothing to unsubscribe from.
            _despawner.Despawned += HandleDespawned;
        }

        /// <summary>Soldiers with an attack order (chasing or attacking).</summary>
        public int AttackerCount => _orders.Count;

        /// <summary>What <paramref name="unit"/> is doing in combat.</summary>
        public CombatState StateOf(Unit unit)
        {
            AttackOrder order = FindOrder(unit);
            return order != null ? order.State : CombatState.Idle;
        }

        /// <summary>Target of <paramref name="unit"/>; null while it is idle.</summary>
        public IDamageable TargetOf(Unit unit)
        {
            return FindOrder(unit)?.Target;
        }

        /// <summary>
        /// Orders every soldier in <paramref name="group"/> the rule allows (never <paramref name="target"/> itself) to
        /// attack <paramref name="target"/>; each walks to its own cell within range. Buildings in the group are skipped.
        /// </summary>
        /// <returns>False when no soldier of the group may attack it (e.g. the only selected soldier clicked itself).</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public bool Attack(IReadOnlyList<IBoardOccupant> group, IDamageable target)
        {
            if (group == null)
            {
                throw new ArgumentNullException(nameof(group));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (!target.Health.IsAlive)
            {
                return false;
            }

            _attackers.Clear();
            for (int i = 0; i < group.Count; i++)
            {
                if (group[i] is Unit unit && _rule.CanAttack(unit, target))
                {
                    _attackers.Add(unit);
                }
            }

            if (_attackers.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < _attackers.Count; i++)
            {
                Unit unit = _attackers[i];
                AttackOrder order = FindOrder(unit) ?? StartOrder(unit);
                order.Target = target;
                order.State = CombatState.Chasing;
                order.RepathTimer = RepathInterval;
                order.PlannedTargetCell = target.Origin;
            }

            // One call for the group: every old stop cell is let go first, then each soldier picks its own.
            _movement.Approach(_attackers, target);
            _attackers.Clear();
            return true;
        }

        /// <summary>Drops the attack orders of the soldiers in <paramref name="group"/> (e.g. the player sent them elsewhere).</summary>
        /// <exception cref="ArgumentNullException"><paramref name="group"/> is null.</exception>
        public void Stop(IReadOnlyList<IBoardOccupant> group)
        {
            if (group == null)
            {
                throw new ArgumentNullException(nameof(group));
            }

            for (int i = 0; i < group.Count; i++)
            {
                if (group[i] is Unit unit)
                {
                    AttackOrder order = FindOrder(unit);
                    if (order != null)
                    {
                        RemoveOrder(order);
                    }
                }
            }
        }

        /// <summary>Advances every attack by <paramref name="deltaTime"/> seconds, then removes what died.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="deltaTime"/> is negative.</exception>
        public void Tick(float deltaTime)
        {
            if (deltaTime < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaTime), deltaTime, "Time cannot run backwards.");
            }

            _clock += deltaTime;
            for (int i = 0; i < _orders.Count; i++)
            {
                Step(_orders[i], deltaTime);
            }

            // Despawning raises Despawned, which removes orders: done after the loop, never inside it.
            for (int i = 0; i < _killed.Count; i++)
            {
                _despawner.Despawn(_killed[i]);
            }

            _killed.Clear();
        }

        private void Step(AttackOrder order, float deltaTime)
        {
            Unit attacker = order.Attacker;
            IDamageable target = order.Target;
            // Killed earlier this frame; it is despawned after the loop and this order with it.
            if (!target.Health.IsAlive)
            {
                return;
            }

            bool inRange = IsInRange(attacker, target);
            if (order.State == CombatState.Attacking)
            {
                if (inRange)
                {
                    TryHit(order);
                }
                else
                {
                    // The target walked off: follow it.
                    order.State = CombatState.Chasing;
                    PlanChase(order);
                }

                return;
            }

            // Chasing: attack only once standing on a cell in range, never while still walking to it.
            bool isMoving = _movement.IsMoving(attacker);
            if (inRange && !isMoving)
            {
                order.State = CombatState.Attacking;
                TryHit(order);
                return;
            }

            order.RepathTimer -= deltaTime;
            // Planning again while nothing changed would only restart the walk from the current cell. A building
            // never moves, so its chasers plan again only once they stopped out of range.
            if (order.RepathTimer <= 0f && (target.Origin != order.PlannedTargetCell || !isMoving))
            {
                PlanChase(order);
            }
        }

        private void PlanChase(AttackOrder order)
        {
            order.RepathTimer = RepathInterval;
            order.PlannedTargetCell = order.Target.Origin;
            _movement.Approach(order.Attacker, order.Target);
        }

        private void TryHit(AttackOrder order)
        {
            Unit attacker = order.Attacker;
            attacker.FaceTowards(order.Target.WorldCenter - attacker.WorldCenter);
            if (_clock < attacker.NextAttackTime)
            {
                return;
            }

            attacker.NextAttackTime = _clock + attacker.Definition.AttackInterval;
            // Only the fatal hit returns true, so a target is queued for removal once.
            if (order.Target.Health.TakeDamage(attacker.Definition.Damage))
            {
                _killed.Add(order.Target);
            }
        }

        private static bool IsInRange(Unit attacker, IDamageable target)
        {
            return GridDistance.ToArea(attacker.Origin, target.Origin, target.Footprint) <= attacker.Definition.AttackRange;
        }

        private void HandleDespawned(IBoardOccupant gone)
        {
            // Backwards with swap-remove: removing an order moves the last one into its slot, which is already checked.
            for (int i = _orders.Count - 1; i >= 0; i--)
            {
                AttackOrder order = _orders[i];
                // Its attackers go idle; they finish the step to the stop cell they reserved and stand there.
                if (ReferenceEquals(order.Attacker, gone) || ReferenceEquals(order.Target, gone))
                {
                    RemoveAt(i);
                }
            }
        }

        private AttackOrder StartOrder(Unit unit)
        {
            AttackOrder order = _orderPool.Get();
            order.Attacker = unit;
            _orders.Add(order);
            return order;
        }

        private AttackOrder FindOrder(Unit unit)
        {
            // A loop over the few attackers; no dictionary to keep in sync with the list.
            for (int i = 0; i < _orders.Count; i++)
            {
                if (ReferenceEquals(_orders[i].Attacker, unit))
                {
                    return _orders[i];
                }
            }

            return null;
        }

        private void RemoveOrder(AttackOrder order)
        {
            RemoveAt(_orders.IndexOf(order));
        }

        private void RemoveAt(int index)
        {
            AttackOrder order = _orders[index];
            _orders[index] = _orders[_orders.Count - 1];
            _orders.RemoveAt(_orders.Count - 1);
            _orderPool.Release(order);
        }
    }
}
