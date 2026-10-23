package tank;

import org.junit.jupiter.api.*;
import org.junit.jupiter.api.io.TempDir;
import java.nio.file.*;
import java.net.Socket;
import java.util.*;
import static tank.CampaignRun.*;
import static org.junit.jupiter.api.Assertions.*;

class CampaignProgressionTest {
    @TempDir Path tmp;
    CampaignRun clean(){var r=new CampaignRun(0,42);r.enemies.clear();r.loot.clear();r.generated=GOAL;r.nextEnemy=200;return r;}
    Enemy boss(CampaignRun r,int type){r.x=6;r.y=500;r.distance=500;r.turn=10;var b=new Enemy(100,5,501,200,12,type);r.enemies.add(b);return b;}
    @Test void eachFiveHundredMetersChangesTheSquadAndIncreasesPressure(){
        Set<String> squads=new HashSet<>();int size=0,hp=0,damage=0,spacing=100;
        for(int i=0;i<20;i++){
            var stage=CampaignDifficulty.at(i*500);assertEquals(i,stage.index());assertTrue(squads.add(Arrays.toString(stage.roles())));
            assertTrue(stage.roles().length>=size);size=stage.roles().length;
            assertTrue(CampaignDifficulty.hp(i*500,Role.MELEE)>=hp);hp=CampaignDifficulty.hp(i*500,Role.MELEE);
            assertTrue(CampaignDifficulty.damage(i*500,Role.RANGED)>damage);damage=CampaignDifficulty.damage(i*500,Role.RANGED);
            assertTrue(stage.spacing()<=spacing);spacing=stage.spacing();assertFalse(stage.name().isBlank());
        }
        assertEquals(0,CampaignDifficulty.at(499).index());assertEquals(1,CampaignDifficulty.at(500).index());assertEquals(19,CampaignDifficulty.at(10000).index());
    }
    @Test void generatedSquadsMatchStageSizeAndNeverOccupyWallsOrEachOther(){
        for(int stage=0;stage<20;stage++){
            var r=clean();int row=stage*500+7;CampaignTactics.spawn(r,row);
            assertEquals(CampaignDifficulty.at(row).roles().length,r.enemies.size(),"stage="+stage);
            Set<Integer> occupied=new HashSet<>();for(var e:r.enemies){assertTrue(occupied.add(e.y*WIDTH+e.x));assertEquals(0,r.terrain(e.x,e.y));}
        }
    }
    @Test void advancedSquadsRallyWhenTheFrontLineSpotsThePlayer(){
        var r=clean();var front=new Enemy(1,6,3,100,0,0);front.squad=7;
        int fy=15;var rear=new Enemy(2,CampaignMap.pathX(r.seed,fy),fy,100,0,0);rear.squad=7;r.enemies.add(front);r.enemies.add(rear);
        int oldX=rear.x,oldY=rear.y;assertTrue(r.range(rear)>12);r.react();assertEquals(oldX,rear.x);assertEquals(oldY,rear.y);
        r.distance=1500;r.react();assertEquals(1,CampaignTactics.distance(oldX,oldY,rear.x,rear.y));
    }
    @Test void halfHealthChangesPhaseOnceAndSummonsDelayedReinforcements(){
        var r=clean();var b=boss(r,2);r.hit(b,99);assertEquals(1,b.phase);assertEquals(1,r.enemies.size());
        r.hit(b,1);assertEquals(2,b.phase);assertTrue(r.enemies.size()>1);int count=r.enemies.size();
        var reserves=r.enemies.stream().filter(e->e.boss==0).toList();for(var e:reserves)assertEquals(r.turn+2,e.readyTurn);
        r.hit(b,1);assertEquals(count,r.enemies.size());r.turn++;r.react();assertEquals(100,r.hp);
        assertTrue(r.dangers.stream().allMatch(d->d.due()==r.turn+1));
    }
    @Test void lethalHitDoesNotSummonOrTriggerAnExtraPhase(){
        var r=clean();var b=boss(r,4);r.hit(b,10000);assertTrue(r.enemies.isEmpty());assertTrue(r.dangers.isEmpty());assertEquals(1,r.bosses);
    }
    @Test void bossWarningsGiveOneFullTurnAndShopDoesNotAdvanceTheirTimer(){
        var r=clean();var b=boss(r,1);b.phase=2;r.hp=50;r.react();assertEquals(50,r.hp);assertEquals(1,r.dangers.size());assertEquals(11,r.dangers.getFirst().due());
        r.shot=1;assertFalse(r.act(4));assertEquals(10,r.turn);assertEquals(1,r.dangers.size());
        r.gold=1000;assertTrue(r.buy(0));assertEquals(75,r.hp);assertEquals(10,r.turn);assertEquals(1,r.dangers.size());
        assertTrue(r.act(1));assertEquals(11,r.turn);assertEquals(75,r.hp);assertTrue(r.dangers.isEmpty());
    }
    @Test void stayingInTheWarningTakesOneAttackAndShieldBlocksIt(){
        for(int shield=0;shield<=1;shield++){
            var r=clean();var b=boss(r,1);b.phase=2;r.shields=shield;r.react();r.facing=1;assertTrue(r.act(4));
            assertEquals(shield==0?84:100,r.hp);assertEquals(0,r.shields);assertTrue(r.dangers.isEmpty());
        }
    }
    @Test void killingBossCancelsPendingStrikes(){
        var r=clean();var b=boss(r,1);b.phase=2;r.react();assertFalse(r.dangers.isEmpty());r.hit(b,10000);
        assertTrue(r.dangers.isEmpty());r.turn++;r.react();assertEquals(100,r.hp);
    }
    @Test void everyBossPatternLeavesAFreeEscapeCell(){
        Set<String> patterns=new HashSet<>();
        for(int type=1;type<=4;type++){
            var r=clean();var b=boss(r,type);b.phase=2;r.react();assertEquals(100,r.hp);assertFalse(r.dangers.isEmpty());
            Set<Integer> danger=new HashSet<>();for(var d:r.dangers){assertEquals(11,d.due());assertEquals(0,r.terrain(d.x(),d.y()));danger.add(d.y()*WIDTH+d.x());}
            patterns.add(danger.toString());boolean safe=false;
            for(int d=0;d<4;d++){int x=r.x+DX[d],y=r.y+DY[d];if(r.terrain(x,y)==0&&r.at(x,y)==null&&!danger.contains(y*WIDTH+x))safe=true;}
            assertTrue(safe,"boss="+type);
        }
        assertEquals(4,patterns.size());
    }
    @Test void pressureGatesHaveARealFlankRouteAndBlockShootingThroughTheBarrier(){
        for(int stage=0;stage<20;stage++){
            var r=clean();int section=(stage*500+64)/CampaignMap.SECTION,row=section*CampaignMap.SECTION+CampaignMap.pressureRow(stage)+1;
            int main=CampaignMap.pathX(r.seed,row),flank=(CampaignMap.hash(r.seed,section)&32)==0?1:11;
            assertEquals(0,r.terrain(main,row));assertEquals(0,r.terrain(flank,row));int closed=0;
            for(int x=0;x<WIDTH;x++)if(r.terrain(x,row)!=0)closed++;
            assertTrue(closed>=6,"Pressure wall missing at stage "+stage);
            for(int x=0;x<WIDTH;x++)if(r.terrain(x,row)!=0&&r.terrain(x,row-1)==0&&r.terrain(x,row+1)==0)assertFalse(CampaignTactics.clear(r,x,row-1,x,row+1,true));
        }
    }
    @Test void allLateGameWalkableCellsRemainConnected(){
        var map=new CampaignMap(42);boolean[] seen=new boolean[(GOAL+1)*WIDTH];ArrayDeque<Integer> q=new ArrayDeque<>();q.add(6);seen[6]=true;
        while(!q.isEmpty()){
            int cell=q.remove(),x=cell%WIDTH,y=cell/WIDTH;
            for(int d=0;d<4;d++){int nx=x+DX[d],ny=y+DY[d];if(nx<0||nx>=WIDTH||ny<0||ny>GOAL)continue;int next=ny*WIDTH+nx;if(!seen[next]&&map.tile(nx,ny)==0){seen[next]=true;q.add(next);}}
        }
        for(int y=0;y<=GOAL;y++)for(int x=0;x<WIDTH;x++)if(map.tile(x,y)==0)assertTrue(seen[y*WIDTH+x],"Disconnected tile at "+x+","+y);
    }
    @Test void exportsBossPhaseAndHazardsWithoutChangingVersionFourPayload()throws Exception {
        var helper=new RoomFeaturesTest();helper.tmp=tmp;
        try(Server server=helper.server()){
            var session=server.new Session(new Socket());var r=clean();var b=boss(r,2);r.y=1500;r.distance=1500;b.y=1501;b.hp=80;b.phase=2;r.react();
            session.campaign.run=r;session.campaign.visualEvents=session.campaign.tactics=session.campaign.shop=true;
            session.campaign.state(128);byte[] old=Protocol.decode(session.outbound.remove(),false).payload();
            session.campaign.progression=true;session.campaign.state(128);byte[] packet=session.outbound.remove();assertArrayEquals(old,Arrays.copyOf(Protocol.decode(packet,false).payload(),old.length));
            Path path=Path.of("target/interop/campaign-progression.bin");Files.createDirectories(path.getParent());Files.write(path,packet);
        }
    }
    @Test void tcpVersionFiveNegotiatesProgressionAndStillAcceptsShopPurchases()throws Exception {
        var helper=new RoomFeaturesTest();helper.tmp=tmp;
        try(Server server=helper.server()){
            server.start();try(var socket=new Socket("127.0.0.1",server.tcp.getLocalPort())){
                socket.setSoTimeout(5000);byte[] token=BaselineTest.auth(socket,"progress_user");
                var session=server.tokens.get(java.util.HexFormat.of().formatHex(token));
                BaselineTest.send(socket,90,new Protocol.Writer().u8(5).u8(0).u8(0).done());
                assertEquals(0,new Protocol.Reader(BaselineTest.waitFor(socket,91).payload()).u16());
                assertTrue(session.campaign.progression);assertTrue(session.campaign.shop);assertNull(session.room);
                var r=session.campaign.run;r.gold=100;r.hp=50;
                BaselineTest.send(socket,90,new Protocol.Writer().u8(5).u8(5).str(r.id).u32(0).u32(0).u8(0).done());
                assertEquals(0,new Protocol.Reader(BaselineTest.waitFor(socket,91).payload()).u16());assertEquals(75,r.hp);assertEquals(80,r.gold);assertEquals(0,r.turn);
            }
        }
    }

}
