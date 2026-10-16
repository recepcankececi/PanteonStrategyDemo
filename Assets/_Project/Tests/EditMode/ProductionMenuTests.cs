using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using StrategyDemo.Core;
using StrategyDemo.Data;
using StrategyDemo.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace StrategyDemo.Tests.EditMode
{
    /// <summary>
    /// The production menu's infinite scroll (I1, U1): pooled rows, looping data, recycling, rebasing,
    /// resizing and the click event. Built in a preview scene so the open scenes are never touched.
    /// </summary>
    public class ProductionMenuTests
    {
        private const string ItemPrefabPath = "Assets/_Project/Prefabs/UI/ProductionMenuItem.prefab";
        private const float Spacing = 12f;
        private const float ViewportHeight = 500f;  // 4.3 rows of the 104 px prefab -> 6 views

        private float _stride;

        private readonly List<Object> _assets = new List<Object>();
        private Scene _scene;
        private RectTransform _root;
        private RectTransform _content;
        private ProductionMenu _menu;

        [SetUp]
        public void CreateMenu()
        {
            _scene = EditorSceneManager.NewPreviewScene();

            var rootObject = new GameObject("ProductionScrollView", typeof(RectTransform), typeof(ScrollRect));
            SceneManager.MoveGameObjectToScene(rootObject, _scene);
            _root = (RectTransform)rootObject.transform;
            _root.sizeDelta = new Vector2(300f, ViewportHeight);

            var viewport = (RectTransform)new GameObject("ProductionViewport", typeof(RectTransform)).transform;
            viewport.SetParent(_root, false);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.sizeDelta = Vector2.zero;

            _content = (RectTransform)new GameObject("ProductionContent", typeof(RectTransform)).transform;
            _content.SetParent(viewport, false);
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = Vector2.one;
            _content.pivot = new Vector2(0.5f, 1f);

            var scrollRect = rootObject.GetComponent<ScrollRect>();
            scrollRect.viewport = viewport;
            scrollRect.content = _content;
            scrollRect.movementType = ScrollRect.MovementType.Unrestricted;

            var prefab = AssetDatabase.LoadAssetAtPath<ProductionMenuItemView>(ItemPrefabPath);
            // The row prefab owns the row height; the menu only adds the gap.
            _stride = ((RectTransform)prefab.transform).rect.height + Spacing;

            _menu = rootObject.AddComponent<ProductionMenu>();
            var serialized = new SerializedObject(_menu);
            serialized.FindProperty("_itemPrefab").objectReferenceValue = prefab;
            serialized.FindProperty("_spacing").floatValue = Spacing;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void DestroyMenu()
        {
            EditorSceneManager.ClosePreviewScene(_scene);
            foreach (Object asset in _assets)
            {
                Object.DestroyImmediate(asset);
            }

            _assets.Clear();
        }

        [Test]
        public void Init_ViewportFitsFourAndAHalfRows_CreatesSixRows()
        {
            _menu.Init(Catalog("A", "B", "C"));

            Assert.That(_menu.Views, Has.Count.EqualTo(6));
            Assert.That(_menu.CreatedViewCount, Is.EqualTo(6));
        }

        [Test]
        public void Init_FewerBuildingsThanRows_RepeatsCatalogInOrder()
        {
            _menu.Init(Catalog("A", "B", "C"));

            Assert.That(ShownNames(), Is.EqualTo(new[] { "A", "B", "C", "A", "B", "C" }));
            for (int i = 0; i < _menu.Views.Count; i++)
            {
                Assert.That(Row(i).anchoredPosition.y, Is.EqualTo(-i * _stride), $"row {i}");
            }
        }

        [Test]
        public void Init_RowsAreNamedAndParentedUnderContent()
        {
            _menu.Init(Catalog("A", "B", "C"));

            foreach (ProductionMenuItemView view in _menu.Views)
            {
                Assert.That(view.transform.parent, Is.SameAs(_content));
                Assert.That(view.name, Does.Match(@"^ProductionMenuItem_\d+$"));
            }
        }

        [Test]
        public void Refresh_ScrolledOneRowDown_MovesTopRowToBottomWithoutCreating()
        {
            _menu.Init(Catalog("A", "B", "C", "D", "E", "F", "G"));
            ProductionMenuItemView top = _menu.Views[0];

            ScrollTo(_stride + 10f);

            Assert.That(_menu.Views[_menu.Views.Count - 1], Is.SameAs(top), "The row that left the top is reused at the bottom.");
            Assert.That(ShownNames(), Is.EqualTo(new[] { "B", "C", "D", "E", "F", "G" }));
            Assert.That(_menu.CreatedViewCount, Is.EqualTo(6));
        }

        [Test]
        public void Refresh_ScrolledAboveStart_ShowsLastBuildingOnTop()
        {
            _menu.Init(Catalog("A", "B", "C"));

            ScrollTo(-10f);

            Assert.That(ShownNames().First(), Is.EqualTo("C"));
            Assert.That(ShownNames().Skip(1).First(), Is.EqualTo("A"));
        }

        [Test]
        public void Refresh_FlingAcrossManyRows_ShowsCorrectWindowAndRebases()
        {
            _menu.Init(Catalog("A", "B", "C"));

            ScrollTo(37 * _stride + 5f);

            // Row 37 of a 3-item loop is item 37 % 3 = 1 ("B").
            Assert.That(ShownNames(), Is.EqualTo(new[] { "B", "C", "A", "B", "C", "A" }));
            Assert.That(_content.anchoredPosition.y, Is.EqualTo(_stride + 5f).Within(0.01f), "Content is pulled back into the first cycle.");
            Assert.That(_menu.CreatedViewCount, Is.EqualTo(6));
        }

        [Test]
        public void Refresh_WhileDragging_DoesNotRebaseUntilRelease()
        {
            _menu.Init(Catalog("A", "B", "C"));
            _menu.OnBeginDrag(LeftButton());

            ScrollTo(10 * _stride + 30f);

            Assert.That(_content.anchoredPosition.y, Is.EqualTo(10 * _stride + 30f), "A drag owns the content position.");
            Assert.That(ShownNames().First(), Is.EqualTo("B"));

            // No manual Refresh: a release without inertia sends no scroll change, so OnEndDrag must rebase itself.
            _menu.OnEndDrag(LeftButton());

            Assert.That(_content.anchoredPosition.y, Is.EqualTo(_stride + 30f).Within(0.01f));
        }

        [Test]
        public void Refresh_RightButtonDrag_DoesNotBlockRebase()
        {
            _menu.Init(Catalog("A", "B", "C"));
            // ScrollRect ignores non-left drags, so they must not count as dragging here either.
            _menu.OnBeginDrag(new PointerEventData(null) { button = PointerEventData.InputButton.Right });

            ScrollTo(10 * _stride + 30f);

            Assert.That(_content.anchoredPosition.y, Is.EqualTo(_stride + 30f).Within(0.01f));
        }

        [Test]
        public void Rebase_KeepsEveryRowOnScreenWithTheSameBuilding()
        {
            _menu.Init(Catalog("A", "B", "C"));
            _menu.OnBeginDrag(LeftButton());
            ScrollTo(10 * _stride + 30f);
            var before = _menu.Views.Select(view => (view.Building.DisplayName, ScreenY(view))).ToArray();

            _menu.OnEndDrag(LeftButton());
            var after = _menu.Views.Select(view => (view.Building.DisplayName, ScreenY(view))).ToArray();

            Assert.That(after.Length, Is.EqualTo(before.Length));
            for (int i = 0; i < before.Length; i++)
            {
                Assert.That(after[i].DisplayName, Is.EqualTo(before[i].DisplayName), $"row {i}");
                Assert.That(after[i].Item2, Is.EqualTo(before[i].Item2).Within(0.01f), $"row {i}");
            }
        }

        [Test]
        public void UpdateViewCount_ViewportResized_GrowsAndShrinksThroughThePool()
        {
            _menu.Init(Catalog("A", "B", "C"));

            Resize(800f);
            int tall = InfiniteScrollMath.ViewCount(800f, _stride);
            Assert.That(_menu.Views, Has.Count.EqualTo(tall));
            Assert.That(_menu.CreatedViewCount, Is.EqualTo(tall));

            Resize(300f);
            int shortCount = InfiniteScrollMath.ViewCount(300f, _stride);
            Assert.That(_menu.Views, Has.Count.EqualTo(shortCount));
            Assert.That(_menu.CreatedViewCount, Is.EqualTo(tall), "Shrinking returns rows to the pool instead of destroying them.");
            Assert.That(_content.GetComponentsInChildren<ProductionMenuItemView>(true).Count(view => !view.gameObject.activeSelf), Is.EqualTo(tall - shortCount));

            Resize(800f);
            string[] loop = { "A", "B", "C" };
            Assert.That(_menu.CreatedViewCount, Is.EqualTo(tall), "Growing again reuses pooled rows.");
            Assert.That(ShownNames(), Is.EqualTo(Enumerable.Range(0, tall).Select(i => loop[i % loop.Length]).ToArray()));
        }

        [Test]
        public void ReleasedRow_IsResetAndRaisesNoEvents()
        {
            _menu.Init(Catalog("A", "B", "C"));
            ProductionMenuItemView last = _menu.Views[_menu.Views.Count - 1];
            int raised = 0;
            _menu.BuildRequested += _ => raised++;

            Resize(300f);
            last.HandleClick();

            Assert.That(last.Building, Is.Null);
            Assert.That(last.gameObject.activeSelf, Is.False);
            Assert.That(raised, Is.EqualTo(0));
        }

        [Test]
        public void ReusedRow_Click_RaisesBuildRequestedExactlyOnce()
        {
            _menu.Init(Catalog("A", "B", "C"));
            Resize(300f);
            Resize(ViewportHeight);
            var requested = new List<BuildingDefinition>();
            _menu.BuildRequested += requested.Add;
            ProductionMenuItemView reused = _menu.Views[_menu.Views.Count - 1];

            reused.HandleClick();

            Assert.That(requested, Is.EqualTo(new[] { reused.Building }), "A row taken from the pool twice must not be subscribed twice.");
        }

        [Test]
        public void Click_RaisesBuildRequestedWithTheRowsBuilding()
        {
            _menu.Init(Catalog("A", "B", "C"));
            BuildingDefinition requested = null;
            _menu.BuildRequested += building => requested = building;

            _menu.Views[1].HandleClick();

            Assert.That(requested, Is.Not.Null);
            Assert.That(requested.DisplayName, Is.EqualTo("B"));
        }

        [Test]
        public void Init_EmptyCatalog_CreatesNoRowsAndScrollingIsSafe()
        {
            _menu.Init(Catalog());

            Assert.That(_menu.Views, Is.Empty);
            Assert.DoesNotThrow(() => ScrollTo(500f));
            Assert.DoesNotThrow(() => Resize(900f));
        }

        [Test]
        public void Init_NullCatalog_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => _menu.Init(null));
        }

        [Test]
        public void Init_CatalogWithEmptyEntry_Throws()
        {
            BuildingCatalog catalog = Catalog("A", "B");
            var serialized = new SerializedObject(catalog);
            serialized.FindProperty("_buildings").GetArrayElementAtIndex(1).objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.Throws<InvalidOperationException>(() => _menu.Init(catalog));
        }

        [Test]
        public void Bind_BuildingWithoutSprite_HidesIconInsteadOfWhiteSquare()
        {
            _menu.Init(Catalog("A"));

            Image icon = _menu.Views[0].GetComponentsInChildren<Image>(true).Single(image => image.name == "Icon");

            Assert.That(icon.enabled, Is.False);
        }

        [Test]
        public void Init_Twice_Throws()
        {
            BuildingCatalog catalog = Catalog("A");
            _menu.Init(catalog);

            Assert.Throws<InvalidOperationException>(() => _menu.Init(catalog));
        }

        private BuildingCatalog Catalog(params string[] names)
        {
            var buildings = new BuildingDefinition[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                buildings[i] = ScriptableObject.CreateInstance<BuildingDefinition>();
                var building = new SerializedObject(buildings[i]);
                building.FindProperty("_displayName").stringValue = names[i];
                building.FindProperty("_footprint").vector2IntValue = new Vector2Int(i + 1, 2);
                building.ApplyModifiedPropertiesWithoutUndo();
                _assets.Add(buildings[i]);
            }

            var catalog = ScriptableObject.CreateInstance<BuildingCatalog>();
            var serialized = new SerializedObject(catalog);
            SerializedProperty list = serialized.FindProperty("_buildings");
            list.arraySize = buildings.Length;
            for (int i = 0; i < buildings.Length; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = buildings[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            _assets.Add(catalog);
            return catalog;
        }

        private void ScrollTo(float offset)
        {
            _content.anchoredPosition = new Vector2(0f, offset);
            _menu.Refresh();
        }

        private void Resize(float viewportHeight)
        {
            _root.sizeDelta = new Vector2(_root.sizeDelta.x, viewportHeight);
            _menu.UpdateViewCount();
        }

        private string[] ShownNames()
        {
            return _menu.Views.Select(view => view.Building.DisplayName).ToArray();
        }

        private RectTransform Row(int index)
        {
            return (RectTransform)_menu.Views[index].transform;
        }

        /// <summary>Row top relative to the viewport top: what the player actually sees.</summary>
        private float ScreenY(ProductionMenuItemView view)
        {
            return _content.anchoredPosition.y + ((RectTransform)view.transform).anchoredPosition.y;
        }

        private static PointerEventData LeftButton()
        {
            return new PointerEventData(null) { button = PointerEventData.InputButton.Left };
        }
    }
}
