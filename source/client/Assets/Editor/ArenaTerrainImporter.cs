using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Tank;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class ArenaTerrainImporter : AssetPostprocessor {
    public const string AssetRoot="Assets/Resources/ArenaTerrain/";
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
    public static string ValidateAlpha(ArenaTerrainArt.Entry entry,Texture2D source) {
        if(source==null||source.width!=entry.width||source.height!=entry.height)return entry.key+" (decoded dimensions)";
        var pixels=source.GetPixels32();int solid=0,clear=0;
        for(int y=0;y<source.height;y++)for(int x=0;x<source.width;x++) {
            byte alpha=pixels[y*source.width+x].a;
            if(entry.alpha=="opaque"&&alpha<250)return entry.key+" (opaque material alpha)";
            if(alpha>=128)solid++;if(alpha<=8)clear++;
            // Generated source has at most 1/255 alpha haze on its outer edge;
            // retain the original bytes while rejecting any visible clipping.
            if(entry.alpha=="cutout"&&(x==0||y==0||x==source.width-1||y==source.height-1)&&alpha>8)return entry.key+" (transparent edge padding)";
        }
        if(entry.alpha=="cutout"&&(solid<pixels.Length*.1f||solid>pixels.Length*.85f||clear<pixels.Length*.2f))return entry.key+" (cutout coverage)";
        return null;
    }
    public static string ValidateSource(ArenaTerrainArt.Entry entry,byte[] png) {
        if(png==null||png.Length<33||png[0]!=137||png[1]!=80||png[2]!=78||png[3]!=71||png[4]!=13||png[5]!=10||png[6]!=26||png[7]!=10||png[24]!=8||(png[25]!=6&&!(entry.alpha=="opaque"&&png[25]==2)))return entry.key+" (8-bit PNG source)";
        using(var sha=SHA256.Create())if(BitConverter.ToString(sha.ComputeHash(png)).Replace("-","").ToLowerInvariant()!=entry.sourceHash)return entry.key+" (source hash)";
        var source=new Texture2D(2,2,TextureFormat.RGBA32,false);
        try {
            if(!source.LoadImage(png))return entry.key+" (PNG decode)";
            return ValidateAlpha(entry,source);
        }finally{UnityEngine.Object.DestroyImmediate(source);}
    }
    [MenuItem("Tools/Tank Battle/Validate Arena Terrain Art")]
    public static void ValidateAssets() {
        ArenaTerrainArt.Reset();string error=ArenaTerrainArt.Preload();
        if(error!=null)throw new BuildFailedException("Invalid arena terrain art: "+error);
        var manifest=JsonUtility.FromJson<ArenaTerrainArt.Manifest>(Resources.Load<TextAsset>(ArenaTerrainArt.Root+"catalog").text);
        foreach(var entry in manifest.images) {
            string path=AssetRoot+entry.file+".png";
            if(!File.Exists(path))throw new BuildFailedException("Missing terrain source: "+entry.key);
            error=ValidateSource(entry,File.ReadAllBytes(path));if(error!=null)throw new BuildFailedException("Invalid arena terrain source: "+error);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);var texture=ArenaTerrainArt.Get(entry.key);
            if(importer==null||importer.textureType!=TextureImporterType.Default||importer.textureShape!=TextureImporterShape.Texture2D||importer.alphaSource!=TextureImporterAlphaSource.FromInput||!importer.alphaIsTransparency||importer.npotScale!=TextureImporterNPOTScale.None||importer.textureCompression!=TextureImporterCompression.Uncompressed||texture.isReadable||texture.mipmapCount!=1||texture.filterMode!=FilterMode.Bilinear||texture.wrapMode!=TextureWrapMode.Clamp)throw new BuildFailedException("Arena terrain importer settings: "+entry.key);
        }
    }
}
public sealed class ArenaTerrainBuildValidation : IPreprocessBuildWithReport {
    public int callbackOrder {get{return 5;}}
    public void OnPreprocessBuild(BuildReport report){ArenaTerrainImporter.ValidateAssets();}
}
