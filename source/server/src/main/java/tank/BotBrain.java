package tank;

import java.util.*;

/** Medium preset: delayed perception, imperfect aim and the same movement/combat rules as humans. */
final class BotBrain {
    final Room room;
    final Room.Member member;
    final Random random;
    final BotNavigation navigation;
    ArrayDeque<float[]> path=new ArrayDeque<>();
    int thinkAt,pathAt,roamAt,target=-1,seenAt,reactionAt,skillAt,burstAt,evadeAt,stuckAt;
    int desiredAim,lastMove,life=-1,blockedTicks,evadeUntil,recoverUntil,sprintUntil,sprintRestUntil;
    float goalX,goalY,seenX,seenY,previousX,previousY,lastX,lastY,windowTravel,aimError,evadeX,evadeY,recoverX,recoverY;
    boolean fireBurst,hasGoal,tryingToMove;
    long decisions,skillUses,fireTicks,sprintTicks,dodges,recoveries;
    BotBrain(Room room,Room.Member member,long seed){this.room=room;this.member=member;random=new Random(seed);navigation=new BotNavigation(room.arenaMap);}
    void reset(){path.clear();thinkAt=pathAt=roamAt=seenAt=reactionAt=skillAt=burstAt=evadeAt=stuckAt=desiredAim=lastMove=blockedTicks=evadeUntil=recoverUntil=sprintUntil=sprintRestUntil=0;target=life=-1;hasGoal=fireBurst=tryingToMove=false;windowTravel=0;decisions=skillUses=fireTicks=sprintTicks=dodges=recoveries=0;}
    static int angle(float dx,float dy){return Math.floorMod((int)Math.round(Math.toDegrees(Math.atan2(dy,dx))*100),36000);}
    void goal(float x,float y) {
        if(!hasGoal||Math.hypot(x-goalX,y-goalY)>3)pathAt=0;
        goalX=Math.max(2,Math.min(room.arenaMap.width-2,x));goalY=Math.max(2,Math.min(room.arenaMap.height-2,y));hasGoal=true;
    }
    void think(World.Tank tank) {
        World world=room.world;int now=world.tick;decisions++;thinkAt=now+6+random.nextInt(7);
        World.Tank visible=null;double best=Double.MAX_VALUE;
        for(var other:world.tanks.values())if(other!=tank&&other.hp>0) {
            double distance=Math.hypot(other.x-tank.x,other.y-tank.y);
            if(distance>38||!world.visible(tank.x,tank.y,other.x,other.y))continue;
            double score=distance-(other.slot==target?5:0);
            if(score<best){best=score;visible=other;}
        }
        if(visible!=null) {
            if(target!=visible.slot){target=visible.slot;reactionAt=now+9+random.nextInt(12);}
            seenX=visible.x;seenY=visible.y;seenAt=now;
            aimError=(random.nextFloat()*2-1)*(250+random.nextInt(350));
            desiredAim=Math.floorMod(angle(seenX-tank.x,seenY-tank.y)+(int)aimError,36000);
        }else if(target>=0&&now-seenAt>45){target=-1;roamAt=0;}
        if(now>=burstAt){fireBurst=!fireBurst;burstAt=now+(fireBurst?12+random.nextInt(17):9+random.nextInt(13));}
        boolean seesTarget=visible!=null;
        WorldItems.Item pickup=null;double itemScore=Double.MAX_VALUE;
        for(var item:world.items.items.values()) {
            double distance=Math.hypot(item.x-tank.x,item.y-tank.y),score=distance;
            if(item.type==WorldItems.MEDICAL){if(tank.hp>=tank.maxHp*.85f)continue;score*=.5;}
            if(distance>(seesTarget?12:32)||score>=itemScore)continue;itemScore=score;pickup=item;
        }
        if(pickup!=null)goal(pickup.x,pickup.y);
        else if(seesTarget) {
            float dx=seenX-tank.x,dy=seenY-tank.y;double distance=Math.max(.1,Math.hypot(dx,dy));
            float side=((now/65+member.slot)%2==0?1:-1)*(5+random.nextFloat()*3),keep=distance<10?15:12;
            goal(seenX-(float)(dx/distance)*keep-(float)(dy/distance)*side,seenY-(float)(dy/distance)*keep+(float)(dx/distance)*side);
        }else if(target>=0)goal(seenX,seenY);
        else if(!hasGoal||now>=roamAt||Math.hypot(goalX-tank.x,goalY-tank.y)<3) {
            goal(room.arenaMap.width*(.25f+random.nextFloat()*.5f),room.arenaMap.height*(.25f+random.nextFloat()*.5f));roamAt=now+120+random.nextInt(121);
        }
        navigation.bricks=world.bricks;
        // React only at perception updates to visible incoming trajectories, then commit to the dodge.
        if(now>=evadeAt&&now>=evadeUntil) {
            var threat=BotMovement.threat(world,tank,lastMove);
            var escape=threat==null?null:BotMovement.evade(world,tank,threat,navigation);
            if(escape!=null){evadeX=escape[0];evadeY=escape[1];evadeUntil=now+10+random.nextInt(6);evadeAt=evadeUntil+6+random.nextInt(7);recoverUntil=0;dodges++;}
        }
        if(now>=evadeUntil&&now>=recoverUntil&&(now>=pathAt||path.isEmpty())) {
            path=navigation.route(tank.x,tank.y,goalX,goalY,world.bricks);pathAt=now+24+random.nextInt(22);
            if(!path.isEmpty()&&!navigation.free(goalX,goalY)){goalX=path.getLast()[0];goalY=path.getLast()[1];}
        }
        if(room.roleplay!=null&&seesTarget&&now>=reactionAt&&now>=skillAt&&(now>=tank.qReadyTick||tank.qStage==1)) {
            float x=seenX,y=seenY;World expected=world;int slot=member.slot;
            world.skillIntents.add(()->{if(room.world==expected&&room.members.get(member.slot)==member&&member.control==Room.Control.AI){expected.skillAim(slot,x,y);if(expected.useSkill(slot,0)==Protocol.Error.OK){skillUses++;member.roleplay.qUses++;}}});
            skillAt=now+(tank.nation==1&&tank.qStage==0?12+random.nextInt(10):75+random.nextInt(91));
        }
    }
    void observeMotion(World.Tank tank) {
        int now=room.world.tick;
        if(life!=tank.life) {
            life=tank.life;lastMove=blockedTicks=evadeUntil=recoverUntil=sprintUntil=sprintRestUntil=0;path.clear();hasGoal=false;target=-1;thinkAt=pathAt=roamAt=0;
            previousX=lastX=tank.x;previousY=lastY=tank.y;stuckAt=now+24;windowTravel=0;tryingToMove=false;
        }
        double moved=Math.hypot(tank.x-lastX,tank.y-lastY);windowTravel+=moved;
        if(tryingToMove&&moved<.002)blockedTicks++;else blockedTicks=0;
        if(now>=stuckAt) {
            if(tryingToMove&&windowTravel>1&&hasGoal&&Math.hypot(goalX-tank.x,goalY-tank.y)>2&&Math.hypot(tank.x-previousX,tank.y-previousY)<.65)blockedTicks=12;
            previousX=tank.x;previousY=tank.y;stuckAt=now+24;windowTravel=0;
        }
        lastX=tank.x;lastY=tank.y;
        if(evadeUntil>0&&now>=evadeUntil){evadeUntil=0;pathAt=0;}
        if(recoverUntil>0&&now>=recoverUntil){recoverUntil=0;path.clear();pathAt=0;blockedTicks=0;}
    }
    boolean sprint(World.Tank tank,float x,float y,boolean evading,boolean recovering) {
        int now=room.world.tick;double distance=Math.hypot(x-tank.x,y-tank.y);
        if(evading)return tank.energy>8&&distance>1;
        if(recovering||distance<2)return false;
        float scale=(float)(Math.min(2.5,distance)/distance);
        if(!navigation.clear(tank.x,tank.y,tank.x+(x-tank.x)*scale,tank.y+(y-tank.y)*scale))return false;
        if(now<sprintUntil&&tank.energy>28)return true;
        if(sprintUntil>0){sprintUntil=0;sprintRestUntil=now+45;}
        if(now<sprintRestUntil||tank.energy<80||distance<4)return false;
        sprintUntil=now+18+random.nextInt(19);return true;
    }
    World.Input input() {
        World world=room.world;World.Tank tank=world.tanks.get(member.slot);
        if(tank==null||tank.hp<=0){thinkAt=0;life=-1;lastMove=0;tryingToMove=false;path.clear();return new World.Input(world.tick+1,0,tank==null?0:tank.turret);}
        observeMotion(tank);
        if(world.tick>=thinkAt)think(tank);
        while(!path.isEmpty()&&Math.hypot(path.peek()[0]-tank.x,path.peek()[1]-tank.y)<.35)path.remove();
        if(world.tick>=evadeUntil&&world.tick>=recoverUntil&&(blockedTicks>=12||path.isEmpty()&&hasGoal&&Math.hypot(goalX-tank.x,goalY-tank.y)>2)) {
            var escape=BotMovement.escape(world,tank,goalX,goalY,navigation);
            if(escape!=null){recoverX=escape[0];recoverY=escape[1];recoverUntil=world.tick+12+random.nextInt(7);blockedTicks=0;recoveries++;}
            else {pathAt=roamAt=0;hasGoal=false;}
        }
        boolean evading=world.tick<evadeUntil,recovering=!evading&&world.tick<recoverUntil;
        float[] waypoint=evading?new float[]{evadeX,evadeY}:recovering?new float[]{recoverX,recoverY}:path.peek();
        tryingToMove=waypoint!=null&&Math.hypot(waypoint[0]-tank.x,waypoint[1]-tank.y)>.35;
        int buttons=0;
        if(waypoint!=null) {
            float dx=waypoint[0]-tank.x,dy=waypoint[1]-tank.y;
            boolean running=sprint(tank,waypoint[0],waypoint[1],evading,recovering);
            buttons=BotMovement.steer(world,tank,waypoint[0],waypoint[1],lastMove,running,navigation);
            if(target<0)desiredAim=angle(dx,dy);
            if(running&&buttons!=0){buttons|=32;sprintTicks++;}
        }
        lastMove=buttons;
        World.Tank enemy=world.tanks.get(target);
        if(enemy!=null&&enemy.hp>0&&world.tick>=reactionAt&&world.tick-seenAt<=12&&fireBurst&&Math.abs(Math.floorMod(tank.turret-desiredAim+18000,36000)-18000)<950&&world.visible(tank.x,tank.y,seenX,seenY)){buttons|=16;fireTicks++;}
        return new World.Input(world.tick+1,buttons,desiredAim);
    }
}
