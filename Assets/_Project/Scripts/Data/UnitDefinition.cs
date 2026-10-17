using System;
using System.Collections.Generic;
using UnityEngine;

namespace StrategyDemo.Data
{
    /// <summary>
    /// Static description of a soldier type. Holds no runtime state: every soldier of this type
    /// shares the asset, so current HP lives on the soldier, never here (ADR-001 Karar 4).
    /// </summary>
    [CreateAssetMenu(fileName = "UnitDefinition", menuName = "Strategy Demo/Unit Definition")]
    public sealed class UnitDefinition : ScriptableObject
    {
        [SerializeField] private string _displayName;
        [SerializeField, Tooltip("Image shown in menus and the information panel.")] private Sprite _icon;
        [SerializeField, Min(1)] private int _maxHealth = 10;
        [SerializeField, Min(0)] private int _damage = 1;
        [SerializeField, Min(1), Tooltip("Attack reach in cells; 1 = adjacent cells only.")] private int _attackRange = 1;
        [SerializeField, Min(0.01f), Tooltip("Seconds between two attacks.")] private float _attackInterval = 1f;
        [SerializeField, Min(0.01f), Tooltip("Movement speed in cells per second.")] private float _moveSpeed = 3f;
        [SerializeField, Tooltip("Idle animation frames, looped on the board. Pivot below the feet: at the bottom-edge middle of the cell.")]
        private Sprite[] _idleFrames = Array.Empty<Sprite>();
        [SerializeField, Min(0.01f), Tooltip("Idle animation speed in frames per second.")] private float _idleFramesPerSecond = 8f;

        /// <summary>Name shown to the player.</summary>
        public string DisplayName => _displayName;

        /// <summary>Image shown in menus and the information panel.</summary>
        public Sprite Icon => _icon;

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

        /// <summary>Idle animation frames in order. Read-only by contract: never cast back to an array, the asset is shared.</summary>
        public IReadOnlyList<Sprite> IdleFrames => _idleFrames;

        /// <summary>Idle animation speed in frames per second.</summary>
        public float IdleFramesPerSecond => _idleFramesPerSecond;
    }
}
