using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Tank;
using UnityEditor;
using UnityEngine;

[Category("UnityAssets")]
public class ArenaTerrainArtTests {
    ArenaTerrainArt.Manifest Manifest(){return JsonUtility.FromJson<ArenaTerrainArt.Manifest>(Resources.Load<TextAsset>(ArenaTerrainArt.Root+"catalog").text);}
    Texture2D Imported(string key){return Resources.Load<Texture2D>(ArenaTerrainArt.Root+key);}
    object ValidateEditor(string method,params object[] args){return Type.GetType("ArenaTerrainImporter, Assembly-CSharp-Editor",true).GetMethod(method,BindingFlags.Public|BindingFlags.Static).Invoke(null,args);}
    [TearDown] public void Cleanup(){ArenaTerrainArt.Reset();}
    [Test] public void TwoStandaloneSourcesAreCachedWithoutCroppedTextureCopies() {
        ArenaTerrainArt.Reset();Assert.IsNull(ArenaTerrainArt.Preload());long bytes=0;
        foreach(var entry in Manifest().images) {
            var texture=ArenaTerrainArt.Get(entry.key);Assert.AreSame(Imported(entry.file),texture);Assert.AreSame(texture,ArenaTerrainArt.Get(entry.key));
            Assert.AreEqual(entry.width,texture.width);Assert.AreEqual(entry.height,texture.height);bytes+=(long)texture.width*texture.height*4;
        }
        Assert.AreNotSame(ArenaTerrainArt.Get("ice"),ArenaTerrainArt.Get("oasis_palm"));Assert.LessOrEqual(bytes,ArenaTerrainArt.MaxTextureBytes);
        var ice=ArenaTerrainArt.Get("ice");ArenaTerrainArt.Reset();Assert.AreSame(ice,ArenaTerrainArt.Get("ice"));
        Assert.IsNull(ArenaTerrainArt.Get(null));Assert.IsNull(ArenaTerrainArt.Get("unknown"));
    }
    [Test] public void SourcesHashesProvenanceAndIsolatedImportersAreValid() {
        foreach(var entry in Manifest().images) {
            var texture=Imported(entry.file);var importer=(TextureImporter)AssetImporter.GetAtPath("Assets/Resources/ArenaTerrain/"+entry.file+".png");Assert.IsNotNull(importer);
            Assert.AreEqual(TextureFormat.RGBA32,texture.format);Assert.IsFalse(texture.isReadable);Assert.AreEqual(1,texture.mipmapCount);
            Assert.AreEqual(FilterMode.Bilinear,texture.filterMode);Assert.AreEqual(TextureWrapMode.Clamp,texture.wrapMode);
            Assert.AreEqual(TextureImporterNPOTScale.None,importer.npotScale);Assert.AreEqual(TextureImporterCompression.Uncompressed,importer.textureCompression);
        }
        Assert.DoesNotThrow(()=>ValidateEditor("ValidateAssets"));
    }
    [Test] public void FrozenSurfaceAndPalmSilhouetteRemainLegibleAtTwelveTwentyFourAndFortyEightPixels() {
        foreach(var entry in Manifest().images) {
            var source=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try {
                Assert.IsTrue(source.LoadImage(File.ReadAllBytes(Path.Combine(Application.dataPath,"Resources/ArenaTerrain/"+entry.file+".png"))));
                foreach(int size in new[]{12,24,48}) {
                    int visible=0,clear=0;
                    for(int y=0;y<size;y++)for(int x=0;x<size;x++) {
                        float alpha=source.GetPixelBilinear((x+.5f)/size,(y+.5f)/size).a;
                        if(entry.alpha=="opaque")Assert.GreaterOrEqual(alpha,250/255f,entry.key+" at "+size+"px");
                        if(alpha>=.5f)visible++;if(alpha<=8/255f)clear++;
                    }
                    if(entry.alpha=="cutout") {Assert.Greater(visible,size*size*.08f,"palm disappeared at "+size+"px");Assert.Greater(clear,size*size*.15f,"palm lost its silhouette at "+size+"px");}
                }
            }finally{UnityEngine.Object.DestroyImmediate(source);}
        }
    }
    [Test] public void CatalogRejectsMissingSourcesWrongDimensionsFormatMappingAndHashes() {
        Assert.AreEqual("catalog.json",ArenaTerrainArt.Validate(null,Imported));
        var manifest=Manifest();StringAssert.Contains("ice",ArenaTerrainArt.Validate(manifest,key=>null));
        var size=new Texture2D(1,1,TextureFormat.RGBA32,false);var format=new Texture2D(manifest.images[0].width,manifest.images[0].height,TextureFormat.RGB24,false);
        try {StringAssert.Contains("dimensions",ArenaTerrainArt.Validate(manifest,key=>size));StringAssert.Contains("RGBA32",ArenaTerrainArt.Validate(manifest,key=>format));}
        finally{UnityEngine.Object.DestroyImmediate(size);UnityEngine.Object.DestroyImmediate(format);}
        manifest=Manifest();manifest.images[1].key="ice";StringAssert.Contains("oasis_palm",ArenaTerrainArt.Validate(manifest,Imported));
        manifest=Manifest();manifest.images[0].file="../ice";StringAssert.Contains("mapping",ArenaTerrainArt.Validate(manifest,Imported));
        manifest=Manifest();manifest.images[0].alpha="cutout";StringAssert.Contains("alpha intent",ArenaTerrainArt.Validate(manifest,Imported));
        manifest=Manifest();manifest.images[0].sourceHash=new string('x',64);StringAssert.Contains("source hash",ArenaTerrainArt.Validate(manifest,Imported));
        manifest=Manifest();manifest.images[0].width=2049;StringAssert.Contains("catalog dimensions",ArenaTerrainArt.Validate(manifest,Imported));
    }
    [Test] public void SourceValidationRejectsMissingBytesAndHashChanges() {
        var entry=Manifest().images[0];StringAssert.Contains("PNG source",(string)ValidateEditor("ValidateSource",entry,null));
        var png=File.ReadAllBytes(Path.Combine(Application.dataPath,"Resources/ArenaTerrain/"+entry.file+".png"));Assert.IsNull(ValidateEditor("ValidateSource",entry,png));
        png[png.Length-1]^=1;StringAssert.Contains("source hash",(string)ValidateEditor("ValidateSource",entry,png));
    }
    [Test] public void CatalogRejectsTexturePairsThatExceedTheTotalRuntimeBudget() {
        var manifest=Manifest();foreach(var entry in manifest.images)entry.width=entry.height=2048;
        var oversized=new Texture2D(2048,2048,TextureFormat.RGBA32,false);
        try {StringAssert.Contains("budget",ArenaTerrainArt.Validate(manifest,key=>oversized));}
        finally{UnityEngine.Object.DestroyImmediate(oversized);}
    }
    [Test] public void AlphaValidationRejectsHolesOpaqueCutoutsEmptySpritesAndClippedEdges() {
        var source=new Texture2D(32,32,TextureFormat.RGBA32,false);var pixels=new Color32[32*32];
        var entry=new ArenaTerrainArt.Entry{key="ice",alpha="opaque",width=32,height=32};
        try {
            for(int i=0;i<pixels.Length;i++)pixels[i]=new Color32(150,200,240,255);source.SetPixels32(pixels);source.Apply();Assert.IsNull(ValidateEditor("ValidateAlpha",entry,source));
            pixels[500].a=0;source.SetPixels32(pixels);source.Apply();StringAssert.Contains("opaque",(string)ValidateEditor("ValidateAlpha",entry,source));
            entry.key="oasis_palm";entry.alpha="cutout";pixels[500].a=255;source.SetPixels32(pixels);source.Apply();Assert.IsNotNull(ValidateEditor("ValidateAlpha",entry,source));
            Array.Clear(pixels,0,pixels.Length);source.SetPixels32(pixels);source.Apply();StringAssert.Contains("coverage",(string)ValidateEditor("ValidateAlpha",entry,source));
            for(int y=8;y<24;y++)for(int x=8;x<24;x++)pixels[y*32+x]=new Color32(50,110,60,255);source.SetPixels32(pixels);source.Apply();Assert.IsNull(ValidateEditor("ValidateAlpha",entry,source));
            pixels[0].a=1;source.SetPixels32(pixels);source.Apply();Assert.IsNull(ValidateEditor("ValidateAlpha",entry,source));
            pixels[0].a=9;source.SetPixels32(pixels);source.Apply();StringAssert.Contains("edge padding",(string)ValidateEditor("ValidateAlpha",entry,source));
        }finally{UnityEngine.Object.DestroyImmediate(source);}
    }
}
