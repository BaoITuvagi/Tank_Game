package tank;

import java.util.*;

/** Twelve authored 13x32 layouts, varied by seed, connected and cached independently. */
public final class CampaignMap {
    public static final int OPEN=0,TREE=1,ROCK=2,CRATE=3,RUINS=4,SECTION=32;
    public static final int GRASS=0,ROAD=1,WATER=2,BRIDGE=3,DIRT=4,PAVING=5;
    public static final int FLOWERS=1,FLAG=2,HOUSE=3,HANGAR=4,FACTORY=5,TOWER=6,RUBBLE=7,TRUCK=8,SUPPLIES=9,RUIN_HOUSE=10,RUIN_FACTORY=11,RUIN_TOWER=12,MARKING=13,PARKED_TANK=14;
    static final String[] NAMES={"Đường rừng","Bãi nghỉ trong rừng","Hồ rừng","Đèo đá","Mỏ đá","Khe sông","Ngã tư tiền đồn","Kho tiếp tế","Trạm gác","Sân phế tích","Kênh nước","Đoàn xe bỏ lại"};
    static final class Section {
        final byte[] tiles=new byte[13*SECTION],ground=new byte[13*SECTION],decor=new byte[13*SECTION];
        final int template;int rewardX,rewardRow;
        Section(int template){this.template=template;}
    }
    final long seed;
    final Map<Integer,Section> sections=new LinkedHashMap<>(8,.75f,true){protected boolean removeEldestEntry(Map.Entry<Integer,Section> e){return size()>8;}};
    public CampaignMap(long seed){this.seed=seed;}
    Section section(int index){return sections.computeIfAbsent(index,i->build(seed,i));}
    static boolean inside(int x,int y){return x>=0&&x<13&&y>=0&&y<=CampaignRun.GOAL;}
    public int tile(int x,int y){return inside(x,y)?section(y/SECTION).tiles[y%SECTION*13+x]:ROCK;}
    public int ground(int x,int y){return inside(x,y)?section(y/SECTION).ground[y%SECTION*13+x]:GRASS;}
    public int decoration(int x,int y){return inside(x,y)?section(y/SECTION).decor[y%SECTION*13+x]:0;}
    public boolean blocksShot(int x,int y){return !inside(x,y)||tile(x,y)!=OPEN&&ground(x,y)!=WATER;}
    public int template(int y){return template(seed,Math.max(0,y)/SECTION);}
    public String name(int y){return NAMES[template(y)];}
    public boolean peaceful(int y){return template(y)==1;}
    public int rewardY(int y){return y/SECTION*SECTION+section(y/SECTION).rewardRow;}
    public int rewardX(int y){return section(y/SECTION).rewardX;}
    public int encounterRow(int y){return y/SECTION*SECTION+new int[]{18,16,19,17,18,21,20,16,12,17,18,18}[template(y)];}
    public int encounterX(int y){
        int t=template(y);if(y!=encounterRow(y))return pathX(seed,y);
        if(t!=2&&t!=5&&t!=7&&t!=11)return pathX(seed,y);
        int x=t==7?3:9;
        return (hash(seed,y/SECTION)&16)==0?x:12-x;
    }
    static long hash(long seed,int section){long h=seed+section*0x9E3779B97F4A7C15L;h=(h^(h>>>30))*0xBF58476D1CE4E5B9L;h=(h^(h>>>27))*0x94D049BB133111EBL;return h^(h>>>31);}
    public static int style(long seed,int section){return Math.floorMod(section/3+(int)(hash(seed,0)&3),4);}
    public static int template(long seed,int section){return style(seed,section)*3+Math.floorMod(section%3+(int)((hash(seed,section/3)>>>8)%3),3);}
    public static int pathX(long seed,int y){
        int row=Math.floorMod(y,SECTION),t=template(seed,Math.max(0,y)/SECTION);
        if(CampaignLayouts.authored(t)){
            int x=CampaignLayouts.routeX(t,row);return (hash(seed,y/SECTION)&16)==0?x:12-x;
        }
        if(t==10)return 6;
        int offset=t==2?((hash(seed,y/SECTION)&8)==0?-5:5):t==1||t==5?((hash(seed,y/SECTION)&8)==0?-3:3):(int)((hash(seed,y/SECTION)>>>8)%5)-2;
        float ramp=Math.min(1f,Math.min(row,SECTION-1-row)/6f);return 6+Math.round(offset*ramp);
    }
    static void set(Section s,int x,int row,int tile,int ground,int decor){if(x<0||x>=13||row<0||row>=SECTION)return;int i=row*13+x;s.tiles[i]=(byte)tile;s.ground[i]=(byte)ground;s.decor[i]=(byte)decor;}
    static void solid(Section s,int x,int row,int tile,int decor){if(x<0||x>=13||row<0||row>=SECTION)return;int i=row*13+x;set(s,x,row,tile,s.ground[i],decor);}
    static void rect(Section s,int x,int y,int w,int h,int tile,int ground,int decor){for(int row=y;row<y+h;row++)for(int col=x;col<x+w;col++)set(s,col,row,tile,ground,decor);}
    static void ellipse(Section s,int cx,int cy,int rx,int ry,int tile,int ground,int decor){for(int y=cy-ry;y<=cy+ry;y++)for(int x=cx-rx;x<=cx+rx;x++)if((x-cx)*(x-cx)*ry*ry+(y-cy)*(y-cy)*rx*rx<=rx*rx*ry*ry)set(s,x,y,tile,ground,decor);}
    static void lane(Section s,int x,int y,int surface){
        if(x<0||x>=13||y<0||y>=SECTION)return;
        int at=y*13+x,existing=s.ground[at];
        set(s,x,y,OPEN,existing==WATER||existing==BRIDGE?BRIDGE:existing==ROAD?ROAD:surface,0);
    }
    static void horizontal(Section s,int left,int right,int row,int surface){for(int x=left;x<=right;x++)lane(s,x,row,surface);}
    static void vertical(Section s,int x,int bottom,int top,int surface){for(int y=bottom;y<=top;y++)lane(s,x,y,surface);}
    static void ring(Section s,int left,int bottom,int w,int h,int tile,int decor){for(int y=bottom;y<bottom+h;y++)for(int x=left;x<left+w;x++)if(x==left||x==left+w-1||y==bottom||y==bottom+h-1)solid(s,x,y,tile,decor);}
    static Section build(long seed,int index){
        int t=template(seed,index);var s=new Section(t);boolean mirror=(hash(seed,index)&16)!=0;
        // Shared grouped vegetation makes transitions readable, rather than random single blockers.
        if(!CampaignLayouts.authored(t)){ellipse(s,1,4,2,2,TREE,GRASS,0);ellipse(s,11,27,2,3,TREE,GRASS,0);}
        switch(t){
            case 2->{
                for(int row=9;row<=23;row++){
                    int half=row<=10||row>=22?2:row<=12||row>=20?3:4;
                    rect(s,6-half,row,half*2+1,1,ROCK,WATER,0);
                }
                vertical(s,1,6,26,DIRT);vertical(s,11,6,26,DIRT);
                horizontal(s,1,11,6,DIRT);horizontal(s,1,11,26,DIRT);
                ellipse(s,0,18,1,2,TREE,GRASS,0);solid(s,10,8,ROCK,0);
            }
            case 5->{
                rect(s,5,4,3,24,ROCK,WATER,0);vertical(s,3,4,27,ROAD);vertical(s,9,4,27,ROAD);
                horizontal(s,3,9,10,ROAD);horizontal(s,3,9,23,ROAD);
                solid(s,1,12,RUINS,RUIN_HOUSE);solid(s,11,19,RUINS,RUIN_FACTORY);solid(s,10,7,RUINS,RUIN_TOWER);
                solid(s,1,13,CRATE,SUPPLIES);solid(s,11,20,ROCK,TRUCK);
            }
            case 10->{
                rect(s,0,13,13,3,ROCK,WATER,0);horizontal(s,0,12,11,ROAD);horizontal(s,0,12,18,ROAD);
                vertical(s,2,11,18,ROAD);vertical(s,10,11,18,ROAD);
                solid(s,3,7,RUINS,RUIN_FACTORY);solid(s,9,23,RUINS,RUIN_HOUSE);
                solid(s,1,20,RUINS,RUIN_TOWER);solid(s,11,9,RUINS,RUBBLE);
                solid(s,2,7,RUINS,0);solid(s,3,8,RUINS,0);solid(s,10,23,CRATE,SUPPLIES);
            }
            default->CampaignLayouts.build(s,t);
        }
        if(mirror){for(int row=0;row<SECTION;row++)for(int x=0;x<6;x++){int a=row*13+x,b=row*13+12-x;swap(s.tiles,a,b);swap(s.ground,a,b);swap(s.decor,a,b);}}
        // Stitch the wandering main trail into section joins. Water becomes a bridge, never grass.
        int surface=t>=6&&t!=9?ROAD:DIRT;
        for(int row=0;row<SECTION;row++){
            int global=index*SECTION+row,main=pathX(seed,global),previous=pathX(seed,Math.max(index*SECTION,global-1));
            horizontal(s,Math.min(main,previous),Math.max(main,previous),row,surface);
            if(row==0||row==31){for(int x=0;x<13;x++)lane(s,x,row,GRASS);lane(s,main,row,surface);}
            if(global%CampaignRun.BOSS_INTERVAL<=3||global%CampaignRun.BOSS_INTERVAL>=CampaignRun.BOSS_INTERVAL-3){
                for(int x=0;x<13;x++){int existing=s.ground[row*13+x];set(s,x,row,OPEN,existing==ROAD||existing==BRIDGE?existing:global>10?PAVING:GRASS,0);}
                lane(s,main,row,surface);
            }
        }
        s.rewardRow=CampaignLayouts.rewardRow(t);s.rewardX=CampaignLayouts.rewardX(t);
        if(mirror)s.rewardX=12-s.rewardX;
        // Rewards use existing floors; placing a coin must never erase a road or scenery.
        if(s.tiles[s.rewardRow*13+s.rewardX]!=OPEN)lane(s,s.rewardX,s.rewardRow,t/3>=2?PAVING:DIRT);
        trimWaterTips(s);
        // Tiny decorative accents are seeded and do not affect collision or line of sight.
        for(int row=2;row<30;row++)for(int x=0;x<13;x++){
            int i=row*13+x;long h=hash(seed+row*31L+x,index);
            if(s.tiles[i]==OPEN&&s.ground[i]==GRASS&&s.decor[i]==0&&(h&31)==0)s.decor[i]=FLOWERS;
        }
        repair(s);return s;
    }
    static boolean wet(Section s,int x,int row){
        if(x<0||x>=13||row<0||row>=SECTION)return false;
        int ground=s.ground[row*13+x];return ground==WATER||ground==BRIDGE;
    }
    static void trimWaterTips(Section s){
        // Boss clearings can leave a one-row sliver of a lake/river. Remove its
        // unsupported three-sided end caps, retaining bridge routes as roads.
        boolean changed;
        do{
            changed=false;
            for(int row=0;row<SECTION;row++)for(int x=0;x<13;x++)if(wet(s,x,row)){
                int land=(!wet(s,x,row+1)?1:0)+(!wet(s,x+1,row)?1:0)+(!wet(s,x,row-1)?1:0)+(!wet(s,x-1,row)?1:0);
                if(land>=3){int at=row*13+x;set(s,x,row,OPEN,s.ground[at]==BRIDGE?ROAD:GRASS,0);changed=true;}
            }
        }while(changed);
    }
    static void swap(byte[] a,int x,int y){byte t=a[x];a[x]=a[y];a[y]=t;}
    static void repair(Section s){
        boolean[] seen=flood(s.tiles);
        for(int cell=0;cell<s.tiles.length;cell++)if(s.tiles[cell]==OPEN&&!seen[cell]){
            int[] cost=new int[s.tiles.length],parent=new int[s.tiles.length];Arrays.fill(cost,Integer.MAX_VALUE);Arrays.fill(parent,-1);
            var queue=new PriorityQueue<int[]>(Comparator.comparingInt(a->a[1]));queue.add(new int[]{cell,0});cost[cell]=0;int reached=-1;
            while(!queue.isEmpty()){
                var entry=queue.remove();int at=entry[0];if(entry[1]!=cost[at])continue;if(seen[at]){reached=at;break;}
                for(int next:neighbors(at,13,SECTION)){
                    int weight=s.tiles[next]==OPEN?0:s.ground[next]==WATER?8:1,newCost=cost[at]+weight;
                    if(newCost>=cost[next])continue;cost[next]=newCost;parent[next]=at;queue.add(new int[]{next,newCost});
                }
            }
            for(int at=reached;at>=0;at=parent[at]){if(s.tiles[at]!=OPEN)lane(s,at%13,at/13,s.ground[at]==PAVING?PAVING:DIRT);if(at==cell)break;}
            seen=flood(s.tiles);
        }
    }
    static boolean[] flood(byte[] tiles){
        boolean[] seen=new boolean[tiles.length];ArrayDeque<Integer> queue=new ArrayDeque<>();queue.add(6);seen[6]=true;
        while(!queue.isEmpty())for(int next:neighbors(queue.remove(),13,SECTION))if(!seen[next]&&tiles[next]==OPEN){seen[next]=true;queue.add(next);}return seen;
    }
    static int[] neighbors(int cell,int width,int height){
        int x=cell%width,y=cell/width;int[] result=new int[(x>0?1:0)+(x<width-1?1:0)+(y>0?1:0)+(y<height-1?1:0)];int n=0;
        if(y<height-1)result[n++]=cell+width;if(x<width-1)result[n++]=cell+1;if(y>0)result[n++]=cell-width;if(x>0)result[n]=cell-1;return result;
    }
}
