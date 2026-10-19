using NUnit.Framework;
using StrategyDemo.Gameplay.Buildings;
using StrategyDemo.Gameplay.Combat;
using StrategyDemo.Gameplay.Effects;
using StrategyDemo.Gameplay.Units;
using StrategyDemo.Tests.EditMode.Support;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StrategyDemo.Tests.EditMode.Gameplay.Effects
{
    /// <summary>Health bar (shown when damaged or selected, fill anchored left) and hit flash (vertex colour, fades back).</summary>
    public class DamageFeedbackTests
    {
        private const float Tolerance = 1e-4f;

        private TestBoard _board;
        private Unit _unit;
        private Building _building;
        private HealthBar _unitBar;
        private SpriteRenderer _unitFill;
        private HitFlash _unitFlash;
        private SpriteRenderer _unitRenderer;
        private Texture2D _barTexture;
        private Sprite _barSprite;

        [SetUp]
        public void CreateObjects()
        {
            _board = new TestBoard(Vector3.zero, 8, 8);
            _unit = TestBoard.CreateUnitObject("Soldier");
            _unit.Init(_board.Unit("Soldier", health: 10), new Vector2Int(1, 1), 1, _board.View.CellToWorld(new Vector2Int(1, 1)));
            _building = TestBoard.CreateBuildingObject("Barracks");
            _building.Init(_board.Definition("Barracks", new Vector2Int(3, 2), health: 100), new Vector2Int(4, 4), 2, Vector3.zero);

            _unitBar = _unit.GetComponentInChildren<HealthBar>();
            _unitFill = _unitBar.transform.Find("Fill").GetComponent<SpriteRenderer>();
            _unitFlash = _unit.GetComponentInChildren<HitFlash>();
            _unitRenderer = _unitFlash.GetComponent<SpriteRenderer>();

            // A 4-unit-wide sprite makes the fill's offset easy to read.
            _barTexture = new Texture2D(4, 1);
            _barSprite = Sprite.Create(_barTexture, new Rect(0f, 0f, 4f, 1f), new Vector2(0.5f, 0.5f), 1f);
            _unitBar.transform.Find("Background").GetComponent<SpriteRenderer>().sprite = _barSprite;
            _unitFill.sprite = _barSprite;
        }

        [TearDown]
        public void DestroyObjects()
        {
            Object.DestroyImmediate(_unit.gameObject);
            Object.DestroyImmediate(_building.gameObject);
            Object.DestroyImmediate(_barSprite);
            Object.DestroyImmediate(_barTexture);
            _board.Dispose();
        }

        [Test]
        public void HealthBar_FullHealthNotSelected_IsHidden()
        {
            Assert.That(_unitBar.IsVisible, Is.False);
        }

        [Test]
        public void HealthBar_Damaged_ShowsShrunkFillAnchoredLeft()
        {
            _unit.Health.TakeDamage(5);

            Assert.That(_unitBar.IsVisible, Is.True);
            Assert.That(_unitFill.transform.localScale.x, Is.EqualTo(0.5f).Within(Tolerance));
            // Half of a 4-unit bar is lost: the fill's centre moves left by a quarter of the bar.
            Assert.That(_unitFill.transform.localPosition.x, Is.EqualTo(-1f).Within(Tolerance));
        }

        [Test]
        public void HealthBar_Pinned_ShowsAtFullHealthUntilUnpinned()
        {
            _unit.PinHealthBar(true);
            Assert.That(_unitBar.IsVisible, Is.True);

            _unit.PinHealthBar(false);
            Assert.That(_unitBar.IsVisible, Is.False);
        }

        [Test]
        public void HealthBar_Building_SpansFootprintColumnsAtTopOfFootprint()
        {
            var bar = _building.GetComponentInChildren<HealthBar>();
            _building.Health.TakeDamage(1);

            Assert.That(bar.IsVisible, Is.True);
            Assert.That(bar.transform.localScale.x, Is.EqualTo(3f));
            Assert.That(bar.transform.localPosition.y, Is.GreaterThan(0f).And.LessThan(1f), "Inside the top row of a 2-row footprint.");
        }

        [Test]
        public void HealthBar_AfterPoolReset_IsHiddenAndIgnoresOldHealth()
        {
            Health oldHealth = _unit.Health;
            _unit.PinHealthBar(true);

            _unit.ResetState();
            oldHealth.Reset(10);
            oldHealth.TakeDamage(3);

            Assert.That(_unitBar.IsVisible, Is.False);
        }

        [Test]
        public void HitFlash_OnDamage_TintsThenFadesBackToWhiteAndDisables()
        {
            _unit.Health.TakeDamage(1);

            Assert.That(_unitFlash.IsPlaying, Is.True);
            Assert.That(_unitFlash.enabled, Is.True);
            Assert.That(_unitRenderer.color, Is.Not.EqualTo(Color.white));

            _unitFlash.Advance(1f);

            Assert.That(_unitFlash.IsPlaying, Is.False);
            Assert.That(_unitFlash.enabled, Is.False, "Idle objects pay for no Update.");
            Assert.That(_unitRenderer.color, Is.EqualTo(Color.white));
        }

        [Test]
        public void HitFlash_PoolResetMidFlash_RestoresWhite()
        {
            _unit.Health.TakeDamage(1);

            _unit.ResetState();

            Assert.That(_unitRenderer.color, Is.EqualTo(Color.white));
            Assert.That(_unitFlash.IsPlaying, Is.False);
        }

        [Test]
        public void Init_MissingHealthBar_Throws()
        {
            var bare = new GameObject("Bare", typeof(SpriteRenderer)).AddComponent<Building>();
            try
            {
                Assert.Throws<System.InvalidOperationException>(() =>
                    bare.Init(_board.Definition("PowerPlant", Vector2Int.one), Vector2Int.zero, 3, Vector3.zero));
            }
            finally
            {
                Object.DestroyImmediate(bare.gameObject);
            }
        }
    }
}
