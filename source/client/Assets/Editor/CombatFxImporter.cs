using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Tank;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class CombatFxImporter : AssetPostprocessor {
    public const string AssetRoot="Assets/Resources/CombatFx/CozyChunky/";
    const string ArchiveRoot="Cooked_FX_FREE/Cozy_Chunky/";
    const string Sha="0ebb14888c970e6d4e08f3eeb9c469712c81bd44932ea2d7d151717e940af3af";
    public override uint GetVersion() {return 1;}
    void OnPreprocessTexture() {
        if(!assetPath.StartsWith(AssetRoot,StringComparison.Ordinal))return;
        var t=(TextureImporter)assetImporter;
        t.textureType=TextureImporterType.Default;t.alphaSource=TextureImporterAlphaSource.FromInput;t.alphaIsTransparency=true;
        t.isReadable=false;t.mipmapEnabled=false;t.sRGBTexture=true;t.textureCompression=TextureImporterCompression.Uncompressed;
        t.filterMode=FilterMode.Bilinear;t.wrapMode=TextureWrapMode.Clamp;t.npotScale=TextureImporterNPOTScale.None;t.maxTextureSize=4096;
        var settings=t.GetDefaultPlatformTextureSettings();settings.format=TextureImporterFormat.RGBA32;settings.maxTextureSize=4096;t.SetPlatformTextureSettings(settings);
    }
    [MenuItem("Tools/Tank Battle/Import Combat FX")]
    public static void ChooseArchive() {
        string path=EditorUtility.OpenFilePanel("Cooked FX FREE Cozy Chunky ZIP","","zip");
        if(!string.IsNullOrEmpty(path))ImportArchive(path);
    }
    public static void ImportFromEnvironment() {
        string path=Environment.GetEnvironmentVariable("TANK_COMBAT_FX_ZIP");
        if(string.IsNullOrEmpty(path))throw new BuildFailedException("Set TANK_COMBAT_FX_ZIP to the downloaded archive.");
        ImportArchive(path);
    }
    static byte[] Read(ZipArchive archive,string key) {
        var entry=archive.GetEntry(key);
        if(entry==null||entry.Length>16*1024*1024)throw new InvalidDataException("Missing/oversized entry: "+key);
        using(var input=entry.Open())using(var output=new MemoryStream()) {input.CopyTo(output);return output.ToArray();}
    }
    static int BigEndian(byte[] data,int offset) {return data[offset]<<24|data[offset+1]<<16|data[offset+2]<<8|data[offset+3];}
    static bool HasPngAlpha(byte[] data) {
        if(data[25]==4||data[25]==6)return true;
        // The pinned Cozy sheets use indexed PNG + tRNS, not necessarily RGBA PNG.
        for(int offset=8;offset+12<=data.Length;) {
            int size=BigEndian(data,offset);if(size<0||size>data.Length-offset-12)return false;
            if(data[offset+4]==116&&data[offset+5]==82&&data[offset+6]==78&&data[offset+7]==83) {
                for(int i=offset+8;i<offset+8+size;i++)if(data[i]!=255)return true;
            }
            offset+=size+12;
        }
        return false;
    }
    public static void ImportArchive(string path) {
        using(var file=File.OpenRead(path))using(var sha=SHA256.Create()) {
            if(BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant()!=Sha)throw new InvalidDataException("Cooked FX ZIP SHA-256 mismatch.");
        }
        var files=new System.Collections.Generic.Dictionary<string,byte[]>();
        var selected=new CombatFxCatalog.Manifest{clips=new CombatFxCatalog.Description[CombatFxCatalog.Required.Length]};
        using(var file=File.OpenRead(path))using(var archive=new ZipArchive(file,ZipArchiveMode.Read)) {
            string json=Encoding.UTF8.GetString(Read(archive,ArchiveRoot+"anims.json"));
            for(int i=0;i<selected.clips.Length;i++) {
                var spec=CombatFxCatalog.Required[i];
                var match=Regex.Match(json,"\""+Regex.Escape(spec.key)+"\"\\s*:\\s*(\\{[^}]+\\})");
                var info=match.Success?JsonUtility.FromJson<CombatFxCatalog.Description>(match.Groups[1].Value):null;
                if(!CombatFxCatalog.Matches(info,spec))throw new InvalidDataException("Invalid metadata: "+spec.key);
                info.key=spec.key;selected.clips[i]=info;
                byte[] png=Read(archive,ArchiveRoot+"PNG_1x/"+spec.key+".png");
                if(png.Length<33||png[0]!=137||png[1]!=80||png[2]!=78||png[3]!=71||BigEndian(png,16)!=spec.frames*128||BigEndian(png,20)!=128||!HasPngAlpha(png))throw new InvalidDataException("Invalid PNG dimensions/alpha: "+spec.key);
                files.Add(spec.key+".png",png);
            }
            files.Add("LICENSE.pdf",Read(archive,"Cooked_FX_FREE/LICENSE.pdf"));
        }
        files.Add("catalog.json",Encoding.UTF8.GetBytes(JsonUtility.ToJson(selected,true)));
        // Validate every whitelisted entry before writing anything; preserve existing .meta GUIDs.
        foreach(var pair in files) {
            string destination=AssetRoot+pair.Key;Directory.CreateDirectory(Path.GetDirectoryName(destination));File.WriteAllBytes(destination,pair.Value);
        }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ValidateAssets();Debug.Log("Imported "+CombatFxCatalog.Required.Length+" Cozy Chunky Combat FX clips.");
    }
    public static void ValidateAssets() {
        CombatFxCatalog.Reset();string error=CombatFxCatalog.Preload();
        if(error!=null)throw new BuildFailedException("Thiếu hoặc sai hiệu ứng chiến đấu: "+error+". Dùng Tools/Tank Battle/Import Combat FX.");
        if(!File.Exists(AssetRoot+"LICENSE.pdf"))throw new BuildFailedException("Missing Cooked FX LICENSE.pdf");
    }
}
// Covers every BuildPipeline entry, including direct/manual builds and BuildCurrentScene.
public sealed class CombatFxBuildValidation : IPreprocessBuildWithReport,IPostprocessBuildWithReport {
    public int callbackOrder {get {return 0;}}
    public void OnPreprocessBuild(BuildReport report) {CombatFxImporter.ValidateAssets();}
    public void OnPostprocessBuild(BuildReport report) {
        string folder=Path.Combine(Path.GetDirectoryName(report.summary.outputPath),"ThirdParty");Directory.CreateDirectory(folder);
        File.Copy(CombatFxImporter.AssetRoot+"LICENSE.pdf",Path.Combine(folder,"CookedFX-LICENSE.pdf"),true);
    }
}
