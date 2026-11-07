using System;
using System.IO;
using System.Text;
using Tank;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class ArenaWinterImporter : AssetPostprocessor {
    public const string AssetRoot="Assets/Resources/ArenaWinter/";
    public override uint GetVersion(){return 1;}
    void OnPreprocessTexture() {
        if(!assetPath.StartsWith(AssetRoot,StringComparison.Ordinal)||!assetPath.EndsWith(".png",StringComparison.OrdinalIgnoreCase))return;
        var texture=(TextureImporter)assetImporter;
        texture.textureType=TextureImporterType.Default;texture.textureShape=TextureImporterShape.Texture2D;
        texture.alphaSource=TextureImporterAlphaSource.FromInput;texture.alphaIsTransparency=true;
        texture.isReadable=false;texture.mipmapEnabled=false;texture.sRGBTexture=true;
        texture.textureCompression=TextureImporterCompression.Uncompressed;texture.filterMode=FilterMode.Bilinear;
        texture.wrapMode=TextureWrapMode.Clamp;texture.npotScale=TextureImporterNPOTScale.None;texture.maxTextureSize=2048;
        var settings=texture.GetDefaultPlatformTextureSettings();settings.format=TextureImporterFormat.RGBA32;settings.maxTextureSize=2048;texture.SetPlatformTextureSettings(settings);
    }
    [MenuItem("Tools/Tank Battle/Validate Arena Winter Art")]
    public static void ValidateAssets() {
        ArenaWinterArt.Reset();string error=ArenaWinterArt.Preload();
        if(error!=null)throw new BuildFailedException("Invalid arena winter art: "+error);
        var manifest=JsonUtility.FromJson<ArenaWinterArt.Manifest>(Resources.Load<TextAsset>(ArenaWinterArt.Root+"catalog").text);
        foreach(var entry in manifest.images) {
            string path=AssetRoot+entry.file+".png";
            if(!File.Exists(path))throw new BuildFailedException("Missing winter source: "+entry.key);
            error=ArenaTerrainImporter.ValidateSource(entry,File.ReadAllBytes(path));
            if(error!=null)throw new BuildFailedException("Invalid arena winter source: "+error);
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;var texture=ArenaWinterArt.Get(entry.key);
            if(importer==null||importer.textureType!=TextureImporterType.Default||importer.textureShape!=TextureImporterShape.Texture2D
                ||importer.alphaSource!=TextureImporterAlphaSource.FromInput||!importer.alphaIsTransparency||!importer.sRGBTexture
                ||importer.npotScale!=TextureImporterNPOTScale.None||importer.textureCompression!=TextureImporterCompression.Uncompressed
                ||importer.maxTextureSize!=2048||importer.GetDefaultPlatformTextureSettings().format!=TextureImporterFormat.RGBA32
                ||texture.isReadable||texture.mipmapCount!=1||texture.filterMode!=FilterMode.Bilinear||texture.wrapMode!=TextureWrapMode.Clamp)
                throw new BuildFailedException("Arena winter importer settings: "+entry.key);
        }
    }
}
public sealed class ArenaWinterBuildValidation : IPreprocessBuildWithReport {
    public int callbackOrder {get{return 6;}}
    public void OnPreprocessBuild(BuildReport report){ArenaWinterImporter.ValidateAssets();}
}
