using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Tank;
using UnityEditor;
using UnityEngine;

[Category("UnityAssets")]
public class ArenaWinterArtTests {
    ArenaWinterArt.Manifest Manifest(){return JsonUtility.FromJson<ArenaWinterArt.Manifest>(Resources.Load<TextAsset>(ArenaWinterArt.Root+"catalog").text);}
    Texture2D Imported(string key){return Resources.Load<Texture2D>(ArenaWinterArt.Root+key);}
    object ValidateEditor(string type,string method,params object[] args){return Type.GetType(type+", Assembly-CSharp-Editor",true).GetMethod(method,BindingFlags.Public|BindingFlags.Static).Invoke(null,args);}
    [TearDown] public void Cleanup(){ArenaWinterArt.Reset();}
    [Test] public void IglooCachesItsOriginalWholeTextureWithinTheSeparateBudget() {
        ArenaWinterArt.Reset();Assert.IsNull(ArenaWinterArt.Preload());long bytes=0;
        CollectionAssert.AreEqual(new[]{"igloo"},ArenaWinterArt.Required);
        foreach(var entry in Manifest().images) {
            Assert.AreEqual("cutout",entry.alpha);var texture=ArenaWinterArt.Get(entry.key);
            Assert.AreSame(Imported(entry.file),texture);Assert.AreSame(texture,ArenaWinterArt.Get(entry.key));
            Assert.AreEqual(entry.width,texture.width);Assert.AreEqual(entry.height,texture.height);bytes+=(long)texture.width*texture.height*4;
        }
        Assert.LessOrEqual(bytes,ArenaWinterArt.MaxTextureBytes);
        var igloo=ArenaWinterArt.Get("igloo");ArenaWinterArt.Reset();Assert.AreSame(igloo,ArenaWinterArt.Get("igloo"));
        Assert.IsNull(ArenaWinterArt.Get(null));Assert.IsNull(ArenaWinterArt.Get("unknown"));
    }
    [Test] public void OriginalHashesCutoutAlphaProvenanceAndIsolatedImportersAreValidatedBeforeBuild() {
        foreach(var entry in Manifest().images) {
            var texture=Imported(entry.file);var importer=(TextureImporter)AssetImporter.GetAtPath("Assets/Resources/ArenaWinter/"+entry.file+".png");Assert.IsNotNull(importer);
            Assert.AreEqual(TextureFormat.RGBA32,texture.format);Assert.IsFalse(texture.isReadable);Assert.AreEqual(1,texture.mipmapCount);
            Assert.AreEqual(FilterMode.Bilinear,texture.filterMode);Assert.AreEqual(TextureWrapMode.Clamp,texture.wrapMode);
            Assert.AreEqual(TextureImporterType.Default,importer.textureType);Assert.AreEqual(TextureImporterShape.Texture2D,importer.textureShape);
            Assert.AreEqual(TextureImporterAlphaSource.FromInput,importer.alphaSource);Assert.IsTrue(importer.alphaIsTransparency);
            Assert.AreEqual(TextureImporterNPOTScale.None,importer.npotScale);Assert.AreEqual(TextureImporterCompression.Uncompressed,importer.textureCompression);
        }
        Assert.DoesNotThrow(()=>ValidateEditor("ArenaWinterImporter","ValidateAssets"));
    }
    [Test] public void IglooKeepsVisibleMassAndTransparentSurroundingsAtTwelveTwentyFourAndFortyEightPixels() {
        foreach(var entry in Manifest().images) {
            var source=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try {
                Assert.IsTrue(source.LoadImage(File.ReadAllBytes(Path.Combine(Application.dataPath,"Resources/ArenaWinter/"+entry.file+".png"))));
                Assert.IsNull(ValidateEditor("ArenaTerrainImporter","ValidateAlpha",entry,source));
                foreach(int size in new[]{12,24,48}) {
                    int visible=0,clear=0;
                    for(int y=0;y<size;y++)for(int x=0;x<size;x++) {
                        float alpha=source.GetPixelBilinear((x+.5f)/size,(y+.5f)/size).a;
                        if(alpha>=.5f)visible++;if(alpha<=8/255f)clear++;
                    }
                    Assert.Greater(visible,size*size*.08f,entry.key+" disappeared at "+size+"px");
                    Assert.Greater(clear,size*size*.15f,entry.key+" lost its cutout silhouette at "+size+"px");
                }
            }finally{UnityEngine.Object.DestroyImmediate(source);}
        }
    }
    [Test] public void CatalogRejectsMissingSourcesWrongDimensionsFormatUnsafeMappingOpaqueIntentAndInvalidHashes() {
        Assert.AreEqual("catalog.json",ArenaWinterArt.Validate(null,Imported));
        var manifest=Manifest();StringAssert.Contains("igloo",ArenaWinterArt.Validate(manifest,key=>null));
        var size=new Texture2D(1,1,TextureFormat.RGBA32,false);var format=new Texture2D(manifest.images[0].width,manifest.images[0].height,TextureFormat.RGB24,false);
        try {StringAssert.Contains("dimensions",ArenaWinterArt.Validate(manifest,key=>size));StringAssert.Contains("RGBA32",ArenaWinterArt.Validate(manifest,key=>format));}
        finally{UnityEngine.Object.DestroyImmediate(size);UnityEngine.Object.DestroyImmediate(format);}
        manifest=Manifest();manifest.images[0].key="unknown";StringAssert.Contains("igloo",ArenaWinterArt.Validate(manifest,Imported));
        manifest=Manifest();manifest.images[0].file="../igloo";StringAssert.Contains("mapping",ArenaWinterArt.Validate(manifest,Imported));
        manifest=Manifest();manifest.images[0].alpha="opaque";StringAssert.Contains("alpha intent",ArenaWinterArt.Validate(manifest,Imported));
        manifest=Manifest();manifest.images[0].sourceHash=new string('x',64);StringAssert.Contains("source hash",ArenaWinterArt.Validate(manifest,Imported));
        manifest=Manifest();manifest.images[0].width=2049;StringAssert.Contains("catalog dimensions",ArenaWinterArt.Validate(manifest,Imported));
    }
    [Test] public void SharedSourceValidationRejectsMissingBytesChangedHashesAndRgbInsteadOfCutoutRgba() {
        var entry=Manifest().images[0];StringAssert.Contains("PNG source",(string)ValidateEditor("ArenaTerrainImporter","ValidateSource",entry,null));
        var png=File.ReadAllBytes(Path.Combine(Application.dataPath,"Resources/ArenaWinter/"+entry.file+".png"));
        Assert.IsNull(ValidateEditor("ArenaTerrainImporter","ValidateSource",entry,png));
        var changed=(byte[])png.Clone();changed[changed.Length-1]^=1;StringAssert.Contains("source hash",(string)ValidateEditor("ArenaTerrainImporter","ValidateSource",entry,changed));
        changed=(byte[])png.Clone();changed[25]=2;StringAssert.Contains("PNG source",(string)ValidateEditor("ArenaTerrainImporter","ValidateSource",entry,changed));
    }
    [Test] public void CatalogRejectsACutoutThatExceedsTheEightMebibyteBudget() {
        var manifest=Manifest();foreach(var entry in manifest.images)entry.width=entry.height=2048;
        var oversized=new Texture2D(2048,2048,TextureFormat.RGBA32,false);
        try {StringAssert.Contains("budget",ArenaWinterArt.Validate(manifest,key=>oversized));}
        finally{UnityEngine.Object.DestroyImmediate(oversized);}
    }
}
