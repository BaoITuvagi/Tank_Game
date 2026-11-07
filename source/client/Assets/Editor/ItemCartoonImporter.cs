using System;
using System.IO;
using Tank;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Retain the first iteration's PNG import settings/GUIDs. Active item artwork now uses
// the original ArenaArt sources plus code-native marks; no source importer is changed.
public sealed class ItemCartoonImporter : AssetPostprocessor {
    public const string AssetRoot="Assets/Resources/ItemCartoon/";
    public override uint GetVersion(){return 1;}
    void OnPreprocessTexture() {
        if(!assetPath.StartsWith(AssetRoot,StringComparison.Ordinal))return;
        var texture=(TextureImporter)assetImporter;
        texture.textureType=TextureImporterType.Default;texture.textureShape=TextureImporterShape.Texture2D;
        texture.alphaSource=TextureImporterAlphaSource.FromInput;texture.alphaIsTransparency=true;
        texture.isReadable=false;texture.mipmapEnabled=false;texture.sRGBTexture=true;
        texture.textureCompression=TextureImporterCompression.Uncompressed;texture.filterMode=FilterMode.Bilinear;
        texture.wrapMode=TextureWrapMode.Clamp;texture.npotScale=TextureImporterNPOTScale.None;texture.maxTextureSize=ItemIconArt.ImportedSize;
        var settings=texture.GetDefaultPlatformTextureSettings();settings.format=TextureImporterFormat.RGBA32;settings.maxTextureSize=ItemIconArt.ImportedSize;texture.SetPlatformTextureSettings(settings);
    }
    [MenuItem("Tools/Tank Battle/Validate Item Art")]
    public static void ValidateAssets() {
        ItemIconArt.Reset();string error=ItemIconArt.Preload();
        if(error!=null)throw new BuildFailedException("Thiếu hoặc sai hình vật phẩm: "+error);
        foreach(var resource in ItemIconArt.SourceResources) {
            string path="Assets/Resources/"+resource+".png";byte[] data=File.ReadAllBytes(path);
            if(data.Length<33||data[0]!=137||data[1]!=80||data[2]!=78||data[3]!=71||data[25]!=6)throw new BuildFailedException("Kenney source PNG must have RGBA transparency: "+resource);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);var texture=Resources.Load<Texture2D>(resource);
            if(importer==null||importer.textureShape!=TextureImporterShape.Texture2D||texture.mipmapCount!=1||texture.filterMode!=FilterMode.Bilinear)throw new BuildFailedException("Item source importer settings: "+resource);
        }
        if(!File.Exists("Assets/Resources/ArenaArt/Kenney-LICENSE.txt"))throw new BuildFailedException("Missing original Kenney CC0 licence");
    }
}
public sealed class ItemCartoonBuildValidation : IPreprocessBuildWithReport {
    public int callbackOrder {get{return 2;}}
    public void OnPreprocessBuild(BuildReport report){ItemCartoonImporter.ValidateAssets();}
}
