using System;
using System.IO;
using System.Security.Cryptography;
using Tank;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Only the new atlas is processed; the original arena and campaign importers
// and their source assets remain independent.
public sealed class ArenaEnvironmentImporter : AssetPostprocessor {
    public const string AssetRoot="Assets/Resources/ArenaEnvironment/";
    public const string SourceHash="d495a06c21772afd464f97d07c5bd82cda636631b4c3e1ae0e97e6117d1d5250";
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
    [MenuItem("Tools/Tank Battle/Validate Arena Environment Art")]
    public static void ValidateAssets() {
        ArenaEnvironmentArt.Reset();string error=ArenaEnvironmentArt.Preload();
        if(error!=null)throw new BuildFailedException("Invalid arena environment art: "+error);
        string path=AssetRoot+"materials.png";byte[] png=File.ReadAllBytes(path);
        if(png.Length<33||png[0]!=137||png[1]!=80||png[2]!=78||png[3]!=71||png[25]!=6)throw new BuildFailedException("Arena environment PNG must have RGBA transparency");
        using(var sha=SHA256.Create())if(BitConverter.ToString(sha.ComputeHash(png)).Replace("-","").ToLowerInvariant()!=SourceHash)throw new BuildFailedException("Arena environment source PNG changed; review provenance and material crops");
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);var imported=Resources.Load<Texture2D>(ArenaEnvironmentArt.Root+"materials");
        if(importer==null||importer.textureShape!=TextureImporterShape.Texture2D||importer.alphaSource!=TextureImporterAlphaSource.FromInput||!importer.alphaIsTransparency||importer.npotScale!=TextureImporterNPOTScale.None||importer.textureCompression!=TextureImporterCompression.Uncompressed||imported.isReadable||imported.mipmapCount!=1||imported.filterMode!=FilterMode.Bilinear||imported.wrapMode!=TextureWrapMode.Clamp)throw new BuildFailedException("Arena environment importer settings");
        var manifest=JsonUtility.FromJson<ArenaEnvironmentArt.Manifest>(Resources.Load<TextAsset>(ArenaEnvironmentArt.Root+"catalog").text);
        var source=new Texture2D(2,2,TextureFormat.RGBA32,false);
        try {
            if(!source.LoadImage(png))throw new BuildFailedException("Cannot decode arena environment PNG");
            var pixels=source.GetPixels32();
            foreach(var material in manifest.materials) {
                // Material sheets are sampled inside each swatch. Generated
                // background haze and the unused cell never enter a render UV.
                for(int y=material.sampleY;y<material.sampleY+material.sampleSize;y++)for(int x=material.sampleX;x<material.sampleX+material.sampleSize;x++)if(pixels[(source.height-1-y)*source.width+x].a<250)throw new BuildFailedException(material.key+" inner material sample must be opaque");
            }
        }finally{UnityEngine.Object.DestroyImmediate(source);}
        string provenance=Path.Combine(AssetRoot,"PROVENANCE.md");
        if(!File.Exists(provenance))throw new BuildFailedException("Missing arena environment provenance");
        string notice=File.ReadAllText(provenance);
        if(!notice.Contains("image_gen")||!notice.Contains("2026-11-06")||!notice.Contains(SourceHash))throw new BuildFailedException("Incomplete arena environment provenance");
        if(!File.Exists("Assets/Resources/ArenaArt/Kenney-LICENSE.txt"))throw new BuildFailedException("Missing original Kenney licence");
    }
}
public sealed class ArenaEnvironmentBuildValidation : IPreprocessBuildWithReport,IPostprocessBuildWithReport {
    public int callbackOrder {get{return 4;}}
    public void OnPreprocessBuild(BuildReport report){ArenaEnvironmentImporter.ValidateAssets();}
    public void OnPostprocessBuild(BuildReport report) {
        string directory=Path.Combine(Path.GetDirectoryName(report.summary.outputPath),"ThirdParty");Directory.CreateDirectory(directory);
        File.Copy(ArenaEnvironmentImporter.AssetRoot+"PROVENANCE.md",Path.Combine(directory,"ArenaEnvironment-PROVENANCE.md"),true);
    }
}
