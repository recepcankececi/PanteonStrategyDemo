using System.Linq;
using NUnit.Framework;
using StrategyDemo.Data;
using UnityEditor;
using UnityEngine;

namespace StrategyDemo.Tests.EditMode
{
    /// <summary>
    /// Guards the shipped data assets against the case specification (R1, R2, R5, R7, R8)
    /// and Can's M1 decisions (sprites, extra buildings, grid size).
    /// </summary>
    public class GameDataTests
    {
        private const string DataFolder = "Assets/_Project/ScriptableObjects/";
        private const string CatalogPath = DataFolder + "BuildingCatalog.asset";
        private const string GridConfigPath = DataFolder + "Grid/GridConfig.asset";
        private const int SoldierHealth = 10;

        // name, asset, HP, sprite texture, footprint w, footprint h
        private static readonly object[] Buildings =
        {
            new object[] { "Barracks", "Barracks", 100, "Barracks", 3, 4 },
            new object[] { "Power Plant", "PowerPlant", 50, "Tower", 2, 4 },
            new object[] { "House", "House", 60, "House1", 2, 3 },
            new object[] { "Archery", "Archery", 80, "Archery", 3, 4 },
            new object[] { "Monastery", "Monastery", 120, "Monastery", 3, 5 },
            new object[] { "Castle", "Castle", 200, "Castle", 5, 4 }
        };

        // asset, display name, damage, range, icon sprite
        private static readonly object[] Soldiers =
        {
            new object[] { "Soldier1", "Soldier 1", 10, 1, "Warrior_Idle_0" },
            new object[] { "Soldier2", "Soldier 2", 5, 1, "Lancer_Idle_0" },
            new object[] { "Soldier3", "Soldier 3", 2, 3, "Archer_Idle_0" }
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
            Assert.That(building.GetFootprint(_grid.CellPixelSize), Is.EqualTo(new Vector2Int(footprintX, footprintY)));
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

            Assert.That(produced, Is.EqualTo(new[] { "Soldier 1", "Soldier 2", "Soldier 3" }));
        }

        [Test]
        public void Barracks_SpawnCellIsJustOutsideItsFootprint()
        {
            BuildingDefinition barracks = LoadBuilding("Barracks");
            Vector2Int size = barracks.GetFootprint(_grid.CellPixelSize);
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
        public void Soldier_HasSpecifiedHealthDamageRangeAndIcon(string asset, string displayName, int damage, int range, string icon)
        {
            UnitDefinition soldier = AssetDatabase.LoadAssetAtPath<UnitDefinition>($"{DataFolder}Units/{asset}.asset");

            Assert.That(soldier, Is.Not.Null, asset);
            Assert.That(soldier.DisplayName, Is.EqualTo(displayName));
            Assert.That(soldier.MaxHealth, Is.EqualTo(SoldierHealth));
            Assert.That(soldier.Damage, Is.EqualTo(damage));
            Assert.That(soldier.AttackRange, Is.EqualTo(range));
            Assert.That(soldier.Icon, Is.Not.Null);
            Assert.That(soldier.Icon.name, Is.EqualTo(icon));
        }

        private static BuildingDefinition LoadBuilding(string asset)
        {
            var building = AssetDatabase.LoadAssetAtPath<BuildingDefinition>($"{DataFolder}Buildings/{asset}.asset");
            Assert.That(building, Is.Not.Null, asset);
            return building;
        }
    }
}
