using UnityEditor;
// GUI draws source PNGs directly; disable mipmaps/compression to retain clean outlines.
public sealed class ArenaArtImporter : AssetPostprocessor {
 public override uint GetVersion(){return 2;}
 void OnPreprocessTexture(){
  if(!assetPath.StartsWith("Assets/Resources/ArenaArt/"))return;
  var texture=(TextureImporter)assetImporter;
  texture.textureType=TextureImporterType.Default;texture.alphaIsTransparency=true;
  texture.isReadable=true;texture.mipmapEnabled=false;texture.textureCompression=TextureImporterCompression.Uncompressed;
  texture.filterMode=UnityEngine.FilterMode.Bilinear;texture.maxTextureSize=256;
  texture.npotScale=TextureImporterNPOTScale.None;
 }
}
