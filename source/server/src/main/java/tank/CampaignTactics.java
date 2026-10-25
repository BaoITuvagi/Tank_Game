package tank;

import java.util.*;
import static tank.CampaignRun.*;

/** Campaign-only squad generation and one-action-per-turn enemy tactics. */
final class CampaignTactics {
    static final int GUARD=10, FIRE_RANGE=SHOT_RANGE;
    private CampaignTactics(){}
    static int distance(int ax,int ay,int bx,int by){return Math.abs(ax-bx)+Math.abs(ay-by);}
    static boolean clear(CampaignRun r,int ax,int ay,int bx,int by,boolean actors){
        if(ax!=bx&&ay!=by)return false;
        int dx=Integer.signum(bx-ax),dy=Integer.signum(by-ay);
        for(int x=ax+dx,y=ay+dy;x!=bx||y!=by;x+=dx,y+=dy)
            if(r.blocksShot(x,y)||actors&&r.at(x,y)!=null)return false;
        return true;
    }
    static boolean covered(CampaignRun r,Enemy target){
        return target.boss==0&&target.role!=Role.SUPPORT&&target.squad>0&&r.enemies.stream().anyMatch(s->
            s.hp>0&&s.role==Role.SUPPORT&&s.squad==target.squad&&distance(s.x,s.y,target.x,target.y)<=2&&linked(r,s,target));
    }
    static boolean linked(CampaignRun r,Enemy a,Enemy b){
        if(distance(a.x,a.y,b.x,b.y)==1)return true;
        for(int d=0;d<4;d++){
            int nx=a.x+DX[d],ny=a.y+DY[d];
            if(r.terrain(nx,ny)==0&&distance(nx,ny,b.x,b.y)==1)return true;
        }
        return false;
    }
    static void refreshGuards(CampaignRun r){for(Enemy e:r.enemies)e.guard=covered(r,e)?GUARD:0;}
    static void spawn(CampaignRun r,int row){
        Role[] roles=row<120?new Role[]{Role.MELEE,Role.MELEE,Role.RANGED}:row<500?new Role[]{Role.MELEE,Role.RANGED,Role.SUPPORT}:CampaignDifficulty.at(row).roles();
        spawn(r,row,roles,false);
    }
    static java.util.List<Enemy> spawn(CampaignRun r,int row,Role[] roles,boolean reserve){
        if(r.enemies.size()+roles.length>64)return java.util.List.of();
        var cells=new ArrayList<int[]>();
        int center=r.campaignMap.encounterX(row);int[] counts=new int[3];
        for(int i=0;i<roles.length;i++){
            int order=counts[roles[i].ordinal()]++,offset=roles[i]==Role.SUPPORT?new int[]{0,-1,1}[Math.min(order,2)]:new int[]{-1,1,-2,2}[Math.min(order,3)];
            int wantedX=center+offset,wantedY=row+(roles[i]==Role.MELEE?0:roles[i]==Role.RANGED?2:3);
            int[] chosen=null;int best=Integer.MAX_VALUE;
            for(int y=row;y<=Math.min(GOAL,row+5);y++)for(int x=Math.max(0,center-3);x<=Math.min(WIDTH-1,center+3);x++){
                final int px=x,py=y;
                if(r.terrain(x,y)!=0||r.at(x,y)!=null||x==r.x&&y==r.y||cells.stream().anyMatch(c->c[0]==px&&c[1]==py))continue;
                int score=distance(x,y,wantedX,wantedY);
                if(score<best){chosen=new int[]{x,y};best=score;}
            }
            if(chosen==null)return java.util.List.of();
            cells.add(chosen);
        }
        int squad=r.nextSquad++;var added=new ArrayList<Enemy>();
        for(int i=0;i<roles.length;i++){
            Role role=roles[i];
            var e=new Enemy(r.nextEnemy++,cells.get(i)[0],cells.get(i)[1],CampaignDifficulty.hp(row,role),CampaignDifficulty.damage(row,role),0);
            e.role=role;e.squad=squad;e.readyTurn=reserve?r.turn+2:0;r.enemies.add(e);added.add(e);
        }
        return added;
    }
    static void attack(CampaignRun r,Enemy e,boolean shot){
        r.visual(shot?SHOT:MELEE,e.id,0,e.x,e.y,r.x,r.y,0);
        r.receiveDamage(e,e.damage);
    }
    static void react(CampaignRun r){
        var detonated=CampaignBosses.resolve(r);if(r.hp<=0)return;
        Set<Integer> occupied=new HashSet<>();for(Enemy e:r.enemies)occupied.add(e.y*WIDTH+e.x);
        var order=new ArrayList<>(r.enemies);
        // The back line finds its position before the front line advances.
        order.sort(Comparator.comparingInt(e->e.boss>0?3:e.role==Role.SUPPORT?0:e.role==Role.RANGED?1:2));
        for(Enemy e:order){
            if(e.readyTurn>r.turn)continue;
            if(e.cooldown>0)e.cooldown--;
            boolean alerted=CampaignDifficulty.at(r.distance).index()>=3&&e.squad>0&&r.enemies.stream().anyMatch(a->a.squad==e.squad&&r.range(a)<=8);
            if(r.range(e)<=(alerted?18:12))e.awake=true;
            if(!e.awake||r.range(e)>(e.boss>0?12:28))continue;
            if(e.boss>0&&e.phase==2&&CampaignBosses.react(r,e,detonated,occupied))continue;
            if(e.role==Role.MELEE||e.boss>0){
                if(r.range(e)==1)attack(r,e,false);
                else move(r,e,occupied,meleeStep(r,e,occupied));
            }else if(e.role==Role.RANGED&&r.range(e)>=2&&r.range(e)<=FIRE_RANGE&&clear(r,e.x,e.y,r.x,r.y,true)){
                if(e.cooldown==0){attack(r,e,true);e.cooldown=2;}
            }else {
                int next=backLineStep(r,e,occupied);
                if(next>=0)move(r,e,occupied,next);
                else if(r.range(e)==1)attack(r,e,false);
            }
            if(r.hp<=0)break;
        }
        refreshGuards(r);
    }
    static void move(CampaignRun r,Enemy e,Set<Integer> occupied,int next){
        if(next<0||next==r.y*WIDTH+r.x||occupied.contains(next)||r.terrain(next%WIDTH,next/WIDTH)!=0)return;
        occupied.remove(e.y*WIDTH+e.x);e.x=next%WIDTH;e.y=next/WIDTH;occupied.add(next);
    }
    static int meleeStep(CampaignRun r,Enemy e,Set<Integer> occupied){
        if(e.squad>0){
            if(CampaignDifficulty.at(r.distance).index()>=8){
                for(int i=0;i<4;i++){
                    int direction=(e.id+i)%4,gx=r.x+DX[direction],gy=r.y+DY[direction];
                    if(gx<0||gx>=WIDTH||gy<0||gy>GOAL||r.terrain(gx,gy)!=0||occupied.contains(gy*WIDTH+gx))continue;
                    int step=r.pathStep(e,occupied,gx,gy);if(step>=0)return step;
                }
            }
            int side=(e.id&1)==0?1:-1;
            for(int dx:new int[]{side,-side}){
                int gx=r.x+dx;
                if(gx<0||gx>=WIDTH||r.terrain(gx,r.y)!=0||occupied.contains(r.y*WIDTH+gx))continue;
                int step=r.pathStep(e,occupied,gx,r.y);if(step>=0)return step;
            }
        }
        return r.pathStep(e,occupied);
    }
    static int backLineStep(CampaignRun r,Enemy e,Set<Integer> occupied){
        Enemy ally=r.enemies.stream().filter(a->a!=e&&a.squad==e.squad&&a.squad>0&&a.role!=Role.SUPPORT&&r.range(a)<=12)
            .min(Comparator.comparingInt(r::range)).orElse(null);
        int best=Integer.MAX_VALUE,gx=e.x,gy=e.y;
        for(int y=Math.max(0,r.y-5);y<=Math.min(GOAL,r.y+5);y++)for(int x=Math.max(0,r.x-5);x<=Math.min(WIDTH-1,r.x+5);x++){
            int range=distance(x,y,r.x,r.y);
            if(range<2||range>6||r.terrain(x,y)!=0||occupied.contains(y*WIDTH+x)&&(x!=e.x||y!=e.y))continue;
            int score=distance(x,y,e.x,e.y);
            if(e.role==Role.RANGED){
                if(range>FIRE_RANGE||!clear(r,x,y,r.x,r.y,true))continue;
                int preferred=CampaignDifficulty.at(r.distance).index()>=4&&(e.id&1)==0?4:3;
                score+=Math.abs(range-preferred)*4;
            }else {
                if(ally!=null){int gap=distance(x,y,ally.x,ally.y);score+=Math.abs(gap-1)*6;}
                score+=Math.abs(range-4)*3;
            }
            if(score<best){best=score;gx=x;gy=y;}
        }
        if(gx==e.x&&gy==e.y)return -1;
        return r.pathStep(e,occupied,gx,gy);
    }
}
