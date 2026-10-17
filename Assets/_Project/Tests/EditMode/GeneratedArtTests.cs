using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using StrategyDemo.Data;
using StrategyDemo.EditorTools;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;

namespace StrategyDemo.Tests.EditMode
{
    /// <summary>
    /// The generated art and its atlases (ADR-007): import settings, the soldier rotation rule, atlases packing only
    /// sprites the game uses, and no third-party art left in the project.
    /// </summary>
    public class GeneratedArtTests
    {
        private const string BuildingsFolder = GeneratedArtImporter.RootFolder + "Buildings";
        private const string UnitsFolder = GeneratedArtImporter.RootFolder + "Units";
        private const string UiFolder = GeneratedArtImporter.RootFolder + "UI";
        private const string CellHighlightPath = GeneratedArtImporter.RootFolder + "Board/CellHighlight.png";
        private const string BoardEdgePath = GeneratedArtImporter.BoardEdgePath;
        private const string WorldAtlasPath = "Assets/_Project/Art/Atlases/WorldAtlas.spriteatlasv2";
        private const string UiAtlasPath = "Assets/_Project/Art/Atlases/UIAtlas.spriteatlasv2";
        private const string CatalogPath = "Assets/_Project/ScriptableObjects/BuildingCatalog.asset";
        private const string GridConfigPath = "Assets/_Project/ScriptableObjects/Grid/GridConfig.asset";
        private const string UnitDataFolder = "Assets/_Project/ScriptableObjects/Units";
        private const string ThirdPartyFolder = "Assets/_Project/Art/ThirdParty";
        private const float HalfPixelDiagonal = 0.7071068f;

        [Test]
        public void BoardPixelsPerUnit_EqualsGridCellSize()
        {
            var grid = AssetDatabase.LoadAssetAtPath<GridConfig>(GridConfigPath);

            Assert.That(GeneratedArtImporter.BoardPixelsPerUnit, Is.EqualTo(grid.CellPixelSize));
        }

