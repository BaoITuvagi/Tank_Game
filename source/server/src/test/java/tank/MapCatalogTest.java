package tank;
import java.nio.file.*;
import java.security.MessageDigest;
import java.util.*;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.io.TempDir;
import static org.junit.jupiter.api.Assertions.*;
import static tank.GameRules.*;

class MapCatalogTest {
    static final Path DIRECTORY=Path.of("../../source/client/Assets/StreamingAssets");
    MapCatalog catalog()throws Exception {return MapCatalog.load(DIRECTORY,null);}
    @Test void sharedManifestLoadsThreeNamedMapsAndHashesExactFileBytes()throws Exception {
        var catalog=catalog();assertEquals(List.of(2,3,4),catalog.entries().stream().map(e->e.id).toList());
        assertEquals(List.of("Tiền Đồn Xanh","Pháo Đài Cát","Sông Băng"),catalog.entries().stream().map(e->e.name).toList());
        assertTrue(catalog.contains(MapCatalog.DEFAULT_ID));assertFalse(catalog.contains(1));assertNull(catalog.get(99));
        for(var entry:catalog.entries()) {
            var map=catalog.get(entry.id);assertEquals(entry.id,map.mapId);assertEquals(120,map.width);assertEquals(80,map.height);
            assertEquals(16,map.spawns.length);assertTrue(map.bricks.length<=64);
            assertArrayEquals(MessageDigest.getInstance("SHA-256").digest(Files.readAllBytes(DIRECTORY.resolve(entry.file))),map.hash);
        }
        assertEquals("1afb24e8b0681e24637d8ee7927fb0a1f630b660c1aff09dd09463f1c1a0c8de",HexFormat.of().formatHex(catalog.get(2).hash),"default river has mirrored banks and a real dry central crossing");
        Path fixture=Path.of("target/interop/map-catalog.tsv");Files.createDirectories(fixture.getParent());
        Files.write(fixture,catalog.entries().stream().map(e->e.id+"\t"+e.file+"\t"+HexFormat.of().formatHex(catalog.get(e.id).hash)).toList());
    }
    @Test void configuredDefaultMapInstanceIsPreserved()throws Exception {
        var configured=GameMap.load(DIRECTORY.resolve("map-large.json"));
        assertSame(configured,MapCatalog.load(DIRECTORY,configured).get(2));
    }
    @Test void malformedCatalogCannotSelectUnknownDuplicateMismatchedOrEscapingMaps(@TempDir Path temp)throws Exception {
        for(var entry:catalog().entries())Files.copy(DIRECTORY.resolve(entry.file),temp.resolve(entry.file));
        String original=Files.readString(DIRECTORY.resolve("multiplayer-maps.json"));
        for(String broken:new String[]{original.replace("\"defaultId\":2","\"defaultId\":9"),original.replace("\"id\":3","\"id\":2"),original.replace("map-fortress.json","../map-fortress.json"),original.replace("\"id\":4","\"id\":9"),"{\"defaultId\":2,\"maps\":[]}"}) {
            Files.writeString(temp.resolve("multiplayer-maps.json"),broken);
            assertThrows(IllegalArgumentException.class,()->MapCatalog.load(temp,null));
        }
    }
    @Test void intactBricksAndRotatedTankClearanceStillConnectEverySpawnAndItem()throws Exception {
        var catalog=catalog();
        for(var entry:catalog.entries()) {
            var map=catalog.get(entry.id);byte[] hp=new byte[64];Arrays.fill(hp,(byte)BrickMaxHp);
            int columns=(int)(map.width*2)+1,rows=(int)(map.height*2)+1;
            boolean[] seen=new boolean[columns*rows];var queue=new ArrayDeque<Integer>();
            int start=Math.round(map.spawns[0].y*2)*columns+Math.round(map.spawns[0].x*2);queue.add(start);seen[start]=true;
            while(!queue.isEmpty()) {
                int index=queue.removeFirst(),ix=index%columns,iy=index/columns;
                for(int next:new int[]{index-1,index+1,index-columns,index+columns}) {
                    if(next<0||next>=seen.length||Math.abs(next%columns-ix)+Math.abs(next/columns-iy)!=1||seen[next])continue;
                    float x=next%columns*.5f,y=next/columns*.5f;
                    if(map.blocked(x,y,4500,hp)||wet(map,x,y))continue;
                    seen[next]=true;queue.add(next);
                }
            }
            for(var group:List.of(map.spawns,map.itemSpawns))for(var p:group) {
                assertFalse(map.blocked(p.x,p.y,4500,hp),entry.name+" rotated start");
                assertTrue(seen[Math.round(p.y*2)*columns+Math.round(p.x*2)],entry.name+" unreachable "+p.x+","+p.y);
            }
        }
    }
    boolean wet(GameMap map,float x,float y) {
        for(var r:map.water)if(GameMap.overlap(x,y,r,TankHalfSize*(float)Math.sqrt(2)))return true;return false;
    }
    @Test void newMapsHaveDistinctTopologyAnOpenFrozenLakeAndNoLayerOverlaps()throws Exception {
        var catalog=catalog();var fortress=catalog.get(3);var river=catalog.get(4);
        assertEquals(60,river.walls.length);assertEquals(44,Arrays.stream(river.walls).filter(w->w.id>=16).count());assertEquals(5,river.water.length);
        assertFalse(Arrays.stream(fortress.water).anyMatch(a->Arrays.stream(river.water).anyMatch(b->a.x==b.x&&a.y==b.y&&a.w==b.w&&a.h==b.h)));
        for(var map:List.of(fortress,river))for(var water:map.water)for(var group:List.of(map.walls,map.bricks))for(var r:group)
            assertFalse(water.x<r.x+r.w&&water.x+water.w>r.x&&water.y<r.y+r.h&&water.y+water.h>r.y,"terrain layers overlap");
        byte[] hp=new byte[64];Arrays.fill(hp,(byte)BrickMaxHp);
        for(float y=32;y<=52;y+=.5f)assertTrue(river.water(56,y)&&!river.blocked(56,y,4500,hp),"uninterrupted ice north/south transit");
        for(float x=40;x<=72;x+=.5f)assertTrue(river.water(x,42)&&!river.blocked(x,42,4500,hp),"uninterrupted ice east/west transit");
    }
    @Test void frozenLakeKeepsDefaultWaterBudgetAndStandaloneSceneryHasSafeRoots()throws Exception {
        var catalog=catalog();var baseMap=catalog.get(2);var snow=catalog.get(4);
        assertEquals(5,snow.water.length);assertEquals(60,snow.walls.length);assertEquals(24,snow.bricks.length);
        assertEquals(648,Arrays.stream(snow.water).mapToDouble(r->r.w*r.h).sum());
        assertEquals(Arrays.stream(baseMap.water).mapToDouble(r->r.w*r.h).sum(),Arrays.stream(snow.water).mapToDouble(r->r.w*r.h).sum());
        assertEquals(30,Arrays.stream(snow.water).mapToDouble(r->r.y).min().orElseThrow());
        assertEquals(54,Arrays.stream(snow.water).mapToDouble(r->r.y+r.h).max().orElseThrow());
        assertTrue(snow.water(56,32)&&snow.water(56,42)&&snow.water(56,52));assertFalse(snow.water(40,32)||snow.water(72,52));
        for(int i=0;i<snow.water.length;i++) {
            var pond=snow.water[i];
            if(i>0) {
                var previous=snow.water[i-1];assertEquals(previous.y+previous.h,pond.y);
                assertTrue(Math.min(previous.x+previous.w,pond.x+pond.w)-Math.max(previous.x,pond.x)>12,"connected lake shelves");
            }
            for(var other:snow.water)if(other!=pond)assertFalse(pond.x<other.x+other.w&&pond.x+pond.w>other.x&&pond.y<other.y+other.h&&pond.y+pond.h>other.y);
            for(var group:List.of(snow.spawns,snow.itemSpawns))for(var p:group)
                assertFalse(GameMap.overlap(p.x,p.y,pond,TankHalfSize*(float)Math.sqrt(2)),"spawn/item needs dry clearance for a rotated tank");
        }
        byte[] hp=new byte[64];Arrays.fill(hp,(byte)BrickMaxHp);
        for(int id:new int[]{2,3}) {
            var map=catalog.get(id);int start=id==2?16:18,count=id==2?16:18;assertEquals(start+count,map.walls.length);
            for(int i=start;i<map.walls.length;i++) {
                var root=map.walls[i];assertEquals(i,root.id);assertTrue(map.blocked(root.x+root.w/2,root.y+root.h/2,hp));
                assertTrue(root.w<2&&root.h<2);
                for(var pond:map.water)assertFalse(root.x<pond.x+pond.w&&root.x+root.w>pond.x&&root.y<pond.y+pond.h&&root.y+root.h>pond.y);
                for(var group:List.of(map.spawns,map.itemSpawns))for(var p:group)assertTrue(Math.hypot(p.x-root.x-root.w/2,p.y-root.y-root.h/2)>4);
            }
        }
        for(int i=16;i<snow.walls.length;i++) {
            var root=snow.walls[i];assertEquals(i,root.id);assertTrue(snow.blocked(root.x+root.w/2,root.y+root.h/2,hp));
            assertTrue(root.w<=2.8f&&root.h<=2.4f,"winter scenery uses a small authoritative trunk or building footprint");
            for(var pond:snow.water)assertFalse(root.x<pond.x+pond.w&&root.x+root.w>pond.x&&root.y<pond.y+pond.h&&root.y+root.h>pond.y);
            for(var brick:snow.bricks)assertFalse(root.x<brick.x+brick.w&&root.x+root.w>brick.x&&root.y<brick.y+brick.h&&root.y+root.h>brick.y);
            for(var group:List.of(snow.spawns,snow.itemSpawns))for(var p:group)assertTrue(Math.hypot(p.x-root.x-root.w/2,p.y-root.y-root.h/2)>4);
        }
    }
    @Test void allThreeMapsScheduleCompleteSafeFireAndBombstormRoundsAcrossSeeds()throws Exception {
        var catalog=catalog();
        for(var entry:catalog.entries())for(int kind:new int[]{WorldBattlefield.FIRE,WorldBattlefield.AIRSTRIKE})for(int seed=0;seed<100;seed++) {
            var map=catalog.get(entry.id);World world=new World(map,seed);world.battlefield.configure(18000);world.battlefield.bag.add(kind);world.tick=BattlefieldFirstTicks;world.battlefield.beginTick();
            var wave=world.battlefield.wave;assertNotNull(wave,entry.name+" kind="+kind+" seed="+seed);assertEquals(15,wave.targets().size());
            for(int round=0;round<3;round++)for(int i=0;i<5;i++) {
                var a=map.battlefield.zone(kind,wave.target(round,i));
                for(int previous=kind==WorldBattlefield.AIRSTRIKE?Math.max(0,round-1):round;previous<=round;previous++)for(int j=0;j<(previous==round?i:5);j++) {
                    var b=map.battlefield.zone(kind,wave.target(previous,j));assertTrue(Math.hypot(a.x-b.x,a.y-b.y)>=BattlefieldAirstrikeSpacing);
                }
            }
        }
    }
}
