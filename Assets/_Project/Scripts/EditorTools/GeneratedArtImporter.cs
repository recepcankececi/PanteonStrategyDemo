using System;
using UnityEditor;
using UnityEngine;

namespace StrategyDemo.EditorTools
{
    /// <summary>
    /// Import settings for the art written by <c>Tools/ArtGen</c>, applied by folder on every import. A regenerated or
    /// new PNG is correct the moment Unity sees it; nobody sets pixels per unit, pivot or 9-slice borders by hand.
    /// </summary>
    /// <remarks>
    /// Board sprites (buildings, soldiers, ground, cell highlight) use one cell = one world unit and a centred pivot,
    /// so a building's sprite covers exactly its footprint (ADR-007). UI sprites use the Canvas reference density and
    /// a fixed 9-slice border. Filtering is bilinear: the art is vector, drawn at non-integer zoom and rotated.
    /// </remarks>
    public sealed class GeneratedArtImporter : AssetPostprocessor
    {
        /// <summary>Folder the generator installs into; only textures below it are touched.</summary>
        public const string RootFolder = "Assets/_Project/Art/Generated/";

        /// <summary>Folder of the uGUI sprites.</summary>
        public const string UiFolder = RootFolder + "UI/";

        /// <summary>The ground tile; the only texture that repeats, so the only one that wraps.</summary>
        public const string GroundPath = RootFolder + "Board/Ground.png";

        /// <summary>Pixels per unit of board sprites; equals <c>GridConfig.CellPixelSize</c> (test-enforced).</summary>
        public const int BoardPixelsPerUnit = 64;

        /// <summary>Pixels per unit of UI sprites; equals the Canvas reference, so 1 art pixel = 1 reference pixel.</summary>
        public const int UiPixelsPerUnit = 100;

        /// <summary>9-slice border of every UI sprite on all sides; equals <c>SLICE_BORDER</c> in <c>Tools/ArtGen/src/ui.js</c>.</summary>
        public const int UiSliceBorder = 16;

        // Bump when the rules below change: Unity then re-imports every texture this postprocessor handles.
        private const uint Version = 1;

        /// <inheritdoc/>
        public override uint GetVersion()
        {
            return Version;
        }

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(RootFolder, StringComparison.Ordinal))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            bool isUi = assetPath.StartsWith(UiFolder, StringComparison.Ordinal);

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = isUi ? UiPixelsPerUnit : BoardPixelsPerUnit;
            importer.spriteBorder = isUi ? new Vector4(UiSliceBorder, UiSliceBorder, UiSliceBorder, UiSliceBorder) : Vector4.zero;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            // Repeat lets bilinear filtering at the tile edge blend with the opposite edge, which a seamless tile matches.
            importer.wrapMode = assetPath == GroundPath ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;

            // Pivot and mesh type are only reachable through the settings struct.
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            // Full Rect: tiled and sliced draw modes need it, and a rectangle is all these sprites are anyway.
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);
        }
    }
}
