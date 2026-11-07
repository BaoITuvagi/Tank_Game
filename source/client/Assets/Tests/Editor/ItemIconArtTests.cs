using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Tank;
using UnityEditor;
using UnityEngine;

[Category("UnityAssets")]
public class ItemIconArtTests {
    ItemIconArt.Manifest Manifest(){return JsonUtility.FromJson<ItemIconArt.Manifest>(Resources.Load<TextAsset>(ItemIconArt.Root+"catalog").text);}
    [TearDown] public void Cleanup(){ItemIconArt.Reset();}
    [Test] public void ItemIdsShareUntouchedArenaArtAndGoldIsOneCodeNativeCoin() {
        ItemIconArt.Reset();Assert.IsNull(ItemIconArt.Preload());
        CollectionAssert.AreEqual(new[]{"medical","armor","he","purification","invincibility","mirror"},Array.ConvertAll(new[]{0,1,2,3,4,5},ItemIconArt.ItemKey));
        Assert.IsNull(ItemIconArt.ItemKey(-1));Assert.IsNull(ItemIconArt.ItemKey(GameRules.ItemTypeCount));
        Assert.AreEqual("gold",ItemIconArt.CampaignPickupKey(3));
        var crate=Resources.Load<Texture2D>("ArenaArt/crateMetal");var bullet=Resources.Load<Texture2D>("ArenaArt/bulletSand1_outline");
        var marks=new System.Collections.Generic.HashSet<Texture2D>();
        for(int type=0;type<GameRules.ItemTypeCount;type++) {
            var image=ItemIconArt.Get(ItemIconArt.ItemKey(type));Assert.AreSame(crate,image.Base);
            Assert.AreEqual("crate",image.Source);Assert.IsTrue(marks.Add(image.Decoration));
            if(type==2||type==5)Assert.AreSame(bullet,image.Projectile);else Assert.IsNull(image.Projectile);
        }
        var gold=ItemIconArt.Get("gold");Assert.AreEqual(ItemIconArt.CoinSource,gold.Source);Assert.IsNull(gold.Base);Assert.IsNull(gold.Projectile);
        Assert.IsNotNull(gold.Decoration);Assert.IsNull(ItemIconArt.Get(null));Assert.IsNull(ItemIconArt.Get("missing"));
    }
    [Test] public void ActiveSourcePngsHaveAlphaAndKeepTheirOriginalGuidsAndCc0Licence() {
        string[] guids={"b1addcef057347640af4534e5e5b4c87","9e3c0d85247c9fc42bb525fdff21aedb"};
        var manifest=Manifest();
        for(int i=0;i<manifest.sources.Length;i++) {
            var source=manifest.sources[i];string path="Assets/Resources/"+source.resource+".png";
            Assert.AreEqual(guids[i],AssetDatabase.AssetPathToGUID(path));
            byte[] png=File.ReadAllBytes(path);Assert.AreEqual(6,png[25],source.resource+" must retain alpha");
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try {
                Assert.IsTrue(texture.LoadImage(png));Assert.AreEqual(source.width,texture.width);Assert.AreEqual(source.height,texture.height);
                bool opaque=false,transparent=false;
                foreach(Color32 pixel in texture.GetPixels32()){opaque|=pixel.a>240;transparent|=pixel.a<240;}
                Assert.IsTrue(opaque,source.resource);Assert.IsTrue(transparent,source.resource);
            }finally{UnityEngine.Object.DestroyImmediate(texture);}
        }
        StringAssert.Contains("CC0",File.ReadAllText("Assets/Resources/ArenaArt/Kenney-LICENSE.txt"));
    }
    [Test] public void CachedVectorMarksAreUncompressedAndFitSmallCurrencyAndItemBounds() {
        Assert.IsNull(ItemIconArt.Preload());
        foreach(string key in ItemIconArt.Required) {
            var image=ItemIconArt.Get(key);Texture2D texture=image.Decoration;
            Assert.AreEqual(ItemIconArt.DecorationSize,texture.width);Assert.AreEqual(ItemIconArt.DecorationSize,texture.height);
            Assert.AreEqual(TextureFormat.RGBA32,texture.format);Assert.IsFalse(texture.isReadable);Assert.AreEqual(1,texture.mipmapCount);
            Assert.AreEqual(FilterMode.Bilinear,texture.filterMode);Assert.AreEqual(TextureWrapMode.Clamp,texture.wrapMode);
            foreach(float size in new[]{17,18,24,32,44,46,64}) {
                Rect bounds=new Rect(10,20,size+4,size),fit=ItemIconArt.Fit(bounds,image.Aspect);
                Assert.GreaterOrEqual(fit.xMin,bounds.xMin-.001f);Assert.GreaterOrEqual(fit.yMin,bounds.yMin-.001f);
                Assert.LessOrEqual(fit.xMax,bounds.xMax+.001f);Assert.LessOrEqual(fit.yMax,bounds.yMax+.001f);
                Assert.AreEqual(1,fit.width/fit.height,.0001f);
            }
        }
        Type validator=Type.GetType("ItemCartoonImporter, Assembly-CSharp-Editor",true);
        Assert.DoesNotThrow(()=>validator.GetMethod("ValidateAssets",BindingFlags.Public|BindingFlags.Static).Invoke(null,null));
    }
    [Test] public void CatalogRejectsMissingSourcesWrongSizesAndInactiveIllustrationMappings() {
        var manifest=Manifest();Func<string,Texture2D> load=key=>Resources.Load<Texture2D>(key);
        foreach(string missing in ItemIconArt.SourceResources)StringAssert.Contains(missing,ItemIconArt.Validate(manifest,key=>key==missing?null:load(key)));
        manifest.sources[0].resource="ItemCartoon/medical";StringAssert.Contains("crate",ItemIconArt.Validate(manifest,load));
        manifest=Manifest();manifest.sources[1].height++;StringAssert.Contains("bullet",ItemIconArt.Validate(manifest,load));
        manifest=Manifest();manifest.icons[1].key="medical";StringAssert.Contains("armor",ItemIconArt.Validate(manifest,load));
        manifest=Manifest();manifest.icons[6].source="crate";StringAssert.Contains("gold",ItemIconArt.Validate(manifest,load));
        manifest=Manifest();manifest.renderer="ai-raster";Assert.AreEqual("catalog.json",ItemIconArt.Validate(manifest,load));
    }
}
