using System.Linq;
using NUnit.Framework;
using StrategyDemo.EditorTools;
using StrategyDemo.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace StrategyDemo.Tests.EditMode
{
    /// <summary>UI assets outside the scene: the menu and production row prefabs and TMP resources.</summary>
    public class UiAssetTests
    {
        private const string ItemPrefabPath = "Assets/_Project/Prefabs/UI/ProductionMenuItem.prefab";
        private const string UnitRowPrefabPath = "Assets/_Project/Prefabs/UI/UnitProductionItem.prefab";
        private const string RowSpritePath = GeneratedArtImporter.UiFolder + "Row.png";
        private const string ButtonSpritePath = GeneratedArtImporter.UiFolder + "Button.png";
        private const string ScriptPropertyName = "m_Script";

        [Test]
        public void ItemPrefab_HasEveryReferenceAssigned()
        {
            var view = AssetDatabase.LoadAssetAtPath<ProductionMenuItemView>(ItemPrefabPath);
            Assert.That(view, Is.Not.Null, ItemPrefabPath);

            SerializedProperty property = new SerializedObject(view).GetIterator();
            int checkedReferences = 0;
            for (bool enterChildren = true; property.NextVisible(enterChildren); enterChildren = false)
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference || property.name == ScriptPropertyName)
                {
                    continue;
                }

                Assert.That(property.objectReferenceValue, Is.Not.Null, $"{nameof(ProductionMenuItemView)}.{property.name} is unassigned.");
                checkedReferences++;
            }

            Assert.That(checkedReferences, Is.EqualTo(4));
        }

        [Test]
        public void ItemPrefab_OnlyTheButtonBackgroundReceivesClicks()
        {
            var view = AssetDatabase.LoadAssetAtPath<ProductionMenuItemView>(ItemPrefabPath);
            Graphic background = view.GetComponent<Button>().targetGraphic;

            // Icon and labels must not swallow the pointer, or clicks on them would miss the button.
            Graphic[] raycastTargets = view.GetComponentsInChildren<Graphic>(true).Where(graphic => graphic.raycastTarget).ToArray();

            Assert.That(raycastTargets, Is.EqualTo(new[] { background }));
        }

        [TestCase(ItemPrefabPath, RowSpritePath)]
        [TestCase(UnitRowPrefabPath, ButtonSpritePath)]
        public void RowPrefab_BackgroundIsSlicedGeneratedSprite(string prefabPath, string spritePath)
        {
            var image = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath).GetComponent<Image>();

            Assert.That(image.sprite, Is.Not.Null, prefabPath);
            Assert.That(AssetDatabase.GetAssetPath(image.sprite), Is.EqualTo(spritePath));
            Assert.That(image.type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(image.pixelsPerUnitMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void TextMeshPro_EssentialResourcesAreImported()
        {
            // Without TMP Essentials every label renders nothing and logs a missing-font error.
            Assert.That(TMP_Settings.instance, Is.Not.Null);
            Assert.That(TMP_Settings.defaultFontAsset, Is.Not.Null);
        }
    }
}
