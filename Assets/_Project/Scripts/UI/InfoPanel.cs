using System;
using System.Collections.Generic;
using StrategyDemo.Core;
using StrategyDemo.Data;
using StrategyDemo.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StrategyDemo.UI
{
    /// <summary>
    /// Information panel (I3, R4, R5): image, name, current/max HP (live) and footprint of the selected building or
    /// soldier. A building that produces soldiers also lists them; clicking one produces it instantly (R3).
    /// Buildings without producible units (Power Plant) show no production section at all. Several selected soldiers
    /// show as "N soldiers selected" with the first one's image.
    /// </summary>
    /// <remarks>
    /// Production rows come from an <see cref="ObjectPool{T}"/>: they are rebound on every selection change
    /// instead of being instantiated and destroyed each time.
    /// </remarks>
    public sealed class InfoPanel : MonoBehaviour
    {
        private const string NoFreeCellMessage = "No free cell on the board.";
        private const string GroupHeaderFormat = "{0} soldiers selected";

        [SerializeField, Tooltip("Shown while nothing is selected.")] private GameObject _emptyState;
        [SerializeField, Tooltip("Shown while something is selected.")] private GameObject _details;
        [SerializeField] private Image _image;
        [SerializeField] private TMP_Text _nameLabel;
        [SerializeField] private TMP_Text _healthLabel;
        [SerializeField] private TMP_Text _footprintLabel;
        [SerializeField, Tooltip("Shown for soldiers only.")] private TMP_Text _damageLabel;
        [SerializeField, Tooltip("Hidden for buildings that produce nothing (R5).")] private GameObject _productionSection;
        [SerializeField, Tooltip("Layout parent of the production rows.")] private Transform _unitList;
        [SerializeField] private UnitProductionItemView _unitItemPrefab;
        [SerializeField, Tooltip("Feedback when production fails, e.g. the board is full.")] private TMP_Text _statusLabel;

        private readonly List<UnitProductionItemView> _rows = new List<UnitProductionItemView>();
        private ObjectPool<UnitProductionItemView> _rowPool;
        private SelectionService _selection;
        private UnitProducer _producer;
        private bool _isSubscribed;
        // Health shown in the HP label, followed live while its object stays selected.
        private Health _shownHealth;
        private Action _showHealthHandler;

        /// <summary>Production rows currently shown, top to bottom.</summary>
        internal IReadOnlyList<UnitProductionItemView> Rows => _rows;

        /// <summary>Starts showing <paramref name="selection"/> and producing through <paramref name="producer"/>.</summary>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="InvalidOperationException">A scene reference is missing or the panel was already initialised.</exception>
        public void Init(SelectionService selection, UnitProducer producer)
        {
            if (_selection != null)
            {
                throw new InvalidOperationException($"{nameof(InfoPanel)} on '{name}' is already initialised.");
            }

            if (_emptyState == null || _details == null || _image == null || _nameLabel == null || _healthLabel == null
                || _footprintLabel == null || _damageLabel == null || _productionSection == null || _unitList == null
                || _unitItemPrefab == null || _statusLabel == null)
            {
                throw new InvalidOperationException($"{nameof(InfoPanel)} on '{name}' has unassigned references.");
            }

            _selection = selection ?? throw new ArgumentNullException(nameof(selection));
            _producer = producer ?? throw new ArgumentNullException(nameof(producer));
            _rowPool = new ObjectPool<UnitProductionItemView>(CreateRow, OnRowTaken, OnRowReleased);
            // Init runs after OnEnable on a scene object, so subscribe here too; the flag prevents doubling.
            if (isActiveAndEnabled)
            {
                Subscribe();
            }

            Refresh();
        }

        /// <summary>Shows the current selection; runs on every selection change. Internal so EditMode tests can call it.</summary>
        internal void Refresh()
        {
            ReleaseRows();
            UnfollowHealth();
            _statusLabel.text = string.Empty;

            IBoardOccupant selected = _selection.Selected;
            _emptyState.SetActive(selected == null);
            _details.SetActive(selected != null);

            int count = _selection.All.Count;
            if (count > 1 && selected is Unit first)
            {
                // A box selects soldiers only; their stats differ, so the group shows only its size and an image.
                ShowGroup(first.Definition, count);
                return;
            }

            switch (selected)
            {
                case Building building:
                    ShowBuilding(building.Definition);
                    FollowHealth(building.Health);
                    break;
                case Unit unit:
                    ShowUnit(unit.Definition);
                    FollowHealth(unit.Health);
                    break;
                case null:
                    break;
                default:
                    // A new occupant type needs its own view here; say so instead of showing stale data.
                    _details.SetActive(false);
                    _emptyState.SetActive(true);
                    Debug.LogWarning($"{nameof(InfoPanel)} cannot show a {selected.GetType().Name}.", this);
                    break;
            }
        }

        private void ShowBuilding(BuildingDefinition building)
        {
            ShowCommon(building.Sprite, building.DisplayName, building.Footprint);
            _damageLabel.gameObject.SetActive(false);

            // R5: no production section at all, not an empty one, for buildings that produce nothing.
            _productionSection.SetActive(building.CanProduceUnits);
            for (int i = 0; i < building.ProducibleUnits.Count; i++)
            {
                UnitProductionItemView row = _rowPool.Get();
                // Tracked before binding: if Bind throws on bad data, the next refresh still returns the row to the pool.
                _rows.Add(row);
                row.Bind(building.ProducibleUnits[i]);
                // Pooled rows come back in any order; re-append so the list follows the definition's order.
                row.transform.SetAsLastSibling();
            }
        }

        private void ShowUnit(UnitDefinition unit)
        {
            ShowCommon(unit.Sprite, unit.DisplayName, Vector2Int.one);
            _damageLabel.gameObject.SetActive(true);
            _damageLabel.SetText("Damage {0}", unit.Damage);
            _productionSection.SetActive(false);
        }

        private void ShowGroup(UnitDefinition first, int count)
        {
            ShowImage(first.Sprite);
            _nameLabel.SetText(GroupHeaderFormat, count);
            _healthLabel.gameObject.SetActive(false);
            _footprintLabel.gameObject.SetActive(false);
            _damageLabel.gameObject.SetActive(false);
            _productionSection.SetActive(false);
        }

        private void ShowCommon(Sprite image, string displayName, Vector2Int footprint)
        {
            ShowImage(image);
            _nameLabel.text = displayName;
            // The text itself is written by ShowHealth, which also follows every later hit.
            _healthLabel.gameObject.SetActive(true);
            _footprintLabel.gameObject.SetActive(true);
            _footprintLabel.SetText("Size {0}×{1}", footprint.x, footprint.y);
        }

        private void ShowImage(Sprite image)
        {
            _image.sprite = image;
            // A uGUI Image without a sprite draws a white square; hide it instead.
            _image.enabled = image != null;
        }

        private void FollowHealth(Health health)
        {
            _shownHealth = health;
            _shownHealth.Changed += ShowHealthHandler;
            ShowHealth();
        }

        private void UnfollowHealth()
        {
            if (_shownHealth != null)
            {
                _shownHealth.Changed -= ShowHealthHandler;
                _shownHealth = null;
            }
        }

        // Created once: "-= ShowHealth" would convert the method group into a new delegate whenever the shown object
        // changes, including when a selected object dies mid-combat.
        private Action ShowHealthHandler => _showHealthHandler ??= ShowHealth;

        private void ShowHealth()
        {
            // SetText with arguments formats without allocating a string per hit.
            _healthLabel.SetText("HP {0}/{1}", _shownHealth.Current, _shownHealth.Max);
        }

        private void HandleProduceClicked(UnitDefinition unit)
        {
            // Rows exist only while a producing building is selected; re-check in case the selection changed
            // between the click and this callback.
            if (!(_selection.Selected is Building building))
            {
                return;
            }

            bool produced = _producer.TryProduce(building.Definition, building.Origin, unit, out _);
            _statusLabel.text = produced ? string.Empty : NoFreeCellMessage;
        }

        private void OnEnable()
        {
            if (_selection != null)
            {
                Subscribe();
                Refresh();
            }
        }

        private void OnDisable()
        {
            if (_isSubscribed)
            {
                _selection.SelectionChanged -= Refresh;
                _isSubscribed = false;
            }

            UnfollowHealth();
        }

        private void Subscribe()
        {
            if (!_isSubscribed)
            {
                _selection.SelectionChanged += Refresh;
                _isSubscribed = true;
            }
        }

        private void ReleaseRows()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                _rowPool.Release(_rows[i]);
            }

            _rows.Clear();
        }

        private UnitProductionItemView CreateRow()
        {
            UnitProductionItemView row = Instantiate(_unitItemPrefab, _unitList);
            row.name = $"{_unitItemPrefab.name}_{_rowPool.CountAll}";
            return row;
        }

        private void OnRowTaken(UnitProductionItemView row)
        {
            row.gameObject.SetActive(true);
            row.Clicked += HandleProduceClicked;
        }

        private void OnRowReleased(UnitProductionItemView row)
        {
            // ResetState also drops the Clicked subscription added in OnRowTaken.
            row.ResetState();
            row.gameObject.SetActive(false);
        }
    }
}
