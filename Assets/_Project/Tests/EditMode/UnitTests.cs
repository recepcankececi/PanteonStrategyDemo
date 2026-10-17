using System;
using NUnit.Framework;
using StrategyDemo.Data;
using StrategyDemo.Gameplay;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StrategyDemo.Tests.EditMode
{
    /// <summary>A soldier's look on the board: standing sprite, walk cycle while moving, rotation to its heading.</summary>
    public class UnitTests
    {
        private const float AngleTolerance = 1e-3f;

        private TestBoard _board;
        private UnitDefinition _definition;
        private Unit _unit;
        private SpriteRenderer _renderer;
        private SpriteFlipbook _flipbook;

        [SetUp]
        public void SetUp()
        {
            _board = new TestBoard(Vector3.zero, 4, 4);
            _definition = _board.Unit("Rifleman", frameCount: 2);
            _unit = TestBoard.CreateUnitObject("Unit");
            _renderer = _unit.GetComponentInChildren<SpriteRenderer>();
            _flipbook = _unit.GetComponentInChildren<SpriteFlipbook>();
            _unit.Init(_definition, Vector2Int.one, 3, new Vector3(1.5f, 1.5f, 0f));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_unit.gameObject);
            _board.Dispose();
        }

        [Test]
        public void Init_ShowsStandingSpriteFacingUp()
        {
            Assert.That(_renderer.sprite, Is.SameAs(_definition.Sprite));
            Assert.That(_unit.IsWalking, Is.False);
            Assert.That(_flipbook.IsPlaying, Is.False);
            Assert.That(_renderer.transform.rotation, Is.EqualTo(Quaternion.identity));
        }

        [Test]
        public void Init_VisualNotAssigned_Throws()
        {
            var bare = new GameObject("Bare").AddComponent<Unit>();
            try
            {
                Assert.Throws<InvalidOperationException>(() => bare.Init(_definition, Vector2Int.zero, 1, Vector3.zero));
            }
            finally
            {
                Object.DestroyImmediate(bare.gameObject);
            }
        }

        [Test]
        public void Init_DefinitionWithoutWalkFrames_Throws()
        {
            UnitDefinition broken = _board.Unit("Broken", frameCount: 0);

            Assert.Throws<ArgumentException>(() => _unit.Init(broken, Vector2Int.zero, 1, Vector3.zero));
        }

        [Test]
        public void SetWalkingTrue_PlaysWalkCycle()
        {
            _unit.SetWalking(true);

            Assert.That(_unit.IsWalking, Is.True);
            Assert.That(_flipbook.IsPlaying, Is.True);
            Assert.That(_definition.WalkFrames, Has.Member(_renderer.sprite));
        }

        [Test]
        public void SetWalkingFalse_StopsAndShowsStandingSprite()
        {
            _unit.SetWalking(true);

            _unit.SetWalking(false);

            Assert.That(_unit.IsWalking, Is.False);
            Assert.That(_flipbook.IsPlaying, Is.False);
            Assert.That(_renderer.sprite, Is.SameAs(_definition.Sprite));
        }

        [Test]
        public void SetWalkingTrue_WhileWalking_DoesNotRestartCycle()
        {
            _unit.SetWalking(true);
            _flipbook.Advance(1f / _definition.WalkFramesPerSecond);
            int frame = _flipbook.CurrentFrame;

            _unit.SetWalking(true);

            Assert.That(_flipbook.CurrentFrame, Is.EqualTo(frame), "Movement calls this every step; the cycle must keep going.");
        }

        [Test]
        public void SetWalking_OnPooledSoldier_Throws()
        {
            _unit.ResetState();

            Assert.Throws<InvalidOperationException>(() => _unit.SetWalking(true));
        }

        [Test]
        public void FaceTowards_OnPooledSoldier_Throws()
        {
            _unit.ResetState();

            Assert.Throws<InvalidOperationException>(() => _unit.FaceTowards(Vector2.right));
        }

        [Test]
        public void Flipbook_UpdatesOnlyWhileWalking()
        {
            Assert.That(_flipbook.enabled, Is.False, "A standing soldier costs no Update call.");

            _unit.SetWalking(true);
            Assert.That(_flipbook.enabled, Is.True);

            _unit.SetWalking(false);
            Assert.That(_flipbook.enabled, Is.False);
        }

        // The art faces up (+Y): up needs no turn, right is a quarter turn clockwise (-90°), and so on.
        [TestCase(0f, 1f, 0f)]
        [TestCase(1f, 0f, 270f)]
        [TestCase(0f, -1f, 180f)]
        [TestCase(-1f, 0f, 90f)]
        [TestCase(3f, 3f, 315f)]
        public void FaceTowards_RotatesArtToHeading(float x, float y, float expectedZ)
        {
            _unit.FaceTowards(new Vector2(x, y));
            Transform visual = _renderer.transform;

            Assert.That(Mathf.DeltaAngle(visual.eulerAngles.z, expectedZ), Is.EqualTo(0f).Within(AngleTolerance));
            // The sprite's up axis now points along the heading; the root stays upright for attachments.
            Assert.That(Vector2.Angle(visual.up, new Vector2(x, y)), Is.EqualTo(0f).Within(AngleTolerance));
            Assert.That(_unit.transform.rotation, Is.EqualTo(Quaternion.identity));
        }

        [Test]
        public void FaceTowards_ZeroDirection_KeepsHeading()
        {
            _unit.FaceTowards(Vector2.left);
            Quaternion heading = _renderer.transform.rotation;

            _unit.FaceTowards(Vector2.zero);

            Assert.That(_renderer.transform.rotation, Is.EqualTo(heading));
        }
    }
}
