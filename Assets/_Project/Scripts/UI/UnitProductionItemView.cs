using System;
using StrategyDemo.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StrategyDemo.UI
{
    /// <summary>
    /// One row in the information panel's production list: a soldier's icon, name and damage (R4, R7).
    /// Clicking it asks for one soldier; pooled and rebound whenever the selection changes.
    /// </summary>
    public sealed class UnitProductionItemView : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _nameLabel;
        [SerializeField] private TMP_Text _damageLabel;

        /// <summary>Raised when the player clicks the row. Cleared by <see cref="ResetState"/>.</summary>
        public event Action<UnitDefinition> Clicked;

        /// <summary>Soldier type shown; null while the view waits in the pool.</summary>
        public UnitDefinition Unit { get; private set; }

        /// <summary>Shows <paramref name="unit"/> in this row.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="unit"/> is null.</exception>
        public void Bind(UnitDefinition unit)
        {
            Unit = unit != null ? unit : throw new ArgumentNullException(nameof(unit));
            _icon.sprite = unit.Sprite;
            // A uGUI Image without a sprite draws a white square; hide it instead.
            _icon.enabled = unit.Sprite != null;
            _nameLabel.text = unit.DisplayName;
            _damageLabel.SetText("Damage {0}", unit.Damage);
        }

        /// <summary>Clears the shown data and every <see cref="Clicked"/> subscriber, so a reused row cannot call a previous owner.</summary>
        public void ResetState()
        {
            Clicked = null;
            Unit = null;
            _icon.sprite = null;
            _icon.enabled = false;
            _nameLabel.text = string.Empty;
            _damageLabel.text = string.Empty;
        }

        private void OnEnable()
        {
            _button.onClick.AddListener(HandleClick);
        }

        private void OnDisable()
        {
            _button.onClick.RemoveListener(HandleClick);
        }

        /// <summary>Button callback; internal so EditMode tests can click without an EventSystem.</summary>
        internal void HandleClick()
        {
            if (Unit != null)
            {
                Clicked?.Invoke(Unit);
            }
        }
    }
}
