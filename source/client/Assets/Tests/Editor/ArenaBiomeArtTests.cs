using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Tank;
using UnityEditor;
using UnityEngine;

[Category("UnityAssets")]
public class ArenaBiomeArtTests {
    ArenaBiomeArt.Manifest Manifest(){return JsonUtility.FromJson<ArenaBiomeArt.Manifest>(Resources.Load<TextAsset>(ArenaBiomeArt.Root+"catalog").text);}
    Texture2D Imported(){return Resources.Load<Texture2D>(ArenaBiomeArt.Root+"props");}
    [TearDown] public void Cleanup(){ArenaBiomeArt.Reset();}
    [Test] public void EightDistinctPropsShareOneCachedAtlas() {
        ArenaBiomeArt.Reset();Assert.IsNull(ArenaBiomeArt.Preload());
        var sprites=new System.Collections.Generic.HashSet<ArenaBiomeArt.Image>();
        foreach(string key in ArenaBiomeArt.Required) {
            var image=ArenaBiomeArt.Get(key);Assert.IsNotNull(image,key);Assert.IsTrue(sprites.Add(image),key);
            Assert.AreSame(Imported(),image.Texture);Assert.AreSame(image,ArenaBiomeArt.Get(key));
        }
        Assert.IsNull(ArenaBiomeArt.Get(null));Assert.IsNull(ArenaBiomeArt.Get("missing"));
    }
    [Test] public void ImportedAtlasHasAlphaAndFitsItsTextureBudgetWithoutRuntimePixelCopies() {
        var texture=Imported();var importer=(TextureImporter)AssetImporter.GetAtPath("Assets/Resources/ArenaBiomes/props.png");
        Assert.IsNotNull(importer);Assert.AreEqual(TextureImporterShape.Texture2D,importer.textureShape);
        Assert.AreEqual(TextureFormat.RGBA32,texture.format);Assert.IsFalse(texture.isReadable);Assert.AreEqual(1,texture.mipmapCount);
        Assert.AreEqual(FilterMode.Bilinear,texture.filterMode);Assert.AreEqual(TextureWrapMode.Clamp,texture.wrapMode);
        Assert.LessOrEqual((long)texture.width*texture.height*4,ArenaBiomeArt.MaxTextureBytes);
        Type validator=Type.GetType("ArenaBiomeImporter, Assembly-CSharp-Editor",true);
        Assert.DoesNotThrow(()=>validator.GetMethod("ValidateAssets",BindingFlags.Public|BindingFlags.Static).Invoke(null,null));
    }
    [Test] public void SpriteCropsKeepVisibleAlphaAndRemainReadableAtThumbnailAndArenaSizes() {
        var manifest=Manifest();var source=new Texture2D(2,2,TextureFormat.RGBA32,false);
        try {
            Assert.IsTrue(source.LoadImage(File.ReadAllBytes(Path.Combine(Application.dataPath,"Resources/ArenaBiomes/props.png"))));
            var pixels=source.GetPixels32();Assert.LessOrEqual(pixels[0].a,8);
            foreach(var sprite in manifest.sprites) {
                var image=ArenaBiomeArt.Get(sprite.key);
                Assert.Greater(image.Uv.xMin,0);Assert.Greater(image.Uv.yMin,0);Assert.Less(image.Uv.xMax,1);Assert.Less(image.Uv.yMax,1);
                // Sample the actual source silhouette at small render sizes, rather than
                // accepting a crop that is mostly transparent or a fine, unreadable line.
                foreach(int size in new[]{12,24,32,48}) {
                    Rect bounds=new Rect(10,20,size,size),fit=ArenaBiomeArt.Fit(bounds,image.Aspect);
                    Assert.GreaterOrEqual(fit.xMin,bounds.xMin-.001f);Assert.GreaterOrEqual(fit.yMin,bounds.yMin-.001f);
                    Assert.LessOrEqual(fit.xMax,bounds.xMax+.001f);Assert.LessOrEqual(fit.yMax,bounds.yMax+.001f);
                    int visible=0;
                    for(int y=0;y<size;y++)for(int x=0;x<size;x++)if(source.GetPixelBilinear(image.Uv.x+(x+.5f)/size*image.Uv.width,image.Uv.y+(y+.5f)/size*image.Uv.height).a>.5f)visible++;
                    Assert.Greater(visible,size*size/4,sprite.key+" silhouette at "+size+"px");
                }
                int count=0;
                for(int y=sprite.y;y<sprite.y+sprite.height;y++)for(int x=sprite.x;x<sprite.x+sprite.width;x++)if(pixels[(source.height-1-y)*source.width+x].a>8)count++;
                Assert.Greater(count,100,sprite.key);
            }
        }finally{UnityEngine.Object.DestroyImmediate(source);}
    }
    [Test] public void CatalogRejectsMissingTextureWrongDimensionsAndFormat() {
        var manifest=Manifest();StringAssert.Contains("props",ArenaBiomeArt.Validate(manifest,key=>null));
        var wrongSize=new Texture2D(1,1,TextureFormat.RGBA32,false);var wrongFormat=new Texture2D(ArenaBiomeArt.Width,ArenaBiomeArt.Height,TextureFormat.RGB24,false);
        try {
            StringAssert.Contains("props",ArenaBiomeArt.Validate(manifest,key=>wrongSize));
            StringAssert.Contains("props",ArenaBiomeArt.Validate(manifest,key=>wrongFormat));
        }finally{UnityEngine.Object.DestroyImmediate(wrongSize);UnityEngine.Object.DestroyImmediate(wrongFormat);}
        manifest.width++;Assert.AreEqual("catalog.json",ArenaBiomeArt.Validate(manifest,key=>Imported()));
    }
    [Test] public void CatalogRejectsDuplicateMappingMissingPropAndCrossCellCrop() {
        var manifest=Manifest();manifest.sprites[1].key=manifest.sprites[0].key;StringAssert.Contains("desert_scrub",ArenaBiomeArt.Validate(manifest,key=>Imported()));
        manifest=Manifest();manifest.sprites[0]=null;StringAssert.Contains("desert_cactus",ArenaBiomeArt.Validate(manifest,key=>Imported()));
        manifest=Manifest();manifest.sprites[0].width=manifest.width;StringAssert.Contains("crop outside cell",ArenaBiomeArt.Validate(manifest,key=>Imported()));
        manifest=Manifest();manifest.sprites[5].cell=4;StringAssert.Contains("snow_rock",ArenaBiomeArt.Validate(manifest,key=>Imported()));
        manifest=Manifest();manifest.sprites[7].height=1;StringAssert.Contains("ice_shards",ArenaBiomeArt.Validate(manifest,key=>Imported()));
    }
}
