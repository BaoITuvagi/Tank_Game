package tank;

import org.junit.jupiter.api.*;
import org.junit.jupiter.api.io.TempDir;
import java.nio.file.*;
import java.net.Socket;
import java.util.*;
import static org.junit.jupiter.api.Assertions.*;

class CampaignMapTest {
    @TempDir Path tmp;
    @Test void everySectionConnectsAndAllWalkableCellsAreReachable(){
        for(long seed=0;seed<12;seed++){
            var map=new CampaignMap(seed);
            int height=512;boolean[][] seen=new boolean[height][13];ArrayDeque<int[]> q=new ArrayDeque<>();q.add(new int[]{6,0});seen[0][6]=true;
            while(!q.isEmpty()){
                var p=q.remove();for(var d:new int[][]{{0,1},{1,0},{0,-1},{-1,0}}){int x=p[0]+d[0],y=p[1]+d[1];if(x<0||x>=13||y<0||y>=height||seen[y][x]||map.tile(x,y)!=0)continue;seen[y][x]=true;q.add(new int[]{x,y});}
            }
            for(int y=0;y<height;y++)for(int x=0;x<13;x++)if(map.tile(x,y)==0)assertTrue(seen[y][x],"Unreachable clearing seed="+seed+" x="+x+" y="+y);
            assertTrue(seen[height-1][6]);
        }
    }
    @Test void passagesBossArenasAndChestsStayWalkableThroughTheEntireCampaign(){
        for(long seed:new long[]{0,17,42,1234567}){
            var run=new CampaignRun(0,seed);
            for(int y=0;y<=10000;y++){
                int path=CampaignMap.pathX(seed,y);assertEquals(0,run.terrain(path,y));
                for(int x=0;x<13;x++)if(y%500<=3||y%500>=497)assertEquals(0,run.terrain(x,y));
                run.y=y;run.distance=y;run.generate();assertTrue(run.campaignMap.sections.size()<=8);for(var item:run.loot)assertEquals(0,run.terrain(item.x(),item.y()));
            }
        }
    }
    @Test void layoutsAreStableContainRealTreesAndVaryAcrossSections(){
        Set<Integer> styles=new HashSet<>();int[] kinds=new int[5];var map=new CampaignMap(42);
        for(int section=0;section<24;section++){
            styles.add(CampaignMap.style(42,section));
            for(int row=0;row<32;row++)for(int x=0;x<13;x++){int y=section*32+row,t=map.tile(x,y);kinds[t]++;assertEquals(t,map.tile(x,y));}
        }
        assertEquals(4,styles.size());for(int i=1;i<5;i++)assertTrue(kinds[i]>20,"Missing terrain type "+i);
        // Forest is clustered: most trees have another blocking tree next to them.
        int trees=0,clustered=0;for(int y=4;y<128;y++)for(int x=0;x<13;x++)if(map.tile(x,y)==1){trees++;if(map.tile(x-1,y)==1||map.tile(x+1,y)==1||map.tile(x,y-1)==1||map.tile(x,y+1)==1)clustered++;}
        assertTrue(clustered>trees*.8);
    }
    @Test void enemiesWalkAroundForestAndRoomWalls(){
        int scenarios=0;
        for(long seed=0;seed<8;seed++)for(int row=7;row<30;row+=4){
            var run=new CampaignRun(0,seed);run.enemies.clear();run.loot.clear();run.y=row;run.x=CampaignMap.pathX(seed,row);
            for(int startX:new int[]{1,11}){
                if(run.terrain(startX,row)!=0)continue;
                var enemy=new CampaignRun.Enemy(1,startX,row,100,1,0);run.enemies.clear();run.enemies.add(enemy);
                for(int i=0;i<45&&run.range(enemy)>1;i++){run.react();assertEquals(0,run.terrain(enemy.x,enemy.y));}
                assertEquals(1,run.range(enemy),"Enemy stuck seed="+seed+" row="+row+" x="+startX);scenarios++;
            }
        }
        assertTrue(scenarios>=30);
    }
    @Test void exportAuthoritativeLayoutFixturesForClientPreview()throws Exception{
        var helper=new RoomFeaturesTest();helper.tmp=tmp;Set<Integer> exported=new HashSet<>();
        try(Server server=helper.server()){
            for(long seed=0;exported.size()<4&&seed<100;seed++){
                int style=CampaignMap.style(seed,0);if(!exported.add(style))continue;
                var session=server.new Session(new Socket());var run=new CampaignRun(1,seed);run.y=8;run.distance=8;run.x=CampaignMap.pathX(seed,run.y);run.generate();
                session.campaign.run=run;session.campaign.visualEvents=true;session.campaign.state(125);
                Path path=Path.of("target/interop/campaign-layout-"+style+".bin");Files.createDirectories(path.getParent());Files.write(path,session.outbound.remove());
            }
        }
        assertEquals(4,exported.size());
    }
}
