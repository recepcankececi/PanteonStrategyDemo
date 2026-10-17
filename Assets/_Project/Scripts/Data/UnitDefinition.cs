using System;
using System.Collections.Generic;
using UnityEngine;

namespace StrategyDemo.Data
{
    /// <summary>
    /// Static description of a soldier type. Holds no runtime state: every soldier of this type
    /// shares the asset, so current HP lives on the soldier, never here (ADR-001 Karar 4).
    /// </summary>
    /// <remarks>
    /// Art is top-down and faces up (+Y); the soldier rotates to its heading. Every sprite is one cell with a centred
    /// pivot and stays inside the cell's inscribed circle, so it never leaves its cell at any rotation (ADR-007).
    /// </remarks>
    [CreateAssetMenu(fileName = "UnitDefinition", menuName = "Strategy Demo/Unit Definition")]
    public sealed class UnitDefinition : ScriptableObject
    {
        [SerializeField] private string _displayName;
        [SerializeField, Tooltip("Standing pose on the board; also the icon in menus and the information panel.")]
        private Sprite _sprite;
        [SerializeField, Min(1)] private int _maxHealth = 10;
        [SerializeField, Min(0)] private int _damage = 1;
        [SerializeField, Min(1), Tooltip("Attack reach in cells; 1 = adjacent cells only.")] private int _attackRange = 1;
        [SerializeField, Min(0.01f), Tooltip("Seconds between two attacks.")] private float _attackInterval = 1f;
        [SerializeField, Min(0.01f), Tooltip("Movement speed in cells per second.")] private float _moveSpeed = 3f;
        [SerializeField, Tooltip("Walk cycle, looped while the soldier moves. Same size and pivot as the standing sprite.")]
        private Sprite[] _walkFrames = Array.Empty<Sprite>();
        [SerializeField, Min(0.01f), Tooltip("Walk cycle speed in frames per second.")] private float _walkFramesPerSecond = 6f;
        [SerializeField, Tooltip("Every hit draws a tracer from the soldier to its target (ranged soldiers).")]
        private bool _firesTracer;

        /// <summary>Name shown to the player.</summary>
        public string DisplayName => _displayName;

        /// <summary>Standing pose on the board, also used as the menu and information-panel icon.</summary>
        public Sprite Sprite => _sprite;

        /// <summary>Health a freshly produced soldier starts with.</summary>
        public int MaxHealth => _maxHealth;

        /// <summary>Damage dealt per attack.</summary>
        public int Damage => _damage;

        /// <summary>Attack reach in cells (Chebyshev distance to the target's closest cell).</summary>
        public int AttackRange => _attackRange;

        /// <summary>Seconds between two attacks.</summary>
        public float AttackInterval => _attackInterval;

        /// <summary>Movement speed in cells per second.</summary>
        public float MoveSpeed => _moveSpeed;

        /// <summary>Walk cycle frames in order. Read-only by contract: never cast back to an array, the asset is shared.</summary>
        public IReadOnlyList<Sprite> WalkFrames => _walkFrames;

        /// <summary>Walk cycle speed in frames per second.</summary>
        public float WalkFramesPerSecond => _walkFramesPerSecond;

        /// <summary>True when every hit of this soldier draws a tracer to its target; the damage itself stays instant.</summary>
        public bool FiresTracer => _firesTracer;
    }
}
