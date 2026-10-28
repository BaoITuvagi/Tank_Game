using UnityEditor;
using UnityEngine;

// This importer intentionally applies only to campaign's private art folder.
public sealed class CampaignArtImporter : AssetPostprocessor {
    public override uint GetVersion(){return 1;}
    void OnPreprocessTexture() {
        if(!assetPath.StartsWith("Assets/Resources/CampaignArt/"))return;
        var texture=(TextureImporter)assetImporter;
        texture.textureType=TextureImporterType.Default;
        texture.alphaIsTransparency=true;
        texture.isReadable=false;
        texture.mipmapEnabled=false;
        texture.textureCompression=TextureImporterCompression.Uncompressed;
        texture.filterMode=FilterMode.Point;
        texture.wrapMode=TextureWrapMode.Clamp;
        texture.npotScale=TextureImporterNPOTScale.None;
        texture.maxTextureSize=256;
    }
}
