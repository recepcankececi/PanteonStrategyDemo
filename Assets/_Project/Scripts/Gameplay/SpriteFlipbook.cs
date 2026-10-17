using System;
using System.Collections.Generic;
using UnityEngine;

namespace StrategyDemo.Gameplay
{
    /// <summary>
    /// Plays a looping list of sprites on a <see cref="SpriteRenderer"/> at a fixed frame rate: a soldier's walk
    /// cycle without an Animator Controller per unit type.
    /// </summary>
    /// <remarks>
    /// Swapping <see cref="SpriteRenderer.sprite"/> between frames of the same atlas keeps the shared material,
    /// so animated soldiers still batch. Stopping keeps the last sprite; the owner decides what
    /// to show next (a soldier shows its standing pose). The sprite is assigned only when the frame changes;
    /// <c>Update</c> allocates nothing.
    /// </remarks>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SpriteFlipbook : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private IReadOnlyList<Sprite> _frames;
        private float _frameDuration;
        private float _timeInFrame;

        /// <summary>Index of the frame on screen; 0 while stopped.</summary>
        public int CurrentFrame { get; private set; }

        /// <summary>True while a clip is playing.</summary>
        public bool IsPlaying => _frames != null;

        /// <summary>Starts looping <paramref name="frames"/> from <paramref name="startFrame"/>.</summary>
        /// <param name="frames">Clip frames in order; read, never modified.</param>
        /// <param name="framesPerSecond">Playback speed; must be positive.</param>
        /// <param name="startFrame">First frame shown, wrapped into the clip; lets soldiers of one type walk out of step.</param>
        /// <exception cref="ArgumentNullException"><paramref name="frames"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="frames"/> is empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="framesPerSecond"/> is not positive.</exception>
        public void Play(IReadOnlyList<Sprite> frames, float framesPerSecond, int startFrame)
        {
            if (frames == null)
            {
                throw new ArgumentNullException(nameof(frames));
            }

            if (frames.Count == 0)
            {
                throw new ArgumentException("A flipbook needs at least one frame.", nameof(frames));
            }

            if (!(framesPerSecond > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(framesPerSecond), framesPerSecond, "Frame rate must be positive.");
            }

            _frames = frames;
            _frameDuration = 1f / framesPerSecond;
            _timeInFrame = 0f;
            // Non-negative modulo: a negative start would otherwise index before the first frame.
            CurrentFrame = ((startFrame % frames.Count) + frames.Count) % frames.Count;
            Renderer.sprite = _frames[CurrentFrame];
            // Update runs only while a clip plays: most soldiers stand most of the time.
            enabled = true;
        }

        /// <summary>Stops playback and forgets the clip; the renderer keeps its last sprite.</summary>
        public void Stop()
        {
            _frames = null;
            _timeInFrame = 0f;
            CurrentFrame = 0;
            enabled = false;
        }

        /// <summary>
        /// Moves playback forward by <paramref name="deltaTime"/> seconds. Internal so EditMode tests can step time
        /// without the player loop.
        /// </summary>
        internal void Advance(float deltaTime)
        {
            if (_frames == null || deltaTime <= 0f)
            {
                return;
            }

            _timeInFrame += deltaTime;
            if (_timeInFrame < _frameDuration)
            {
                return;
            }

            // Division, not a loop: after a long hitch (or a paused editor) the clip jumps straight to the right frame.
            int steps = Mathf.FloorToInt(_timeInFrame / _frameDuration);
            _timeInFrame -= steps * _frameDuration;
            CurrentFrame = (CurrentFrame + steps) % _frames.Count;
            Renderer.sprite = _frames[CurrentFrame];
        }

        // Lazy so Play works before Awake (EditMode tests, or a pooled object configured while inactive).
        private SpriteRenderer Renderer => _renderer != null ? _renderer : _renderer = GetComponent<SpriteRenderer>();

        private void Update()
        {
            Advance(Time.deltaTime);
        }
    }
}
