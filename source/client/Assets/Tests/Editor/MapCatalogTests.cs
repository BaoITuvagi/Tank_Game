using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using NUnit.Framework;
using Tank;
using UnityEngine;

public class MapCatalogTests {
    [SetUp] public void Load() {MapCatalog.Load(Application.streamingAssetsPath);}
    [TearDown] public void Restore() {MapCatalog.Load(Application.streamingAssetsPath);}
    string Hex(byte[] bytes) {return BitConverter.ToString(bytes).Replace("-","").ToLowerInvariant();}
    [Test] public void ThreeNamedMapsMatchJavaHashesAndCurrentAuthoredBytes() {
        CollectionAssert.AreEqual(new[]{2,3,4},MapCatalog.Entries.Select(e=>e.id));
        CollectionAssert.AreEqual(new[]{"Tiền Đồn Xanh","Pháo Đài Cát","Sông Băng"},MapCatalog.Entries.Select(e=>e.name));
        var java=File.ReadAllLines(Path.GetFullPath("../../source/server/target/interop/map-catalog.tsv"));Assert.AreEqual(3,java.Length);
        foreach(string line in java) {
            var fields=line.Split('\t');var entry=MapCatalog.Find(int.Parse(fields[0]));Assert.NotNull(entry);Assert.AreEqual(fields[1],entry.file);Assert.AreEqual(fields[2],Hex(entry.Hash));
            using(var sha=SHA256.Create())CollectionAssert.AreEqual(sha.ComputeHash(File.ReadAllBytes(Path.Combine(Application.streamingAssetsPath,entry.file))),entry.Hash);
            Assert.AreEqual(entry.id,entry.Map.mapId);Assert.AreEqual(120,entry.Map.width);Assert.AreEqual(80,entry.Map.height);Assert.AreEqual(16,entry.Map.spawns.Length);
        }
        Assert.AreEqual("1afb24e8b0681e24637d8ee7927fb0a1f630b660c1aff09dd09463f1c1a0c8de",Hex(MapCatalog.Find(2).Hash));
        Assert.AreEqual("Sông Băng",MapCatalog.Name(4));Assert.AreEqual("Bản đồ 1",MapCatalog.Name(1));Assert.IsNull(MapCatalog.Find(1));Assert.IsFalse(MapCatalog.IsSelectable(1));Assert.IsTrue(MapCatalog.IsSelectable(2));
    }
    [Test] public void MapLoaderRegistersEveryMapWhileStartingWithTheDefault() {
        GameMap initial;using(var client=MapLoader.CreateClient(out initial)) {
            Assert.AreEqual(MapCatalog.DefaultId,initial.mapId);Assert.AreSame(initial,client.ActiveMap);
            var maps=(IDictionary)typeof(ClientCore).GetField("maps",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(client);
            CollectionAssert.AreEquivalent(new[]{2,3,4},maps.Keys);Assert.AreEqual(3,maps.Count);
        }
    }
    [Test] public void InvalidManifestFailsAtomicallyAndRejectsPathTraversalDuplicatesAndWrongIdentity() {
        string directory=Path.Combine(Path.GetTempPath(),"tank-map-catalog-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try {
            foreach(var entry in MapCatalog.Entries)File.Copy(Path.Combine(Application.streamingAssetsPath,entry.file),Path.Combine(directory,entry.file));
            string original=File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"multiplayer-maps.json"));var entries=MapCatalog.Entries;
            foreach(string broken in new[]{original.Replace("\"defaultId\":2","\"defaultId\":9"),original.Replace("\"id\":3","\"id\":2"),original.Replace("map-fortress.json","../map-fortress.json"),original.Replace("\"id\":4","\"id\":9"),"{\"defaultId\":2,\"maps\":[]}"}) {
                File.WriteAllText(Path.Combine(directory,"multiplayer-maps.json"),broken);
                Assert.Throws<InvalidDataException>(()=>MapCatalog.Load(directory));Assert.AreSame(entries,MapCatalog.Entries);
            }
            File.WriteAllText(Path.Combine(directory,"multiplayer-maps.json"),original);
            string mapFile=Path.Combine(directory,"map-fortress.json");File.WriteAllText(mapFile,File.ReadAllText(mapFile).Replace("\"mapId\":3","\"mapId\":99"));
            Assert.Throws<InvalidDataException>(()=>MapCatalog.Load(directory));Assert.AreSame(entries,MapCatalog.Entries);
        } finally {Directory.Delete(directory,true);}
    }
    [Test] public void RotatedTanksReachAllSpawnsAndItemsWithIntactBricksAlongDryRoutes() {
        foreach(var entry in MapCatalog.Entries) {
            var map=entry.Map;var hp=new byte[64];for(int i=0;i<hp.Length;i++)hp[i]=(byte)GameRules.BrickMaxHp;
            int columns=(int)(map.width*2)+1,rows=(int)(map.height*2)+1;var seen=new bool[columns*rows];var queue=new Queue<int>();
            int start=Mathf.RoundToInt(map.spawns[0].y*2)*columns+Mathf.RoundToInt(map.spawns[0].x*2);seen[start]=true;queue.Enqueue(start);
            while(queue.Count>0) {
                int index=queue.Dequeue(),ix=index%columns,iy=index/columns;
                foreach(int next in new[]{index-1,index+1,index-columns,index+columns}) {
                    if(next<0||next>=seen.Length||Math.Abs(next%columns-ix)+Math.Abs(next/columns-iy)!=1||seen[next])continue;
                    float x=next%columns*.5f,y=next/columns*.5f;
                    if(map.Blocked(x,y,4500,hp)||Wet(map,x,y))continue;seen[next]=true;queue.Enqueue(next);
                }
            }
            foreach(var group in new[]{map.spawns,map.itemSpawns})foreach(var p in group) {
                Assert.IsFalse(map.Blocked(p.x,p.y,4500,hp),entry.name+" rotated start");
                Assert.IsTrue(seen[Mathf.RoundToInt(p.y*2)*columns+Mathf.RoundToInt(p.x*2)],entry.name+" unreachable "+p.x+","+p.y);
            }
        }
    }
    bool Wet(GameMap map,float x,float y) {
        float r=GameRules.TankHalfSize*Mathf.Sqrt(2);
        foreach(var rect in map.water)if(x+r>rect.x&&x-r<rect.x+rect.w&&y+r>rect.y&&y-r<rect.y+rect.h)return true;return false;
    }
    [Test] public void NewMapsOfferDistinctTerrainContinuousIceTransitAndSafeDryBanks() {
        var fortress=MapCatalog.Find(3).Map;var river=MapCatalog.Find(4).Map;
        Assert.AreEqual(60,river.walls.Length);Assert.AreEqual(44,river.walls.Count(w=>w.id>=16));Assert.AreEqual(5,river.water.Length);
        Assert.IsFalse(fortress.water.Any(a=>river.water.Any(b=>a.x==b.x&&a.y==b.y&&a.w==b.w&&a.h==b.h)));
        var hp=new byte[64];for(int i=0;i<hp.Length;i++)hp[i]=(byte)GameRules.BrickMaxHp;
        for(float y=32;y<=52;y+=.5f)Assert.IsTrue(river.Water(56,y)&&!river.Blocked(56,y,4500,hp),"the lake has an uninterrupted fast north/south route");
        for(float x=40;x<=72;x+=.5f)Assert.IsTrue(river.Water(x,42)&&!river.Blocked(x,42,4500,hp),"the lake has an uninterrupted fast east/west route");
        foreach(var entry in MapCatalog.Entries) {
            Assert.DoesNotThrow(()=>entry.Map.ValidateItemSpawns());Assert.GreaterOrEqual(entry.Map.battlefield.fireZones.Length,10);Assert.GreaterOrEqual(entry.Map.battlefield.airstrikeZones.Length,10);
            Assert.LessOrEqual(entry.Map.bricks.Length,64);for(int i=0;i<entry.Map.bricks.Length;i++)Assert.AreEqual(i,entry.Map.bricks[i].id);
        }
    }
}
