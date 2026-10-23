package tank;

import java.util.*;
import static tank.CampaignRun.*;

/** Boss phase changes and turn-delayed, avoidable strikes, exclusive to campaign. */
final class CampaignBosses {
    private CampaignBosses(){}
    static void escalate(CampaignRun r,Enemy boss){
        boss.phase=2;boss.damage+=Math.max(2,CampaignDifficulty.at(r.distance).index()/2);boss.cooldown=0;
        Role[] reserve=switch(boss.boss){
            case 1->new Role[]{Role.MELEE,Role.RANGED};
            case 2->new Role[]{Role.MELEE,Role.RANGED,Role.SUPPORT};
            case 3->new Role[]{Role.MELEE,Role.MELEE,Role.RANGED};
            default->new Role[]{Role.MELEE,Role.RANGED,Role.RANGED,Role.SUPPORT};
        };
        CampaignTactics.spawn(r,Math.clamp(boss.y+1,0,GOAL-3),reserve,true);
        r.message="Trùm sang giai đoạn 2! Viện binh đến; tránh các ô cảnh báo đỏ.";
    }
    static Set<Integer> resolve(CampaignRun r){
        Set<Integer> resolved=new HashSet<>();
        for(var it=r.dangers.iterator();it.hasNext();){
            Danger d=it.next();Enemy boss=r.enemies.stream().filter(e->e.id==d.source()&&e.boss>0).findFirst().orElse(null);
            if(boss==null){it.remove();continue;}
            if(d.due()>r.turn)continue;
            it.remove();resolved.add(boss.id);
            r.visual(SKILL,boss.id,-1,boss.x,boss.y,d.x(),d.y(),0);
            if(r.x==d.x()&&r.y==d.y()&&r.hp>0){
                if(r.shields>0){r.shields--;r.visual(BLOCK,boss.id,0,boss.x,boss.y,r.x,r.y,0);}
                else {r.visual(HIT,boss.id,0,boss.x,boss.y,r.x,r.y,Math.min(r.hp,d.damage()));r.hp-=d.damage();}
            }
        }
        return resolved;
    }
    static boolean react(CampaignRun r,Enemy boss,Set<Integer> detonated,Set<Integer> occupied){
        if(detonated.contains(boss.id))return true; // A blast consumes this boss's attack action.
        if(boss.cooldown>0)return false;
        var zones=new LinkedHashSet<Integer>();
        switch(boss.boss){
            case 1->add(r,zones,r.x,r.y);
            case 2->{for(int dx=-1;dx<=1;dx++)add(r,zones,r.x+dx,r.y);}
            case 3->{for(int dy=-1;dy<=1;dy++)add(r,zones,r.x,r.y+dy);}
            default->{add(r,zones,r.x,r.y);for(int dx:new int[]{-1,1})for(int dy:new int[]{-1,1})add(r,zones,r.x+dx,r.y+dy);}
        }
        // Never cover every available escape. The player always gets a full turn to react.
        var exits=new ArrayList<Integer>();
        for(int d=0;d<4;d++){
            int x=r.x+DX[d],y=r.y+DY[d],cell=y*WIDTH+x;
            if(r.terrain(x,y)==0&&!occupied.contains(cell))exits.add(cell);
        }
        if(exits.isEmpty())zones.remove(r.y*WIDTH+r.x);
        else if(exits.stream().allMatch(zones::contains))zones.remove(exits.getFirst());
        for(int cell:zones)r.dangers.add(new Danger(boss.id,cell%WIDTH,cell/WIDTH,r.turn+1,boss.damage+4));
        boss.cooldown=3;
        r.message="Trùm giai đoạn 2: vùng đỏ sẽ nổ sau 1 lượt. Di chuyển để né!";
        return true;
    }
    static void add(CampaignRun r,Set<Integer> cells,int x,int y){if(r.terrain(x,y)==0)cells.add(y*WIDTH+x);}
}
