package tank;

import java.util.*;

/** Isolated, turn-based campaign simulation. No Room, World or roleplay dependencies. */
public final class CampaignRun {
    public static final int WIDTH=13, VIEW=19, GOAL=10000;
    public final String id=UUID.randomUUID().toString();
    final Random random;
    final long seed;
    final CampaignMap campaignMap;
    public final int nation;
    public int x=6,y=0,distance=0,hp=100,turn=0,facing=0,shot=0,skill=0;
    public int gold=0,kills=0,doubleHits=0,shields=0,bosses=0;
    public boolean finished=false,victory=false;
    public String message="Tiến về phía bắc để giải cứu Tổng thống Đô-na-tri và gia đình khỏi Vu-a-lo.";
    public final List<Enemy> enemies=new ArrayList<>();
    public final List<Loot> loot=new ArrayList<>();
    // Presentation only: events never feed back into turn rules or persistence.
    public static final int MELEE=1,SHOT=2,SKILL=3,HIT=4,BLOCK=5,HEAL=6,DEATH=7;
    public record Visual(int kind,int source,int target,int x,int y,int tx,int ty,int amount){}
    public final List<Visual> visuals=new ArrayList<>();
    void visual(int kind,int source,int target,int ax,int ay,int bx,int by,int amount){visuals.add(new Visual(kind,source,target,ax,ay,Math.clamp(bx,0,WIDTH-1),Math.clamp(by,0,GOAL),amount));}
    int generated=-1,nextEnemy=1;
    static final int[] DX={0,1,0,-1},DY={1,0,-1,0};
    public static final class Enemy {
        public int id,x,y,hp,maxHp,damage,boss;
        Enemy(int id,int x,int y,int hp,int damage,int boss){this.id=id;this.x=x;this.y=y;this.hp=this.maxHp=hp;this.damage=damage;this.boss=boss;}
    }
    public record Loot(int x,int y,int kind,int amount) {} // heal, damage, shield, chest
    public CampaignRun(int nation,long seed) {
        if(nation!=0&&nation!=1&&nation!=5)throw new IllegalArgumentException("campaign nation");
        this.nation=nation;this.seed=seed;campaignMap=new CampaignMap(seed);random=new Random(seed);generate();
    }
    public int terrain(int px,int py) {return campaignMap.tile(px,py);}
    Enemy at(int px,int py){return enemies.stream().filter(e->e.x==px&&e.y==py).findFirst().orElse(null);}
    int range(Enemy e){return Math.abs(e.x-x)+Math.abs(e.y-y);}
    void generate() {
        int top=Math.min(GOAL,y+VIEW);
        for(int row=generated+1;row<=top;row++) {
            if(row>5&&row%500!=0&&random.nextInt(100)<Math.min(70,22+distance/250)) {
                int px=random.nextInt(WIDTH);
                if(terrain(px,row)==0)enemies.add(new Enemy(nextEnemy++,px,row,30+distance/250*3,5+distance/1000,0));
            }
            if(row>0&&row%17==0)loot.add(new Loot(CampaignMap.pathX(seed,row),row,3,10+random.nextInt(16)));
        }
        generated=Math.max(generated,top);
        // Bounded rolling window even after thousands of meters.
        enemies.removeIf(e->e.boss==0&&e.y<y-10);
        loot.removeIf(e->e.y<y-10);
    }
    /** 0..3 move north/east/south/west, 4 shoot I, 5 skill J. */
    public boolean act(int action) {
        if(finished||action<0||action>5)return false;
        if(action==4&&shot>0||action==5&&skill>0){message="Kỹ năng đang hồi chiêu.";return false;}
        message="";
        if(action<4) {
            facing=action;int nx=x+DX[action],ny=y+DY[action];
            if(terrain(nx,ny)!=0){message="Địa hình chặn đường.";return false;}
            int gate=(bosses+1)*500;
            if(ny>gate){message="Hạ trùm tại mốc "+gate+" m trước khi tiến tiếp.";return false;}
            visuals.clear();
            Enemy e=at(nx,ny);
            if(e!=null){visual(MELEE,0,e.id,x,y,e.x,e.y,0);hit(e,20*(doubleHits>0?2:1));if(doubleHits>0)doubleHits--;}
            else {x=nx;y=ny;distance=Math.max(distance,y);}
        } else if(action==4) {
            visuals.clear();
            Enemy target=null;int tx=x,ty=y;
            for(int n=1;n<=2;n++){int nx=x+DX[facing]*n,ny=y+DY[facing]*n;if(terrain(nx,ny)!=0)break;tx=nx;ty=ny;target=at(nx,ny);if(target!=null)break;}
            visual(SHOT,0,target==null?-1:target.id,x,y,tx,ty,0);
            if(target!=null)hit(target,40);
            shot=11;
        } else {
            List<Enemy> targets=new ArrayList<>(enemies);targets.sort(Comparator.comparingInt(this::range));
            if(targets.isEmpty()){message="Chưa có mục tiêu.";return false;}
            Enemy target=targets.getFirst();
            if(range(target)>12){message="Mục tiêu ngoài tầm kỹ năng (12 ô).";return false;}
            visuals.clear();visual(SKILL,0,target.id,x,y,target.x,target.y,nation);
            if(nation==5)hit(target,120);
            else for(Enemy e:targets)if(nation==0?Math.abs(e.x-target.x)+Math.abs(e.y-target.y)<=2:Math.abs(e.x-target.x)+Math.abs(e.y-target.y)<=3)hit(e,nation==0?60:45);
            skill=51;
        }
        turn++;shot=Math.max(0,shot-1);skill=Math.max(0,skill-1);
        if(!finished) {
            if(distance>=(bosses+1)*500&&!enemies.stream().anyMatch(e->e.boss>0)) {
                int stage=bosses+1;
                int bossX=x==6?5:6;
                // Never overlap an enemy that has moved onto the checkpoint row.
                for(int candidate=0;at(bossX,stage*500)!=null&&candidate<WIDTH;candidate++)if(candidate!=x)bossX=candidate;
                enemies.add(new Enemy(nextEnemy++,bossX,stage*500,140+stage*20,12+stage,1+(stage-1)%4));
                message=stage==20?"Trùm cuối đang giữ Tổng thống!":"Trùm Vu-a-lo tại "+stage*500+" m!";
            }
            react();collect();generate();
            if(hp<=0){hp=0;finished=true;message="Xe đã bị phá hủy. Chiến dịch kết thúc.";}
        }
        return true;
    }
    void hit(Enemy e,int damage) {
        visual(HIT,0,e.id,x,y,e.x,e.y,Math.min(e.hp,damage));
        e.hp-=damage;
        if(e.hp>0)return;
        visual(DEATH,0,e.id,x,y,e.x,e.y,e.boss);
        enemies.remove(e);kills++;
        if(e.boss>0){bosses++;loot.add(new Loot(e.x,e.y,3,100+bosses*10));if(bosses==20){collect();gold+=500;finished=victory=true;message="Đã giải cứu Tổng thống Đô-na-tri và gia đình!";}}
        else loot.add(new Loot(e.x,e.y,random.nextInt(3),0));
    }
    void react() {
        Set<Integer> occupied=new HashSet<>();for(Enemy e:enemies)occupied.add(e.y*WIDTH+e.x);
        for(Enemy e:new ArrayList<>(enemies)) {
            if(range(e)>12)continue;
            if(range(e)==1){
                visual(MELEE,e.id,0,e.x,e.y,x,y,0);
                if(shields>0){shields--;visual(BLOCK,e.id,0,e.x,e.y,x,y,0);}
                else {visual(HIT,e.id,0,e.x,e.y,x,y,Math.min(Math.max(0,hp),e.damage));hp-=e.damage;}
            }
            else {
                occupied.remove(e.y*WIDTH+e.x);
                int next=pathStep(e,occupied);
                if(next>=0){e.x=next%WIDTH;e.y=next/WIDTH;}
                occupied.add(e.y*WIDTH+e.x);
            }
            if(hp<=0)break;
        }
    }
    int pathStep(Enemy e,Set<Integer> occupied){
        int low=Math.max(0,Math.min(e.y,y)-CampaignMap.SECTION),high=Math.min(GOAL,Math.max(e.y,y)+CampaignMap.SECTION);
        int[] parent=new int[(high-low+1)*WIDTH];Arrays.fill(parent,-1);
        int start=(e.y-low)*WIDTH+e.x,goal=(y-low)*WIDTH+x;
        ArrayDeque<Integer> queue=new ArrayDeque<>();queue.add(start);parent[start]=start;
        while(!queue.isEmpty()){
            int cell=queue.remove(),cx=cell%WIDTH,cy=cell/WIDTH+low;
            if(cell==goal){int first=goal;while(parent[first]!=start&&first!=start)first=parent[first];return (first/WIDTH+low)*WIDTH+first%WIDTH;}
            for(int d=0;d<4;d++){
                int nx=cx+DX[d],ny=cy+DY[d];if(nx<0||nx>=WIDTH||ny<low||ny>high||terrain(nx,ny)!=0)continue;
                int next=(ny-low)*WIDTH+nx;if(parent[next]>=0||occupied.contains(ny*WIDTH+nx)&&next!=goal)continue;
                parent[next]=cell;queue.add(next);
            }
        }
        return -1;
    }
    void collect(){for(var it=loot.iterator();it.hasNext();){Loot l=it.next();if(l.x!=x||l.y!=y)continue;switch(l.kind){case 0->{int healed=Math.min(100,hp+25)-hp;hp+=healed;if(healed>0)visual(HEAL,0,0,x,y,x,y,healed);}case 1->doubleHits=3;case 2->shields=3;case 3->gold+=l.amount;}it.remove();}}
}
