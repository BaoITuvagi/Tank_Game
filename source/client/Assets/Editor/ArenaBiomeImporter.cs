using System;
using System.IO;
using System.Security.Cryptography;
using Tank;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// This importer is isolated from the original ArenaArt/CampaignArt assets.
public sealed class ArenaBiomeImporter : AssetPostprocessor {
    public const string AssetRoot="Assets/Resources/ArenaBiomes/";
    public const string SourceHash="06270564ac3505c4c18a79453d2fc637ad9f0d82368bddd6c7e4af187189e8cb";
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
    [MenuItem("Tools/Tank Battle/Validate Arena Biome Art")]
    public static void ValidateAssets() {
        ArenaBiomeArt.Reset();string error=ArenaBiomeArt.Preload();
        if(error!=null)throw new BuildFailedException("Invalid arena biome art: "+error);
        string path=AssetRoot+"props.png";byte[] png=File.ReadAllBytes(path);
        if(png.Length<33||png[0]!=137||png[1]!=80||png[2]!=78||png[3]!=71||png[25]!=6)throw new BuildFailedException("Arena biome PNG must have RGBA transparency");
        using(var sha=SHA256.Create())if(BitConverter.ToString(sha.ComputeHash(png)).Replace("-","").ToLowerInvariant()!=SourceHash)throw new BuildFailedException("Arena biome source PNG changed; review its provenance and crop bounds");
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);var imported=Resources.Load<Texture2D>(ArenaBiomeArt.Root+"props");
        if(importer==null||importer.textureShape!=TextureImporterShape.Texture2D||importer.alphaSource!=TextureImporterAlphaSource.FromInput||!importer.alphaIsTransparency||importer.npotScale!=TextureImporterNPOTScale.None||importer.textureCompression!=TextureImporterCompression.Uncompressed||imported.isReadable||imported.mipmapCount!=1||imported.filterMode!=FilterMode.Bilinear||imported.wrapMode!=TextureWrapMode.Clamp)throw new BuildFailedException("Arena biome importer settings");
        var manifest=JsonUtility.FromJson<ArenaBiomeArt.Manifest>(Resources.Load<TextAsset>(ArenaBiomeArt.Root+"catalog").text);
        var source=new Texture2D(2,2,TextureFormat.RGBA32,false);
        try {
            if(!source.LoadImage(png))throw new BuildFailedException("Cannot decode arena biome PNG");
            var pixels=source.GetPixels32();bool transparent=false;
            foreach(var pixel in pixels)if(pixel.a<=8){transparent=true;break;}
            if(!transparent)throw new BuildFailedException("Arena biome PNG has no transparent background");
            foreach(var sprite in manifest.sprites) {
                int left=Mathf.CeilToInt((sprite.cell%manifest.columns)*manifest.width/(float)manifest.columns),right=Mathf.FloorToInt((sprite.cell%manifest.columns+1)*manifest.width/(float)manifest.columns);
                int top=Mathf.CeilToInt((sprite.cell/manifest.columns)*manifest.height/(float)manifest.rows),bottom=Mathf.FloorToInt((sprite.cell/manifest.columns+1)*manifest.height/(float)manifest.rows);int visible=0;
                for(int y=top;y<bottom;y++)for(int x=left;x<right;x++)if(pixels[(source.height-1-y)*source.width+x].a>8) {
                    visible++;
                    if(x<sprite.x||x>=sprite.x+sprite.width||y<sprite.y||y>=sprite.y+sprite.height)throw new BuildFailedException(sprite.key+" crop excludes visible pixels");
                    if(x<=left||x>=right-1||y<=top||y>=bottom-1)throw new BuildFailedException(sprite.key+" touches an atlas neighbor");
                }
                if(visible<100)throw new BuildFailedException(sprite.key+" is empty");
            }
        }finally{UnityEngine.Object.DestroyImmediate(source);}
        string provenance=Path.Combine(AssetRoot,"PROVENANCE.md");
        if(!File.Exists(provenance))throw new BuildFailedException("Missing arena biome provenance");
        string notice=File.ReadAllText(provenance);
        if(!notice.Contains("image_gen")||!notice.Contains("2026-11-06")||!notice.Contains(SourceHash))throw new BuildFailedException("Incomplete arena biome provenance");
        if(!File.Exists("Assets/Resources/ArenaArt/Kenney-LICENSE.txt"))throw new BuildFailedException("Missing original Kenney licence");
    }
}
public sealed class ArenaBiomeBuildValidation : IPreprocessBuildWithReport,IPostprocessBuildWithReport {
    public int callbackOrder {get{return 3;}}
    public void OnPreprocessBuild(BuildReport report){ArenaBiomeImporter.ValidateAssets();}
    public void OnPostprocessBuild(BuildReport report) {
        string directory=Path.Combine(Path.GetDirectoryName(report.summary.outputPath),"ThirdParty");Directory.CreateDirectory(directory);
        File.Copy(ArenaBiomeImporter.AssetRoot+"PROVENANCE.md",Path.Combine(directory,"ArenaBiomes-PROVENANCE.md"),true);
    }
}
