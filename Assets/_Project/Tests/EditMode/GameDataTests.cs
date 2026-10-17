using System.Linq;
using NUnit.Framework;
using StrategyDemo.Data;
using UnityEditor;
using UnityEngine;

namespace StrategyDemo.Tests.EditMode
{
    /// <summary>
    /// Guards the shipped data assets against the case specification (R1, R2, R5, R7, R8), the chosen
    /// building set and soldier types, and the art invariant (sprite = footprint, centred pivot).
    /// </summary>
    public class GameDataTests
    {
        private const string DataFolder = "Assets/_Project/ScriptableObjects/";
        private const string CatalogPath = DataFolder + "BuildingCatalog.asset";
        private const string GridConfigPath = DataFolder + "Grid/GridConfig.asset";
        private const int SoldierHealth = 10;

        // name, asset, HP, sprite, footprint w, footprint h (Barracks and Power Plant HP from R8).
        private static readonly object[] Buildings =
        {
            new object[] { "Barracks", "Barracks", 100, "Barracks", 3, 3 },
            new object[] { "Power Plant", "PowerPlant", 50, "PowerPlant", 2, 2 },
            new object[] { "Command Center", "CommandCenter", 200, "CommandCenter", 4, 4 },
            new object[] { "Supply Depot", "SupplyDepot", 60, "SupplyDepot", 2, 1 },
            new object[] { "Radar", "Radar", 80, "Radar", 2, 2 },
            new object[] { "Guard Tower", "GuardTower", 120, "GuardTower", 1, 1 }
        };

        // asset, display name, damage, range, standing sprite. R7's Soldier 1/2/3 are Assault, Rifleman and Scout.
        private static readonly object[] Soldiers =
        {
            new object[] { "Assault", "Assault", 10, 1, "Assault_Stand" },
            new object[] { "Rifleman", "Rifleman", 5, 1, "Rifleman_Stand" },
            new object[] { "Scout", "Scout", 2, 3, "Scout_Stand" }
        };

        private BuildingCatalog _catalog;
        private GridConfig _grid;

        [OneTimeSetUp]
        public void LoadAssets()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<BuildingCatalog>(CatalogPath);
            _grid = AssetDatabase.LoadAssetAtPath<GridConfig>(GridConfigPath);
            Assert.That(_catalog, Is.Not.Null, CatalogPath);
            Assert.That(_grid, Is.Not.Null, GridConfigPath);
        }

        [Test]
        public void GridConfig_Is24By16With64PixelCells()
        {
            Assert.That(_grid.Width, Is.EqualTo(24));
            Assert.That(_grid.Height, Is.EqualTo(16));
            Assert.That(_grid.CellPixelSize, Is.EqualTo(64));
        }

        [Test]
        public void Catalog_ListsAllBuildingsInMenuOrder()
        {
            string[] expected = Buildings.Cast<object[]>().Select(row => (string)row[0]).ToArray();

            Assert.That(_catalog.Buildings.Select(building => building.DisplayName), Is.EqualTo(expected));
        }

        [TestCaseSource(nameof(Buildings))]
        public void Building_HasSpecifiedNameHealthSpriteAndFootprint(
            string displayName, string asset, int health, string sprite, int footprintX, int footprintY)
        {
            BuildingDefinition building = LoadBuilding(asset);

            Assert.That(building.DisplayName, Is.EqualTo(displayName));
            Assert.That(building.MaxHealth, Is.EqualTo(health));
            Assert.That(building.Sprite, Is.Not.Null);
            // Sprite name, not texture name: once packed, Sprite.texture is the atlas page.
            Assert.That(building.Sprite.name, Is.EqualTo(sprite));
            Assert.That(building.Footprint, Is.EqualTo(new Vector2Int(footprintX, footprintY)));
        }

        [TestCaseSource(nameof(Buildings))]
        public void Building_SpriteIsExactlyFootprintTimesCellPixels(
            string displayName, string asset, int health, string sprite, int footprintX, int footprintY)
        {
            BuildingDefinition building = LoadBuilding(asset);

            // Art invariant: the image is its cells, no larger, so nothing draws outside them.
            Assert.That(building.Sprite.rect.size, Is.EqualTo((Vector2)building.Footprint * _grid.CellPixelSize));
        }

