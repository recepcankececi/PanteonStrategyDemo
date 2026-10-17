using UnityEngine;

namespace StrategyDemo.Data
{
    /// <summary>
    /// Look and timing of the combat effects: the tracer a ranged soldier's hit draws, and the burst left where a
    /// building or soldier is destroyed. Both sprites come from the World atlas, so effects batch with the board (T1).
    /// </summary>
    [CreateAssetMenu(fileName = "CombatEffectSettings", menuName = "Strategy Demo/Combat Effect Settings")]
    public sealed class CombatEffectSettings : ScriptableObject
    {
        [SerializeField, Tooltip("White streak, stretched from shooter to target and tinted.")]
        private Sprite _tracerSprite;
        [SerializeField] private Color _tracerColor = new Color(1f, 0.89f, 0.48f, 1f);
        [SerializeField, Min(0.01f), Tooltip("Seconds the tracer takes to fade out.")]
        private float _tracerDuration = 0.12f;
        [SerializeField, Tooltip("Burst drawn where something is destroyed; one cell in size.")]
        private Sprite _burstSprite;
        [SerializeField, Min(0.01f), Tooltip("Seconds the burst takes to grow and fade out.")]
        private float _burstDuration = 0.45f;
        [SerializeField, Min(0f), Tooltip("Burst size at the start, relative to the destroyed object's larger side.")]
        private float _burstStartScale = 0.4f;
        [SerializeField, Min(0f), Tooltip("Burst size at the end, relative to the destroyed object's larger side.")]
        private float _burstEndScale = 1.1f;

        /// <summary>White streak stretched from shooter to target; tinted with <see cref="TracerColor"/>.</summary>
        public Sprite TracerSprite => _tracerSprite;

        /// <summary>Tint of the tracer when it appears; its alpha fades to zero.</summary>
        public Color TracerColor => _tracerColor;

        /// <summary>Seconds from a tracer appearing to it being gone.</summary>
        public float TracerDuration => _tracerDuration;

        /// <summary>Sprite of the destruction burst, one cell in size.</summary>
        public Sprite BurstSprite => _burstSprite;

        /// <summary>Seconds from a burst appearing to it being gone.</summary>
        public float BurstDuration => _burstDuration;

        /// <summary>Burst size when it appears, times the destroyed object's larger footprint side.</summary>
        public float BurstStartScale => _burstStartScale;

        /// <summary>Burst size when it is gone, times the destroyed object's larger footprint side.</summary>
        public float BurstEndScale => _burstEndScale;
    }
}
