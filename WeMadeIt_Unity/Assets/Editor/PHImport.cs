// Import rules for the Poly Haven (CC0) assets under Assets/Resources/PH and our own Blender models.
// Normal maps as normal maps, data maps linear, the sky as a cubemap, models without Unity's own
// material guesses (WellLook builds HDRP materials from the textures by name).
using UnityEditor;
using UnityEngine;

public class PHImport : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (assetPath.Contains("/Resources/Mixamo/"))
        {
            var mt = (TextureImporter)assetImporter;
            string q = assetPath.ToLowerInvariant();
            if (q.Contains("normal")) mt.textureType = TextureImporterType.NormalMap;
            else if (q.Contains("specular") || q.Contains("gloss") || q.Contains("rough") || q.Contains("metal")) mt.sRGBTexture = false;
            mt.maxTextureSize = 2048;
            if (q.Contains("_diffusea") || q.Contains("ch02_1002_diffuse"))
            {
                mt.alphaIsTransparency = true;
                mt.mipMapsPreserveCoverage = true;     // hair keeps its coverage at distance
                mt.alphaTestReferenceValue = 0.4f;
            }
            return;
        }
        if (!assetPath.Contains("/Resources/PH/")) return;
        var ti = (TextureImporter)assetImporter;
        string p = assetPath.ToLowerInvariant();
        ti.mipmapEnabled = true;
        ti.anisoLevel = 8;
        ti.textureCompression = TextureImporterCompression.CompressedHQ;
        if (p.Contains("/hdri/"))
        {
            ti.textureShape = TextureImporterShape.TextureCube;
            ti.generateCubemap = TextureImporterGenerateCubemap.AutoCubemap;
            ti.sRGBTexture = false;
            ti.maxTextureSize = 4096;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            return;
        }
        if (p.Contains("nor_gl"))
        {
            ti.textureType = TextureImporterType.NormalMap;
            return;
        }
        if (p.Contains("_mask") || p.Contains("rough") || p.Contains("_metal") || p.Contains("_alpha") || p.Contains("_ao") || p.Contains("_spec"))
            ti.sRGBTexture = false;
        if (p.Contains("_diffa_"))
        {
            ti.alphaIsTransparency = true;
            ti.mipMapsPreserveCoverage = true;          // leaves keep their coverage at distance (no bare twigs)
            ti.alphaTestReferenceValue = 0.45f;         // = the materials' alpha cutoff
        }
    }

    void OnPreprocessModel()
    {
        var mi = (ModelImporter)assetImporter;
        if (!assetPath.Contains("/Resources/")) return;
        mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;   // keep material NAMES (textures are wired by name)
        mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
        bool mixamo = assetPath.Contains("/Resources/Mixamo/");
        mi.importAnimation = mixamo;          // Mixamo clips are sampled by name in edit mode (Generic rig, same bone paths)
        if (mixamo)
        {
            mi.animationType = ModelImporterAnimationType.Generic;
            mi.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
            mi.resampleCurves = true;
            mi.animationCompression = ModelImporterAnimationCompression.Off;
        }
        mi.importCameras = false;
        mi.importLights = false;
        mi.isReadable = false;
    }
}
