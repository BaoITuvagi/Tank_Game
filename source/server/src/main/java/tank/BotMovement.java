package tank;

/** Short movement forecasts use the real rules, including rotated bodies and terrain. */
final class BotMovement {
    static final int[] DIRECTIONS={8,9,1,5,4,6,2,10};
    record Threat(World.Bullet bullet,float vx,float vy,float impact){}
    static World.Tank preview(World world,World.Tank tank,int buttons,int ticks) {
        var future=tank.copy();
        for(int i=1;i<=ticks;i++)World.movement(future,new World.Input(world.tick+i,buttons,tank.turret),world.map,world.bricks,world.tick+i,world.skills.effects,world.battlefield.wave);
        return future;
    }
    static int steer(World world,World.Tank tank,float gx,float gy,int previous,boolean sprint,BotNavigation navigation) {
        double distance=Math.hypot(gx-tank.x,gy-tank.y);if(distance<.3)return 0;
        int horizon=Math.max(1,Math.min(6,(int)(distance/.65))),best=0;double score=-Double.MAX_VALUE;
        boolean safeStart=navigation.free(tank.x,tank.y);
        for(int direction:DIRECTIONS) {
            var future=preview(world,tank,direction|(sprint?32:0),horizon);
            double moved=Math.hypot(future.x-tank.x,future.y-tank.y);if(moved<.001)continue;
            if(safeStart&&!navigation.clear(tank.x,tank.y,future.x,future.y))continue;
            double progress=distance-Math.hypot(gx-future.x,gy-future.y);
            double value=progress+(direction==(previous&15)?.035:0);
            if(value>score){score=value;best=direction;}
        }
        return best;
    }
    static Threat threat(World world,World.Tank tank,int movement) {
        var future=preview(world,tank,movement,1);float tx=future.x-tank.x,ty=future.y-tank.y;
        Threat nearest=null;
        for(var bullet:world.bullets.values()) {
            if(bullet.owner==tank.slot&&world.tick-bullet.born<6)continue;
            if(Math.hypot(bullet.x-tank.x,bullet.y-tank.y)>24||!world.visible(tank.x,tank.y,bullet.x,bullet.y))continue;
            double heading=Math.toRadians(bullet.heading/100.0);float speed=GameRules.BulletStep*bullet.speedPercent/100f;
            float vx=(float)Math.cos(heading)*speed,vy=(float)Math.sin(heading)*speed;
            float rx=bullet.x-tank.x,ry=bullet.y-tank.y,dx=vx-tx,dy=vy-ty,v2=dx*dx+dy*dy;
            if(v2<.001)continue;
            float impact=-(rx*dx+ry*dy)/v2;
            if(impact<0||impact>Math.min(26,bullet.remainingLifetime)||Math.hypot(rx+dx*impact,ry+dy*impact)>2.1)continue;
            if(nearest==null||impact<nearest.impact)nearest=new Threat(bullet,vx,vy,impact);
        }
        return nearest;
    }
    static float[] evade(World world,World.Tank tank,Threat threat,BotNavigation navigation) {
        float[] best=null;double score=-Double.MAX_VALUE;
        boolean safeStart=navigation.free(tank.x,tank.y);
        for(int direction:DIRECTIONS) {
            var future=preview(world,tank,direction|(tank.energy>12?32:0),10);
            float mx=future.x-tank.x,my=future.y-tank.y;
            if(Math.hypot(mx,my)<.6)continue;
            if(safeStart&&!navigation.clear(tank.x,tank.y,future.x,future.y))continue;
            float rx=threat.bullet.x-tank.x,ry=threat.bullet.y-tank.y,dx=threat.vx-mx/10,dy=threat.vy-my/10;
            float v2=dx*dx+dy*dy,time=v2<.001?0:Math.max(0,Math.min(20,-(rx*dx+ry*dy)/v2));
            double separation=Math.hypot(rx+dx*time,ry+dy*time);
            double sideways=Math.abs(mx*threat.vy-my*threat.vx)/Math.max(.01,Math.hypot(threat.vx,threat.vy));
            double value=Math.min(5,separation)*3+sideways*.3+Math.min(3,navigation.clearance(future.x,future.y))*.1;
            if(value>score){score=value;best=new float[]{future.x,future.y};}
        }
        return best;
    }
    static float[] escape(World world,World.Tank tank,float gx,float gy,BotNavigation navigation) {
        float[] best=null;double score=-Double.MAX_VALUE,distance=Math.hypot(gx-tank.x,gy-tank.y);
        boolean safeStart=navigation.free(tank.x,tank.y);
        for(int direction:DIRECTIONS) {
            var future=preview(world,tank,direction,10);
            double moved=Math.hypot(future.x-tank.x,future.y-tank.y);if(moved<.4)continue;
            if(safeStart&&!navigation.clear(tank.x,tank.y,future.x,future.y))continue;
            double progress=distance-Math.hypot(gx-future.x,gy-future.y);
            double value=Math.min(5,navigation.clearance(future.x,future.y))*2+progress*.2+moved*.1;
            if(value>score){score=value;best=new float[]{future.x,future.y};}
        }
        return best;
    }
}
