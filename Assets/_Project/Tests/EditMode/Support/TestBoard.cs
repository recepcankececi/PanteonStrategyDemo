using System;
using System.Collections.Generic;
using StrategyDemo.Data;
using StrategyDemo.Gameplay.Board;
using StrategyDemo.Gameplay.Buildings;
using StrategyDemo.Gameplay.Effects;
using StrategyDemo.Gameplay.Units;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StrategyDemo.Tests.EditMode.Support
{
    /// <summary>
    /// A small initialised <see cref="GridView"/> and building definitions for tests that need world
    /// positions. Dispose destroys everything it created.
    /// </summary>
    internal sealed class TestBoard : IDisposable
    {
        private readonly GameObject _gridObject;
        private readonly GridConfig _config;
        private readonly List<Object> _owned = new List<Object>();

        public TestBoard(Vector3 origin, int width, int height)
        {
            _gridObject = new GameObject(nameof(TestBoard));
            _gridObject.transform.position = origin;
            var ground = new GameObject("Ground").AddComponent<SpriteRenderer>();
            ground.transform.SetParent(_gridObject.transform, false);
            ground.drawMode = SpriteDrawMode.Tiled;
            var edge = new GameObject("Edge").AddComponent<SpriteRenderer>();
            edge.transform.SetParent(_gridObject.transform, false);
            edge.drawMode = SpriteDrawMode.Sliced;

            View = _gridObject.AddComponent<GridView>();
            var serializedView = new SerializedObject(View);
            serializedView.FindProperty("_ground").objectReferenceValue = ground;
            serializedView.FindProperty("_edge").objectReferenceValue = edge;
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            _config = ScriptableObject.CreateInstance<GridConfig>();
            var serializedConfig = new SerializedObject(_config);
            serializedConfig.FindProperty("_width").intValue = width;
            serializedConfig.FindProperty("_height").intValue = height;
            serializedConfig.ApplyModifiedPropertiesWithoutUndo();
            View.Init(_config);
        }

        public GridView View { get; }

        /// <summary>Creates a definition with the given footprint, health and a small sprite; destroyed with the board.</summary>
        public BuildingDefinition Definition(string assetName, Vector2Int footprint, int health = 100)
        {
            var texture = new Texture2D(footprint.x, footprint.y) { name = assetName };
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, footprint.x, footprint.y), new Vector2(0.5f, 0.5f), 1f);
            var definition = ScriptableObject.CreateInstance<BuildingDefinition>();
            definition.name = assetName;
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_footprint").vector2IntValue = footprint;
            serialized.FindProperty("_sprite").objectReferenceValue = sprite;
            serialized.FindProperty("_maxHealth").intValue = health;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            _owned.Add(texture);
            _owned.Add(sprite);
            _owned.Add(definition);
            return definition;
        }

        /// <summary>Creates a producing building definition; destroyed with the board.</summary>
        public BuildingDefinition Producer(string assetName, Vector2Int footprint, params UnitDefinition[] units)
        {
            BuildingDefinition definition = Definition(assetName, footprint);
            var serialized = new SerializedObject(definition);
            SerializedProperty list = serialized.FindProperty("_producibleUnits");
            list.arraySize = units.Length;
            for (int i = 0; i < units.Length; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = units[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        /// <summary>
        /// Creates a soldier definition with a standing sprite and <paramref name="frameCount"/> walk frames; destroyed
        /// with the board.
        /// </summary>
        public UnitDefinition Unit(string assetName, int damage = 1, int frameCount = 2, int range = 1, float interval = 1f, int health = 10,
            bool firesTracer = false)
        {
            // One pixel per walk frame plus one for the standing sprite.
            var texture = new Texture2D(frameCount + 1, 1) { name = assetName };
            _owned.Add(texture);
            var definition = ScriptableObject.CreateInstance<UnitDefinition>();
            definition.name = assetName;
            _owned.Add(definition);

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_displayName").stringValue = assetName;
            serialized.FindProperty("_damage").intValue = damage;
            serialized.FindProperty("_attackRange").intValue = range;
            serialized.FindProperty("_attackInterval").floatValue = interval;
            serialized.FindProperty("_maxHealth").intValue = health;
            serialized.FindProperty("_firesTracer").boolValue = firesTracer;
            Sprite standing = Sprite.Create(texture, new Rect(frameCount, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            standing.name = $"{assetName}_Stand";
            _owned.Add(standing);
            serialized.FindProperty("_sprite").objectReferenceValue = standing;
            SerializedProperty frames = serialized.FindProperty("_walkFrames");
            frames.arraySize = frameCount;
            for (int i = 0; i < frameCount; i++)
            {
                Sprite frame = Sprite.Create(texture, new Rect(i, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
                frame.name = $"{assetName}_Walk_{i}";
                _owned.Add(frame);
                frames.GetArrayElementAtIndex(i).objectReferenceValue = frame;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        /// <summary>
        /// Builds a soldier like the Unit prefab: <see cref="Unit"/> on the root, renderer, flipbook and hit flash on a
        /// "Visual" child (the first child), health bar on a "HealthBar" child. The caller destroys the returned object.
        /// </summary>
        public static Unit CreateUnitObject(string name)
        {
            var root = new GameObject(name);
            var visual = new GameObject("Visual", typeof(SpriteRenderer), typeof(SpriteFlipbook), typeof(HitFlash));
            visual.transform.SetParent(root.transform, false);
            HealthBar healthBar = CreateHealthBar(root.transform);
            var unit = root.AddComponent<Unit>();
            var serialized = new SerializedObject(unit);
            serialized.FindProperty("_visual").objectReferenceValue = visual.GetComponent<SpriteFlipbook>();
            serialized.FindProperty("_healthBar").objectReferenceValue = healthBar;
            serialized.FindProperty("_hitFlash").objectReferenceValue = visual.GetComponent<HitFlash>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return unit;
        }

        /// <summary>
        /// Builds a building like the Building prefab: renderer, hit flash and <see cref="Building"/> on the root,
        /// health bar on a "HealthBar" child, spawn marker on a "SpawnMarker" child. The caller destroys the returned object.
        /// </summary>
        public static Building CreateBuildingObject(string name)
        {
            var root = new GameObject(name, typeof(SpriteRenderer), typeof(HitFlash));
            HealthBar healthBar = CreateHealthBar(root.transform);
            var spawnMarker = new GameObject("SpawnMarker", typeof(SpriteRenderer));
            spawnMarker.transform.SetParent(root.transform, false);
            var building = root.AddComponent<Building>();
            var serialized = new SerializedObject(building);
            serialized.FindProperty("_healthBar").objectReferenceValue = healthBar;
            serialized.FindProperty("_hitFlash").objectReferenceValue = root.GetComponent<HitFlash>();
            serialized.FindProperty("_spawnMarker").objectReferenceValue = spawnMarker.GetComponent<SpriteRenderer>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return building;
        }

        private static HealthBar CreateHealthBar(Transform parent)
        {
            var bar = new GameObject("HealthBar");
            bar.transform.SetParent(parent, false);
            var background = new GameObject("Background", typeof(SpriteRenderer));
            background.transform.SetParent(bar.transform, false);
            var fill = new GameObject("Fill", typeof(SpriteRenderer));
            fill.transform.SetParent(bar.transform, false);
            var healthBar = bar.AddComponent<HealthBar>();
            var serialized = new SerializedObject(healthBar);
            serialized.FindProperty("_background").objectReferenceValue = background.GetComponent<SpriteRenderer>();
            serialized.FindProperty("_fill").objectReferenceValue = fill.GetComponent<SpriteRenderer>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return healthBar;
        }

        public void Dispose()
        {
            foreach (Object owned in _owned)
            {
                Object.DestroyImmediate(owned);
            }

            Object.DestroyImmediate(_gridObject);
            Object.DestroyImmediate(_config);
        }
    }
}
