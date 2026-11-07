using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Tank;
using Tank.Campaign;
using UnityEditor;
using UnityEngine;

[Category("UnityAssets")]
public class CampaignArtTests {
    CampaignCartoonCatalog.Manifest Manifest(){return JsonUtility.FromJson<CampaignCartoonCatalog.Manifest>(Resources.Load<TextAsset>(CampaignCartoonCatalog.Root+"catalog").text);}
    [Test] public void EveryCampaignDecorationEnemyAndBossUsesTheCartoonCatalog() {
        CampaignCartoonCatalog.Reset();Assert.IsNull(CampaignCartoonCatalog.Preload());
        for(int decoration=2;decoration<=14;decoration++)Assert.IsNotNull(CampaignCartoonCatalog.Get(CampaignArt.SceneryAsset(decoration)),"decoration "+decoration);
        foreach(EnemyRole role in Enum.GetValues(typeof(EnemyRole)))Assert.IsNotNull(CampaignCartoonCatalog.Get(CampaignArt.EnemyAsset(0,role)),role.ToString());
        for(int boss=1;boss<=4;boss++)Assert.IsNotNull(CampaignCartoonCatalog.Get(CampaignArt.EnemyAsset(boss,EnemyRole.Melee)),"boss "+boss);
        foreach(var key in new[]{"tree","trees","rock","rubble"})Assert.IsNotNull(CampaignCartoonCatalog.Get(key),key);
        foreach(int nation in new[]{0,1,5}) {Assert.IsNotNull(TankPalette.Body(nation));Assert.IsNotNull(TankPalette.Barrel(nation));}
        // Every cardinal road mask and cardinal/diagonal water combination remains supported.
        for(int mask=0;mask<16;mask++)Assert.IsNotEmpty(CampaignArt.RoadAsset(mask));
        for(int mask=0;mask<256;mask++)Assert.IsNotEmpty(CampaignArt.WaterAsset(mask));
    }
    [Test] public void FourVehicleDirectionsRotateNorthFacingSpritesWithoutMirroring() {
        for(int facing=0;facing<4;facing++) {
            Assert.AreEqual(facing*90,CampaignArt.VehicleRotation(facing));
            Assert.AreEqual(new Rect(0,0,1,1),CampaignArt.VehicleUV(facing));
            var heading=Quaternion.Euler(0,0,CampaignArt.VehicleRotation(facing))*Vector3.up;
            Assert.AreEqual(1,heading.magnitude,.0001f);
        }
    }
    [Test] public void SheetsHaveTransparencyAndPaddedNonOverlappingSpriteCrops() {
        var manifest=Manifest();
        foreach(var sheet in manifest.sheets) {
            var importer=(TextureImporter)AssetImporter.GetAtPath("Assets/Resources/CampaignCartoon/"+sheet.key+".png");
            Assert.IsNotNull(importer,sheet.key);Assert.AreEqual(TextureImporterShape.Texture2D,importer.textureShape,sheet.key+" shape");
            var imported=Resources.Load<Texture2D>(CampaignCartoonCatalog.Root+sheet.key);
            Assert.IsNotNull(imported,sheet.key+" Texture2D");
            Assert.IsFalse(imported.isReadable,sheet.key);Assert.AreEqual(TextureFormat.RGBA32,imported.format);
            Assert.AreEqual(FilterMode.Bilinear,imported.filterMode);Assert.AreEqual(TextureWrapMode.Clamp,imported.wrapMode);Assert.AreEqual(1,imported.mipmapCount);
            var source=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try {
                Assert.IsTrue(source.LoadImage(File.ReadAllBytes(Path.Combine(Application.dataPath,"Resources/CampaignCartoon/"+sheet.key+".png"))));
                Assert.AreEqual(sheet.width,source.width);Assert.AreEqual(sheet.height,source.height);
                // Generated edge antialiasing can leave alpha 1/255 in an empty
                // corner. Use the same visible-pixel threshold as crop checks.
                var pixels=source.GetPixels32();Assert.LessOrEqual(pixels[0].a,8,sheet.key+" transparent background");
                foreach(var sprite in manifest.sprites)if(sprite.sheet==sheet.key) {
                    int left=Mathf.CeilToInt((sprite.cell%sheet.columns)*sheet.width/(float)sheet.columns),right=Mathf.FloorToInt((sprite.cell%sheet.columns+1)*sheet.width/(float)sheet.columns);
                    int top=Mathf.CeilToInt((sprite.cell/sheet.columns)*sheet.height/(float)sheet.rows),bottom=Mathf.FloorToInt((sprite.cell/sheet.columns+1)*sheet.height/(float)sheet.rows);int opaque=0;bool contained=true,padded=true;
                    for(int y=top;y<bottom;y++)for(int x=left;x<right;x++)if(pixels[(source.height-1-y)*source.width+x].a>8) {
                        opaque++;contained&=x>=sprite.x&&x<sprite.x+sprite.width&&y>=sprite.y&&y<sprite.y+sprite.height;
                        padded&=x>left&&x<right-1&&y>top&&y<bottom-1;
                    }
                    Assert.Greater(opaque,100,sprite.key+" is empty");Assert.IsTrue(contained,sprite.key+" crop excludes visible pixels");Assert.IsTrue(padded,sprite.key+" touches atlas neighbor");
                    var image=CampaignCartoonCatalog.Get(sprite.key);Assert.Greater(image.Uv.xMin,0);Assert.Greater(image.Uv.yMin,0);Assert.Less(image.Uv.xMax,1);Assert.Less(image.Uv.yMax,1);
                }
            }finally{UnityEngine.Object.DestroyImmediate(source);}
        }
        Type validator=Type.GetType("CampaignCartoonImporter, Assembly-CSharp-Editor",true);
        Assert.DoesNotThrow(()=>validator.GetMethod("ValidateAssets",BindingFlags.Public|BindingFlags.Static).Invoke(null,null));
    }
    [Test] public void CatalogRejectsMissingSheetsBadDimensionsAndAnAssetMappedAcrossCells() {
        var manifest=Manifest();Func<string,Texture2D> load=key=>Resources.Load<Texture2D>(CampaignCartoonCatalog.Root+key);
        foreach(var missing in manifest.sheets)StringAssert.Contains(missing.key,CampaignCartoonCatalog.Validate(manifest,key=>key==missing.key?null:load(key)));
        var wrong=new Texture2D(1,1,TextureFormat.RGBA32,false);
        try {StringAssert.Contains("scenery",CampaignCartoonCatalog.Validate(manifest,key=>key=="scenery"?wrong:load(key)));}
        finally {UnityEngine.Object.DestroyImmediate(wrong);}
        manifest.sprites[0].width=manifest.sheets[0].width;
        StringAssert.Contains("tree",CampaignCartoonCatalog.Validate(manifest,load));
        manifest=Manifest();manifest.sprites[1].key="tree";StringAssert.Contains("trees",CampaignCartoonCatalog.Validate(manifest,load));
    }
}
