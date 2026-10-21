package tank;
import java.util.*;
import static tank.GameRules.*;
import static tank.Protocol.*;

/** Server-owned casts survive their caller; temporary vehicle states do not. */
final class NationSkills {
    static final int ROCKET=1,BOMB=2,KAMIKAZE=3,BEAM=4,RADAR=5,SABOTAGE=6,AREA=7,ACTIVATE=8;
    static final class Effect {
        int id,kind,owner,start,end,angle;
        float x,y,radius;
        void write(Writer w) {w.u32(id).u8(kind).u8(owner).u16(start).u16(end).f32(x).f32(y).f32(radius).u16(angle);}
        static Effect read(Reader r) {
            Effect e=new Effect();e.id=r.u32();e.kind=r.u8();e.owner=r.u8();e.start=r.u16();e.end=r.u16();e.x=r.f32();e.y=r.f32();e.radius=r.f32();e.angle=r.u16();
            if(e.id<=0||e.kind<1||e.kind>8||e.owner>7||e.end<e.start||e.radius<0||!Float.isFinite(e.radius)||!Float.isFinite(e.x)||!Float.isFinite(e.y)||e.angle>=36000)throw new IllegalArgumentException("skill effect");
            return e;
        }
    }
    static final class Cast {
        int owner,kind,locked;
        final List<Effect> zones=new ArrayList<>();
        final Map<Integer,Integer> lives=new HashMap<>(),damage=new HashMap<>();
    }
    final World w;
    final List<Cast> casts=new ArrayList<>();
    final List<Effect> effects=new ArrayList<>();
    int nextId=1;
    NationSkills(World world) {w=world;}
    Effect effect(int kind,int owner,int start,int end,float x,float y,float radius,int angle) {
        Effect e=new Effect();e.id=nextId++;e.kind=kind;e.owner=owner;e.start=start;e.end=end;e.x=x;e.y=y;e.radius=radius;e.angle=angle;
        // At most 8 concurrent casts; reserve room for warnings before old afterglows.
        if(effects.size()>=MaxSkillEffects)effects.removeIf(old->w.tick>=old.end);
        if(effects.size()>=MaxSkillEffects)throw new IllegalStateException("skill effect budget");
        effects.add(e);return e;
    }
    static float clampAim(float value,float extent) {return Math.max(KatyushaRadius,Math.min(extent-KatyushaRadius,value));}
    void aim(World.Tank t,float x,float y) {
        if(t.hp>0&&t.nation==1&&(t.qStage==1||w.tick>=t.qReadyTick)) {t.aimX=clampAim(x,w.map.width);t.aimY=clampAim(y,w.map.height);}
    }
    Protocol.Error use(World.Tank t) {
        if(t.nation==1&&t.qStage==1) {lock(t);return Protocol.Error.OK;}
        if(w.tick<t.qReadyTick)return Protocol.Error.COOLDOWN;
        t.qStartTick=w.tick;
        switch(t.nation) {
            case 0 -> {
                Cast cast=new Cast();cast.owner=t.slot;cast.kind=BOMB;cast.locked=w.tick;
                for(World.Tank o:w.tanks.values())if(o!=t&&o.hp>0) {
                    cast.lives.put(o.slot,o.life);
                    cast.zones.add(effect(BOMB,t.slot,w.tick,w.tick+BombDelayTicks,o.x,o.y,BombRadius,0));
                }
                casts.add(cast);t.qEndTick=w.tick+BombDelayTicks;
            }
            case 1 -> {
                if(w.map.width<2*KatyushaRadius||w.map.height<2*KatyushaRadius)return Protocol.Error.NO_EFFECT;
                t.qStage=1;t.qEndTick=w.tick+KatyushaAimTicks;
                t.aimX=clampAim(t.aimX,w.map.width);t.aimY=clampAim(t.aimY,w.map.height);
                effect(ACTIVATE,t.slot,w.tick,w.tick+15,t.x,t.y,0,t.nation);
                return Protocol.Error.OK;
            }
            case 2 -> {
                for(World.Tank o:w.tanks.values())if(o!=t&&o.hp>0)o.confusionEndTick=w.tick+ConfusionTicks;
                effect(RADAR,t.slot,w.tick,w.tick+24,t.x,t.y,0,0);
                t.qEndTick=w.tick+ConfusionTicks;
            }
            case 3 -> {
                for(World.Tank o:w.tanks.values())if(o!=t&&o.hp>0)o.fogEndTick=w.tick+FogEnemyTicks;
                t.qEndTick=w.tick+FogEnemyTicks;
            }
            case 4 -> t.qEndTick=w.tick+150;
            case 5 -> {t.qStage=1;t.qEndTick=w.tick+BeamChargeTicks;}
            case 6 -> {t.qStage=1;t.qEndTick=w.tick+KamikazeTicks;}
            case 7 -> {
                for(World.Tank o:w.tanks.values())if(o!=t&&o.hp>0) {
                    o.fireLockEndTick=w.tick+FireLockTicks;
                    if(o.nation>=0)o.qReadyTick=Math.min(65535,Math.max(w.tick,o.qReadyTick)+SabotageCooldownPenaltyTicks);
                }
                effect(SABOTAGE,t.slot,w.tick,w.tick+24,t.x,t.y,0,0);
                t.qEndTick=w.tick+FireLockTicks;
            }
        }
        t.qReadyTick=w.tick+RoleplayContent.NationCooldowns[t.nation];
        effect(ACTIVATE,t.slot,w.tick,w.tick+15,t.x,t.y,0,t.nation);
        return Protocol.Error.OK;
    }
    void lock(World.Tank t) {
        // While aiming, qReadyTick can hold a sabotage penalty that must survive locking.
        t.qStage=0;t.qReadyTick=Math.min(65535,Math.max(w.tick,t.qReadyTick)+RoleplayContent.NationCooldowns[1]);t.qEndTick=w.tick+KatyushaDelayTicks+KatyushaRainTicks;
        Cast cast=new Cast();cast.owner=t.slot;cast.kind=ROCKET;cast.locked=w.tick;
        effect(AREA,t.slot,w.tick,t.qEndTick,t.aimX,t.aimY,KatyushaRadius,0);
        for(int i=0;i<9;i++) {
            double a=w.random.nextDouble()*Math.PI*2,r=Math.sqrt(w.random.nextDouble())*KatyushaRadius;
            int impact=w.tick+KatyushaDelayTicks+Math.round(i*KatyushaRainTicks/8f);
            cast.zones.add(effect(ROCKET,t.slot,impact-15,impact,t.aimX+(float)(Math.cos(a)*r),t.aimY+(float)(Math.sin(a)*r),RocketRadius,0));
        }
        casts.add(cast);
    }
    void tick() {
        effects.removeIf(e->w.tick>e.end+24);
        for(World.Tank t:new ArrayList<>(w.tanks.values()))if(t.hp>0&&t.qStage==1&&w.tick>=t.qEndTick) {
            if(t.nation==1)lock(t);
            else if(t.nation==6) {
                t.qStage=0;
                effect(KAMIKAZE,t.slot,w.tick,w.tick,t.x,t.y,KamikazeRadius,0);
                breakCircle(t.x,t.y,KamikazeRadius);
                for(World.Tank o:w.tanks.values())if(o!=t&&o.hp>0&&circle(o,t.x,t.y,KamikazeRadius))w.damage(o,t.slot,o.hp);
                w.damage(t,t.slot,t.hp);
            }
            else if(t.nation==5) {
                t.qStage=0;
                double a=Math.toRadians(t.turret/100.0);float x=t.x+(float)Math.cos(a)*1.05f,y=t.y+(float)Math.sin(a)*1.05f;
                effect(BEAM,t.slot,w.tick,w.tick,x,y,BeamWidth,t.turret);
                for(GameMap.Rect r:w.map.bricks)if(w.bricks[r.id]>0&&beamRect(r.x+r.w/2,r.y+r.h/2,r.w/2,r.h/2,0,x,y,t.turret))w.bricks[r.id]=0;
                for(World.Tank o:w.tanks.values())if(o!=t&&o.hp>0&&beam(o,x,y,t.turret))w.damage(o,t.slot,o.hp);
            }
        }
        for(Iterator<Cast> it=casts.iterator();it.hasNext();) {
            Cast cast=it.next();
            for(Iterator<Effect> zones=cast.zones.iterator();zones.hasNext();) {
                Effect e=zones.next();if(w.tick<e.end)continue;
                breakCircle(e.x,e.y,e.radius);
                for(World.Tank t:w.tanks.values())if(t.hp>0&&circle(t,e.x,e.y,e.radius)) {
                    if(cast.kind==BOMB) {
                        if(t.slot==cast.owner||!Objects.equals(cast.lives.get(t.slot),t.life)||cast.damage.containsKey(t.slot))continue;
                        cast.damage.put(t.slot,BombDamage);
                        t.slowEndTick=Math.max(t.slowEndTick,w.tick+SlowDurationTicks);
                        w.damage(t,cast.owner,BombDamage);
                    }
                    else {
                        int damage=Math.min(RocketDamage,KatyushaDamageCap-cast.damage.getOrDefault(t.slot,0));
                        if(damage>0) {cast.damage.merge(t.slot,damage,Integer::sum);w.damage(t,cast.owner,damage);}
                    }
                }
                zones.remove();
            }
            if(cast.zones.isEmpty())it.remove();
        }
    }
    static boolean circle(World.Tank t,float x,float y,float radius) {
        double a=Math.toRadians(t.body/100.0),c=Math.cos(a),s=Math.sin(a),dx=x-t.x,dy=y-t.y;
        double lx=Math.max(0,Math.abs(dx*c+dy*s)-TankHalfSize),ly=Math.max(0,Math.abs(-dx*s+dy*c)-TankHalfSize);
        return lx*lx+ly*ly<=radius*radius+1e-6;
    }
    void breakCircle(float x,float y,float radius) {
        double reach=radius+(double)CollisionEpsilon;
        for(GameMap.Rect r:w.map.bricks)if(w.bricks[r.id]>0) {
            double dx=Math.max(r.x,Math.min(r.x+r.w,x))-x,dy=Math.max(r.y,Math.min(r.y+r.h,y))-y;
            if(dx*dx+dy*dy<=reach*reach)w.bricks[r.id]=0;
        }
    }
    boolean beam(World.Tank t,float x,float y,int angle) {
        return beamRect(t.x,t.y,TankHalfSize,TankHalfSize,t.body,x,y,angle);
    }
    boolean beamRect(float targetX,float targetY,float halfX,float halfY,int body,float x,float y,int angle) {
        double a=Math.toRadians(angle/100.0),c=Math.cos(a),s=Math.sin(a);
        double length=Double.POSITIVE_INFINITY;
        if(c>1e-9)length=Math.min(length,(w.map.width-x)/c);if(c< -1e-9)length=Math.min(length,-x/c);
        if(s>1e-9)length=Math.min(length,(w.map.height-y)/s);if(s< -1e-9)length=Math.min(length,-y/s);
        if(length<=0)return false;
        // Reuse continuous OBB SAT: moving point expanded by beam's perpendicular half width.
        double b=Math.toRadians(body/100.0),tc=Math.cos(b),ts=Math.sin(b),cx=x+c*length/2,cy=y+s*length/2;
        for(double[] axis:new double[][]{{c,s},{-s,c},{tc,ts},{-ts,tc}}) {
            double ax=axis[0],ay=axis[1];
            double r=length/2*Math.abs(ax*c+ay*s)+BeamWidth/2*Math.abs(-ax*s+ay*c)+halfX*Math.abs(ax*tc+ay*ts)+halfY*Math.abs(-ax*ts+ay*tc);
            if(Math.abs((targetX-cx)*ax+(targetY-cy)*ay)>r+CollisionEpsilon)return false;
        }
        return true;
    }
    byte[] encode() {Writer out=new Writer().u16(effects.size());for(Effect e:effects)e.write(out);return out.done();}
    void read(Reader r) {
        int count=r.u16();if(count>MaxSkillEffects)throw new IllegalArgumentException("effects");
        Set<Integer> ids=new HashSet<>();effects.clear();for(int i=0;i<count;i++) {Effect e=Effect.read(r);if(!ids.add(e.id))throw new IllegalArgumentException("duplicate effect");effects.add(e);}
    }
    void clear() {casts.clear();effects.clear();for(World.Tank t:w.tanks.values())w.clearEffects(t);w.skillIntents.clear();}
}
