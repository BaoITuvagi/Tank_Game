using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Tank;
using UnityEditor;
using UnityEngine;

[Category("UnityAssets")]
public class ArenaEnvironmentArtTests {
    ArenaEnvironmentArt.Manifest Manifest(){return JsonUtility.FromJson<ArenaEnvironmentArt.Manifest>(Resources.Load<TextAsset>(ArenaEnvironmentArt.Root+"catalog").text);}
    Texture2D Imported(){return Resources.Load<Texture2D>(ArenaEnvironmentArt.Root+"materials");}
    [TearDown] public void Cleanup(){ArenaEnvironmentArt.Reset();}
    [Test] public void FiveMaterialsUseDistinctCachedCropsInOneAtlas() {
        ArenaEnvironmentArt.Reset();Assert.IsNull(ArenaEnvironmentArt.Preload());
        var crops=new System.Collections.Generic.HashSet<Rect>();
        foreach(string key in ArenaEnvironmentArt.Required) {
            var image=ArenaEnvironmentArt.Get(key);Assert.IsNotNull(image,key);Assert.IsTrue(crops.Add(image.Uv),key);
            Assert.AreSame(Imported(),image.Texture);Assert.AreSame(image,ArenaEnvironmentArt.Get(key));Assert.AreEqual(1,image.Aspect);
        }
        Assert.IsNull(ArenaEnvironmentArt.Get(null));Assert.IsNull(ArenaEnvironmentArt.Get("missing"));
        var previous=ArenaEnvironmentArt.Get("wall_sandstone");ArenaEnvironmentArt.Reset();
        Assert.AreNotSame(previous,ArenaEnvironmentArt.Get("wall_sandstone"));
        Assert.AreSame(previous.Texture,ArenaEnvironmentArt.Get("wall_sandstone").Texture);
    }
    [Test] public void ImporterKeepsOriginalAlphaWithinOneNonReadableTextureBudget() {
        var texture=Imported();var importer=(TextureImporter)AssetImporter.GetAtPath("Assets/Resources/ArenaEnvironment/materials.png");
        Assert.IsNotNull(importer);Assert.AreEqual(TextureImporterShape.Texture2D,importer.textureShape);
        Assert.AreEqual(TextureFormat.RGBA32,texture.format);Assert.IsFalse(texture.isReadable);Assert.AreEqual(1,texture.mipmapCount);
        Assert.AreEqual(FilterMode.Bilinear,texture.filterMode);Assert.AreEqual(TextureWrapMode.Clamp,texture.wrapMode);
        Assert.AreEqual(TextureImporterNPOTScale.None,importer.npotScale);Assert.AreEqual(TextureImporterCompression.Uncompressed,importer.textureCompression);
        Assert.LessOrEqual((long)texture.width*texture.height*4,ArenaEnvironmentArt.MaxTextureBytes);
        Type validator=Type.GetType("ArenaEnvironmentImporter, Assembly-CSharp-Editor",true);
        Assert.DoesNotThrow(()=>validator.GetMethod("ValidateAssets",BindingFlags.Public|BindingFlags.Static).Invoke(null,null));
    }
    [Test] public void MaterialSamplesStayOpaqueAtSmallAndLargeRenderSizes() {
        var source=new Texture2D(2,2,TextureFormat.RGBA32,false);
        try {
            Assert.IsTrue(source.LoadImage(File.ReadAllBytes(Path.Combine(Application.dataPath,"Resources/ArenaEnvironment/materials.png"))));
            foreach(var material in Manifest().materials) {
                var image=ArenaEnvironmentArt.Get(material.key);
                Assert.Greater(image.Uv.xMin,0);Assert.Greater(image.Uv.yMin,0);Assert.Less(image.Uv.xMax,1);Assert.Less(image.Uv.yMax,1);
                foreach(int size in new[]{12,24,48,96})for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                    Assert.GreaterOrEqual(source.GetPixelBilinear(image.Uv.x+(x+.5f)/size*image.Uv.width,image.Uv.y+(y+.5f)/size*image.Uv.height).a,250/255f,material.key+" material at "+size+"px");
            }
        }finally{UnityEngine.Object.DestroyImmediate(source);}
    }
    [Test] public void CatalogRejectsMissingTextureWrongDimensionsOrFormat() {
        var manifest=Manifest();StringAssert.Contains("materials",ArenaEnvironmentArt.Validate(manifest,key=>null));
        var wrongSize=new Texture2D(1,1,TextureFormat.RGBA32,false);var wrongFormat=new Texture2D(ArenaEnvironmentArt.Width,ArenaEnvironmentArt.Height,TextureFormat.RGB24,false);
        try {
            StringAssert.Contains("materials",ArenaEnvironmentArt.Validate(manifest,key=>wrongSize));
            StringAssert.Contains("materials",ArenaEnvironmentArt.Validate(manifest,key=>wrongFormat));
        }finally{UnityEngine.Object.DestroyImmediate(wrongSize);UnityEngine.Object.DestroyImmediate(wrongFormat);}
        manifest.width++;Assert.AreEqual("catalog.json",ArenaEnvironmentArt.Validate(manifest,key=>Imported()));
        Assert.AreEqual("catalog.json",ArenaEnvironmentArt.Validate(null,key=>Imported()));
    }
    [Test] public void CatalogRejectsMissingDuplicateAndIncorrectCellMappings() {
        var manifest=Manifest();manifest.materials[1].key=manifest.materials[0].key;StringAssert.Contains("wall_snow",ArenaEnvironmentArt.Validate(manifest,key=>Imported()));
        manifest=Manifest();manifest.materials[2]=null;StringAssert.Contains("water_green",ArenaEnvironmentArt.Validate(manifest,key=>Imported()));
        manifest=Manifest();manifest.materials[4].cell=3;StringAssert.Contains("water_snow",ArenaEnvironmentArt.Validate(manifest,key=>Imported()));
        manifest=Manifest();manifest.materials=new ArenaEnvironmentArt.Material[0];Assert.AreEqual("catalog.json",ArenaEnvironmentArt.Validate(manifest,key=>Imported()));
    }
    [Test] public void CatalogRejectsAtlasBleedAndSamplesThatIncludeSwatchBorders() {
        var manifest=Manifest();manifest.materials[0].x=1;StringAssert.Contains("crop padding",ArenaEnvironmentArt.Validate(manifest,key=>Imported()));
        manifest=Manifest();manifest.materials[1].width=ArenaEnvironmentArt.Width;StringAssert.Contains("crop padding",ArenaEnvironmentArt.Validate(manifest,key=>Imported()));
        manifest=Manifest();manifest.materials[2].sampleX=manifest.materials[2].x;StringAssert.Contains("inner material sample",ArenaEnvironmentArt.Validate(manifest,key=>Imported()));
        manifest=Manifest();manifest.materials[3].sampleSize=manifest.materials[3].width;StringAssert.Contains("inner material sample",ArenaEnvironmentArt.Validate(manifest,key=>Imported()));
        manifest=Manifest();manifest.materials[4].sampleSize=1;StringAssert.Contains("inner material sample",ArenaEnvironmentArt.Validate(manifest,key=>Imported()));
    }
}
