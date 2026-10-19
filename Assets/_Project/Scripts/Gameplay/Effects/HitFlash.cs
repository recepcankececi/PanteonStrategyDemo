using System;
using StrategyDemo.Gameplay.Combat;
using UnityEngine;

namespace StrategyDemo.Gameplay.Effects
{
    /// <summary>
    /// Tints a sprite briefly when its owner takes damage, then fades back to white. Uses
    /// <see cref="SpriteRenderer.color"/> (a vertex colour), never <c>renderer.material</c>, so batching holds.
    /// </summary>
    /// <remarks>
    /// The component is enabled only while a flash plays: idle objects cost no <c>Update</c> call.
    /// </remarks>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class HitFlash : MonoBehaviour
    {
        [SerializeField] private Color _flashColor = new Color(1f, 0.4f, 0.4f);
        [SerializeField, Min(0.01f), Tooltip("Seconds from the hit back to the normal colour.")]
        private float _duration = 0.15f;

        private SpriteRenderer _renderer;
        private Health _health;
        private float _remaining;
        private Action<int> _damagedHandler;

        /// <summary>True while a flash is fading.</summary>
        public bool IsPlaying => _remaining > 0f;

        /// <summary>Flashes on every hit <paramref name="health"/> takes. Replaces an earlier binding.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="health"/> is null.</exception>
        public void Bind(Health health)
        {
            if (health == null)
            {
                throw new ArgumentNullException(nameof(health));
            }

            Unbind();
            _health = health;
            _health.Damaged += DamagedHandler;
        }

        /// <summary>Stops listening and restores the normal colour; the pooled owner calls this on release.</summary>
        public void Unbind()
        {
            if (_health != null)
            {
                _health.Damaged -= DamagedHandler;
                _health = null;
            }

            Stop();
        }

        /// <summary>Starts (or restarts) the flash at full strength.</summary>
        public void Play()
        {
            _remaining = _duration;
            Renderer.color = _flashColor;
            enabled = true;
        }

        /// <summary>Advances the fade by <paramref name="deltaTime"/> seconds. Internal so EditMode tests can step it.</summary>
        internal void Advance(float deltaTime)
        {
            // A negative frame time (an editor quirk, see UnitMovementDriver) must not make the flash longer.
            if (!IsPlaying || deltaTime <= 0f)
            {
                return;
            }

            _remaining -= deltaTime;
            if (_remaining <= 0f)
            {
                Stop();
                return;
            }

            Renderer.color = Color.Lerp(Color.white, _flashColor, _remaining / _duration);
        }

        private void Stop()
        {
            _remaining = 0f;
            Renderer.color = Color.white;
            enabled = false;
        }

        // Created once: "-= HandleDamaged" would convert the method group into a new delegate on every unbind.
        private Action<int> DamagedHandler => _damagedHandler ??= HandleDamaged;

        private void HandleDamaged(int amount)
        {
            Play();
        }

        private void Awake()
        {
            // Nothing to fade yet; Play enables the component.
            if (!IsPlaying)
            {
                enabled = false;
            }
        }

        private void Update()
        {
            Advance(Time.deltaTime);
        }

        private void OnDestroy()
        {
            if (_health != null)
            {
                _health.Damaged -= DamagedHandler;
            }
        }

        // Lazy: the pool configures objects before their first Awake could run (and EditMode tests never run it).
        private SpriteRenderer Renderer => _renderer != null ? _renderer : _renderer = GetComponent<SpriteRenderer>();
    }
}