        [TestCaseSource(nameof(Buildings))]
        public void Building_SpritePivotIsFootprintCentre(
            string displayName, string asset, int health, string sprite, int footprintX, int footprintY)
        {
            Sprite image = LoadBuilding(asset).Sprite;

            // Centre: the factory puts the transform at the footprint centre, so the image covers exactly the cells.
            Assert.That(image.pivot, Is.EqualTo(image.rect.size * 0.5f));
        }

        [TestCaseSource(nameof(Buildings))]
        public void Building_SpriteUsesCellSizeAsPixelsPerUnit(
            string displayName, string asset, int health, string sprite, int footprintX, int footprintY)
        {
            // One cell = one world unit only holds if the art is imported at PPU = cell pixels.
            Assert.That(LoadBuilding(asset).Sprite.pixelsPerUnit, Is.EqualTo(_grid.CellPixelSize));
        }

        [Test]
        public void Barracks_IsTheOnlyProducer()
        {
            string[] producers = _catalog.Buildings.Where(building => building.CanProduceUnits)
                .Select(building => building.DisplayName).ToArray();

            Assert.That(producers, Is.EqualTo(new[] { "Barracks" }));
        }

        [Test]
        public void Barracks_ProducesTheThreeSoldiersInOrder()
        {
            string[] produced = LoadBuilding("Barracks").ProducibleUnits.Select(unit => unit.DisplayName).ToArray();

            Assert.That(produced, Is.EqualTo(new[] { "Assault", "Rifleman", "Scout" }));
        }

        [Test]
        public void Barracks_SpawnCellIsJustOutsideItsFootprint()
        {
            BuildingDefinition barracks = LoadBuilding("Barracks");
            Vector2Int size = barracks.Footprint;
            Vector2Int spawn = barracks.SpawnOffset;

            // Chebyshev distance from the spawn cell to the nearest footprint cell: 0 = inside, 1 = touching.
            int dx = Mathf.Max(0, -spawn.x, spawn.x - (size.x - 1));
            int dy = Mathf.Max(0, -spawn.y, spawn.y - (size.y - 1));

            Assert.That(Mathf.Max(dx, dy), Is.EqualTo(1), "Units must appear next to the Barracks, not inside or far away.");
        }

        [Test]
        public void Catalog_HasNoMissingOrDuplicateEntries()
        {
            Assert.That(_catalog.Buildings, Has.None.Null);
            Assert.That(_catalog.Buildings, Is.Unique);
        }

        [Test]
        public void ProducibleUnits_HaveNoMissingOrDuplicateEntries()
        {
            foreach (BuildingDefinition building in _catalog.Buildings)
            {
                Assert.That(building.ProducibleUnits, Has.None.Null, building.DisplayName);
                Assert.That(building.ProducibleUnits, Is.Unique, building.DisplayName);
            }
        }

        [TestCaseSource(nameof(Soldiers))]
        public void Soldier_HasSpecifiedHealthDamageRangeAndSprite(string asset, string displayName, int damage, int range, string sprite)
        {
            UnitDefinition soldier = AssetDatabase.LoadAssetAtPath<UnitDefinition>($"{DataFolder}Units/{asset}.asset");

            Assert.That(soldier, Is.Not.Null, asset);
            Assert.That(soldier.DisplayName, Is.EqualTo(displayName));
            Assert.That(soldier.MaxHealth, Is.EqualTo(SoldierHealth));
            Assert.That(soldier.Damage, Is.EqualTo(damage));
            Assert.That(soldier.AttackRange, Is.EqualTo(range));
            Assert.That(soldier.Sprite, Is.Not.Null);
            Assert.That(soldier.Sprite.name, Is.EqualTo(sprite));
        }

        private static BuildingDefinition LoadBuilding(string asset)
        {
            var building = AssetDatabase.LoadAssetAtPath<BuildingDefinition>($"{DataFolder}Buildings/{asset}.asset");
            Assert.That(building, Is.Not.Null, asset);
            return building;
        }
    }
}
