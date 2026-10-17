using System;
using NUnit.Framework;
using StrategyDemo.Core;
using StrategyDemo.Data;
using StrategyDemo.Gameplay;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StrategyDemo.Tests.EditMode
{
    /// <summary>The single reused preview: visibility, position, footprint overlay and green/red tint (R2).</summary>
    public class PlacementGhostTests
    {
        private static readonly Vector3 BoardOrigin = new Vector3(-4f, 1f, 0f);

        private TestBoard _board;
        private BuildingPlacer _placer;
        private GameObject _ghostObject;
        private PlacementGhost _ghost;
        private SpriteRenderer _buildingRenderer;
        private SpriteRenderer _footprintRenderer;
        private BuildingDefinition _barracks;
        private BuildingDefinition _powerPlant;

        [SetUp]
        public void CreateGhost()
        {
            _board = new TestBoard(BoardOrigin, 10, 8);
            _barracks = _board.Definition("Barracks", new Vector2Int(3, 2));
            _powerPlant = _board.Definition("PowerPlant", new Vector2Int(2, 2));
            _placer = new BuildingPlacer(new GridMap(10, 8), new OccupantRegistry(), new NullBuildingFactory());

            _ghostObject = new GameObject(nameof(PlacementGhostTests));
            _buildingRenderer = _ghostObject.AddComponent<SpriteRenderer>();
            _footprintRenderer = new GameObject("FootprintOverlay").AddComponent<SpriteRenderer>();
            _footprintRenderer.transform.SetParent(_ghostObject.transform, false);
            _footprintRenderer.drawMode = SpriteDrawMode.Tiled;
            _ghost = _ghostObject.AddComponent<PlacementGhost>();
            var serialized = new SerializedObject(_ghost);
            serialized.FindProperty("_buildingRenderer").objectReferenceValue = _buildingRenderer;
            serialized.FindProperty("_footprintRenderer").objectReferenceValue = _footprintRenderer;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void DestroyGhost()
        {
            Object.DestroyImmediate(_ghostObject);
            _board.Dispose();
        }

        [Test]
        public void Init_NotPlacing_GhostHidden()
        {
            _ghost.Init(_placer, _board.View);

            Assert.That(_buildingRenderer.enabled, Is.False);
            Assert.That(_footprintRenderer.enabled, Is.False);
        }

        [Test]
        public void Init_Twice_Throws()
        {
            _ghost.Init(_placer, _board.View);

            Assert.Throws<InvalidOperationException>(() => _ghost.Init(_placer, _board.View));
        }

        [Test]
        public void Begin_WithoutCandidate_StaysHidden()
        {
            _ghost.Init(_placer, _board.View);

            _placer.Begin(_barracks);

            Assert.That(_buildingRenderer.enabled, Is.False, "Hidden until the pointer reaches the board.");
        }

        [Test]
        public void ValidCandidate_ShowsBuildingAtFootprintCentreWithGreenTint()
        {
            _ghost.Init(_placer, _board.View);
            _placer.Begin(_barracks);

            _placer.MoveTo(new Vector2Int(2, 3));

            Assert.That(_buildingRenderer.enabled, Is.True);
            Assert.That(_footprintRenderer.enabled, Is.True);
            Assert.That(_buildingRenderer.sprite, Is.SameAs(_barracks.Sprite));
            // Centre of cells (2..4, 3..4): x = 2 + 1.5, y = 3 + 1, offset by the board origin.
            Assert.That(_ghostObject.transform.position, Is.EqualTo(BoardOrigin + new Vector3(3.5f, 4f, 0f)));
            Assert.That(_footprintRenderer.size, Is.EqualTo(new Vector2(3f, 2f)));
            Assert.That(_footprintRenderer.transform.localPosition, Is.EqualTo(Vector3.zero), "Overlay and building share the centre.");
            Assert.That(_buildingRenderer.color, Is.EqualTo(Tint("_validBuildingTint")));
            Assert.That(_footprintRenderer.color, Is.EqualTo(Tint("_validFootprintTint")));
        }

        [Test]
        public void InvalidCandidate_ShowsRedTint()
        {
            _ghost.Init(_placer, _board.View);
            _placer.Begin(_barracks);

            _placer.MoveTo(new Vector2Int(-1, 0));

            Assert.That(_buildingRenderer.enabled, Is.True, "Invalid spots are shown, not hidden (R2).");
            Assert.That(_buildingRenderer.color, Is.EqualTo(Tint("_invalidBuildingTint")));
            Assert.That(_footprintRenderer.color, Is.EqualTo(Tint("_invalidFootprintTint")));
        }

        [Test]
        public void SwitchingBuilding_ReusesTheSameGhostWithTheNewSpriteAndFootprint()
        {
            _ghost.Init(_placer, _board.View);
            _placer.Begin(_barracks);
            _placer.MoveTo(new Vector2Int(2, 3));

            _placer.Begin(_powerPlant);
            _placer.MoveTo(new Vector2Int(5, 5));

            Assert.That(_buildingRenderer.sprite, Is.SameAs(_powerPlant.Sprite));
            Assert.That(_footprintRenderer.size, Is.EqualTo(new Vector2(2f, 2f)));
        }

        [Test]
        public void Cancel_HidesGhost()
        {
            _ghost.Init(_placer, _board.View);
            _placer.Begin(_barracks);
            _placer.MoveTo(new Vector2Int(2, 3));

            _placer.Cancel();

            Assert.That(_buildingRenderer.enabled, Is.False);
            Assert.That(_footprintRenderer.enabled, Is.False);
        }

        private Color Tint(string field)
        {
            return new SerializedObject(_ghost).FindProperty(field).colorValue;
        }
    }
}
