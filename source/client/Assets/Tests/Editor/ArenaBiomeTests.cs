using System.IO;
using NUnit.Framework;
using Tank;
using UnityEngine;

public class ArenaBiomeTests {
    static GameMap Load(int id) {
        string file=id==3?"map-fortress.json":id==4?"map-river.json":"map-large.json";
        return JsonUtility.FromJson<GameMap>(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,file)));
    }
    static Rect Bounds(MapRect r) {return new Rect(r.x,r.y,r.w,r.h);}
    static bool Contains(Rect outer,Rect inner) {return inner.xMin>=outer.xMin&&inner.yMin>=outer.yMin&&inner.xMax<=outer.xMax&&inner.yMax<=outer.yMax;}
    [TestCase(1,ArenaBiome.Green)]
    [TestCase(2,ArenaBiome.Green)]
    [TestCase(3,ArenaBiome.Desert)]
    [TestCase(4,ArenaBiome.Snow)]
    [TestCase(99,ArenaBiome.Green)]
    public void BiomeSelectionIsPresentationOnlyAndLegacyMapsKeepGreen(int id,ArenaBiome expected) {
        Assert.AreEqual(expected,ArenaBiomeGeometry.BiomeFor(id));
    }
    [Test] public void SnowGroundHasAQuietColdPaletteAndDeterministicPixels() {
        var pixels=ArenaRenderer.SnowGroundPixels();var repeated=ArenaRenderer.SnowGroundPixels();
        Assert.AreEqual(64*64,pixels.Length);CollectionAssert.AreEqual(pixels,repeated);
        float min=1,max=0;
        foreach(var p in pixels) {
            Assert.Greater(p.b,p.r);Assert.Greater(p.g,.90f);Assert.AreEqual(1,p.a);
            min=Mathf.Min(min,p.r);max=Mathf.Max(max,p.r);
        }
        Assert.Greater(max-min,.005f);Assert.Less(max-min,.04f);
    }
    [TestCase(2)] [TestCase(3)] [TestCase(4)]
    public void DecorativeRoutesAndPropsLeaveMapDataUntouched(int id) {
        var map=Load(id);string before=JsonUtility.ToJson(map);
        var routes=ArenaBiomeGeometry.Routes(map);ArenaBiomeGeometry.Props(map,routes);
        Assert.AreEqual(before,JsonUtility.ToJson(map));
        if(id==2)Assert.IsEmpty(routes);else Assert.IsNotEmpty(routes);
    }
    [TestCase(3)] [TestCase(4)]
    public void EveryRouteRemainsInBoundsAndNeverPaintsABridgeAcrossWater(int id) {
        var map=Load(id);var bounds=new Rect(0,0,map.width,map.height);
        foreach(var route in ArenaBiomeGeometry.Routes(map)) {
            Assert.Greater(route.width,0);Assert.Greater(route.height,0);Assert.IsTrue(Contains(bounds,route),route.ToString());
            foreach(var water in map.water)Assert.IsFalse(route.Overlaps(Bounds(water)),"decorative route crosses water "+water.id);
            foreach(var wall in map.walls)Assert.IsFalse(route.Overlaps(Bounds(wall)),"route covers solid wall "+wall.id);
        }
    }
    [Test] public void SnowTrailsLinkTheSettlementsWithoutPaintingBridgesAcrossIce() {
        var map=Load(4);var routes=ArenaBiomeGeometry.Routes(map);
        Assert.IsEmpty(ArenaBiomeGeometry.Bridges(map));
        foreach(var p in new[]{new Vector2(90,56),new Vector2(96,40)}) {
            Assert.IsFalse(map.Water(p.x,p.y));
            Assert.IsTrue(System.Array.Exists(routes,r=>r.Contains(p)),p.ToString());
        }
    }
    [Test] public void DefaultBridgeDeckMarksTheDryCrossingBetweenSlowRiverBanks() {
        foreach(int id in new[]{1,3,4})Assert.IsEmpty(ArenaBiomeGeometry.Bridges(new GameMap {mapId=id}));
        Assert.IsEmpty(ArenaBiomeGeometry.Bridges(null));
        var map=Load(2);var bridges=ArenaBiomeGeometry.Bridges(map);
        CollectionAssert.AreEqual(new[]{new Rect(54.5f,35.5f,11,9)},bridges);
        var hp=new byte[64];for(int i=0;i<hp.Length;i++)hp[i]=(byte)GameRules.BrickMaxHp;
        float radius=GameRules.TankHalfSize*Mathf.Sqrt(2);
        foreach(var bridge in bridges) {
            foreach(var group in new[]{map.water,map.walls,map.bricks})foreach(var r in group)Assert.IsFalse(bridge.Overlaps(Bounds(r)));
            for(float x=48;x<=72;x+=.5f)for(float lane=-2.5f;lane<=2.5f;lane+=.5f) {
                float y=bridge.center.y+lane;Assert.IsFalse(map.Blocked(x,y,4500,hp));
                foreach(var water in map.water)Assert.IsFalse(Bounds(water).Overlaps(new Rect(x-radius,y-radius,radius*2,radius*2)));
            }
        }
    }
    [TestCase(3)] [TestCase(4)]
    public void TallPropSilhouettesFitRealCoverAndFlatDressingAvoidsCombatRoutes(int id) {
        var map=Load(id);var routes=ArenaBiomeGeometry.Routes(map);var props=ArenaBiomeGeometry.Props(map,routes);
        Assert.IsNotEmpty(props);
        int solid=0,flat=0;
        foreach(var prop in props) {
            Assert.IsTrue(Contains(new Rect(0,0,map.width,map.height),prop.Bounds));
            if(prop.OnObstacle) {
                solid++;Assert.IsTrue(System.Array.Exists(map.walls,wall=>Contains(Bounds(wall),prop.Bounds)),prop.Key);
            } else {
                flat++;
                foreach(var route in routes)Assert.IsFalse(route.Overlaps(prop.Bounds));
                foreach(var water in map.water)Assert.IsFalse(Bounds(water).Overlaps(prop.Bounds));
                foreach(var wall in map.walls)Assert.IsFalse(Bounds(wall).Overlaps(prop.Bounds));
                foreach(var brick in map.bricks)Assert.IsFalse(Bounds(brick).Overlaps(prop.Bounds));
                var clearance=new Rect(prop.Bounds.x-2,prop.Bounds.y-2,prop.Bounds.width+4,prop.Bounds.height+4);
                foreach(var p in map.spawns)Assert.IsFalse(clearance.Contains(new Vector2(p.x,p.y)));
                foreach(var p in map.itemSpawns)Assert.IsFalse(clearance.Contains(new Vector2(p.x,p.y)));
            }
        }
        Assert.Greater(flat,0);
        if(id==3) {
            Assert.Greater(solid,0);
            Assert.IsTrue(System.Array.Exists(props,p=>p.Key=="desert_cactus"));
            Assert.IsTrue(System.Array.Exists(props,p=>p.Key=="desert_rock"));
        } else Assert.AreEqual(0,solid,"winter trees and rocks belong on their own real ground roots");
    }
}
