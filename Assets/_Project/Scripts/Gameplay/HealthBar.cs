using System;
using StrategyDemo.Data;
using UnityEngine;

namespace StrategyDemo.Gameplay
{
    /// <summary>
    /// Small bar at the top of a building's or soldier's footprint: a dark strip with the remaining health drawn over
    /// it, shrinking to the left and turning from the full colour to the empty colour. Hidden at full health unless
    /// pinned (the object is selected).
    /// </summary>
    /// <remarks>
    /// Both parts use the same white sprite from the world atlas, tinted with <see cref="SpriteRenderer.color"/> (a
    /// vertex colour) and sized with the transform's scale, so the shared material is never copied and every bar
    /// batches with the board (ADR-001 Karar 9). It redraws only when the health changes, not every frame. It sits on
    /// the soldier's root, not on the rotating visual, so it stays upright (ADR-007).
    /// </remarks>
    public sealed class HealthBar : MonoBehaviour
    {
        [SerializeField, Tooltip("Dark strip behind the fill; its sprite's width is the full bar width.")]
        private SpriteRenderer _background;
        [SerializeField, Tooltip("Remaining health; same sprite as the background, centred pivot.")]
        private SpriteRenderer _fill;
        [SerializeField] private Color _fullColor = new Color(0.36f, 0.85f, 0.32f);
        [SerializeField] private Color _emptyColor = new Color(0.9f, 0.22f, 0.18f);
        [SerializeField, Min(0f), Tooltip("World units between the top edge of the footprint and the bar's centre.")]
        private float _topInset = 0.12f;

        private Health _health;
        private bool _isPinned;
        private Action _refreshHandler;
        private Transform _fillTransform;

        /// <summary>True while either part is drawn.</summary>
        public bool IsVisible => _fill != null && _fill.enabled;

        /// <summary>
        /// Starts showing <paramref name="health"/> for an object covering <paramref name="footprint"/>: the bar is as
        /// many sprite widths wide as the footprint has columns. Replaces an earlier binding.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="health"/> is null.</exception>
        /// <exception cref="InvalidOperationException">A renderer is not assigned.</exception>
        public void Bind(Health health, Vector2Int footprint)
        {
            if (health == null)
            {
                throw new ArgumentNullException(nameof(health));
            }

            if (_background == null || _fill == null)
            {
                throw new InvalidOperationException($"{nameof(HealthBar)} on '{name}' has unassigned renderers.");
            }

            Unbind();
            _health = health;
            // Read here, at production: the first read of a component's transform creates its managed wrapper, which
            // would otherwise allocate on the object's first hit, mid-combat.
            _fillTransform = _fill.transform;
            transform.localPosition = new Vector3(0f, footprint.y * GridConfig.CellWorldSize * 0.5f - _topInset, 0f);
            transform.localScale = new Vector3(footprint.x, 1f, 1f);
            _health.Changed += RefreshHandler;
            Refresh();
        }

        /// <summary>Stops showing any health and unpins; the pooled owner calls this on release.</summary>
        public void Unbind()
        {
            if (_health != null)
            {
                _health.Changed -= RefreshHandler;
                _health = null;
            }

            _isPinned = false;
            SetVisible(false);
        }

        /// <summary>Keeps the bar visible at full health while <paramref name="pinned"/>.</summary>
        public void SetPinned(bool pinned)
        {
            _isPinned = pinned;
            Refresh();
        }

        // Created once: "-= Refresh" would convert the method group into a new delegate on every unbind, i.e. every death.
        private Action RefreshHandler => _refreshHandler ??= Refresh;

        private void Refresh()
        {
            bool visible = _health != null && _health.IsAlive && (_isPinned || _health.Current < _health.Max);
            SetVisible(visible);
            if (!visible)
            {
                return;
            }

            float fraction = _health.Fraction;
            // Local units of the bar: one sprite width per footprint column (the root's X scale does the rest).
            float width = _background.sprite != null ? _background.sprite.bounds.size.x : 0f;
            // Centred pivot: shrink, then shift left by half the lost width so the fill stays anchored on the left.
            _fillTransform.localScale = new Vector3(fraction, 1f, 1f);
            _fillTransform.localPosition = new Vector3(-(1f - fraction) * width * 0.5f, 0f, 0f);
            _fill.color = Color.Lerp(_emptyColor, _fullColor, fraction);
        }

        private void SetVisible(bool visible)
        {
            if (_background != null)
            {
                _background.enabled = visible;
            }

            if (_fill != null)
            {
                _fill.enabled = visible;
            }
        }

        private void OnDestroy()
        {
            Unbind();
        }
    }
}
