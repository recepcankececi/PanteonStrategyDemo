using System.Linq;
using NUnit.Framework;
using StrategyDemo.Data;
using StrategyDemo.Gameplay.Buildings;
using StrategyDemo.Gameplay.Combat;
using StrategyDemo.Gameplay.Effects;
using StrategyDemo.Gameplay.Units;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StrategyDemo.Tests.EditMode.Project
{
    /// <summary>
    /// Assets and scene objects combat relies on: health bars and hit flashes on both prefabs, driver, pool; the
    /// pooled effect prefab, its settings and its scene objects.
    /// </summary>
    public class CombatAssetTests
    {
        private const string MainScenePath = "Assets/_Project/Scenes/Main.unity";
        private const string UnitPrefabPath = "Assets/_Project/Prefabs/Board/Unit.prefab";
        private const string BuildingPrefabPath = "Assets/_Project/Prefabs/Board/Building.prefab";
        private const string HealthBarSpritePath = "Assets/_Project/Art/Generated/Board/HealthBar.png";
        private const string EffectPrefabPath = "Assets/_Project/Prefabs/Board/Effect.prefab";
        private const string EffectSettingsPath = "Assets/_Project/ScriptableObjects/Effects/CombatEffectSettings.asset";
        private const string UnitDataFolder = "Assets/_Project/ScriptableObjects/Units";
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
            Assert.That(bar.transform.parent, Is.SameAs(prefab.transform), "On the root, which never rotates.");
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

        [Test]
        public void EffectPrefab_IsOnePlainUnlitRendererOnFx()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EffectPrefabPath);

            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponents<Component>().Length, Is.EqualTo(2), "Transform and SpriteRenderer only.");
            var renderer = prefab.GetComponent<SpriteRenderer>();
            Assert.That(renderer.sortingLayerName, Is.EqualTo(FxLayer));
            Assert.That(renderer.sharedMaterial.name, Is.EqualTo(UnlitSpriteMaterial), "Same material as the board: it batches.");
            Assert.That(renderer.drawMode, Is.EqualTo(SpriteDrawMode.Simple), "Stretched by scale, not by a tiled/sliced mesh.");
        }

        [Test]
        public void EffectSettings_UseGeneratedBoardSprites()
        {
            var settings = AssetDatabase.LoadAssetAtPath<CombatEffectSettings>(EffectSettingsPath);

            Assert.That(settings, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(settings.TracerSprite), Is.EqualTo("Assets/_Project/Art/Generated/Board/Tracer.png"));
            Assert.That(AssetDatabase.GetAssetPath(settings.BurstSprite), Is.EqualTo("Assets/_Project/Art/Generated/Board/DeathBurst.png"));
            Assert.That(settings.BurstEndScale, Is.GreaterThan(settings.BurstStartScale), "The burst grows.");
        }

        [Test]
        public void UnitData_EverySoldierIsRangedAndFiresTracers()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(UnitDefinition), new[] { UnitDataFolder }))
            {
                var unit = AssetDatabase.LoadAssetAtPath<UnitDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                // No melee soldier: every one shoots from at least two cells away, and every shot shows as a tracer.
                Assert.That(unit.AttackRange, Is.GreaterThanOrEqualTo(2), unit.name);
                Assert.That(unit.FiresTracer, Is.True, unit.name);
            }
        }

        [Test]
        public void Board_HasCombatEffectsWithItsPool()
        {
            Transform board = Root(BoardGroup).transform;
            Transform effects = board.Find("Effects");
            Transform pool = board.Find("Pool_Effect");

            Assert.That(effects, Is.Not.Null);
            Assert.That(pool, Is.Not.Null, "Pool parents are named Pool_<Type> (CLAUDE.md).");
            var component = effects.GetComponent<CombatEffects>();
            Assert.That(component, Is.Not.Null);
            var serialized = new SerializedObject(component);
            Assert.That(AssetDatabase.GetAssetPath(serialized.FindProperty("_effectPrefab").objectReferenceValue), Is.EqualTo(EffectPrefabPath));
            Assert.That(serialized.FindProperty("_poolRoot").objectReferenceValue, Is.SameAs(pool));
            Assert.That(AssetDatabase.GetAssetPath(serialized.FindProperty("_settings").objectReferenceValue), Is.EqualTo(EffectSettingsPath));
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
