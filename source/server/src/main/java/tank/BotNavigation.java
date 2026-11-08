package tank;

import java.util.*;

/** Bounded A* on a coarse clearance grid; destructible cover invalidates the grid. */
final class BotNavigation {
    static final float STEP=1,CLEARANCE=(float)(GameRules.TankHalfSize*Math.sqrt(2))+.04f;
    final GameMap map;
    final int columns,rows;
    final boolean[] walkable;
    int brickHash,lastExpansions;
    boolean initialized;
    byte[] bricks;
    record Node(int cell,float score){}
    BotNavigation(GameMap map){this.map=map;columns=(int)Math.ceil(map.width/STEP);rows=(int)Math.ceil(map.height/STEP);walkable=new boolean[columns*rows];}
    float x(int cell){return (cell%columns+.5f)*STEP;}
    float y(int cell){return (cell/columns+.5f)*STEP;}
    float clearance(float px,float py) {
        float distance=Math.min(Math.min(px,map.width-px),Math.min(py,map.height-py));
        for(var r:map.walls)distance=Math.min(distance,distance(px,py,r));
        for(var r:map.bricks)if(bricks[r.id]>0)distance=Math.min(distance,distance(px,py,r));
        return distance;
    }
    static float distance(float px,float py,GameMap.Rect r) {
        float dx=Math.max(Math.max(r.x-px,px-r.x-r.w),0),dy=Math.max(Math.max(r.y-py,py-r.y-r.h),0);
        return (float)Math.hypot(dx,dy);
    }
    boolean free(float px,float py){return clearance(px,py)>=CLEARANCE;}
    boolean clear(float ax,float ay,float bx,float by) {
        int steps=Math.max(1,(int)Math.ceil(Math.hypot(bx-ax,by-ay)/.35));
        for(int i=0;i<=steps;i++)if(!free(ax+(bx-ax)*i/steps,ay+(by-ay)*i/steps))return false;
        return true;
    }
    int nearest(float px,float py,boolean reachable) {
        int cx=(int)(px/STEP),cy=(int)(py/STEP),best=-1;double distance=Double.MAX_VALUE;
        for(int radius=0;radius<=6;radius++) {
            for(int yy=Math.max(0,cy-radius);yy<=Math.min(rows-1,cy+radius);yy++)for(int xx=Math.max(0,cx-radius);xx<=Math.min(columns-1,cx+radius);xx++) {
                int cell=yy*columns+xx;if(!walkable[cell])continue;
                double d=Math.hypot(x(cell)-px,y(cell)-py);
                if(d<distance&&(!reachable||clear(px,py,x(cell),y(cell)))){distance=d;best=cell;}
            }
            if(best>=0)return best;
        }
        return best;
    }
    float heuristic(int a,int b){return (float)Math.hypot(a%columns-b%columns,a/columns-b/columns);}
    ArrayDeque<float[]> route(float sx,float sy,float gx,float gy,byte[] hp) {
        bricks=hp;int hash=Arrays.hashCode(hp);
        if(!initialized||hash!=brickHash){for(int i=0;i<walkable.length;i++)walkable[i]=free(x(i),y(i));brickHash=hash;initialized=true;}
        lastExpansions=0;var path=new ArrayDeque<float[]>();
        if(clear(sx,sy,gx,gy)){path.add(new float[]{gx,gy});return path;}
        int start=nearest(sx,sy,true),goal=nearest(gx,gy,false);if(start<0||goal<0)return path;
        float[] costs=new float[walkable.length];Arrays.fill(costs,Float.POSITIVE_INFINITY);costs[start]=0;
        int[] parent=new int[walkable.length];Arrays.fill(parent,-1);boolean[] closed=new boolean[walkable.length];
        var queue=new PriorityQueue<Node>(Comparator.comparingDouble(Node::score));queue.add(new Node(start,heuristic(start,goal)));int best=start;
        while(!queue.isEmpty()&&lastExpansions<3200) {
            int current=queue.remove().cell;if(closed[current])continue;closed[current]=true;lastExpansions++;
            if(heuristic(current,goal)<heuristic(best,goal))best=current;
            if(current==goal){best=current;break;}
            int cx=current%columns,cy=current/columns;
            for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++) {
                int nx=cx+dx,ny=cy+dy;if(dx==0&&dy==0||nx<0||ny<0||nx>=columns||ny>=rows)continue;
                int next=ny*columns+nx;if(!walkable[next]||closed[next])continue;
                if(dx!=0&&dy!=0&&(!walkable[cy*columns+nx]||!walkable[ny*columns+cx]))continue;
                if(!clear(x(current),y(current),x(next),y(next)))continue;
                float terrain=map.water(x(next),y(next))&&map.mapId==MapCatalog.DEFAULT_ID?1.7f:1;
                float cost=costs[current]+(dx!=0&&dy!=0?1.414214f:1)*terrain;
                if(cost>=costs[next])continue;costs[next]=cost;parent[next]=current;queue.add(new Node(next,cost+heuristic(next,goal)));
            }
        }
        for(int cell=best;cell>=0;cell=parent[cell]){path.addFirst(new float[]{x(cell),y(cell)});if(cell==start)break;}
        if(best==goal&&clear(x(goal),y(goal),gx,gy))path.add(new float[]{gx,gy});
        // Remove grid zigzags while retaining clearance for every body rotation.
        var smooth=new ArrayDeque<float[]>();float ax=sx,ay=sy;
        while(!path.isEmpty()) {
            float[] next=path.removeFirst();
            while(!path.isEmpty()&&clear(ax,ay,path.peek()[0],path.peek()[1]))next=path.removeFirst();
            smooth.add(next);ax=next[0];ay=next[1];
        }
        return smooth;
    }
}
