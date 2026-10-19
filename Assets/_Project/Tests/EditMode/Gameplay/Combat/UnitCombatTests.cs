using System;
using System.Collections.Generic;
using NUnit.Framework;
using StrategyDemo.Core;
using StrategyDemo.Data;
using StrategyDemo.Gameplay.Board;
using StrategyDemo.Gameplay.Buildings;
using StrategyDemo.Gameplay.Combat;
using StrategyDemo.Gameplay.Units;
using StrategyDemo.Tests.EditMode.Support;
using UnityEngine;

namespace StrategyDemo.Tests.EditMode.Gameplay.Combat
{
    /// <summary>
    /// Attacks (R9, R10): chase into range, hit every interval, ranged stop distance, group spread,
    /// moving targets, deaths mid-chase and mid-attack, no free cell in range, mutual kills.
    /// </summary>
    public class UnitCombatTests
    {
        private const int Width = 10;
        private const int Height = 8;
        // Ten seconds of frames: far longer than any chase on this board.
        private const int MaxChaseFrames = 500;

        private CombatWorld _world;
        private UnitDefinition _assault;
        private UnitDefinition _scout;
        private UnitDefinition _weakling;

        [SetUp]
        public void CreateWorld()
        {
            _world = new CombatWorld(Width, Height);
            // R7 shapes: melee 10 damage, ranged 2 damage at range 3; a 1-damage soldier for long fights.
            _assault = _world.Board.Unit("Assault", damage: 10, range: 1, interval: 1f);
            _scout = _world.Board.Unit("Scout", damage: 2, range: 3, interval: 1f);
            _weakling = _world.Board.Unit("Weakling", damage: 1, range: 1, interval: 1f);
        }

        [TearDown]
        public void DestroyWorld()
        {
            _world.Dispose();
        }

        private UnitCombat Combat => _world.Combat;

