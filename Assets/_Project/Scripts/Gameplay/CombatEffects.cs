using System;
using System.Collections.Generic;
using StrategyDemo.Core;
using StrategyDemo.Data;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StrategyDemo.Gameplay
{
    /// <summary>
    /// Draws the short-lived combat effects: a tracer from a ranged soldier to its target on every hit, and a burst
    /// where a building or soldier is destroyed. A view only: combat and despawning work the same without it.
    /// </summary>
    /// <remarks>
    /// <para>Every effect is the same pooled <see cref="SpriteRenderer"/> with a World atlas sprite on the shared
    /// material; size, angle and fade come from the transform and the vertex colour, so effects batch with the board
    /// (T1) and need no LineRenderer, particle system or material of their own.</para>
    /// <para>One <c>Update</c> here animates every live effect, instead of one component per effect: effects are spawned
    /// in bursts, and a list of structs costs nothing to walk. After warm-up nothing allocates.</para>
    /// </remarks>
    public sealed class CombatEffects : MonoBehaviour
    {
        [SerializeField, Tooltip("Plain sprite renderer on the FX layer, copied once per pooled effect.")]
        private SpriteRenderer _effectPrefab;
        [SerializeField, Tooltip("Board container pooled (inactive) effects wait under.")]
        private Transform _poolRoot;
        [SerializeField] private CombatEffectSettings _settings;

        private readonly List<LiveEffect> _live = new List<LiveEffect>();
        private ObjectPool<SpriteRenderer> _pool;
        private UnitCombat _combat;
        private OccupantDespawner _despawner;
        private bool _isSubscribed;

        /// <summary>Effects on screen right now. Internal so EditMode tests can count them.</summary>
        internal int LiveCount => _live.Count;

        /// <summary>Effect objects ever instantiated; stays flat once the pool has warmed up.</summary>
        internal int CreatedCount => _pool?.CountAll ?? 0;

        /// <summary>Starts drawing the hits of <paramref name="combat"/> and the removals of <paramref name="despawner"/>.</summary>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="InvalidOperationException">Already initialised, or a serialized reference is missing.</exception>
        public void Init(UnitCombat combat, OccupantDespawner despawner)
        {
            if (_combat != null)
            {
                throw new InvalidOperationException($"{nameof(CombatEffects)} on '{name}' is already initialised.");
            }

            if (_effectPrefab == null || _poolRoot == null || _settings == null)
            {
                throw new InvalidOperationException($"{nameof(CombatEffects)} on '{name}' has no prefab, pool root or settings assigned.");
            }

            // Checked here, not on the first hit: a throw inside HitLanded or Despawned would abort combat or a
            // removal half-way, and a view must never break the game it shows.
            if (_settings.TracerSprite == null || _settings.BurstSprite == null || _settings.TracerSprite.bounds.size.x <= 0f)
            {
                throw new InvalidOperationException($"{nameof(CombatEffectSettings)} '{_settings.name}' needs a tracer and a burst sprite.");
            }

            _combat = combat ?? throw new ArgumentNullException(nameof(combat));
            _despawner = despawner ?? throw new ArgumentNullException(nameof(despawner));
            _pool = new ObjectPool<SpriteRenderer>(CreateEffect, OnEffectTaken, OnEffectReleased);
            // Init runs after OnEnable on a scene object, so subscribe here too; the flag prevents doubling.
            if (isActiveAndEnabled)
            {
                Subscribe();
            }
        }

        /// <summary>
        /// Ages every live effect by <paramref name="deltaTime"/> seconds and returns finished ones to the pool.
        /// Internal so EditMode tests can step time without the player loop.
        /// </summary>
        internal void Advance(float deltaTime)
        {
            // A negative frame time (an editor quirk, see UnitMovementDriver) must not make effects younger.
            if (deltaTime <= 0f)
            {
                return;
            }

            // Backwards with swap-remove: the effect moved into slot i has already been aged this frame.
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                LiveEffect effect = _live[i];
                effect.Age += deltaTime;
                if (effect.Age >= effect.Duration)
                {
                    _pool.Release(effect.Renderer);
                    _live[i] = _live[_live.Count - 1];
                    _live.RemoveAt(_live.Count - 1);
                    continue;
                }

                Animate(effect);
                _live[i] = effect;
            }
        }

        /// <summary>Draws a tracer for <paramref name="attacker"/>'s hit when its type fires one. Internal for tests.</summary>
        internal void HandleHit(Unit attacker, IDamageable target)
        {
            if (!attacker.Definition.FiresTracer)
            {
                return;
            }

            Vector3 from = attacker.WorldCenter;
            Vector3 to = target.WorldCenter;
            Vector3 line = to - from;
            SpriteRenderer tracer = Spawn(_settings.TracerSprite, EffectKind.Tracer, _settings.TracerDuration, _settings.TracerColor, 0f);
            Transform t = tracer.transform;
            t.position = (from + to) * 0.5f;
            t.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(line.y, line.x) * Mathf.Rad2Deg);
            // The sprite lies along X; stretching X makes it span the whole line, Y keeps its thickness.
            t.localScale = new Vector3(line.magnitude / _settings.TracerSprite.bounds.size.x, 1f, 1f);
        }

        /// <summary>Draws a burst where <paramref name="gone"/> stood, sized to its footprint. Internal for tests.</summary>
        internal void HandleDespawned(IBoardOccupant gone)
        {
            float size = Mathf.Max(gone.Footprint.x, gone.Footprint.y) * GridConfig.CellWorldSize;
            SpriteRenderer burst = Spawn(_settings.BurstSprite, EffectKind.Burst, _settings.BurstDuration, Color.white, size);
            burst.transform.position = gone.WorldCenter;
        }

        private SpriteRenderer Spawn(Sprite sprite, EffectKind kind, float duration, Color color, float size)
        {
            SpriteRenderer renderer = _pool.Get();
            renderer.sprite = sprite;
            var effect = new LiveEffect { Renderer = renderer, Kind = kind, Duration = duration, Color = color, Size = size };
            _live.Add(effect);
            // Its first frame drawn now, not next Update: a burst must not flash at full size.
            Animate(effect);
            return renderer;
        }

        private void Animate(LiveEffect effect)
        {
            float progress = effect.Age / effect.Duration;
            Color color = effect.Color;
            // Vertex colour only: the shared material stays untouched, so the effect keeps batching.
            color.a *= 1f - progress;
            effect.Renderer.color = color;
            if (effect.Kind == EffectKind.Burst)
            {
                // Eased out: the burst grows fast at first, then settles while it fades.
                float eased = 1f - (1f - progress) * (1f - progress);
                float scale = effect.Size * Mathf.Lerp(_settings.BurstStartScale, _settings.BurstEndScale, eased);
                effect.Renderer.transform.localScale = new Vector3(scale, scale, 1f);
            }
        }

        private void Update()
        {
            if (_pool != null)
            {
                Advance(Time.deltaTime);
            }
        }

        private void OnEnable()
        {
            if (_combat != null)
            {
                Subscribe();
            }
        }

        private void OnDisable()
        {
            if (_isSubscribed)
            {
                _combat.HitLanded -= HandleHit;
                _despawner.Despawned -= HandleDespawned;
                _isSubscribed = false;
            }
        }

        private void Subscribe()
        {
            if (!_isSubscribed)
            {
                _combat.HitLanded += HandleHit;
                _despawner.Despawned += HandleDespawned;
                _isSubscribed = true;
            }
        }

        private SpriteRenderer CreateEffect()
        {
            // Created inactive under the pool, like a released effect; OnEffectTaken moves it under this object.
            SpriteRenderer effect = Object.Instantiate(_effectPrefab, _poolRoot);
            effect.name = _effectPrefab.name;
            effect.gameObject.SetActive(false);
            return effect;
        }

        private void OnEffectTaken(SpriteRenderer effect)
        {
            effect.transform.SetParent(transform, false);
            effect.gameObject.SetActive(true);
        }

        private void OnEffectReleased(SpriteRenderer effect)
        {
            effect.gameObject.SetActive(false);
            effect.transform.SetParent(_poolRoot, false);
            // Reset everything Spawn and Animate change, so the next effect starts clean.
            effect.transform.localRotation = Quaternion.identity;
            effect.transform.localScale = Vector3.one;
            effect.sprite = null;
            effect.color = Color.white;
        }

        private enum EffectKind
        {
            Tracer,
            Burst,
        }

        // A struct in a list: no per-effect object to allocate, and the list is walked once per frame.
        private struct LiveEffect
        {
            public SpriteRenderer Renderer;
            public EffectKind Kind;
            public Color Color;
            public float Duration;
            public float Age;
            // Larger footprint side in world units; bursts only.
            public float Size;
        }
    }
}
