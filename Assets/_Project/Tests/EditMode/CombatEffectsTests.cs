using System;
using NUnit.Framework;
using StrategyDemo.Data;
using StrategyDemo.Gameplay;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StrategyDemo.Tests.EditMode
{
    /// <summary>
    /// Combat effects (M7): a tracer per ranged hit spanning shooter to target, a burst sized to what was destroyed,
    /// both fading out and going back to one pool; nothing allocated after warm-up.
    /// </summary>
    public class CombatEffectsTests
    {
        private const float TracerDuration = 0.1f;
        private const float BurstDuration = 0.4f;
        private const float Tolerance = 1e-4f;

        private CombatWorld _world;
        private GameObject _effectsObject;
        private GameObject _poolObject;
        private SpriteRenderer _prefab;
        private CombatEffectSettings _settings;
        private Texture2D _texture;
        private CombatEffects _effects;
        private UnitDefinition _scout;
        private UnitDefinition _assault;

        [SetUp]
        public void CreateEffects()
        {
            _world = new CombatWorld(10, 8);
            _scout = _world.Board.Unit("Scout", damage: 2, range: 3, firesTracer: true);
            _assault = _world.Board.Unit("Assault", damage: 10, range: 1);

            // A 32×4 px tracer at 64 px per unit is half a cell long, like the generated one.
            _texture = new Texture2D(32, 68);
            _settings = ScriptableObject.CreateInstance<CombatEffectSettings>();
            var serialized = new SerializedObject(_settings);
            serialized.FindProperty("_tracerSprite").objectReferenceValue =
                Sprite.Create(_texture, new Rect(0f, 0f, 32f, 4f), new Vector2(0.5f, 0.5f), 64f);
            serialized.FindProperty("_burstSprite").objectReferenceValue =
                Sprite.Create(_texture, new Rect(0f, 4f, 32f, 32f), new Vector2(0.5f, 0.5f), 32f);
            serialized.FindProperty("_tracerDuration").floatValue = TracerDuration;
            serialized.FindProperty("_burstDuration").floatValue = BurstDuration;
            serialized.FindProperty("_burstStartScale").floatValue = 0.5f;
            serialized.FindProperty("_burstEndScale").floatValue = 1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // A scene object stands in for the prefab: Instantiate copies either the same way.
            _prefab = new GameObject("Effect").AddComponent<SpriteRenderer>();
            _poolObject = new GameObject("Pool_Effect");
            _effectsObject = new GameObject("Effects");
            _effects = _effectsObject.AddComponent<CombatEffects>();
            Configure(_effects, _prefab, _poolObject.transform, _settings);
            _effects.Init(_world.Combat, _world.Despawner);
        }

        [TearDown]
        public void DestroyEffects()
        {
            Object.DestroyImmediate(_effectsObject);
            Object.DestroyImmediate(_poolObject);
            Object.DestroyImmediate(_prefab.gameObject);
            Object.DestroyImmediate(_settings.TracerSprite);
            Object.DestroyImmediate(_settings.BurstSprite);
            Object.DestroyImmediate(_settings);
            Object.DestroyImmediate(_texture);
            _world.Dispose();
        }

        [Test]
        public void Init_NullArgument_Throws()
        {
            CombatEffects fresh = CreateConfigured();
            try
            {
                Assert.Throws<ArgumentNullException>(() => fresh.Init(null, _world.Despawner));
                Assert.Throws<ArgumentNullException>(() => fresh.Init(_world.Combat, null));
            }
            finally
            {
                Object.DestroyImmediate(fresh.gameObject);
            }
        }

        [Test]
        public void Init_Twice_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => _effects.Init(_world.Combat, _world.Despawner));
        }

        [Test]
        public void Init_MissingSettings_Throws()
        {
            var bare = new GameObject("Bare").AddComponent<CombatEffects>();
            try
            {
                Assert.Throws<InvalidOperationException>(() => bare.Init(_world.Combat, _world.Despawner));
            }
            finally
            {
                Object.DestroyImmediate(bare.gameObject);
            }
        }

        [Test]
        public void Init_SettingsWithoutTracerSprite_Throws()
        {
            // Failing at start-up, not inside HitLanded where a throw would abort the combat tick.
            CombatEffectSettings broken = ScriptableObject.CreateInstance<CombatEffectSettings>();
            var bare = new GameObject("NoSprites").AddComponent<CombatEffects>();
            try
            {
                Configure(bare, _prefab, _poolObject.transform, broken);

                Assert.Throws<InvalidOperationException>(() => bare.Init(_world.Combat, _world.Despawner));
            }
            finally
            {
                Object.DestroyImmediate(bare.gameObject);
                Object.DestroyImmediate(broken);
            }
        }

        [Test]
        public void HandleHit_TracerSoldier_DrawsStreakFromShooterToTarget()
        {
            Unit scout = _world.Spawn(new Vector2Int(1, 1), _scout);
            Building tower = _world.Place(new Vector2Int(4, 5), Vector2Int.one, 50);

            _effects.HandleHit(scout, tower);

            Assert.That(_effects.LiveCount, Is.EqualTo(1));
            SpriteRenderer tracer = LiveEffect();
            Assert.That(tracer.sprite, Is.SameAs(_settings.TracerSprite));
            Vector3 from = scout.WorldCenter;
            Vector3 to = tower.WorldCenter;
            Assert.That(Vector3.Distance(tracer.transform.position, (from + to) * 0.5f), Is.LessThan(Tolerance));
            // Sprite bounds × scale = the drawn length: it spans the whole line.
            float drawnLength = tracer.sprite.bounds.size.x * tracer.transform.localScale.x;
            Assert.That(drawnLength, Is.EqualTo(Vector3.Distance(from, to)).Within(Tolerance));
            Vector3 along = tracer.transform.right;
            Assert.That(Vector3.Dot(along, (to - from).normalized), Is.EqualTo(1f).Within(Tolerance), "Turned towards the target.");
            Assert.That(tracer.color.a, Is.EqualTo(_settings.TracerColor.a).Within(Tolerance));
        }

        [Test]
        public void HandleHit_SoldierWithoutTracer_DrawsNothing()
        {
            Unit assault = _world.Spawn(new Vector2Int(1, 1), _assault);
            Building tower = _world.Place(new Vector2Int(2, 1), Vector2Int.one, 50);

            _effects.HandleHit(assault, tower);

            Assert.That(_effects.LiveCount, Is.Zero);
        }

        [Test]
        public void Advance_Tracer_FadesThenReturnsToPool()
        {
            Unit scout = _world.Spawn(new Vector2Int(1, 1), _scout);
            Building tower = _world.Place(new Vector2Int(4, 1), Vector2Int.one, 50);
            _effects.HandleHit(scout, tower);
            SpriteRenderer tracer = LiveEffect();

            _effects.Advance(TracerDuration * 0.5f);
            Assert.That(tracer.color.a, Is.EqualTo(_settings.TracerColor.a * 0.5f).Within(Tolerance), "Halfway: half faded.");

            _effects.Advance(TracerDuration);
            Assert.That(_effects.LiveCount, Is.Zero);
            Assert.That(tracer.gameObject.activeSelf, Is.False);
            Assert.That(tracer.transform.parent, Is.SameAs(_poolObject.transform));
            Assert.That(tracer.transform.localScale, Is.EqualTo(Vector3.one), "Reset for the next effect.");
        }

        [Test]
        public void Advance_NegativeTime_ChangesNothing()
        {
            Unit scout = _world.Spawn(new Vector2Int(1, 1), _scout);
            Building tower = _world.Place(new Vector2Int(4, 1), Vector2Int.one, 50);
            _effects.HandleHit(scout, tower);
            _effects.Advance(TracerDuration * 0.5f);
            float alpha = LiveEffect().color.a;

            _effects.Advance(-1f);

            Assert.That(LiveEffect().color.a, Is.EqualTo(alpha), "An editor frame with negative time must not rejuvenate effects.");
        }

        [Test]
        public void HandleDespawned_Building_BurstGrowsFromItsCentreSizedToFootprint()
        {
            Building barracks = _world.Place(new Vector2Int(3, 3), new Vector2Int(3, 2), 100);

            _effects.HandleDespawned(barracks);

            SpriteRenderer burst = LiveEffect();
            Assert.That(burst.sprite, Is.SameAs(_settings.BurstSprite));
            Assert.That(Vector3.Distance(burst.transform.position, barracks.WorldCenter), Is.LessThan(Tolerance));
            Assert.That(burst.transform.localScale.x, Is.EqualTo(3f * 0.5f).Within(Tolerance), "Start scale × larger side.");

            _effects.Advance(BurstDuration * 0.99f);
            Assert.That(burst.transform.localScale.x, Is.EqualTo(3f).Within(0.01f), "End scale × larger side.");
            Assert.That(burst.color.a, Is.LessThan(0.05f));
        }

        [Test]
        public void Kill_ThroughCombat_DrawsTracerThenBurst()
        {
            Unit scout = _world.Spawn(new Vector2Int(1, 1), _scout);
            Building shed = _world.Place(new Vector2Int(3, 1), Vector2Int.one, 2);

            _world.Combat.Attack(new IBoardOccupant[] { scout }, shed);
            _world.Step();

            Assert.That(_world.IsRegistered(shed), Is.False, "One 2-damage hit destroys it.");
            Assert.That(_effects.LiveCount, Is.EqualTo(2), "The hit's tracer and the destruction's burst.");
        }

        [Test]
        public void Spawn_AfterEffectsFinished_ReusesPooledObjects()
        {
            Unit scout = _world.Spawn(new Vector2Int(1, 1), _scout);
            Building tower = _world.Place(new Vector2Int(4, 1), Vector2Int.one, 50);
            _effects.HandleHit(scout, tower);
            _effects.HandleHit(scout, tower);
            _effects.Advance(TracerDuration * 2f);

            _effects.HandleHit(scout, tower);
            _effects.HandleDespawned(tower);

            Assert.That(_effects.CreatedCount, Is.EqualTo(2), "Two objects serve all four effects.");
        }

        [Test]
        public void SpawnAndAdvance_AfterWarmUp_AllocatesNothing()
        {
            Unit scout = _world.Spawn(new Vector2Int(1, 1), _scout);
            Building tower = _world.Place(new Vector2Int(4, 1), Vector2Int.one, 50);
            _effects.HandleHit(scout, tower);
            _effects.HandleDespawned(tower);
            _effects.Advance(BurstDuration * 2f);

            AllocationAssert.None(() =>
            {
                for (int i = 0; i < 20; i++)
                {
                    _effects.HandleHit(scout, tower);
                    _effects.HandleDespawned(tower);
                    _effects.Advance(BurstDuration * 2f);
                }
            });
        }

        private SpriteRenderer LiveEffect()
        {
            SpriteRenderer[] live = _effectsObject.GetComponentsInChildren<SpriteRenderer>(false);
            Assert.That(live.Length, Is.EqualTo(1), "Exactly one effect expected on screen.");
            return live[0];
        }

        private CombatEffects CreateConfigured()
        {
            var effects = new GameObject("Configured").AddComponent<CombatEffects>();
            Configure(effects, _prefab, _poolObject.transform, _settings);
            return effects;
        }

        private static void Configure(CombatEffects effects, SpriteRenderer prefab, Transform poolRoot, CombatEffectSettings settings)
        {
            var serialized = new SerializedObject(effects);
            serialized.FindProperty("_effectPrefab").objectReferenceValue = prefab;
            serialized.FindProperty("_poolRoot").objectReferenceValue = poolRoot;
            serialized.FindProperty("_settings").objectReferenceValue = settings;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
