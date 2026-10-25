package tank;

import static tank.CampaignMap.*;

/** Campaign-only chamber layouts: winding routes, alternate loops and landmark compounds. */
final class CampaignLayouts {
    private CampaignLayouts(){}
    static boolean authored(int t){return t!=2&&t!=5&&t!=10;}
    // Each pair starts a straight run at a row. Horizontal turns are carved at that row.
    static final int[][] ROUTES={
        {0,6,4,3,10,7,18,4,25,8,29,6},
        {0,6,5,4,12,6,22,8,27,6}, {},
        {0,6,4,3,11,9,19,4,27,6},
        {0,6,4,9,11,3,19,8,27,6}, {},
        {0,6,5,3,12,8,20,4,27,6},
        {0,6,5,9,12,4,20,8,27,6},
        {0,6,4,3,11,9,19,3,27,6},
        {0,6,5,9,11,3,19,8,27,6}, {},
        {0,6,5,4,13,8,21,3,27,6}
    };
    static int routeX(int t,int row){int x=6;for(int i=0;i<ROUTES[t].length;i+=2){if(row<ROUTES[t][i])break;x=ROUTES[t][i+1];}return x;}
    static int rewardX(int t){return new int[]{10,6,11,1,11,10,10,2,10,2,10,10}[t];}
    static int rewardRow(int t){return new int[]{8,16,16,22,23,24,24,23,6,24,24,24}[t];}
    static void floor(Section s,int x,int y,int w,int h){rect(s,x,y,w,h,OPEN,GRASS,0);}
    static void clearing(Section s,int x,int y,int rx,int ry){ellipse(s,x,y,rx,ry,OPEN,GRASS,0);}
    static void passage(Section s,int x1,int y1,int x2,int y2){
        floor(s,Math.min(x1,x2)-1,y1,Math.abs(x2-x1)+3,2);
        floor(s,x2-1,Math.min(y1,y2),3,Math.abs(y2-y1)+1);
    }
    static boolean reserved(int t,int x,int row){
        int a=routeX(t,row),b=routeX(t,Math.max(0,row-1));
        return x>=Math.min(a,b)-1&&x<=Math.max(a,b)+1;
    }
    static void object(Section s,int t,int x,int y,int tile,int decor){
        if(!reserved(t,x,y))solid(s,x,y,tile,decor);
    }
    static void patch(Section s,int t,int x,int y,int w,int h,int tile,int decor){
        for(int row=y;row<y+h;row++)for(int col=x;col<x+w;col++)object(s,t,col,row,tile,decor);
    }
    static void flag(Section s,int x,int y){if(s.tiles[y*13+x]==OPEN)set(s,x,y,OPEN,GRASS,FLAG);}
    static void build(Section s,int t){
        int rim=t==3||t==4||t==9?ROCK:TREE;
        rect(s,0,0,13,32,rim,GRASS,0);
        clearing(s,6,2,3,3);clearing(s,6,29,3,3);
        switch(t){
            case 0->{
                clearing(s,3,7,3,4);clearing(s,7,14,4,4);clearing(s,4,22,3,4);clearing(s,8,27,3,2);
                // An S-shaped woodland trail opens into a guarded side clearing.
                clearing(s,10,8,2,2);
            }
            case 1->{
                clearing(s,4,7,3,4);clearing(s,6,16,5,7);clearing(s,8,25,3,3);
            }
            case 3->{
                clearing(s,3,7,3,4);clearing(s,9,15,3,4);clearing(s,4,23,4,4);
            }
            case 4->{
                clearing(s,9,7,3,4);floor(s,1,12,6,7);clearing(s,8,23,4,4);
            }
            case 6->{
                floor(s,1,5,5,6);floor(s,6,12,6,7);floor(s,2,20,6,7);
                // Three staggered courtyards, rather than three parallel avenues.
            }
            case 7->{
                floor(s,6,5,6,7);floor(s,1,12,6,8);floor(s,6,20,6,7);
            }
            case 8->{
                floor(s,1,4,5,7);floor(s,6,11,6,8);floor(s,1,19,5,8);
            }
            case 9->{
                floor(s,6,5,6,6);floor(s,1,11,6,8);floor(s,5,19,7,8);
            }
            case 11->{
                clearing(s,4,8,4,4);clearing(s,8,17,4,4);clearing(s,3,24,3,4);
            }
        }
        // Two-vehicle-width approaches and corner landings keep both player and squads mobile.
        for(int row=0;row<32;row++)passage(s,routeX(t,Math.max(0,row-1)),row,routeX(t,row),row);
        if(t!=1){
            // A visible reward courtyard has two entrances: choose the short route or explore a loop.
            int x=rewardX(t),y=rewardRow(t);
            floor(s,x-1,y-2,3,5);
            passage(s,routeX(t,y-2),y-2,x,y-2);
            passage(s,x,y-2,x,y+2);
            passage(s,x,y+2,routeX(t,y+2),y+2);
        }
        switch(t){
            case 0->{
                patch(s,t,5,12,2,2,TREE,0);object(s,t,1,7,ROCK,0);object(s,t,10,10,CRATE,SUPPLIES);
                object(s,t,7,23,ROCK,0);object(s,t,1,22,TREE,0);
            }
            case 1->{
                object(s,t,2,14,RUINS,HOUSE);object(s,t,10,18,ROCK,TRUCK);
                object(s,t,3,20,CRATE,SUPPLIES);object(s,t,9,12,CRATE,SUPPLIES);
                flag(s,4,14);flag(s,8,18);
            }
            case 3->{
                patch(s,t,5,8,2,3,ROCK,0);patch(s,t,5,15,2,3,ROCK,0);
                object(s,t,11,16,ROCK,0);object(s,t,2,20,RUINS,RUIN_TOWER);object(s,t,2,24,CRATE,SUPPLIES);
            }
            case 4->{
                object(s,t,11,7,ROCK,TRUCK);object(s,t,1,14,RUINS,RUIN_FACTORY);
                patch(s,t,5,14,2,3,ROCK,0);object(s,t,10,21,ROCK,0);object(s,t,11,25,CRATE,SUPPLIES);
                object(s,t,1,17,CRATE,SUPPLIES);
            }
            case 6->{
                object(s,t,1,6,RUINS,HOUSE);object(s,t,5,9,RUINS,HANGAR);
                object(s,t,6,14,RUINS,FACTORY);object(s,t,11,16,RUINS,TOWER);
                object(s,t,2,22,RUINS,HOUSE);object(s,t,7,25,RUINS,HANGAR);
                object(s,t,10,22,ROCK,TRUCK);object(s,t,11,25,CRATE,SUPPLIES);
                flag(s,2,8);flag(s,10,18);flag(s,6,23);
            }
            case 7->{
                object(s,t,6,6,RUINS,HANGAR);object(s,t,11,9,RUINS,HOUSE);
                object(s,t,1,14,RUINS,FACTORY);object(s,t,6,17,RUINS,HANGAR);
                object(s,t,6,22,RUINS,HOUSE);object(s,t,11,24,ROCK,TRUCK);
                patch(s,t,1,16,2,2,CRATE,SUPPLIES);object(s,t,6,14,CRATE,SUPPLIES);
                object(s,t,10,21,CRATE,SUPPLIES);object(s,t,1,25,CRATE,SUPPLIES);
                set(s,10,24,OPEN,GRASS,MARKING);
            }
            case 8->{
                object(s,t,1,7,RUINS,TOWER);object(s,t,5,7,RUINS,HOUSE);
                object(s,t,7,13,RUINS,TOWER);object(s,t,11,16,ROCK,PARKED_TANK);
                patch(s,t,1,9,5,1,CRATE,0);patch(s,t,6,17,6,1,CRATE,0);
                object(s,t,1,22,RUINS,HANGAR);object(s,t,5,24,CRATE,SUPPLIES);
                flag(s,2,6);flag(s,10,14);flag(s,4,23);
            }
            case 9->{
                patch(s,t,6,7,1,3,RUINS,0);object(s,t,11,7,RUINS,RUIN_HOUSE);
                object(s,t,1,13,RUINS,RUIN_FACTORY);object(s,t,6,16,RUINS,RUIN_TOWER);
                patch(s,t,5,12,2,2,RUINS,0);patch(s,t,5,20,1,3,RUINS,0);
                object(s,t,11,24,RUINS,RUIN_FACTORY);object(s,t,6,25,RUINS,RUBBLE);
                object(s,t,1,25,CRATE,SUPPLIES);object(s,t,2,22,RUINS,RUIN_HOUSE);
            }
            case 11->{
                object(s,t,1,7,ROCK,TRUCK);object(s,t,6,10,ROCK,PARKED_TANK);
                object(s,t,10,15,ROCK,TRUCK);object(s,t,5,18,ROCK,PARKED_TANK);
                object(s,t,1,23,ROCK,PARKED_TANK);object(s,t,5,25,CRATE,SUPPLIES);
                object(s,t,11,23,CRATE,SUPPLIES);object(s,t,10,26,RUINS,RUIN_FACTORY);
                set(s,10,24,OPEN,GRASS,MARKING);
            }
        }
        // Gentle visual markers emphasize entrances without blocking them.
        if(t>=6)for(int row:new int[]{6,14,23})flag(s,routeX(t,row)+1,row);
    }
}
