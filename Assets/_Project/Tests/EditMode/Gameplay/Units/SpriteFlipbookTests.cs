using System;
using NUnit.Framework;
using StrategyDemo.Gameplay.Units;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StrategyDemo.Tests.EditMode.Gameplay.Units
{
    /// <summary>The lightweight walk-cycle player: frame timing, looping and argument checks.</summary>
    public class SpriteFlipbookTests
    {
        private const float FramesPerSecond = 10f;
        private const float FrameTime = 1f / FramesPerSecond;

        private GameObject _gameObject;
        private SpriteRenderer _renderer;
        private SpriteFlipbook _flipbook;
        private Texture2D _texture;
        private Sprite[] _frames;

        [SetUp]
        public void CreateFlipbook()
        {
            _gameObject = new GameObject(nameof(SpriteFlipbookTests));
            _renderer = _gameObject.AddComponent<SpriteRenderer>();
            _flipbook = _gameObject.AddComponent<SpriteFlipbook>();
            _texture = new Texture2D(3, 1);
            _frames = new Sprite[3];
            for (int i = 0; i < _frames.Length; i++)
            {
                _frames[i] = Sprite.Create(_texture, new Rect(i, 0f, 1f, 1f), Vector2.zero, 1f);
            }
        }

        [TearDown]
        public void DestroyFlipbook()
        {
            foreach (Sprite frame in _frames)
            {
                Object.DestroyImmediate(frame);
            }

            Object.DestroyImmediate(_texture);
            Object.DestroyImmediate(_gameObject);
        }

        [Test]
        public void Play_ShowsStartFrameImmediately()
        {
            _flipbook.Play(_frames, FramesPerSecond, 1);

            Assert.That(_flipbook.IsPlaying, Is.True);
            Assert.That(_flipbook.CurrentFrame, Is.EqualTo(1));
            Assert.That(_renderer.sprite, Is.SameAs(_frames[1]));
        }

        [TestCase(4, 1)]
        [TestCase(-1, 2)]
        [TestCase(-3, 0)]
        public void Play_StartFrameOutsideClip_IsWrapped(int startFrame, int expected)
        {
            _flipbook.Play(_frames, FramesPerSecond, startFrame);

            Assert.That(_flipbook.CurrentFrame, Is.EqualTo(expected));
        }

        [Test]
        public void Advance_LessThanOneFrame_KeepsFrame()
        {
            _flipbook.Play(_frames, FramesPerSecond, 0);

            _flipbook.Advance(FrameTime * 0.5f);

            Assert.That(_flipbook.CurrentFrame, Is.Zero);
        }

        [Test]
        public void Advance_SmallStepsAddUp_ToNextFrame()
        {
            _flipbook.Play(_frames, FramesPerSecond, 0);

            _flipbook.Advance(FrameTime * 0.6f);
            _flipbook.Advance(FrameTime * 0.6f);

            Assert.That(_flipbook.CurrentFrame, Is.EqualTo(1));
            Assert.That(_renderer.sprite, Is.SameAs(_frames[1]));
        }

        [Test]
        public void Advance_PastLastFrame_LoopsToFirst()
        {
            _flipbook.Play(_frames, FramesPerSecond, 2);

            _flipbook.Advance(FrameTime * 1.01f);

            Assert.That(_flipbook.CurrentFrame, Is.Zero);
        }

        [Test]
        public void Advance_LongHitch_JumpsStraightToRightFrame()
        {
            _flipbook.Play(_frames, FramesPerSecond, 0);

            // 7 frame times later on a 3-frame clip: 7 mod 3 = 1.
            _flipbook.Advance(FrameTime * 7.5f);

            Assert.That(_flipbook.CurrentFrame, Is.EqualTo(1));
        }

        [Test]
        public void Stop_ThenAdvance_DoesNothing()
        {
            _flipbook.Play(_frames, FramesPerSecond, 1);

            _flipbook.Stop();
            _flipbook.Advance(1f);

            Assert.That(_flipbook.IsPlaying, Is.False);
            Assert.That(_flipbook.CurrentFrame, Is.Zero);
        }

        [Test]
        public void Play_InvalidArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => _flipbook.Play(null, FramesPerSecond, 0));
            Assert.Throws<ArgumentException>(() => _flipbook.Play(Array.Empty<Sprite>(), FramesPerSecond, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => _flipbook.Play(_frames, 0f, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => _flipbook.Play(_frames, float.NaN, 0));
        }
    }
}
