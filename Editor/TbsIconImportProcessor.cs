using UnityEditor;
using UnityEngine;

namespace TBSplineS.Editor
{
    sealed class TbsIconImportProcessor : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("TBSplineS/Editor/Icons/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.GUI;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
        }

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFromAssetPaths)
        {
            if (TouchesIcons(imported) || TouchesIcons(moved)) TbsIcons.ClearMissing();
        }

        static bool TouchesIcons(string[] paths)
        {
            for (int i = 0; i < paths.Length; i++)
            {
                if (paths[i].Replace('\\', '/').Contains("/Editor/Icons/")) return true;
            }
            return false;
        }
    }
}
