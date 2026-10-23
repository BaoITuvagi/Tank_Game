package tank;

import java.util.*;

/** Campaign-only deterministic terrain. Each 32-row section joins through an open clearing. */
public final class CampaignMap {
    final long seed;
    final Map<Integer,byte[]> sections=new LinkedHashMap<>(8,.75f,true){protected boolean removeEldestEntry(Map.Entry<Integer,byte[]> e){return size()>8;}};
    public CampaignMap(long seed){this.seed=seed;}
    public int tile(int x,int y){
        if(x<0||x>=CampaignRun.WIDTH||y<0||y>CampaignRun.GOAL)return ROCK;
        byte[] section=sections.computeIfAbsent(y/SECTION,index->build(seed,index));
        return section[(y%SECTION)*CampaignRun.WIDTH+x];
    }
    // Repair enclosed pockets with the fewest removed obstacles, preserving the authored clusters.
    static byte[] build(long seed,int section){
        int width=CampaignRun.WIDTH;byte[] tiles=new byte[width*SECTION];
        for(int y=0;y<SECTION;y++)for(int x=0;x<width;x++)tiles[y*width+x]=(byte)raw(seed,x,section*SECTION+y);
        boolean[] seen=flood(tiles);
        for(int cell=0;cell<tiles.length;cell++)if(tiles[cell]==OPEN&&!seen[cell]){
            int[] cost=new int[tiles.length],parent=new int[tiles.length];Arrays.fill(cost,Integer.MAX_VALUE);Arrays.fill(parent,-1);
            ArrayDeque<Integer> queue=new ArrayDeque<>();queue.add(cell);cost[cell]=0;int reached=-1;
            while(!queue.isEmpty()){
                int at=queue.removeFirst();if(seen[at]){reached=at;break;}
                for(int next:neighbors(at,width,SECTION)){
                    int weight=tiles[next]==OPEN?0:1,newCost=cost[at]+weight;
                    if(newCost>=cost[next])continue;cost[next]=newCost;parent[next]=at;
                    if(weight==0)queue.addFirst(next);else queue.addLast(next);
                }
            }
            for(int at=reached;at>=0;at=parent[at]){tiles[at]=OPEN;if(at==cell)break;}
            seen=flood(tiles);
        }
        return tiles;
    }
    static boolean[] flood(byte[] tiles){
        boolean[] seen=new boolean[tiles.length];ArrayDeque<Integer> queue=new ArrayDeque<>();queue.add(6);seen[6]=true;
        while(!queue.isEmpty())for(int next:neighbors(queue.remove(),CampaignRun.WIDTH,SECTION))if(!seen[next]&&tiles[next]==OPEN){seen[next]=true;queue.add(next);}
        return seen;
    }
    static int[] neighbors(int cell,int width,int height){
        int x=cell%width,y=cell/width;int[] result=new int[(x>0?1:0)+(x<width-1?1:0)+(y>0?1:0)+(y<height-1?1:0)];int n=0;
        if(y<height-1)result[n++]=cell+width;if(x<width-1)result[n++]=cell+1;if(y>0)result[n++]=cell-width;if(x>0)result[n]=cell-1;return result;
    }
    public static final int OPEN=0,TREE=1,ROCK=2,CRATE=3,RUINS=4,SECTION=32;
    record Patch(int x,int y,int rx,int ry,int tile){}
    static final Patch[][] PATCHES={
        {new Patch(1,6,2,4,TREE),new Patch(10,5,2,3,TREE),new Patch(3,14,2,2,TREE),new Patch(10,20,2,3,TREE),new Patch(2,27,2,3,TREE),new Patch(7,11,1,1,ROCK),new Patch(8,27,1,1,ROCK),new Patch(2,20,1,1,CRATE)},
        {new Patch(0,4,1,3,TREE),new Patch(12,10,1,3,TREE),new Patch(1,26,2,3,TREE),new Patch(11,28,1,2,TREE),new Patch(3,6,2,2,ROCK),new Patch(9,12,2,2,ROCK),new Patch(2,21,1,2,ROCK),new Patch(9,25,2,3,ROCK),new Patch(4,28,1,1,CRATE)},
        {new Patch(1,2,2,2,TREE),new Patch(11,3,2,3,TREE),new Patch(1,28,2,2,TREE),new Patch(11,16,1,2,TREE),new Patch(9,11,1,1,CRATE),new Patch(3,25,1,1,CRATE),new Patch(2,17,1,1,ROCK)},
        {new Patch(1,5,2,3,TREE),new Patch(10,4,2,3,TREE),new Patch(3,12,2,3,TREE),new Patch(10,15,2,3,TREE),new Patch(11,27,1,3,TREE),new Patch(2,28,2,2,TREE),new Patch(8,24,1,2,RUINS),new Patch(9,20,1,1,ROCK)}
    };
    static long hash(long seed,int section){long h=seed+section*0x9E3779B97F4A7C15L;h=(h^(h>>>30))*0xBF58476D1CE4E5B9L;h=(h^(h>>>27))*0x94D049BB133111EBL;return h^(h>>>31);}
    public static int style(long seed,int section){return (int)(hash(seed,section)&3);}
    public static int pathX(long seed,int y){
        int row=y%SECTION,offset=(int)((hash(seed,y/SECTION)>>>8)%5)-2;
        float ramp=Math.min(1f,Math.min(row,SECTION-1-row)/6f);
        return 6+Math.round(offset*ramp);
    }
    static int raw(long seed,int x,int y){
        if(x<0||x>=CampaignRun.WIDTH||y<0||y>CampaignRun.GOAL)return ROCK;
        int row=y%SECTION,checkpoint=y%500;
        // Safe arena and section joins stay open; pressure routes are deterministic.
        if(checkpoint<=3||checkpoint>=497||row==0||row==16||row==31)return OPEN;
        int stage=y/500,gate=pressureRow(stage),side=(hash(seed,y/SECTION)&32)==0?1:11;
        int pressure=pressure(x,row,pathX(seed,y),side,gate,stage%2==0?RUINS:ROCK);
        if(pressure>=0)return pressure;
        if(stage>=6){pressure=pressure(x,row,pathX(seed,y),12-side,gate==22?10:22,CRATE);if(pressure>=0)return pressure;}
        if(Math.abs(x-pathX(seed,y))<=1)return OPEN;
        long h=hash(seed,y/SECTION);int localX=(h&16)==0?x:12-x,style=(int)(h&3);
        if(style==2){int room=room(localX,row,1,6,4,7,true);if(room>=0)return room;room=room(localX,row,8,20,4,7,false);if(room>=0)return room;}
        if(style==3){int room=room(localX,row,1,20,4,6,true);if(room>=0)return room;if(row==8&&localX>=8&&localX<=11)return RUINS;}
        for(Patch p:PATCHES[style]){int dx=localX-p.x,dy=row-p.y;if(dx*dx*p.ry*p.ry+dy*dy*p.rx*p.rx<=p.rx*p.rx*p.ry*p.ry)return p.tile;}
        return OPEN;
    }
    static int pressureRow(int stage){return new int[]{8,10,22}[Math.floorMod(stage,3)];}
    static int pressure(int x,int row,int main,int flank,int gate,int material){
        if(row<gate-2||row>gate+4)return -1;
        // Broad approach/exit clearings connect a one-cell gate and a second flank route.
        if(row==gate-2||row==gate+4||x==main||x==flank)return OPEN;
        if(row==gate+1)return material;
        if(row>=gate&&row<=gate+2&&Math.abs(x-main)==1)return material;
        return -1;
    }
    static int room(int x,int y,int left,int bottom,int width,int height,boolean doorRight){
        int right=left+width-1,top=bottom+height-1;
        if(x<left||x>right||y<bottom||y>top)return -1;
        if(y==bottom+height/2&&x==(doorRight?right:left))return OPEN;
        if(x==left||x==right||y==bottom||y==top)return RUINS;
        return OPEN;
    }
}
