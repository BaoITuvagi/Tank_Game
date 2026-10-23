package tank;

import org.junit.jupiter.api.*;
import org.junit.jupiter.api.io.TempDir;
import java.nio.file.*;
import java.net.Socket;
import java.util.*;
import static tank.CampaignRun.*;
import static org.junit.jupiter.api.Assertions.*;

class CampaignTacticsTest {
    @TempDir Path tmp;
    CampaignRun clean(){var r=new CampaignRun(0,42);r.enemies.clear();r.loot.clear();r.generated=GOAL;return r;}
    Enemy add(CampaignRun r,int id,int x,int y,Role role,int squad){var e=new Enemy(id,x,y,100,8,0);e.role=role;e.squad=squad;r.enemies.add(e);return e;}
    @Test void rangedFiresFromDistanceAndNeedsARecoveryTurn(){
        var r=clean();var gun=add(r,1,2,0,Role.RANGED,1);
        r.react();assertEquals(92,r.hp);assertEquals(2,gun.x);assertEquals(2,gun.cooldown);
        assertTrue(r.visuals.stream().anyMatch(v->v.kind()==SHOT&&v.source()==gun.id));
        r.react();assertEquals(92,r.hp);r.react();assertEquals(84,r.hp);
    }
    @Test void rangedCannotShootThroughAnAlly(){
        var r=clean();var gun=add(r,1,6,4,Role.RANGED,1);add(r,2,6,1,Role.MELEE,1);
        r.react();assertFalse(r.visuals.stream().anyMatch(v->v.kind()==SHOT&&v.source()==gun.id));
    }
    @Test void rangedRetreatsInsteadOfTradingMeleeHits(){
        var r=clean();var gun=add(r,1,6,1,Role.RANGED,1);
        r.react();assertEquals(100,r.hp);assertEquals(2,r.range(gun));assertEquals(0,r.terrain(gun.x,gun.y));
    }
    @Test void terrainBlocksRangedLineOfSight(){
        boolean found=false;
        for(long seed=0;seed<10&&!found;seed++){
            var r=new CampaignRun(0,seed);r.enemies.clear();
            for(int y=6;y<50&&!found;y++)for(int x=1;x<12&&!found;x++)
                if(r.terrain(x,y)!=0&&r.terrain(x-1,y)==0&&r.terrain(x+1,y)==0){assertFalse(CampaignTactics.clear(r,x-1,y,x+1,y,true));found=true;}
        }
        assertTrue(found);
    }
    @Test void supportAbsorbsTenDamageOnceUntilTheNextEnemyTurn(){
        var r=clean();var front=add(r,1,6,1,Role.MELEE,7);add(r,2,6,2,Role.SUPPORT,7);
        CampaignTactics.refreshGuards(r);assertEquals(10,front.guard);
        r.hit(front,20);assertEquals(90,front.hp);assertEquals(0,front.guard);
        r.hit(front,20);assertEquals(70,front.hp);
        assertTrue(r.visuals.stream().anyMatch(v->v.kind()==BLOCK&&v.target()==front.id&&v.amount()==10));
        r.react();assertEquals(10,front.guard);
    }
    @Test void eliminatingSupportImmediatelyRemovesProtection(){
        var r=clean();var front=add(r,1,6,1,Role.MELEE,7);var medic=add(r,2,6,2,Role.SUPPORT,7);
        CampaignTactics.refreshGuards(r);r.hit(medic,100);r.hit(front,20);
        assertEquals(80,front.hp);assertFalse(CampaignTactics.covered(r,front));
    }
    @Test void supportNeverProtectsItselfBossesOrOtherSquads(){
        var r=clean();var own=add(r,1,6,1,Role.MELEE,7);var other=add(r,2,7,2,Role.MELEE,8);var medic=add(r,3,6,2,Role.SUPPORT,7);
        var boss=new Enemy(4,5,2,200,20,1);boss.squad=7;r.enemies.add(boss);
        CampaignTactics.refreshGuards(r);assertEquals(10,own.guard);assertEquals(0,other.guard);assertEquals(0,medic.guard);assertEquals(0,boss.guard);
    }
    @Test void shieldStillStopsRangedDamage(){
        var r=clean();r.shields=1;add(r,1,2,0,Role.RANGED,1);r.react();assertEquals(100,r.hp);assertEquals(0,r.shields);
        assertTrue(r.visuals.stream().anyMatch(v->v.kind()==BLOCK&&v.target()==0));
    }
    @Test void squadMeleeUnitsApproachDifferentFlanks(){
        var r=clean();var left=add(r,1,5,3,Role.MELEE,7);var right=add(r,2,7,3,Role.MELEE,7);
        for(int i=0;i<3;i++)r.react();
        assertEquals(5,left.x);assertEquals(0,left.y);assertEquals(7,right.x);assertEquals(0,right.y);
    }
    @Test void formationsAreMixedDeterministicAndDoNotOverlap(){
        for(int row:new int[]{48,120,600}){
            var a=clean();var b=clean();CampaignTactics.spawn(a,row);CampaignTactics.spawn(b,row);
            assertTrue(a.enemies.size()>=3);assertEquals(a.enemies.size(),b.enemies.size());
            Set<Integer> cells=new HashSet<>();Set<Role> roles=new HashSet<>();
            for(int i=0;i<a.enemies.size();i++){
                var e=a.enemies.get(i);var twin=b.enemies.get(i);assertTrue(e.squad>0);assertEquals(a.enemies.getFirst().squad,e.squad);
                assertEquals(0,a.terrain(e.x,e.y));assertTrue(cells.add(e.y*WIDTH+e.x));roles.add(e.role);
                assertEquals(e.x,twin.x);assertEquals(e.y,twin.y);assertEquals(e.role,twin.role);
            }
            assertTrue(roles.contains(Role.MELEE));assertTrue(roles.contains(Role.RANGED));assertEquals(row>=120,roles.contains(Role.SUPPORT));
        }
    }
    @Test void squadMovementPreservesOneCellPerEnemyAndTerrainSafety(){
        var r=clean();CampaignTactics.spawn(r,120);r.y=115;r.x=CampaignMap.pathX(r.seed,r.y);r.hp=100000;
        for(int turn=0;turn<40;turn++){
            Map<Integer,int[]> previous=new HashMap<>();for(var e:r.enemies)previous.put(e.id,new int[]{e.x,e.y});
            r.react();Set<Integer> occupied=new HashSet<>();
            for(var e:r.enemies){var p=previous.get(e.id);assertTrue(CampaignTactics.distance(p[0],p[1],e.x,e.y)<=1);assertEquals(0,r.terrain(e.x,e.y));assertTrue(occupied.add(e.y*WIDTH+e.x));assertFalse(e.x==r.x&&e.y==r.y);}
        }
    }
    @Test void publishesTacticsFixtureWithoutChangingVersionTwoPayload()throws Exception {
        var helper=new RoomFeaturesTest();helper.tmp=tmp;
        try(Server server=helper.server()){
            var session=server.new Session(new Socket());var run=clean();
            add(run,12,6,3,Role.RANGED,7);add(run,13,7,2,Role.SUPPORT,7);add(run,14,6,1,Role.MELEE,7);CampaignTactics.refreshGuards(run);
            session.campaign.run=run;session.campaign.visualEvents=true;session.campaign.state(126);
            byte[] legacy=Protocol.decode(session.outbound.remove(),false).payload();
            session.campaign.tactics=true;session.campaign.state(126);byte[] packet=session.outbound.remove();byte[] modern=Protocol.decode(packet,false).payload();
            assertArrayEquals(legacy,Arrays.copyOf(modern,legacy.length));
            Path path=Path.of("target/interop/campaign-tactics.bin");Files.createDirectories(path.getParent());Files.write(path,packet);
        }
    }
    @Test void supportHoldsBehindTheFrontLineAndLosesCoverageAtDistance(){
        var r=clean();var front=add(r,1,6,1,Role.MELEE,7);var support=add(r,2,6,2,Role.SUPPORT,7);
        r.react();assertTrue(support.y>front.y);assertEquals(10,front.guard);
        support.x=1;assertFalse(CampaignTactics.covered(r,front));r.hit(front,20);assertEquals(80,front.hp);
    }
    @Test void tcpVersionThreeNegotiatesTacticsWithoutEnteringAnyRoom()throws Exception {
        var helper=new RoomFeaturesTest();helper.tmp=tmp;
        try(Server server=helper.server()){
            server.start();try(var socket=new java.net.Socket("127.0.0.1",server.tcp.getLocalPort())){
                socket.setSoTimeout(5000);byte[] token=BaselineTest.auth(socket,"tactics_user");
                var session=server.tokens.get(java.util.HexFormat.of().formatHex(token));
                BaselineTest.send(socket,90,new Protocol.Writer().u8(3).u8(0).u8(0).done());
                var response=BaselineTest.waitFor(socket,91);assertEquals(0,new Protocol.Reader(response.payload()).u16());
                assertTrue(session.campaign.tactics);assertTrue(session.campaign.visualEvents);assertNull(session.room);assertTrue(server.rooms.isEmpty());
                BaselineTest.send(socket,90,new Protocol.Writer().u8(3).u8(1).u32(0).u8(0).done());
                assertEquals(0,new Protocol.Reader(BaselineTest.waitFor(socket,91).payload()).u16());assertEquals(1,session.campaign.run.turn);
            }
        }
    }

}
