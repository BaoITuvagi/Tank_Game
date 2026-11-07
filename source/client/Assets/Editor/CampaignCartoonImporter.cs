using System;
using System.IO;
using Tank.Campaign;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// New artwork has its own importer. The original CampaignArt assets keep their GUIDs/settings.
public sealed class CampaignCartoonImporter : AssetPostprocessor {
    public const string AssetRoot="Assets/Resources/CampaignCartoon/";
    public override uint GetVersion(){return 2;}
    void OnPreprocessTexture() {
        if(!assetPath.StartsWith(AssetRoot,StringComparison.Ordinal))return;
        var texture=(TextureImporter)assetImporter;
        texture.textureType=TextureImporterType.Default;texture.textureShape=TextureImporterShape.Texture2D;texture.alphaSource=TextureImporterAlphaSource.FromInput;texture.alphaIsTransparency=true;
        texture.isReadable=false;texture.mipmapEnabled=false;texture.sRGBTexture=true;
        texture.textureCompression=TextureImporterCompression.Uncompressed;texture.filterMode=FilterMode.Bilinear;
        texture.wrapMode=TextureWrapMode.Clamp;texture.npotScale=TextureImporterNPOTScale.None;texture.maxTextureSize=2048;
        var settings=texture.GetDefaultPlatformTextureSettings();settings.format=TextureImporterFormat.RGBA32;settings.maxTextureSize=2048;texture.SetPlatformTextureSettings(settings);
    }
    [MenuItem("Tools/Tank Battle/Validate Campaign Cartoon Art")]
    public static void ValidateAssets() {
        CampaignCartoonCatalog.Reset();string error=CampaignCartoonCatalog.Preload();
        if(error!=null)throw new BuildFailedException("Thiếu hoặc sai hình chiến dịch: "+error);
        var manifest=JsonUtility.FromJson<CampaignCartoonCatalog.Manifest>(Resources.Load<TextAsset>(CampaignCartoonCatalog.Root+"catalog").text);
        foreach(var sheet in manifest.sheets) {
            byte[] data=File.ReadAllBytes(AssetRoot+sheet.key+".png");
            if(data.Length<33||data[0]!=137||data[1]!=80||data[2]!=78||data[3]!=71||data[25]!=6)throw new BuildFailedException("Campaign cartoon PNG must have RGBA transparency: "+sheet.key);
            var importer=(TextureImporter)AssetImporter.GetAtPath(AssetRoot+sheet.key+".png");
            if(importer==null||importer.textureShape!=TextureImporterShape.Texture2D)throw new BuildFailedException("Campaign cartoon texture must be 2D: "+sheet.key);
            var texture=Resources.Load<Texture2D>(CampaignCartoonCatalog.Root+sheet.key);
            if(texture.isReadable||texture.mipmapCount!=1||texture.filterMode!=FilterMode.Bilinear||texture.wrapMode!=TextureWrapMode.Clamp)throw new BuildFailedException("Campaign cartoon importer settings: "+sheet.key);
        }
    }
}
public sealed class CampaignCartoonBuildValidation : IPreprocessBuildWithReport {
    public int callbackOrder {get{return 1;}}
    public void OnPreprocessBuild(BuildReport report){CampaignCartoonImporter.ValidateAssets();}
}
