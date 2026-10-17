using System;
using System.Collections.Generic;
using StrategyDemo.Data;
using StrategyDemo.Gameplay;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StrategyDemo.Tests.EditMode
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

            View = _gridObject.AddComponent<GridView>();
            var serializedView = new SerializedObject(View);
            serializedView.FindProperty("_ground").objectReferenceValue = ground;
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            _config = ScriptableObject.CreateInstance<GridConfig>();
            var serializedConfig = new SerializedObject(_config);
            serializedConfig.FindProperty("_width").intValue = width;
            serializedConfig.FindProperty("_height").intValue = height;
            serializedConfig.ApplyModifiedPropertiesWithoutUndo();
            View.Init(_config);
        }

        public GridView View { get; }

        /// <summary>Creates a definition with the given footprint and a small sprite; destroyed with the board.</summary>
        public BuildingDefinition Definition(string assetName, Vector2Int footprint)
        {
            var texture = new Texture2D(footprint.x, footprint.y) { name = assetName };
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, footprint.x, footprint.y), new Vector2(0.5f, 0f), 1f);
            var definition = ScriptableObject.CreateInstance<BuildingDefinition>();
            definition.name = assetName;
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_footprint").vector2IntValue = footprint;
            serialized.FindProperty("_sprite").objectReferenceValue = sprite;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            _owned.Add(texture);
            _owned.Add(sprite);
            _owned.Add(definition);
            return definition;
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
