package tank;

import java.util.*;

/** Isolated, turn-based campaign simulation. No Room, World or roleplay dependencies. */
public final class CampaignRun {
    public static final int WIDTH=13, VIEW=19, GOAL=10000, SHOT_RANGE=4, MAX_BUFF=9;
    public final String id=UUID.randomUUID().toString();
    final Random random;
    final long seed;
    final CampaignMap campaignMap;
    public final int nation;
    public final CampaignNation stats;
    public int x=6,y=0,distance=0,hp=100,turn=0,facing=0,shot=0,skill=0;
    public int gold=0,kills=0,doubleHits=0,shields=0,bosses=0,shopRevision=0;
    public boolean finished=false,victory=false;
    public String message="Tiến về phía bắc để giải cứu Tổng thống Đô-na-tri và gia đình khỏi Vu-a-lo.";
    public final List<Enemy> enemies=new ArrayList<>();
    public final List<Loot> loot=new ArrayList<>();
    public record Danger(int source,int x,int y,int due,int damage){}
    public final List<Danger> dangers=new ArrayList<>();
    // Presentation only: events never feed back into turn rules or persistence.
    public static final int MELEE=1,SHOT=2,SKILL=3,HIT=4,BLOCK=5,HEAL=6,DEATH=7;
    public record Visual(int kind,int source,int target,int x,int y,int tx,int ty,int amount){}
    public final List<Visual> visuals=new ArrayList<>();
    void visual(int kind,int source,int target,int ax,int ay,int bx,int by,int amount){visuals.add(new Visual(kind,source,target,ax,ay,Math.clamp(bx,0,WIDTH-1),Math.clamp(by,0,GOAL),amount));}
    int generated=-1,nextEnemy=1,nextSquad=1;
    static final int[] DX={0,1,0,-1},DY={1,0,-1,0};
    public enum Role { MELEE, RANGED, SUPPORT }
    public static final class Enemy {
        public int id,x,y,hp,maxHp,damage,boss,squad,guard,cooldown,phase,readyTurn;
        public Role role=Role.MELEE;public boolean awake;
        Enemy(int id,int x,int y,int hp,int damage,int boss){this.id=id;this.x=x;this.y=y;this.hp=this.maxHp=hp;this.damage=damage;this.boss=boss;this.phase=boss>0?1:0;}
    }
    public record Loot(int x,int y,int kind,int amount) {} // heal, damage, shield, chest
    public CampaignRun(int nation,long seed) {
        if(nation!=0&&nation!=1&&nation!=5)throw new IllegalArgumentException("campaign nation");
        this.nation=nation;this.stats=CampaignNation.of(nation);hp=stats.maxHp();this.seed=seed;campaignMap=new CampaignMap(seed);random=new Random(seed);generate();
    }
    public int terrain(int px,int py) {return campaignMap.tile(px,py);}
    public boolean blocksShot(int px,int py){return campaignMap.blocksShot(px,py);}
    Enemy at(int px,int py){return enemies.stream().filter(e->e.x==px&&e.y==py).findFirst().orElse(null);}
    int range(Enemy e){return Math.abs(e.x-x)+Math.abs(e.y-y);}
    void generate() {
        int top=Math.min(GOAL,y+VIEW);
        for(int row=generated+1;row<=top;row++) {
            if(!campaignMap.peaceful(row)&&row>=40&&(row==campaignMap.encounterRow(row)||row%CampaignDifficulty.at(row).spacing()==0&&Math.abs(row-campaignMap.encounterRow(row))>5)&&row%500>5&&row%500<490)CampaignTactics.spawn(this,row);
            else if(!campaignMap.peaceful(row)&&row>5&&row%500!=0&&enemies.size()<64&&random.nextInt(100)<Math.min(36,22+CampaignDifficulty.at(row).index()/2)) {
                int px=random.nextInt(WIDTH);
                if(terrain(px,row)==0)enemies.add(new Enemy(nextEnemy++,px,row,CampaignDifficulty.hp(row,Role.MELEE),CampaignDifficulty.damage(row,Role.MELEE),0));
            }
            if(row>5&&row==campaignMap.rewardY(row))loot.add(new Loot(campaignMap.rewardX(row),row,campaignMap.peaceful(row)?0:3,campaignMap.peaceful(row)?0:25+CampaignDifficulty.at(row).index()*3));
            if(row>0&&row%17==0)loot.add(new Loot(CampaignMap.pathX(seed,row),row,3,10+random.nextInt(16)));
        }
        generated=Math.max(generated,top);
        // Bounded rolling window even after thousands of meters.
        enemies.removeIf(e->e.boss==0&&e.y<y-10);
        loot.removeIf(e->e.y<y-10);
        CampaignTactics.refreshGuards(this);
    }
    /** 0..3 move north/east/south/west, 4 shoot I, 5 national skill O. */
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
            if(e!=null){visual(MELEE,0,e.id,x,y,e.x,e.y,0);hit(e,stats.meleeDamage()*(doubleHits>0?2:1));if(doubleHits>0)doubleHits--;}
            else {x=nx;y=ny;distance=Math.max(distance,y);}
        } else if(action==4) {
            visuals.clear();
            Enemy target=null;int tx=x,ty=y;
            for(int n=1;n<=SHOT_RANGE;n++){int nx=x+DX[facing]*n,ny=y+DY[facing]*n;if(blocksShot(nx,ny))break;tx=nx;ty=ny;target=at(nx,ny);if(target!=null)break;}
            visual(SHOT,0,target==null?-1:target.id,x,y,tx,ty,0);
            if(target!=null){hit(target,stats.shotDamage()*(doubleHits>0?2:1));if(doubleHits>0)doubleHits--;}
            shot=CampaignNation.SHOT_COOLDOWN+1;
        } else {
            if(!nationalSkill())return false;
            skill=stats.skillCooldown()+1;
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
    boolean nationalSkill(){
        if(nation==1){
            var nearby=enemies.stream().filter(e->range(e)<=1).toList();
            if(shields==MAX_BUFF&&nearby.isEmpty()){message="Khiên đã đầy; chưa có địch ở cự ly xung kích.";return false;}
            visuals.clear();visual(SKILL,0,0,x,y,x,y,nation);shields=Math.min(MAX_BUFF,shields+3);
            for(var e:nearby)hit(e,45);
            message="Pháo đài thép: +3 khiên, xung kích 45 sát thương trong 1 ô.";
            return true;
        }
        if(nation==5){
            var targets=new ArrayList<Enemy>();int tx=x,ty=y;
            for(int n=1;n<=stats.skillRange();n++){
                int nx=x+DX[facing]*n,ny=y+DY[facing]*n;if(blocksShot(nx,ny))break;
                tx=nx;ty=ny;var e=at(nx,ny);if(e!=null)targets.add(e);
            }
            if(targets.isEmpty()){message="Ngắm theo hướng xe: cần mục tiêu trên đường thẳng trong 8 ô.";return false;}
            visuals.clear();visual(SKILL,0,targets.getLast().id,x,y,tx,ty,nation);
            for(var e:targets)hit(e,120,true);
            message="Pháo xuyên giáp: xuyên đội hình và khiên hỗ trợ, dừng ở vật cản.";
            return true;
        }
        var targets=new ArrayList<>(enemies);targets.sort(Comparator.comparingInt(this::range));
        if(targets.isEmpty()){message="Chưa có mục tiêu.";return false;}
        var target=targets.getFirst();
        if(range(target)>stats.skillRange()){message="Mục tiêu ngoài tầm không kích (12 ô).";return false;}
        visuals.clear();visual(SKILL,0,target.id,x,y,target.x,target.y,nation);
        for(var e:targets)if(Math.abs(e.x-target.x)+Math.abs(e.y-target.y)<=2)hit(e,70);
        message="Không kích: 70 sát thương trong bán kính 2 ô quanh mục tiêu gần nhất.";
        return true;
    }
    void receiveDamage(Enemy source,int amount){
        if(shields>0){shields--;visual(BLOCK,source.id,0,source.x,source.y,x,y,0);return;}
        int damage=amount<=0?0:Math.max(1,amount-stats.armor());
        visual(HIT,source.id,0,source.x,source.y,x,y,Math.min(Math.max(0,hp),damage));hp-=damage;
    }
    public int price(int item){
        if(item<0||item>2)throw new IllegalArgumentException("shop item");
        return new int[]{20,30,35}[item]+distance/250*new int[]{5,8,10}[item];
    }
    /** Purchases do not advance combat, cooldowns, spawns or distance. */
    public boolean buy(int item){
        if(finished){message="Chi\u1ebfn d\u1ecbch \u0111\u00e3 k\u1ebft th\u00fac.";return false;}
        if(item<0||item>2){message="V\u1eadt ph\u1ea9m kh\u00f4ng h\u1ee3p l\u1ec7.";return false;}
        if(item==0&&hp>=stats.maxHp()||item==1&&shields>MAX_BUFF-3||item==2&&doubleHits>MAX_BUFF-3){message="Kh\u00f4ng th\u1ec3 nh\u1eadn th\u00eam v\u1eadt ph\u1ea9m n\u00e0y.";return false;}
        int cost=price(item);
        if(gold<cost){message="Kh\u00f4ng \u0111\u1ee7 v\u00e0ng trong chuy\u1ebfn \u0111i.";return false;}
        gold-=cost;shopRevision++;visuals.clear();
        if(item==0){int amount=Math.min(25,stats.maxHp()-hp);hp+=amount;visual(HEAL,0,0,x,y,x,y,amount);}
        else if(item==1)shields+=3;
        else doubleHits+=3;
        message="\u0110\u00e3 mua "+new String[]{"h\u1ed3i m\u00e1u","khi\u00ean","t\u0103ng s\u00e1t th\u01b0\u01a1ng"}[item]+" (-"+cost+" v\u00e0ng).";
        return true;
    }
    void hit(Enemy e,int damage){hit(e,damage,false);}
    void hit(Enemy e,int damage,boolean piercing) {
        int absorbed=Math.min(damage,!piercing&&CampaignTactics.covered(this,e)?e.guard:0);
        if(absorbed>0){e.guard-=absorbed;damage-=absorbed;visual(BLOCK,0,e.id,x,y,e.x,e.y,absorbed);}
        visual(HIT,0,e.id,x,y,e.x,e.y,Math.min(e.hp,damage));
        e.hp-=damage;
        if(e.hp>0){if(e.boss>0&&e.phase==1&&e.hp*2<=e.maxHp)CampaignBosses.escalate(this,e);return;}
        visual(DEATH,0,e.id,x,y,e.x,e.y,e.boss);
        enemies.remove(e);dangers.removeIf(d->d.source()==e.id);kills++;
        if(e.boss>0){bosses++;loot.add(new Loot(e.x,e.y,3,100+bosses*10));if(bosses==20){collect();gold+=500;finished=victory=true;message="Đã giải cứu Tổng thống Đô-na-tri và gia đình!";}}
        else loot.add(new Loot(e.x,e.y,random.nextInt(3),0));
    }
    void react() {CampaignTactics.react(this);}
    int pathStep(Enemy e,Set<Integer> occupied){return pathStep(e,occupied,x,y);}
    int pathStep(Enemy e,Set<Integer> occupied,int gx,int gy){
        int low=Math.max(0,Math.min(e.y,gy)-CampaignMap.SECTION),high=Math.min(GOAL,Math.max(e.y,gy)+CampaignMap.SECTION);
        int[] parent=new int[(high-low+1)*WIDTH];Arrays.fill(parent,-1);
        int start=(e.y-low)*WIDTH+e.x,goal=(gy-low)*WIDTH+gx;
        ArrayDeque<Integer> queue=new ArrayDeque<>();queue.add(start);parent[start]=start;
        while(!queue.isEmpty()){
            int cell=queue.remove(),cx=cell%WIDTH,cy=cell/WIDTH+low;
            if(cell==goal){int first=goal;while(parent[first]!=start&&first!=start)first=parent[first];return (first/WIDTH+low)*WIDTH+first%WIDTH;}
            for(int d=0;d<4;d++){
                int nx=cx+DX[d],ny=cy+DY[d];if(nx<0||nx>=WIDTH||ny<low||ny>high||terrain(nx,ny)!=0)continue;
                int next=(ny-low)*WIDTH+nx;if(parent[next]>=0||occupied.contains(ny*WIDTH+nx)||nx==x&&ny==y&&next!=goal)continue;
                parent[next]=cell;queue.add(next);
            }
        }
        return -1;
    }
    void collect(){for(var it=loot.iterator();it.hasNext();){Loot l=it.next();if(l.x!=x||l.y!=y)continue;switch(l.kind){case 0->{int healed=Math.min(stats.maxHp(),hp+25)-hp;hp+=healed;if(healed>0)visual(HEAL,0,0,x,y,x,y,healed);}case 1->doubleHits=Math.min(MAX_BUFF,doubleHits+3);case 2->shields=Math.min(MAX_BUFF,shields+3);case 3->gold+=l.amount;}it.remove();}}
}
