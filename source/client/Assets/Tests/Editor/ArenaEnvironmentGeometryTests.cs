using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using Tank;
using Tank.Campaign;
using UnityEngine;

public class ArenaEnvironmentGeometryTests {
    static MapRect R(float x,float y,float w,float h) {return new MapRect {x=x,y=y,w=w,h=h};}
    static Rect Bounds(MapRect r) {return new Rect(r.x,r.y,r.w,r.h);}
    static bool Contains(Rect outer,Rect inner) {return inner.xMin>=outer.xMin&&inner.yMin>=outer.yMin&&inner.xMax<=outer.xMax&&inner.yMax<=outer.yMax;}
    static GameMap Load(int id) {return JsonUtility.FromJson<GameMap>(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,FileName(id))));}
    static string FileName(int id) {return id==3?"map-fortress.json":id==4?"map-river.json":"map-large.json";}
    static bool Inside(MapRect[] rects,Vector2 point) {return rects.Any(r=>r.Contains(point.x,point.y));}
    static float Perimeter(ArenaEnvironmentGeometry.Surface surface) {return surface.Edges.Sum(e=>Vector2.Distance(e.A,e.B));}
    static float Area(ArenaEnvironmentGeometry.Surface surface) {return surface.Cells.Sum(r=>r.width*r.height);}
    static void ExteriorEdges(MapRect[] rects,ArenaEnvironmentGeometry.Surface surface) {
        foreach(var edge in surface.Edges) {
            var normal=edge.Side==0?Vector2.up:edge.Side==1?Vector2.right:edge.Side==2?Vector2.down:Vector2.left;
            for(int sample=1;sample<10;sample++) {
                var point=Vector2.Lerp(edge.A,edge.B,sample/10f);
                Assert.IsTrue(Inside(rects,point-normal*.001f),"edge must border the union on its inside");
                Assert.IsFalse(Inside(rects,point+normal*.001f),"an internal join must never receive an exterior cap");
            }
        }
    }
    [Test] public void EmptyAndDegenerateInputProducesNoDrawGeometry() {
        foreach(var rects in new[]{null,new MapRect[0],new[]{null,R(0,0,0,2),R(0,0,2,-1),R(float.NaN,0,2,2)}}) {
            var surface=ArenaEnvironmentGeometry.Build(rects);
            Assert.IsEmpty(surface.Cells);Assert.IsEmpty(surface.Edges);Assert.IsEmpty(surface.Corners);
        }
    }
    [Test] public void DefaultRiverUsesMirroredBanksSidePondsAndOneWideDryCentralCrossing() {
        var map=Load(2);Assert.AreEqual(4,map.water.Length);Assert.AreEqual(648,Area(ArenaEnvironmentGeometry.Build(map.water)));
        Assert.AreEqual(16,map.spawns.Length);Assert.AreEqual(21,map.itemSpawns.Length);
        Assert.AreEqual(32,map.walls.Length);Assert.AreEqual(36,map.bricks.Length);
        Assert.AreEqual(18,map.battlefield.fireZones.Length);Assert.AreEqual(47,map.battlefield.airstrikeZones.Length);
        foreach(var r in map.water.Concat(map.walls.Take(16)).Concat(map.bricks)) {
            var group=r.kind==2?map.water:r.kind==1?map.bricks:map.walls.Take(16).ToArray();
            Assert.IsTrue(group.Any(other=>other.x==map.width-r.x-r.w&&other.y==map.height-r.y-r.h&&other.w==r.w&&other.h==r.h),"authored banks and cover should retain 180 degree symmetry");
        }
        Assert.AreEqual(2,map.water.Count(r=>r.x==55.5f&&r.w==9&&r.h==24));
        Assert.AreEqual(2,map.water.Count(r=>r.w==12&&r.h==9));
        var hp=Enumerable.Repeat((byte)GameRules.BrickMaxHp,64).ToArray();float radius=GameRules.TankHalfSize*Mathf.Sqrt(2);
        for(float x=48;x<=72;x+=.5f)for(float y=37.5f;y<=42.5f;y+=.5f) {
            Assert.IsFalse(map.Blocked(x,y,4500,hp),"central bridge approach needs full rotated tank clearance");
            foreach(var water in map.water)Assert.IsFalse(Bounds(water).Overlaps(new Rect(x-radius,y-radius,radius*2,radius*2)),"wooden bridge must mark a real dry gap");
        }
        foreach(var water in map.water)foreach(var obstacle in map.walls.Concat(map.bricks))Assert.IsFalse(Bounds(water).Overlaps(Bounds(obstacle)));
    }
    [Test] public void OverlappingRectanglesPartitionTheUnionWithoutDrawingAnyAreaTwice() {
        var rects=new[]{R(0,0,4,2),R(2,1,2,3)};
        var surface=ArenaEnvironmentGeometry.Build(rects);
        Assert.AreEqual(12,Area(surface));
        for(int i=0;i<surface.Cells.Length;i++) {
            var cell=surface.Cells[i];Assert.IsTrue(Inside(rects,cell.center));
            for(int j=i+1;j<surface.Cells.Length;j++)Assert.IsFalse(cell.Overlaps(surface.Cells[j]));
        }
        Assert.AreEqual(6,surface.Edges.Length);Assert.AreEqual(16,Perimeter(surface));ExteriorEdges(rects,surface);
    }
    [Test] public void HalfUnitTJunctionHasNoWaterBankAcrossItsConnectedNeck() {
        var rects=new[]{R(.5f,2.5f,6,2),R(2.5f,4.5f,2,3)};
        var surface=ArenaEnvironmentGeometry.Build(rects);
        Assert.AreEqual(18,Area(surface));Assert.AreEqual(22,Perimeter(surface));Assert.AreEqual(8,surface.Edges.Length);
        Assert.IsFalse(surface.Edges.Any(e=>e.A.y==4.5f&&e.B.y==4.5f&&e.A.x<4.5f&&e.B.x>2.5f));
        ExteriorEdges(rects,surface);
    }
    [Test] public void WallLCornerKeepsConvexAndConcaveCornersButNoStraightSubdivisions() {
        var surface=ArenaEnvironmentGeometry.Build(new[]{R(0,0,4,2),R(2,2,2,2)});
        Assert.AreEqual(6,surface.Corners.Length);
        Assert.IsTrue(surface.Corners.Any(c=>c.Position==new Vector2(2,2)&&c.Mask==7),"NE, SE and SW surround the inner turn");
        Assert.IsTrue(surface.Corners.Any(c=>c.Position==Vector2.zero&&c.Mask==1));
        Assert.IsFalse(surface.Corners.Any(c=>c.Position==new Vector2(2,0)),"a split cell along a straight edge is not a corner");
    }
    [Test] public void DuplicateRectanglesAndInputOrderDoNotChangeTheSurface() {
        var a=R(.5f,.5f,4,2);var b=R(2.5f,2.5f,2,2);
        var original=ArenaEnvironmentGeometry.Build(new[]{a,b});var repeated=ArenaEnvironmentGeometry.Build(new[]{b,a,a});
        CollectionAssert.AreEqual(original.Cells,repeated.Cells);CollectionAssert.AreEqual(original.Edges,repeated.Edges);CollectionAssert.AreEqual(original.Corners,repeated.Corners);
    }
    [Test] public void SnowHasOneConnectedOffCentreLakeAndTheDefaultWaterBudget() {
        var map=Load(4);var surface=ArenaEnvironmentGeometry.Build(map.water);
        Assert.AreEqual(5,map.water.Length);Assert.AreEqual(60,map.walls.Length);Assert.AreEqual(24,map.bricks.Length);
        Assert.AreEqual(648,Area(surface));Assert.AreEqual(Area(ArenaEnvironmentGeometry.Build(Load(2).water)),Area(surface));
        Assert.AreEqual(30,map.water.Min(r=>r.y));Assert.AreEqual(54,map.water.Max(r=>r.y+r.h));
        Assert.AreEqual(38,map.water.Min(r=>r.x));Assert.AreEqual(74,map.water.Max(r=>r.x+r.w));
        Assert.IsTrue(map.Water(56,32)&&map.Water(56,42)&&map.Water(56,52));
        Assert.IsFalse(map.Water(40,32)||map.Water(72,52),"tapered shores must not turn back into a rectangle or parallel canals");
        for(int i=0;i<map.water.Length;i++) {
            var water=map.water[i];
            for(int j=i+1;j<map.water.Length;j++)Assert.IsFalse(Bounds(water).Overlaps(Bounds(map.water[j])));
            if(i>0) {
                var previous=map.water[i-1];Assert.AreEqual(previous.y+previous.h,water.y);
                Assert.Greater(Mathf.Min(previous.x+previous.w,water.x+water.w)-Mathf.Max(previous.x,water.x),12,"all shelves belong to one broad connected lake");
            }
            foreach(var rect in map.walls.Concat(map.bricks))Assert.IsFalse(Bounds(water).Overlaps(Bounds(rect)));
            foreach(var p in map.spawns.Concat(map.itemSpawns)) {
                float radius=GameRules.TankHalfSize*Mathf.Sqrt(2);
                Assert.IsFalse(Bounds(water).Overlaps(new Rect(p.x-radius,p.y-radius,radius*2,radius*2)));
            }
            foreach(var zone in map.battlefield.fireZones)Assert.IsFalse(Bounds(water).Overlaps(new Rect(zone.x-5,zone.y-5,10,10)));
        }
        Assert.IsEmpty(ArenaBiomeGeometry.Bridges(map));ExteriorEdges(map.water,surface);
    }
    [TestCase(2)] [TestCase(3)] [TestCase(4)]
    public void ActualMapSurfacesHaveOnlyExternalEdgesAndNoOverlappingCells(int id) {
        var map=Load(id);
        foreach(var rects in new[]{map.water,map.walls}) {
            var surface=ArenaEnvironmentGeometry.Build(rects);Assert.IsNotEmpty(surface.Cells);Assert.IsNotEmpty(surface.Edges);ExteriorEdges(rects,surface);
            for(int i=0;i<surface.Cells.Length;i++)for(int j=i+1;j<surface.Cells.Length;j++)Assert.IsFalse(surface.Cells[i].Overlaps(surface.Cells[j]));
        }
    }
    [TestCase(3)]
    public void FourLandmarksFitUniqueRealCoverAndUseTheExpectedBiomeAssets(int id) {
        var map=Load(id);var props=ArenaEnvironmentGeometry.Scenery(map).Where(p=>!p.HideWallBase).ToArray();var repeated=ArenaEnvironmentGeometry.Scenery(map).Where(p=>!p.HideWallBase).ToArray();
        Assert.AreEqual(4,props.Length);CollectionAssert.AreEqual(props,repeated);Assert.AreEqual(4,props.Select(p=>p.WallId).Distinct().Count());
        Assert.AreEqual("building_tower",props[0].Key);Assert.IsTrue(props[0].Campaign);
        foreach(var prop in props) {
            Assert.GreaterOrEqual(prop.WallId,4);var wall=map.walls.Single(w=>w.id==prop.WallId);
            Assert.GreaterOrEqual(wall.w,3);Assert.GreaterOrEqual(wall.h,3);Assert.IsTrue(Contains(Bounds(wall),prop.Bounds));
            Assert.LessOrEqual(prop.Bounds.width,2.8f);Assert.LessOrEqual(prop.Bounds.height,2.8f);
            Assert.IsTrue(Contains(new Rect(0,0,map.width,map.height),prop.Bounds));
            if(prop.Campaign)Assert.Contains(prop.Key,CampaignCartoonCatalog.Required);else Assert.Contains(prop.Key,ArenaBiomeArt.Required);
            foreach(var spawn in map.spawns.Concat(map.itemSpawns))Assert.IsFalse(prop.Bounds.Contains(new Vector2(spawn.x,spawn.y)));
        }
        CollectionAssert.AreEqual(id==3?new[]{"building_tower","desert_rock","desert_cactus","desert_scrub"}:new[]{"building_tower","snow_rock","snow_pine","snow_pine"},props.Select(p=>p.Key));
    }
    [TestCase(2,16)] [TestCase(3,18)] [TestCase(4,44)]
    public void StandaloneSceneryHasRealSmallRootCollisionAwayFromWallsPondsSpawnsItemsAndFireTargets(int id,int count) {
        var map=Load(id);var props=ArenaEnvironmentGeometry.Scenery(map).Where(p=>p.HideWallBase).ToArray();
        Assert.AreEqual(count,props.Length);Assert.AreEqual(count,map.walls.Count(w=>ArenaEnvironmentGeometry.IsSceneryWall(map,w.id)));
        CollectionAssert.AreEqual(props,ArenaEnvironmentGeometry.Scenery(map).Where(p=>p.HideWallBase));
        var hp=Enumerable.Repeat((byte)GameRules.BrickMaxHp,64).ToArray();
        foreach(var prop in props) {
            var root=map.walls.Single(w=>w.id==prop.WallId);Assert.IsTrue(Contains(prop.Bounds,Bounds(root)));
            Assert.IsTrue(map.Blocked(root.x+root.w/2,root.y+root.h/2,hp),"visible solid scenery needs authoritative collision");
            Assert.Less(root.w,prop.Bounds.width);Assert.Less(root.h,prop.Bounds.height);
            foreach(var wall in map.walls.Where(w=>!ArenaEnvironmentGeometry.IsSceneryWall(map,w.id)))Assert.IsFalse(prop.Bounds.Overlaps(Bounds(wall)),"scenery must not sit on a wall");
            foreach(var r in map.water.Concat(map.bricks))Assert.IsFalse(prop.Bounds.Overlaps(Bounds(r)));
            foreach(var spawn in map.spawns.Concat(map.itemSpawns))Assert.Greater(Vector2.Distance(prop.Bounds.center,new Vector2(spawn.x,spawn.y)),4);
            foreach(var spawn in map.spawns.Concat(map.itemSpawns))Assert.IsFalse(map.Blocked(spawn.x,spawn.y,4500,hp),"scenery roots must leave rotated spawn and item clearance");
            foreach(var zone in map.battlefield.fireZones)Assert.IsFalse(Bounds(root).Overlaps(new Rect(zone.x-5,zone.y-5,10,10)));
        }
        if(id==2) {
            Assert.AreEqual(1,props.Count(p=>p.Key=="building_tower"));Assert.AreEqual(4,props.Count(p=>p.Key=="rock"));
            Assert.IsTrue(props.All(p=>p.Campaign));
            for(int group=0;group<4;group++) {
                var cluster=props.Skip(group*4).Take(4).ToArray();
                Assert.GreaterOrEqual(cluster.Count(p=>p.Key=="tree"||p.Key=="trees"),2);
                foreach(var prop in cluster)Assert.IsTrue(cluster.Any(other=>other.WallId!=prop.WallId&&Vector2.Distance(other.Bounds.center,prop.Bounds.center)<4.5f),"props should read as a group rather than isolated landmarks");
            }
            foreach(var prop in props)Assert.GreaterOrEqual(prop.Bounds.width,prop.Key=="rock"?3.2f:4);
            var roads=new[]{new Rect(4,4,112,4),new Rect(4,72,112,4),new Rect(4,8,4,64),new Rect(112,8,4,64)};
            foreach(var prop in props)foreach(var road in roads)Assert.IsFalse(prop.Bounds.Overlaps(road));
        } else if(id==3) {
            Assert.IsTrue(props.All(p=>p.Key=="oasis_palm"&&!p.Campaign));
            Assert.IsTrue(props.All(p=>p.Bounds.width>=4.2f));
            foreach(var pond in map.water) {
                var shore=Bounds(pond);shore.xMin-=4;shore.xMax+=4;shore.yMin-=4;shore.yMax+=4;
                var palms=props.Where(p=>shore.Contains(p.Bounds.center)).ToArray();
                Assert.AreEqual(9,palms.Length,"each oasis has two dense groups at its outer shore corners");
                foreach(var palm in palms)Assert.IsTrue(palms.Any(other=>other.WallId!=palm.WallId&&Vector2.Distance(other.Bounds.center,palm.Bounds.center)<3.6f));
            }
        } else {
            Assert.AreEqual(44,ArenaEnvironmentGeometry.Scenery(map).Length,"winter scenery belongs on the ground rather than on wall tops");
            Assert.IsTrue(props.All(p=>!p.Campaign));
            Assert.AreEqual(28,props.Count(p=>p.Key=="snow_pine"));Assert.AreEqual(8,props.Count(p=>p.Key=="snow_rock"));
            Assert.AreEqual(4,props.Count(p=>p.Key=="igloo"));Assert.AreEqual(4,props.Count(p=>p.Key=="ice_shards"));
            foreach(var prop in props.Where(p=>p.Key=="snow_pine"||p.Key=="igloo"))
                Assert.IsTrue(props.Any(other=>other.WallId!=prop.WallId&&Vector2.Distance(other.Bounds.center,prop.Bounds.center)<9),"forest and settlement props must form groups");
            Assert.IsTrue(props.Where(p=>p.Key=="igloo").All(p=>p.Bounds.center.x>90),"the igloo settlements belong on the east side of the lake");
            float radius=GameRules.TankHalfSize*Mathf.Sqrt(2);
            for(int i=0;i<props.Length;i++) {
                var prop=props[i];Assert.GreaterOrEqual(prop.Bounds.width,3.2f);Assert.LessOrEqual(prop.Bounds.width,7.2f);
                Assert.IsTrue(Contains(new Rect(0,0,map.width,map.height),prop.Bounds));
                foreach(var p in map.spawns.Concat(map.itemSpawns))Assert.IsFalse(prop.Bounds.Overlaps(new Rect(p.x-radius,p.y-radius,radius*2,radius*2)),"winter canopy must leave the full rotated spawn/item body visible");
                for(int j=i+1;j<props.Length;j++)Assert.IsFalse(prop.Bounds.Overlaps(props[j].Bounds),"separate snowy silhouettes must not merge into a single unreadable obstacle");
            }
        }
    }
    [Test] public void ScenerySkipsLegacyMapsAndCannotInventCoverWhenNoWallFits() {
        Assert.IsEmpty(ArenaEnvironmentGeometry.Scenery(null));
        Assert.IsEmpty(ArenaEnvironmentGeometry.Scenery(new GameMap {mapId=1,walls=Load(2).walls}));
        var map=new GameMap {mapId=2,width=120,height=80,walls=new[]{R(0,0,120,1.5f),R(0,78.5f,120,1.5f),R(0,0,1.5f,80),R(118.5f,0,1.5f,80),R(30,30,2,8)}};
        Assert.IsEmpty(ArenaEnvironmentGeometry.Scenery(map));
        map.mapId=4;map.walls[4]=R(30,30,3,8);Assert.IsEmpty(ArenaEnvironmentGeometry.Scenery(map),"winter decorations require their authored ground roots");
    }
    [TestCase(2,"1afb24e8b0681e24637d8ee7927fb0a1f630b660c1aff09dd09463f1c1a0c8de")]
    [TestCase(3,"246cb0f29ff737b8b4ec41a2b90044f2e01684fe401fba968f6317fa1951d8a4")]
    [TestCase(4,"5693ccbb553ed4c3d6dc9e12b0126ade230a23241dc9f9ad06d3dfbadbc5edd2")]
    public void DressingAndUnionConstructionPreserveMapBytesSpawnsAndItems(int id,string expectedHash) {
        string path=Path.Combine(Application.streamingAssetsPath,FileName(id));var beforeBytes=File.ReadAllBytes(path);var map=Load(id);string before=JsonUtility.ToJson(map);
        var spawns=map.spawns;var items=map.itemSpawns;var walls=map.walls;var water=map.water;
        ArenaEnvironmentGeometry.Build(walls);ArenaEnvironmentGeometry.Build(water);ArenaEnvironmentGeometry.Scenery(map);
        Assert.AreSame(spawns,map.spawns);Assert.AreSame(items,map.itemSpawns);Assert.AreSame(walls,map.walls);Assert.AreSame(water,map.water);
        Assert.AreEqual(before,JsonUtility.ToJson(map));CollectionAssert.AreEqual(beforeBytes,File.ReadAllBytes(path));
        using(var sha=SHA256.Create())Assert.AreEqual(expectedHash,BitConverter.ToString(sha.ComputeHash(beforeBytes)).Replace("-","").ToLowerInvariant());
    }
}
