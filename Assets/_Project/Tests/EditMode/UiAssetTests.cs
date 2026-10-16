using System.Linq;
using NUnit.Framework;
using StrategyDemo.EditorTools;
using StrategyDemo.UI;
using TMPro;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine.UI;

namespace StrategyDemo.Tests.EditMode
{
    /// <summary>UI assets outside the scene: the UI atlas contents, the menu row prefab and TMP resources.</summary>
    public class UiAssetTests
    {
        private const string UiAtlasPath = "Assets/_Project/Art/Atlases/UIAtlas.spriteatlasv2";
        private const string ItemPrefabPath = "Assets/_Project/Prefabs/UI/ProductionMenuItem.prefab";
        private const string ScriptPropertyName = "m_Script";

        [Test]
        public void UiAtlas_PacksExactlyTheStitchedPanels()
        {
            // Read the .spriteatlasv2 file itself: SpriteAtlas.GetPackables() reflects in-memory edits that
            // were never saved, so it can pass while the asset on disk still packs something else.
            SpriteAtlasAsset atlas = SpriteAtlasAsset.Load(UiAtlasPath);
            SerializedProperty packables = new SerializedObject(atlas).FindProperty("m_ImporterData.packables");
            string[] packed = Enumerable.Range(0, packables.arraySize)
                .Select(i => AssetDatabase.GetAssetPath(packables.GetArrayElementAtIndex(i).objectReferenceValue))
                .ToArray();
            string[] expected = PanelSheetStitcher.SourceSheets.Select(PanelSheetStitcher.OutputPathFor).ToArray();

            // Only sprites the UI uses: a folder entry would pack every unused Tiny Swords sheet too.
            Assert.That(packed, Is.EquivalentTo(expected));
        }

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

        [Test]
        public void ItemPrefab_BackgroundUsesStitchedPaper()
        {
            var image = AssetDatabase.LoadAssetAtPath<ProductionMenuItemView>(ItemPrefabPath).GetComponent<Image>();

            Assert.That(image.sprite, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(image.sprite), Is.EqualTo(PanelSheetStitcher.OutputPathFor(PanelSheetStitcher.SourceSheets[0])));
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
