using System;
using StrategyDemo.Data;
using StrategyDemo.Gameplay.Buildings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StrategyDemo.UI
{
    /// <summary>
    /// One pooled row of the production menu: icon, name and footprint of a building.
    /// Rebound to a different building whenever the scroll view recycles it.
    /// </summary>
    public sealed class ProductionMenuItemView : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _nameLabel;
        [SerializeField] private TMP_Text _footprintLabel;

        /// <summary>Raised when the player clicks the row. Cleared by <see cref="ResetState"/>.</summary>
        public event Action<BuildingDefinition> Clicked;

        /// <summary>Building currently shown; null while the view waits in the pool.</summary>
        public BuildingDefinition Building { get; private set; }

        /// <summary>Shows <paramref name="building"/> in this row.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="building"/> is null.</exception>
        public void Bind(BuildingDefinition building)
        {
            if (building == null)
            {
                throw new ArgumentNullException(nameof(building));
            }

            Building = building;
            _icon.sprite = building.Sprite;
            // A uGUI Image without a sprite draws a white square; hide it instead.
            _icon.enabled = building.Sprite != null;
            _nameLabel.text = building.DisplayName;
            // SetText with numeric arguments formats into TMP's own buffer: no string is allocated
            // while the list recycles rows during scrolling.
            _footprintLabel.SetText("{0}×{1}", building.Footprint.x, building.Footprint.y);
        }

        /// <summary>
        /// Clears the shown data and every <see cref="Clicked"/> subscriber, so a reused view
        /// cannot leak the previous building or call a previous owner.
        /// </summary>
        public void ResetState()
        {
            Clicked = null;
            Building = null;
            _icon.sprite = null;
            _icon.enabled = false;
            _nameLabel.text = string.Empty;
            _footprintLabel.text = string.Empty;
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
            if (Building != null)
            {
                Clicked?.Invoke(Building);
            }
        }
    }
}
