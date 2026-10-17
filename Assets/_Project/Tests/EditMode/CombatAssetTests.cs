using System.Linq;
using NUnit.Framework;
using StrategyDemo.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StrategyDemo.Tests.EditMode
{
    /// <summary>Assets and scene objects combat relies on (M6): health bars and hit flashes on both prefabs, driver, pool.</summary>
    public class CombatAssetTests
    {
        private const string MainScenePath = "Assets/_Project/Scenes/Main.unity";
        private const string UnitPrefabPath = "Assets/_Project/Prefabs/Board/Unit.prefab";
        private const string BuildingPrefabPath = "Assets/_Project/Prefabs/Board/Building.prefab";
        private const string HealthBarSpritePath = "Assets/_Project/Art/Generated/Board/HealthBar.png";
        private const string SystemsGroup = "--- Systems ---";
        private const string BoardGroup = "--- Board ---";
        private const string FxLayer = "FX";
        private const string UnlitSpriteMaterial = "Sprite-Unlit-Default";

        private Scene _scene;

        [OneTimeSetUp]
        public void OpenMainScene()
        {
            _scene = EditorSceneManager.OpenPreviewScene(MainScenePath);
        }

        [OneTimeTearDown]
        public void CloseMainScene()
        {
            EditorSceneManager.ClosePreviewScene(_scene);
        }

        [TestCase(UnitPrefabPath)]
        [TestCase(BuildingPrefabPath)]
        public void Prefab_HasUprightHealthBarOnFxFromWorldAtlasSprite(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Component owner = Owner(prefab);
            var bar = new SerializedObject(owner).FindProperty("_healthBar").objectReferenceValue as HealthBar;

            Assert.That(bar, Is.Not.Null, path);
            Assert.That(bar.transform.parent, Is.SameAs(prefab.transform), "On the root, which never rotates (ADR-007).");
            var serializedBar = new SerializedObject(bar);
            var background = (SpriteRenderer)serializedBar.FindProperty("_background").objectReferenceValue;
            var fill = (SpriteRenderer)serializedBar.FindProperty("_fill").objectReferenceValue;
            foreach (SpriteRenderer part in new[] { background, fill })
            {
                Assert.That(part, Is.Not.Null, path);
                Assert.That(AssetDatabase.GetAssetPath(part.sprite), Is.EqualTo(HealthBarSpritePath), part.name);
                Assert.That(part.sortingLayerName, Is.EqualTo(FxLayer), part.name);
                Assert.That(part.sharedMaterial.name, Is.EqualTo(UnlitSpriteMaterial), "Same material as the board: it batches.");
                Assert.That(part.enabled, Is.False, "Hidden until damaged or selected.");
            }

            Assert.That(fill.sortingOrder, Is.GreaterThan(background.sortingOrder));
        }

        [TestCase(UnitPrefabPath)]
        [TestCase(BuildingPrefabPath)]
        public void Prefab_HitFlashSitsOnTheObjectsRendererAndStartsDisabled(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Component owner = Owner(prefab);
            var flash = new SerializedObject(owner).FindProperty("_hitFlash").objectReferenceValue as HitFlash;

            Assert.That(flash, Is.Not.Null, path);
            Assert.That(flash.GetComponent<SpriteRenderer>().sortingLayerName, Is.EqualTo("Objects"));
            Assert.That(flash.enabled, Is.False, "No Update until the first hit.");
        }

        [Test]
        public void SystemsGroup_HasCombatDriverRunningAfterMovement()
        {
            var driver = Root(SystemsGroup).GetComponentInChildren<UnitCombatDriver>(true);

            Assert.That(driver, Is.Not.Null);
            Assert.That(driver.name, Is.EqualTo("UnitCombat"));
            var order = (DefaultExecutionOrder)System.Attribute.GetCustomAttribute(typeof(UnitCombatDriver), typeof(DefaultExecutionOrder));
            Assert.That(order.order, Is.GreaterThan(0).And.LessThan(100), "After movement (0), before input (100).");
        }

        [Test]
        public void Board_HasBuildingPool()
        {
            Assert.That(Root(BoardGroup).transform.Find("Pool_Building"), Is.Not.Null, "Pool parents are named Pool_<Type> (CLAUDE.md).");
        }

        private static Component Owner(GameObject prefab)
        {
            // Not "??": a missing component can be Unity's fake null in the editor, which "??" does not see as null.
            var unit = prefab.GetComponent<Unit>();
            return unit != null ? (Component)unit : prefab.GetComponent<Building>();
        }

        private GameObject Root(string name)
        {
            GameObject root = _scene.GetRootGameObjects().FirstOrDefault(candidate => candidate.name == name);
            Assert.That(root, Is.Not.Null, $"Root group '{name}' is missing.");
            return root;
        }
    }
}