        [Test]
        public void EveryGeneratedTexture_IsCentredBilinearSpriteAtItsFolderDensity()
        {
            string[] paths = TexturePathsUnder(GeneratedArtImporter.RootFolder.TrimEnd('/'));
            Assert.That(paths, Is.Not.Empty);

            foreach (string path in paths)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                bool isUi = path.StartsWith(GeneratedArtImporter.UiFolder);

                Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite), path);
                Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(isUi ? GeneratedArtImporter.UiPixelsPerUnit : GeneratedArtImporter.BoardPixelsPerUnit), path);
                Assert.That(settings.spriteAlignment, Is.EqualTo((int)SpriteAlignment.Center), path);
                Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect), path);
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear), path);
                Assert.That(importer.mipmapEnabled, Is.False, path);
            }
        }

        [Test]
        public void BoardEdge_IsSlicedFrameWithOnePixelLinesOnRightAndBottomOnly()
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BoardEdgePath);
            Assert.That(sprite, Is.Not.Null);
            Assert.That(sprite.border, Is.EqualTo(Vector4.one * GeneratedArtImporter.BoardEdgeSliceBorder));

            // Read the pixels from the PNG itself: the imported texture is not CPU-readable.
            var texture = new Texture2D(2, 2);
            try
            {
                texture.LoadImage(File.ReadAllBytes(BoardEdgePath));
                int last = texture.width - 1;
                // Texture rows start at the bottom: y = 0 is the bottom edge.
                Assert.That(texture.GetPixel(last, last / 2).a, Is.GreaterThan(0f), "Right edge line.");
                Assert.That(texture.GetPixel(last / 2, 0).a, Is.GreaterThan(0f), "Bottom edge line.");
                Assert.That(texture.GetPixel(0, last / 2).a, Is.Zero, "Left edge stays clear: the ground tile draws it.");
                Assert.That(texture.GetPixel(last / 2, last).a, Is.Zero, "Top edge stays clear: the ground tile draws it.");
                Assert.That(texture.GetPixel(last / 2, last / 2).a, Is.Zero, "The stretched centre is transparent.");
                // The pixel inside each line is clear, so bilinear sampling of the stretched centre never reaches the line.
                Assert.That(texture.GetPixel(last - 1, last / 2).a, Is.Zero, "Clear pixel inside the right line.");
                Assert.That(texture.GetPixel(last / 2, 1).a, Is.Zero, "Clear pixel inside the bottom line.");
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void UiSprites_HaveTheSliceBorderOnAllSides()
        {
            foreach (string path in TexturePathsUnder(UiFolder))
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                float border = GeneratedArtImporter.UiSliceBorder;

                Assert.That(sprite.border, Is.EqualTo(new Vector4(border, border, border, border)), path);
            }
        }

        [Test]
        public void UnitSprites_StayInsideTheCellsInscribedCircle()
        {
            foreach (string path in TexturePathsUnder(UnitsFolder))
            {
                // Read the PNG itself: the imported texture is not readable, and its atlas copy is padded.
                var texture = new Texture2D(2, 2);
                try
                {
                    Assert.That(texture.LoadImage(File.ReadAllBytes(path)), Is.True, path);
                    Assert.That(texture.width, Is.EqualTo(GeneratedArtImporter.BoardPixelsPerUnit), path);
                    Assert.That(texture.height, Is.EqualTo(GeneratedArtImporter.BoardPixelsPerUnit), path);

                    // A soldier rotates to its heading; only art inside this circle stays inside the cell at every angle.
                    float radius = texture.width * 0.5f;
                    Color32[] pixels = texture.GetPixels32();
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        if (pixels[i].a == 0)
                        {
                            continue;
                        }

                        // The pixel's farthest corner, not its centre, must lie inside the circle.
                        var centre = new Vector2(i % texture.width + 0.5f, i / texture.width + 0.5f);
                        float farthestCorner = Vector2.Distance(centre, new Vector2(radius, radius)) + HalfPixelDiagonal;
                        Assert.That(farthestCorner, Is.LessThanOrEqualTo(radius), $"{path} pixel {centre}");
                    }
                }
                finally
                {
                    Object.DestroyImmediate(texture);
                }
            }
        }

        [Test]
        public void Soldiers_WalkFramesMatchStandingSprite()
        {
            foreach (UnitDefinition unit in UnitDefinitions())
            {
                Assert.That(unit.WalkFrames.Count, Is.EqualTo(2), unit.name);
                Assert.That(unit.WalkFramesPerSecond, Is.GreaterThan(0f), unit.name);
                foreach (Sprite frame in unit.WalkFrames)
                {
                    // Equal rects and pivots keep the cycle from jittering against the standing pose.
                    Assert.That(frame, Is.Not.Null, unit.name);
                    Assert.That(frame.rect.size, Is.EqualTo(unit.Sprite.rect.size), frame.name);
                    Assert.That(frame.pivot, Is.EqualTo(unit.Sprite.pivot), frame.name);
                }
            }
        }

        [Test]
        public void WorldAtlas_PacksExactlyTheBoardSpritesInUse()
        {
            // The ground is not packed: it tiles, and bilinear sampling at a tile edge would blend in atlas padding.
            Assert.That(Packables(WorldAtlasPath), Is.EquivalentTo(new[] { BuildingsFolder, UnitsFolder, CellHighlightPath, BoardEdgePath }));

            // The cell highlight (selection, placement, drag box) and the board edge are used by the scene, not by data.
            var used = new HashSet<string> { CellHighlightPath, BoardEdgePath };
            foreach (BuildingDefinition building in AssetDatabase.LoadAssetAtPath<BuildingCatalog>(CatalogPath).Buildings)
            {
                used.Add(AssetDatabase.GetAssetPath(building.Sprite));
            }

            foreach (UnitDefinition unit in UnitDefinitions())
            {
                used.Add(AssetDatabase.GetAssetPath(unit.Sprite));
                used.UnionWith(unit.WalkFrames.Select(AssetDatabase.GetAssetPath));
            }

            // Folder entries pack everything inside them, so the folders must hold exactly what the data references.
            string[] packed = TexturePathsUnder(BuildingsFolder).Concat(TexturePathsUnder(UnitsFolder))
                .Append(CellHighlightPath).Append(BoardEdgePath).ToArray();
            Assert.That(packed, Is.EquivalentTo(used));
        }

        [Test]
        public void UiAtlas_PacksTheGeneratedUiFolder()
        {
            Assert.That(Packables(UiAtlasPath), Is.EqualTo(new[] { UiFolder }));
        }

        [Test]
        public void ThirdPartyArt_IsGone()
        {
            Assert.That(AssetDatabase.IsValidFolder(ThirdPartyFolder), Is.False, "M4.5 replaced Tiny Swords with generated art.");
        }

        private static string[] Packables(string atlasPath)
        {
            // Read the .spriteatlasv2 file itself: SpriteAtlas.GetPackables() reflects in-memory edits that were never saved.
            SpriteAtlasAsset atlas = SpriteAtlasAsset.Load(atlasPath);
            SerializedProperty packables = new SerializedObject(atlas).FindProperty("m_ImporterData.packables");
            return Enumerable.Range(0, packables.arraySize)
                .Select(i => AssetDatabase.GetAssetPath(packables.GetArrayElementAtIndex(i).objectReferenceValue))
                .ToArray();
        }

        private static string[] TexturePathsUnder(string folder)
        {
            return AssetDatabase.FindAssets("t:Texture2D", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
        }

        private static IEnumerable<UnitDefinition> UnitDefinitions()
        {
            string[] guids = AssetDatabase.FindAssets("t:" + nameof(UnitDefinition), new[] { UnitDataFolder });
            Assert.That(guids, Is.Not.Empty);
            return guids.Select(guid => AssetDatabase.LoadAssetAtPath<UnitDefinition>(AssetDatabase.GUIDToAssetPath(guid)));
        }
    }
}