        [Test]
        public void Constructor_NullArgument_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new UnitCombat(null, new FreeForAllAttackRule(), _world.Despawner));
            Assert.Throws<ArgumentNullException>(() => new UnitCombat(_world.Movement, null, _world.Despawner));
            Assert.Throws<ArgumentNullException>(() => new UnitCombat(_world.Movement, new FreeForAllAttackRule(), null));
        }

        [Test]
        public void Attack_NullArgument_Throws()
        {
            Building building = _world.Place(new Vector2Int(5, 5), Vector2Int.one, 10);

            Assert.Throws<ArgumentNullException>(() => Combat.Attack(null, building));
            Assert.Throws<ArgumentNullException>(() => Combat.Attack(new IBoardOccupant[0], null));
            Assert.Throws<ArgumentNullException>(() => Combat.Stop(null));
        }

        [Test]
        public void Tick_NegativeTime_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Combat.Tick(-1f));
        }

        [Test]
        public void FreeForAllRule_AllowsAnyTargetButSelf()
        {
            Unit a = _world.Spawn(new Vector2Int(0, 0), _assault);
            Unit b = _world.Spawn(new Vector2Int(1, 0), _assault);
            var rule = new FreeForAllAttackRule();

            Assert.That(rule.CanAttack(a, b), Is.True, "No enemy side: own soldiers are fair targets.");
            Assert.That(rule.CanAttack(a, a), Is.False);
        }

        [Test]
        public void Attack_MeleeOnBuilding_ChasesToAdjacentCellThenHitsOnArrival()
        {
            Unit soldier = _world.Spawn(new Vector2Int(0, 0), _assault);
            Building barracks = _world.Place(new Vector2Int(6, 3), new Vector2Int(3, 3), 100);

            Combat.Attack(new IBoardOccupant[] { soldier }, barracks);

            Assert.That(Combat.StateOf(soldier), Is.EqualTo(CombatState.Chasing));
            Assert.That(Combat.TargetOf(soldier), Is.SameAs(barracks));
            _world.RunUntil(() => Combat.StateOf(soldier) == CombatState.Attacking, "the soldier attacks");

            Assert.That(CombatWorld.DistanceTo(soldier, barracks), Is.EqualTo(1));
            Assert.That(_world.Movement.IsMoving(soldier), Is.False, "Attacks only while standing.");
            Assert.That(barracks.Health.Current, Is.EqualTo(90), "The first hit lands on arrival.");
        }

        [Test]
        public void Hit_RaisesHitLandedBeforeDamage_EvenOnTheFatalHit()
        {
            Building tower = _world.Place(new Vector2Int(5, 5), Vector2Int.one, 10);
            Unit assault = _world.Spawn(new Vector2Int(4, 5), _assault);
            var hits = new List<(Unit attacker, IDamageable target, int healthBefore)>();
            Combat.HitLanded += (attacker, target) => hits.Add((attacker, target, target.Health.Current));

            Combat.Attack(new IBoardOccupant[] { assault }, tower);
            _world.Step();

            Assert.That(hits.Count, Is.EqualTo(1));
            Assert.That(hits[0].attacker, Is.SameAs(assault));
            Assert.That(hits[0].target, Is.SameAs(tower));
            Assert.That(hits[0].healthBefore, Is.EqualTo(10), "Listeners see the target before the hit, still alive.");
            Assert.That(_world.IsRegistered(tower), Is.False, "The 10-damage hit was fatal.");
        }

        [Test]
        public void Attacking_HitsOncePerAttackInterval()
        {
            Unit soldier = _world.Spawn(new Vector2Int(5, 2), _assault);
            Building barracks = _world.Place(new Vector2Int(5, 3), new Vector2Int(3, 3), 100);
            Combat.Attack(new IBoardOccupant[] { soldier }, barracks);
            _world.RunUntil(() => barracks.Health.Current == 90, "the first hit");

            _world.Run(2.5f);

            Assert.That(barracks.Health.Current, Is.EqualTo(70), "Hits at 0 s, 1 s and 2 s.");
        }

        [Test]
        public void Attack_Scout_StopsAtItsRangeNotAdjacent()
        {
            Unit scout = _world.Spawn(new Vector2Int(0, 4), _scout);
            Building tower = _world.Place(new Vector2Int(8, 4), Vector2Int.one, 50);

            Combat.Attack(new IBoardOccupant[] { scout }, tower);
            _world.RunUntil(() => Combat.StateOf(scout) == CombatState.Attacking, "the scout attacks");

            Assert.That(CombatWorld.DistanceTo(scout, tower), Is.EqualTo(3));
            Assert.That(scout.Origin, Is.EqualTo(new Vector2Int(5, 4)), "First cell of the straight path within range 3.");
            Assert.That(tower.Health.Current, Is.EqualTo(48));
        }

        [Test]
        public void Attack_AlreadyInRange_AttacksWithoutWalking()
        {
            Unit scout = _world.Spawn(new Vector2Int(2, 2), _scout);
            Building tower = _world.Place(new Vector2Int(4, 4), Vector2Int.one, 50);

            Combat.Attack(new IBoardOccupant[] { scout }, tower);
            _world.Step();

            Assert.That(Combat.StateOf(scout), Is.EqualTo(CombatState.Attacking));
            Assert.That(scout.Origin, Is.EqualTo(new Vector2Int(2, 2)));
        }

        [Test]
        public void Attack_Group_EachStandsOnItsOwnCellWithinRange()
        {
            Building barracks = _world.Place(new Vector2Int(4, 3), new Vector2Int(2, 2), 1000);
            var group = new List<IBoardOccupant>();
            for (int i = 0; i < 6; i++)
            {
                group.Add(_world.Spawn(new Vector2Int(i, 0), _weakling));
            }

            Combat.Attack(group, barracks);
            _world.RunUntil(() => group.TrueForAll(u => Combat.StateOf((Unit)u) == CombatState.Attacking), "all six attack");

            var cells = new HashSet<Vector2Int>();
            foreach (IBoardOccupant occupant in group)
            {
                var unit = (Unit)occupant;
                Assert.That(CombatWorld.DistanceTo(unit, barracks), Is.EqualTo(1), unit.name);
                Assert.That(cells.Add(unit.Origin), Is.True, $"{unit.name} shares cell {unit.Origin}.");
            }
        }

        [Test]
        public void Attack_SelectedSoldierClickedByItsGroup_NeverTargetsItself()
        {
            Unit a = _world.Spawn(new Vector2Int(0, 0), _weakling);
            Unit b = _world.Spawn(new Vector2Int(3, 0), _weakling);

            Combat.Attack(new IBoardOccupant[] { a, b }, b);

            Assert.That(Combat.StateOf(a), Is.EqualTo(CombatState.Chasing));
            Assert.That(Combat.StateOf(b), Is.EqualTo(CombatState.Idle));
            Assert.That(_world.Movement.IsMoving(b), Is.False);
        }

        [Test]
        public void Attack_OnlyTargetItself_DoesNothing()
        {
            Unit a = _world.Spawn(new Vector2Int(0, 0), _weakling);

            Combat.Attack(new IBoardOccupant[] { a }, a);

            Assert.That(Combat.AttackerCount, Is.Zero);
            Assert.That(_world.Movement.MovingCount, Is.Zero);
        }

        [Test]
        public void Attack_BuildingsInGroup_AreSkipped()
        {
            Building attacker = _world.Place(new Vector2Int(0, 0), Vector2Int.one, 10);
            Building target = _world.Place(new Vector2Int(5, 5), Vector2Int.one, 10);

            Combat.Attack(new IBoardOccupant[] { attacker }, target);

            Assert.That(Combat.AttackerCount, Is.Zero);
        }

        [Test]
        public void Stop_DropsAttackOrder()
        {
            Unit soldier = _world.Spawn(new Vector2Int(0, 0), _assault);
            Building tower = _world.Place(new Vector2Int(8, 6), Vector2Int.one, 50);
            Combat.Attack(new IBoardOccupant[] { soldier }, tower);

            Combat.Stop(new IBoardOccupant[] { soldier });

            Assert.That(Combat.StateOf(soldier), Is.EqualTo(CombatState.Idle));
            Assert.That(Combat.TargetOf(soldier), Is.Null);
        }

        [Test]
        public void Kill_Building_FreesItsCellsAndAttackersGoIdle()
        {
            Unit soldier = _world.Spawn(new Vector2Int(5, 2), _assault);
            Building barracks = _world.Place(new Vector2Int(5, 3), new Vector2Int(3, 3), 20);
            Combat.Attack(new IBoardOccupant[] { soldier }, barracks);

            _world.RunUntil(() => !barracks.Health.IsAlive, "the barracks falls", 5f);
            _world.Step();

            Assert.That(_world.Grid.IsAreaFree(new Vector2Int(5, 3), new Vector2Int(3, 3)), Is.True, "R10: its cells are free.");
            Assert.That(_world.IsRegistered(barracks), Is.False);
            Assert.That(_world.BuildingFactory.ReleasedCount, Is.EqualTo(1));
            Assert.That(Combat.StateOf(soldier), Is.EqualTo(CombatState.Idle));
            Assert.That(Combat.AttackerCount, Is.Zero);
        }

        [Test]
        public void Kill_Soldier_RemovesItFromBoardAndReturnsItToPool()
        {
            Unit killer = _world.Spawn(new Vector2Int(2, 2), _assault);
            Unit victim = _world.Spawn(new Vector2Int(3, 2), _assault);
            int victimId = victim.OccupantId;
            Combat.Attack(new IBoardOccupant[] { killer }, victim);

            _world.Step();
            _world.Step();

            Assert.That(victim.Health.IsAlive, Is.False);
            Assert.That(_world.IsRegistered(victim), Is.False);
            Assert.That(_world.Grid.GetOccupant(new Vector2Int(3, 2)), Is.EqualTo(GridMap.EmptyId));
            Assert.That(_world.Reservations.TryGetCell(victimId, out _), Is.False, "Its stop cell is given back.");
            Assert.That(_world.UnitFactory.ReleasedCount, Is.EqualTo(1));
            Assert.That(Combat.StateOf(killer), Is.EqualTo(CombatState.Idle));
        }

        [Test]
        public void TargetDiesMidChase_ChaserGoesIdleAndStopsOnItsReservedCell()
        {
            Building tower = _world.Place(new Vector2Int(8, 6), Vector2Int.one, 10);
            Unit killer = _world.Spawn(new Vector2Int(7, 6), _assault);
            Unit chaser = _world.Spawn(new Vector2Int(0, 0), _weakling);
            Combat.Attack(new IBoardOccupant[] { chaser }, tower);
            Combat.Attack(new IBoardOccupant[] { killer }, tower);

            _world.RunUntil(() => !_world.IsRegistered(tower), "the tower falls", 2f);

            Assert.That(Combat.StateOf(chaser), Is.EqualTo(CombatState.Idle));
            Assert.That(_world.Movement.IsMoving(chaser), Is.True, "It finishes the walk to its reserved cell.");
            Assert.That(_world.Reservations.TryGetCell(chaser.OccupantId, out Vector2Int stopCell), Is.True);
            _world.RunUntil(() => !_world.Movement.IsMoving(chaser), "the chaser stops");
            Assert.That(chaser.Origin, Is.EqualTo(stopCell));
        }

        [Test]
        public void AttackerDiesMidAttack_TargetTakesNoMoreDamage()
        {
            Building barracks = _world.Place(new Vector2Int(4, 4), new Vector2Int(2, 2), 100);
            Unit attacker = _world.Spawn(new Vector2Int(4, 3), _weakling);
            Unit killer = _world.Spawn(new Vector2Int(5, 2), _assault);
            Combat.Attack(new IBoardOccupant[] { attacker }, barracks);
            _world.RunUntil(() => Combat.StateOf(attacker) == CombatState.Attacking, "the attacker attacks");

            Combat.Attack(new IBoardOccupant[] { killer }, attacker);
            _world.RunUntil(() => !_world.IsRegistered(attacker), "the attacker dies");
            int healthAtDeath = barracks.Health.Current;
            _world.Run(3f);

            Assert.That(barracks.Health.Current, Is.EqualTo(healthAtDeath));
            Assert.That(Combat.AttackerCount, Is.Zero, "The dead attacker's order and its killer's order are both gone.");
        }

        [Test]
        public void SelectedTargetDies_LeavesTheSelection()
        {
            Unit killer = _world.Spawn(new Vector2Int(2, 2), _assault);
            Building tower = _world.Place(new Vector2Int(3, 2), Vector2Int.one, 10);
            _world.Selection.SelectAt(new Vector2Int(3, 2));
            int changes = 0;
            _world.Selection.SelectionChanged += () => changes++;
            Combat.Attack(new IBoardOccupant[] { killer }, tower);

            _world.Step();
            _world.Step();

            Assert.That(_world.Selection.Selected, Is.Null);
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void SelectedSoldierDiesInGroup_RestStaysSelected()
        {
            Unit a = _world.Spawn(new Vector2Int(0, 0), _assault);
            Unit b = _world.Spawn(new Vector2Int(1, 0), _assault);
            Unit killer = _world.Spawn(new Vector2Int(2, 1), _assault);
            _world.Selection.SelectSoldiersIn(new RectInt(0, 0, 2, 1));
            Combat.Attack(new IBoardOccupant[] { killer }, b);

            _world.RunUntil(() => !_world.IsRegistered(b), "b dies");

            Assert.That(_world.Selection.All, Is.EqualTo(new IBoardOccupant[] { a }));
        }

        [Test]
        public void NoFreeCellInRange_WaitsNearbyThenAttacksWhenACellFrees()
        {
            // A corner tower whose three neighbours are taken by standing soldiers.
            Building tower = _world.Place(new Vector2Int(0, 0), Vector2Int.one, 100);
            _world.Spawn(new Vector2Int(1, 0), _weakling);
            _world.Spawn(new Vector2Int(0, 1), _weakling);
            Unit blocker = _world.Spawn(new Vector2Int(1, 1), _weakling);
            Unit attacker = _world.Spawn(new Vector2Int(6, 5), _weakling);

            Combat.Attack(new IBoardOccupant[] { attacker }, tower);
            _world.Run(4f);

            Assert.That(Combat.StateOf(attacker), Is.EqualTo(CombatState.Chasing), "Waits: no cell in range is free.");
            Assert.That(_world.Movement.IsMoving(attacker), Is.False, "It stands on the nearest free cell.");
            Assert.That(CombatWorld.DistanceTo(attacker, tower), Is.EqualTo(2));
            Assert.That(tower.Health.Current, Is.EqualTo(100));

            _world.Despawner.Despawn(blocker);
            _world.RunUntil(() => Combat.StateOf(attacker) == CombatState.Attacking, "a cell frees up", 3f);

            Assert.That(attacker.Origin, Is.EqualTo(new Vector2Int(1, 1)));
        }

        [Test]
        public void MovingTarget_IsFollowedAndAttackedWhereItStops()
        {
            Unit target = _world.Spawn(new Vector2Int(5, 0), _weakling);
            Unit chaser = _world.Spawn(new Vector2Int(0, 0), _weakling);
            _world.Movement.MoveGroup(new IBoardOccupant[] { target }, new Vector2Int(9, 7));

            Combat.Attack(new IBoardOccupant[] { chaser }, target);
            _world.RunUntil(() => target.Health.Current < target.Health.Max, "the chaser lands a hit");

            Assert.That(target.Origin, Is.EqualTo(new Vector2Int(9, 7)));
            Assert.That(CombatWorld.DistanceTo(chaser, target), Is.EqualTo(1));
        }

        [Test]
        public void Attacking_TargetWalksOutOfRange_ChasesAgain()
        {
            Unit target = _world.Spawn(new Vector2Int(3, 3), _weakling);
            Unit attacker = _world.Spawn(new Vector2Int(2, 3), _weakling);
            Combat.Attack(new IBoardOccupant[] { attacker }, target);
            _world.RunUntil(() => Combat.StateOf(attacker) == CombatState.Attacking, "the attacker attacks");

            _world.Movement.MoveGroup(new IBoardOccupant[] { target }, new Vector2Int(9, 3));
            _world.RunUntil(() => Combat.StateOf(attacker) == CombatState.Chasing, "the attacker chases");
            _world.RunUntil(() => Combat.StateOf(attacker) == CombatState.Attacking && !_world.Movement.IsMoving(target), "it catches up");

            Assert.That(CombatWorld.DistanceTo(attacker, target), Is.LessThanOrEqualTo(1));
        }

        [Test]
        public void MutualLethalHitsInOneFrame_BothDie()
        {
            Unit a = _world.Spawn(new Vector2Int(2, 2), _assault);
            Unit b = _world.Spawn(new Vector2Int(3, 2), _assault);
            Combat.Attack(new IBoardOccupant[] { a }, b);
            Combat.Attack(new IBoardOccupant[] { b }, a);

            _world.Step();

            // Deaths are resolved after every soldier acted: neither hit is lost to update order.
            Assert.That(_world.IsRegistered(a), Is.False);
            Assert.That(_world.IsRegistered(b), Is.False);
            Assert.That(Combat.AttackerCount, Is.Zero);
        }

        [Test]
        public void SwitchingTarget_KeepsCooldown()
        {
            Building first = _world.Place(new Vector2Int(3, 3), Vector2Int.one, 100);
            Building second = _world.Place(new Vector2Int(3, 1), Vector2Int.one, 100);
            Unit soldier = _world.Spawn(new Vector2Int(3, 2), _weakling);
            Combat.Attack(new IBoardOccupant[] { soldier }, first);
            _world.Step();
            Assert.That(first.Health.Current, Is.EqualTo(99));

            Combat.Attack(new IBoardOccupant[] { soldier }, second);
            _world.Run(0.5f);

            Assert.That(second.Health.Current, Is.EqualTo(100), "Re-targeting cannot skip the attack interval.");
            _world.Run(0.6f);
            Assert.That(second.Health.Current, Is.EqualTo(99));
        }

        [Test]
        public void MoveThenAttackAgain_StillWaitsForTheAttackInterval()
        {
            Building tower = _world.Place(new Vector2Int(3, 3), Vector2Int.one, 100);
            Unit soldier = _world.Spawn(new Vector2Int(3, 2), _weakling);
            Combat.Attack(new IBoardOccupant[] { soldier }, tower);
            _world.Step();
            Assert.That(tower.Health.Current, Is.EqualTo(99));

            // Right click on the ground (a move to its own cell), then on the tower again.
            Combat.Stop(new IBoardOccupant[] { soldier });
            Combat.Attack(new IBoardOccupant[] { soldier }, tower);
            _world.Run(0.5f);

            Assert.That(tower.Health.Current, Is.EqualTo(99), "Dropping the order does not reset the interval.");
            _world.Run(0.6f);
            Assert.That(tower.Health.Current, Is.EqualTo(98));
        }

        [Test]
        public void ChasingStandingSoldier_DoesNotPlanAgainWhileWalking()
        {
            // The target stands still, so planning again could only restart the walk; the stop cell must not change.
            Unit target = _world.Spawn(new Vector2Int(8, 6), _weakling);
            Unit chaser = _world.Spawn(new Vector2Int(0, 0), _weakling);
            Combat.Attack(new IBoardOccupant[] { chaser }, target);
            Assert.That(_world.Reservations.TryGetCell(chaser.OccupantId, out Vector2Int stopCell), Is.True);
            float previousDistance = float.MaxValue;
            Vector3 goal = _world.Board.View.CellToWorld(stopCell);

            _world.RunUntil(() =>
            {
                float distance = Vector3.Distance(chaser.transform.position, goal);
                Assert.That(distance, Is.LessThanOrEqualTo(previousDistance + 1e-4f), "The chaser never steps back.");
                previousDistance = distance;
                return Combat.StateOf(chaser) == CombatState.Attacking;
            }, "the chaser attacks");

            Assert.That(chaser.Origin, Is.EqualTo(stopCell));
        }

        [Test]
        public void Tick_ChaseOfWalkingSoldierEndingInKill_AllocatesNothing()
        {
            // Warm-up: one full chase and kill first, so pools and lists have grown (their first growth allocates once).
            Unit warmTarget = _world.Spawn(new Vector2Int(9, 0), _world.Board.Unit("WarmUp", health: 3));
            Unit warmChaser = _world.Spawn(new Vector2Int(9, 2), _assault);
            Combat.Attack(new IBoardOccupant[] { warmChaser }, warmTarget);
            _world.RunUntil(() => Combat.AttackerCount == 0 && _world.Movement.MovingCount == 0, "the warm-up kill");

            Unit target = _world.Spawn(new Vector2Int(5, 0), _world.Board.Unit("Fragile", health: 3));
            Unit chaser = _world.Spawn(new Vector2Int(0, 0), _assault);
            _world.Selection.SelectAt(new Vector2Int(5, 0));
            _world.Movement.MoveGroup(new IBoardOccupant[] { target }, new Vector2Int(9, 7));
            Combat.Attack(new IBoardOccupant[] { chaser }, target);
            _world.Step();
            // Plain Step calls, not RunUntil: that test helper allocates by itself, which would hide what the game does.
            AllocationAssert.None(() =>
            {
                for (int frame = 0; frame < MaxChaseFrames && Combat.AttackerCount > 0; frame++)
                {
                    _world.Step();
                }
            });
            Assert.That(_world.IsRegistered(target), Is.False);
            Assert.That(_world.Selection.Selected, Is.Null);
        }

        [Test]
        public void BarracksDestroyedWithSoldiersAround_SoldiersStayAndItsCellsTakeABuildingOrAPath()
        {
            Building barracks = _world.Place(new Vector2Int(3, 3), new Vector2Int(3, 3), 10);
            Unit west = _world.Spawn(new Vector2Int(2, 4), _assault);
            Unit east = _world.Spawn(new Vector2Int(6, 4), _weakling);
            Unit south = _world.Spawn(new Vector2Int(4, 2), _weakling);
            Combat.Attack(new IBoardOccupant[] { west }, barracks);

            _world.RunUntil(() => !_world.IsRegistered(barracks), "the barracks falls", 2f);

            foreach (Unit unit in new[] { west, east, south })
            {
                Assert.That(_world.IsRegistered(unit), Is.True, unit.name);
                Assert.That(_world.Grid.GetOccupant(unit.Origin), Is.EqualTo(unit.OccupantId), unit.name);
            }

            Assert.That(_world.Grid.IsAreaFree(new Vector2Int(3, 3), new Vector2Int(3, 3)), Is.True);
            // The straight line through the old footprint is now the shortest way.
            _world.Movement.MoveGroup(new IBoardOccupant[] { west }, new Vector2Int(5, 4));
            _world.Run(1.1f);
            Assert.That(west.Origin, Is.EqualTo(new Vector2Int(5, 4)), "3 cells at 3 cells/s: it walked straight through.");
        }

        [Test]
        public void Tick_AfterWarmUp_AllocatesNothing()
        {
            Building barracks = _world.Place(new Vector2Int(6, 3), new Vector2Int(2, 2), 100000);
            Unit a = _world.Spawn(new Vector2Int(0, 0), _weakling);
            Unit b = _world.Spawn(new Vector2Int(0, 7), _weakling);
            // Warm-up: one finished walk, so the move-order pool has grown (its first growth allocates once).
            _world.Movement.MoveGroup(new IBoardOccupant[] { a, b }, new Vector2Int(0, 1));
            _world.RunUntil(() => _world.Movement.MovingCount == 0, "the warm-up walk");
            Combat.Attack(new IBoardOccupant[] { a, b }, barracks);
            _world.Run(0.1f);

            AllocationAssert.None(() => _world.Run(4f));
            Assert.That(Combat.StateOf(a), Is.EqualTo(CombatState.Attacking), "The run covered chasing and attacking.");
        }

        [Test]
        public void Attack_FarBuilding_PlanDoesNotExamineTheWholeBoard()
        {
            Unit soldier = _world.Spawn(new Vector2Int(0, 0), _assault);
            Building tower = _world.Place(new Vector2Int(8, 6), Vector2Int.one, 50);

            Combat.Attack(new IBoardOccupant[] { soldier }, tower);

            // The search ends at the first free cell in range instead of flooding towards the tower's blocked cell.
            Assert.That(_world.Movement.LastSearchExpandedCount, Is.LessThan(Width * Height / 2));
            Assert.That(_world.Reservations.TryGetCell(soldier.OccupantId, out Vector2Int stopCell), Is.True);
            Assert.That(GridDistance.ToArea(stopCell, tower.Origin, tower.Footprint), Is.EqualTo(1));
        }

        [Test]
        public void Attack_WalkingTargetWithEveryNeighbourReserved_NeverPicksTheTargetsOwnCell()
        {
            // A walking soldier reserves its destination, not the cell it stands in: that cell looks free.
            Unit target = _world.Spawn(new Vector2Int(5, 4), _weakling);
            _world.Movement.MoveGroup(new IBoardOccupant[] { target }, new Vector2Int(9, 7));
            for (int y = 3; y <= 5; y++)
            {
                for (int x = 4; x <= 6; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (cell != target.Origin)
                    {
                        _world.Reservations.ReserveStopCell(cell, _world.Registry.NextId());
                    }
                }
            }

            Unit attacker = _world.Spawn(new Vector2Int(0, 0), _assault);
            Combat.Attack(new IBoardOccupant[] { attacker }, target);

            Assert.That(_world.Reservations.TryGetCell(attacker.OccupantId, out Vector2Int stopCell), Is.True);
            Assert.That(stopCell, Is.Not.EqualTo(new Vector2Int(5, 4)), "Never stops inside its target.");
        }

        [Test]
        public void InRangeButNoCellToStopOn_StillHits()
        {
            // A 3×1 strip: a Barracks at x=0 owns the spawn cell x=1, the attacker stands on that spawn cell (it was just
            // produced) and its target stands on x=2. Every reachable cell is reserved, so the attacker's move order can
            // never complete; it is in range all the same and must fire.
            using (var strip = new CombatWorld(3, 1))
            {
                Unit attacker = strip.Spawn(new Vector2Int(1, 0), _assault);
                Unit target = strip.Spawn(new Vector2Int(2, 0), _weakling);
                strip.Reservations.Release(attacker.OccupantId);
                strip.Place(strip.Board.Definition("Barracks", Vector2Int.one), Vector2Int.zero, new Vector2Int(1, 0));

                strip.Combat.Attack(new IBoardOccupant[] { attacker }, target);
                strip.Step();

                // Checked after one frame: once the target is gone its cell frees up and the order can complete.
                Assert.That(strip.Movement.IsMoving(attacker), Is.True, "Its move order stays open: no cell to stop on.");
                Assert.That(strip.IsRegistered(target), Is.False, "10 damage on 10 HP: the hit landed.");
            }
        }

        [Test]
        public void FiringWhileWaitingForStopCell_CellFreesUp_NoHitWhileWalkingToIt()
        {
            // A 4×1 strip: Barracks x=0 (spawn cell x=1), the attacker on the spawn cell, a blocker on x=2, the target
            // building on x=3. The attacker fires from where it stands; once the blocker is gone it walks to x=2 and must
            // not shoot on the way.
            using (var strip = new CombatWorld(4, 1))
            {
                UnitDefinition rapid = strip.Board.Unit("Rapid", damage: 1, range: 3, interval: 0.05f);
                Unit attacker = strip.Spawn(new Vector2Int(1, 0), rapid);
                Unit blocker = strip.Spawn(new Vector2Int(2, 0), _weakling);
                strip.Reservations.Release(attacker.OccupantId);
                strip.Place(strip.Board.Definition("Barracks", Vector2Int.one), Vector2Int.zero, new Vector2Int(1, 0));
                Building target = strip.Place(new Vector2Int(3, 0), Vector2Int.one, 1000);
                strip.Combat.Attack(new IBoardOccupant[] { attacker }, target);
                strip.RunUntil(() => target.Health.Current < 1000, "the waiting attacker fires");

                strip.Despawner.Despawn(blocker);
                bool walked = false;
                for (int frame = 0; frame < MaxChaseFrames && !(walked && !strip.Movement.IsMoving(attacker)); frame++)
                {
                    int before = target.Health.Current;
                    strip.Step();
                    if (strip.Movement.IsWalking(attacker))
                    {
                        walked = true;
                        Assert.That(target.Health.Current, Is.EqualTo(before), "A hit landed while walking.");
                    }
                }

                Assert.That(walked, Is.True, "The attacker walked to the freed cell.");
                Assert.That(attacker.Origin, Is.EqualTo(new Vector2Int(2, 0)));
                int arrived = target.Health.Current;
                strip.Run(0.2f);
                Assert.That(target.Health.Current, Is.LessThan(arrived), "It fires again once it stands.");
            }
        }

        [Test]
        public void Tick_ThrowsAfterAKill_KilledTargetIsStillDespawned()
        {
            Building first = _world.Place(new Vector2Int(2, 2), Vector2Int.one, 10);
            Building second = _world.Place(new Vector2Int(6, 2), Vector2Int.one, 10);
            Unit killer = _world.Spawn(new Vector2Int(2, 1), _assault);
            Unit other = _world.Spawn(new Vector2Int(6, 1), _assault);
            Combat.Attack(new IBoardOccupant[] { killer }, first);
            Combat.Attack(new IBoardOccupant[] { other }, second);
            // The first hit (fatal) passes; the second one breaks an invariant halfway through the order loop.
            int hits = 0;
            Combat.HitLanded += (attacker, target) =>
            {
                if (++hits == 2)
                {
                    throw new InvalidOperationException("Broken invariant.");
                }
            };

            Assert.Throws<InvalidOperationException>(() => _world.Step());

            Assert.That(_world.IsRegistered(first), Is.False, "A dead building must not stay on the board.");
            Assert.That(_world.Grid.GetOccupant(new Vector2Int(2, 2)), Is.EqualTo(GridMap.EmptyId));
            Assert.That(Combat.StateOf(killer), Is.EqualTo(CombatState.Idle));
            Assert.That(_world.IsRegistered(second), Is.True, "The hit that threw dealt no damage.");
        }
    }
}
