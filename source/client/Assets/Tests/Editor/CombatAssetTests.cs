using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Tank;
using UnityEngine;
using UnityEngine.Profiling;

[Category("UnityAssets")]
public class CombatAssetTests {
    CombatFxCatalog.Manifest Manifest() {
        return JsonUtility.FromJson<CombatFxCatalog.Manifest>(Resources.Load<TextAsset>(CombatFxCatalog.Root+"catalog").text);
    }
    [Test] public void ImportedSheetsMeetCatalogAndBuildValidation() {
        CombatFxCatalog.Reset();Assert.IsNull(CombatFxCatalog.Preload());long bytes=0,residentBytes=0;
        foreach(var spec in CombatFxCatalog.Required) {
            var clip=CombatFxCatalog.Get(spec.key);Assert.IsNotNull(clip,spec.key);Assert.IsFalse(clip.Texture.isReadable,spec.key);
            Assert.AreEqual(FilterMode.Bilinear,clip.Texture.filterMode);Assert.AreEqual(TextureWrapMode.Clamp,clip.Texture.wrapMode);
            Assert.AreEqual(1,clip.Texture.mipmapCount);bytes+=(long)clip.Texture.width*clip.Texture.height*4;
            long resident=Profiler.GetRuntimeMemorySizeLong(clip.Texture);Assert.Greater(resident,0,spec.key);residentBytes+=resident;
            Assert.IsNull(CombatFxCatalog.Preload());Assert.AreSame(clip,CombatFxCatalog.Get(spec.key),spec.key);
        }
        Assert.LessOrEqual(bytes,32*1024*1024);
        Assert.LessOrEqual(residentBytes,32*1024*1024);
        Debug.Log("COMBAT_FX_TEXTURE_MEMORY rgbaBytes="+bytes+" profilerResidentBytes="+residentBytes);
        Type importer=Type.GetType("CombatFxImporter, Assembly-CSharp-Editor",true);
        Assert.DoesNotThrow(()=>importer.GetMethod("ValidateAssets").Invoke(null,null));
    }
    [Test] public void EveryMissingSheetAndWrongDimensionsFailWithTheClipName() {
        Assert.IsNull(CombatFxCatalog.Preload());var manifest=Manifest();
        foreach(var missing in CombatFxCatalog.Required) {
            string error=CombatFxCatalog.Validate(manifest,key=>key==missing.key?null:CombatFxCatalog.Get(key).Texture);
            StringAssert.Contains(missing.key,error);
        }
        var bad=new Texture2D(1,1,TextureFormat.RGBA32,false);
        try {
            string error=CombatFxCatalog.Validate(manifest,key=>key=="combat/muzzle"?bad:CombatFxCatalog.Get(key).Texture);
            StringAssert.Contains("combat/muzzle",error);manifest.clips[0].frames++;
            StringAssert.Contains("metadata",CombatFxCatalog.Validate(manifest,key=>CombatFxCatalog.Get(key).Texture));
        }
        finally {UnityEngine.Object.DestroyImmediate(bad);}
    }
    [Test] public void InvalidArchiveIsRejectedBeforeAnyAssetIsWrittenAndIndexedPngAlphaIsAccepted() {
        Type importer=Type.GetType("CombatFxImporter, Assembly-CSharp-Editor",true);
        byte[] png=File.ReadAllBytes(Path.Combine(Application.dataPath,"Resources/CombatFx/CozyChunky/combat/muzzle.png"));
        Assert.IsTrue((bool)importer.GetMethod("HasPngAlpha",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{png}));
        string path=Path.GetTempFileName();
        try {
            File.WriteAllText(path,"not the pinned Cooked FX archive");
            var failure=Assert.Throws<TargetInvocationException>(()=>importer.GetMethod("ImportArchive").Invoke(null,new object[]{path}));
            Assert.IsInstanceOf<InvalidDataException>(failure.InnerException);
            CollectionAssert.AreEqual(png,File.ReadAllBytes(Path.Combine(Application.dataPath,"Resources/CombatFx/CozyChunky/combat/muzzle.png")));
        }
        finally {File.Delete(path);}
    }
}
