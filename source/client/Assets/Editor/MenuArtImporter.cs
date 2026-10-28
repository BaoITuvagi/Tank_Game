using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Menu artwork has its own import policy; never apply the arena's 256px limit here.
public sealed class MenuArtImporter : AssetPostprocessor {
    public override uint GetVersion(){return 2;}
    void OnPreprocessTexture() {
        if(!assetPath.StartsWith("Assets/Resources/MenuArt/"))return;
        var texture=(TextureImporter)assetImporter;
        texture.textureType=TextureImporterType.Default;
        texture.alphaSource=TextureImporterAlphaSource.FromInput;
        texture.alphaIsTransparency=true;
        texture.isReadable=Path.GetDirectoryName(assetPath).Replace('\\','/')!="Assets/Resources/MenuArt";
        texture.mipmapEnabled=false;
        texture.sRGBTexture=true;
        texture.textureCompression=TextureImporterCompression.Uncompressed;
        texture.filterMode=FilterMode.Bilinear;
        texture.wrapMode=TextureWrapMode.Clamp;
        texture.npotScale=TextureImporterNPOTScale.None;
        bool supplied=Path.GetDirectoryName(assetPath).Replace('\\','/')=="Assets/Resources/MenuArt";
        texture.maxTextureSize=supplied?2048:assetPath.Contains("pop up window")?256:assetPath.Contains("icon panel")?64:128;
    }
}

public sealed class MenuArtBuildLicense : IPostprocessBuildWithReport {
    public int callbackOrder {get{return 0;}}
    public void OnPostprocessBuild(BuildReport report) {
        string directory=Path.Combine(Path.GetDirectoryName(report.summary.outputPath),"ThirdParty");
        Directory.CreateDirectory(directory);
        File.Copy("Assets/Resources/MenuArt/ChequeredInk-LICENSE.txt",Path.Combine(directory,"ChequeredInk-LICENSE.txt"),true);
        foreach(string name in new[]{"Bungee-OFL.txt","Nunito-OFL.txt","NOTICE.txt"})File.Copy("Assets/Resources/MenuArt/Fonts/"+name,Path.Combine(directory,"Fonts-"+name),true);
    }
}
