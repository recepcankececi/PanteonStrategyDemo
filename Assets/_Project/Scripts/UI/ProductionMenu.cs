using System;
using System.Collections.Generic;
using StrategyDemo.Core;
using StrategyDemo.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StrategyDemo.UI
{
    /// <summary>
    /// Production menu (I1, U1): an infinite, looping scroll list of the buildings in the catalog.
    /// Only enough rows to fill the viewport exist; they come from an <see cref="ObjectPool{T}"/> and are
    /// moved and rebound as the list scrolls.
    /// </summary>
    /// <remarks>
    /// Lives on the same GameObject as a <see cref="ScrollRect"/> set to Unrestricted movement, which
    /// provides dragging, the mouse wheel and inertia. This class only decides which building each row
    /// shows and where. Same GameObject is required: the EventSystem sends drag events only to the
    /// dragged object, and the drag state is what keeps rebasing from fighting the ScrollRect.
    /// </remarks>
    [RequireComponent(typeof(ScrollRect))]
    public sealed class ProductionMenu : MonoBehaviour, IBeginDragHandler, IEndDragHandler
    {
        [SerializeField, Tooltip("Row prefab; its height is the row height.")] private ProductionMenuItemView _itemPrefab;
        [SerializeField, Min(0f), Tooltip("Gap below each row, in reference-resolution pixels.")] private float _spacing = 8f;

        // Rows on screen from top to bottom: _views[i] shows virtual index _firstIndex + i.
        private readonly List<ProductionMenuItemView> _views = new List<ProductionMenuItemView>();
        private IReadOnlyList<BuildingDefinition> _buildings;
        private ObjectPool<ProductionMenuItemView> _pool;
        private ScrollRect _scrollRect;
        private float _itemHeight;
        private int _firstIndex;
        private bool _isDragging;
        private bool _viewCountDirty;

        /// <summary>Raised when the player picks a building to build (consumed by placement).</summary>
        public event Action<BuildingDefinition> BuildRequested;

        /// <summary>Rows currently in use, top to bottom.</summary>
        internal IReadOnlyList<ProductionMenuItemView> Views => _views;

        /// <summary>Rows ever instantiated; stays flat while scrolling because rows are reused.</summary>
        internal int CreatedViewCount => _pool?.CountAll ?? 0;

        private float Stride => _itemHeight + _spacing;

        private RectTransform Content => _scrollRect.content;

        /// <summary>Fills the menu from <paramref name="catalog"/> and shows its first building at the top.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="catalog"/> is null.</exception>
        /// <exception cref="InvalidOperationException">A scene reference is missing or the menu was already initialised.</exception>
        public void Init(BuildingCatalog catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (_pool != null)
            {
                throw new InvalidOperationException($"{nameof(ProductionMenu)} on '{name}' is already initialised.");
            }

            _scrollRect = GetComponent<ScrollRect>();
            if (_scrollRect.content == null || _scrollRect.viewport == null || _itemPrefab == null)
            {
                throw new InvalidOperationException($"{nameof(ProductionMenu)} on '{name}' has unassigned references.");
            }

            _itemHeight = ((RectTransform)_itemPrefab.transform).rect.height;
            if (_itemHeight <= 0f)
            {
                throw new InvalidOperationException($"Row prefab '{_itemPrefab.name}' has no height.");
            }

            // A null entry would make Bind throw halfway through a recycle and leave the rows inconsistent.
            for (int i = 0; i < catalog.Buildings.Count; i++)
            {
                if (catalog.Buildings[i] == null)
                {
                    throw new InvalidOperationException($"Catalog '{catalog.name}' has an empty entry at index {i}.");
                }
            }

            _buildings = catalog.Buildings;
            _pool = new ObjectPool<ProductionMenuItemView>(CreateView, OnViewTaken, OnViewReleased);
            Content.anchoredPosition = Vector2.zero;
            RebuildViews();
        }

        /// <summary>
        /// Recycles rows for the current scroll position. Runs on every scroll change; internal so
        /// EditMode tests can drive it without the ScrollRect's LateUpdate.
        /// </summary>
        internal void Refresh()
        {
            if (_pool == null || _views.Count == 0)
            {
                return;
            }

            int first = InfiniteScrollMath.FirstVisibleIndex(Content.anchoredPosition.y, Stride);
            // ScrollRect recomputes the content position from the drag start on every drag event,
            // so a rebase during a drag would be undone; it waits for the release (or inertia).
            if (!_isDragging)
            {
                first = Rebase(first);
            }

            Recycle(first);
        }

        /// <inheritdoc/>
        public void OnBeginDrag(PointerEventData eventData)
        {
            // Mirrors ScrollRect, which only drags with the left button.
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                _isDragging = true;
            }
        }

        /// <inheritdoc/>
        public void OnEndDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            _isDragging = false;
            // Released without inertia, the ScrollRect never reports another change, so the rebase
            // postponed during the drag would wait until the next scroll. Do it now.
            Refresh();
        }

        private void Awake()
        {
            _scrollRect = GetComponent<ScrollRect>();
        }

        private void OnEnable()
        {
            _scrollRect.onValueChanged.AddListener(HandleScrolled);
        }

        private void OnDisable()
        {
            _scrollRect.onValueChanged.RemoveListener(HandleScrolled);
            // A drag interrupted by disabling never sends OnEndDrag.
            _isDragging = false;
        }

        private void LateUpdate()
        {
            // Resizes are applied here, outside the canvas layout pass that reported them: instantiating
            // and rebinding rows inside that pass would dirty graphics mid-rebuild.
            if (_viewCountDirty)
            {
                _viewCountDirty = false;
                UpdateViewCount();
            }
        }

        /// <summary>
        /// Matches the row count to the viewport height: a taller viewport takes more rows from the pool,
        /// a shorter one returns rows to it. Internal so EditMode tests can call it after resizing.
        /// </summary>
        internal void UpdateViewCount()
        {
            if (_pool != null && NeededViewCount() != _views.Count)
            {
                RebuildViews();
            }
        }

        // Unity calls this when this RectTransform's size changes, e.g. when the window is resized.
        private void OnRectTransformDimensionsChange()
        {
            _viewCountDirty = true;
        }

        private void HandleScrolled(Vector2 normalizedPosition)
        {
            Refresh();
        }

        private void HandleItemClicked(BuildingDefinition building)
        {
            BuildRequested?.Invoke(building);
        }

        private int NeededViewCount()
        {
            return _buildings.Count == 0 ? 0 : InfiniteScrollMath.ViewCount(_scrollRect.viewport.rect.height, Stride);
        }

        private void RebuildViews()
        {
            int needed = NeededViewCount();
            while (_views.Count < needed)
            {
                _views.Add(_pool.Get());
            }

            while (_views.Count > needed)
            {
                int last = _views.Count - 1;
                _pool.Release(_views[last]);
                _views.RemoveAt(last);
            }

            if (needed > 0)
            {
                _firstIndex = InfiniteScrollMath.FirstVisibleIndex(Content.anchoredPosition.y, Stride);
                BindAllViews();
            }
        }

        /// <summary>
        /// Keeps the content position inside the first cycle of the list so it cannot grow until float
        /// precision makes rows jitter. Content and rows move by whole cycles together: nothing moves on
        /// screen and every row keeps its building.
        /// </summary>
        private int Rebase(int first)
        {
            int shift = InfiniteScrollMath.RebaseShift(first, _buildings.Count);
            if (shift == 0)
            {
                return first;
            }

            Content.anchoredPosition -= new Vector2(0f, shift * Stride);
            _firstIndex -= shift;
            for (int i = 0; i < _views.Count; i++)
            {
                PlaceView(_views[i], _firstIndex + i);
            }

            return first - shift;
        }

        private void Recycle(int targetFirst)
        {
            int count = _views.Count;
            // A fling can pass the whole window in one frame; then every row changes anyway.
            if (Math.Abs(targetFirst - _firstIndex) >= count)
            {
                _firstIndex = targetFirst;
                BindAllViews();
                return;
            }

            // Scrolled down: the top row left the viewport, so it becomes the new bottom row.
            while (_firstIndex < targetFirst)
            {
                ProductionMenuItemView view = _views[0];
                _views.RemoveAt(0);
                _views.Add(view);
                BindView(view, _firstIndex + count);
                _firstIndex++;
            }

            // Scrolled up: the bottom row becomes the new top row.
            while (_firstIndex > targetFirst)
            {
                ProductionMenuItemView view = _views[count - 1];
                _views.RemoveAt(count - 1);
                _views.Insert(0, view);
                _firstIndex--;
                BindView(view, _firstIndex);
            }
        }

        private void BindAllViews()
        {
            for (int i = 0; i < _views.Count; i++)
            {
                BindView(_views[i], _firstIndex + i);
            }
        }

        private void BindView(ProductionMenuItemView view, int virtualIndex)
        {
            view.Bind(_buildings[InfiniteScrollMath.Wrap(virtualIndex, _buildings.Count)]);
            PlaceView(view, virtualIndex);
        }

        private void PlaceView(ProductionMenuItemView view, int virtualIndex)
        {
            // Rows hang down from the content's top edge; negative indices sit above it.
            ((RectTransform)view.transform).anchoredPosition = new Vector2(0f, -virtualIndex * Stride);
        }

        private ProductionMenuItemView CreateView()
        {
            ProductionMenuItemView view = Instantiate(_itemPrefab, Content);
            view.name = $"{_itemPrefab.name}_{_pool.CountAll}";

            // Layout is owned here, not by the prefab: rows span the content width and hang from its top.
            var rectTransform = (RectTransform)view.transform;
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 1f);
            rectTransform.sizeDelta = new Vector2(0f, _itemHeight);
            return view;
        }

        private void OnViewTaken(ProductionMenuItemView view)
        {
            view.gameObject.SetActive(true);
            view.Clicked += HandleItemClicked;
        }

        private void OnViewReleased(ProductionMenuItemView view)
        {
            // ResetState also drops the Clicked subscription added in OnViewTaken.
            view.ResetState();
            view.gameObject.SetActive(false);
        }
    }
}
