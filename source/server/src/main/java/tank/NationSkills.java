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
            if(e.id<=0||e.kind<1||e.kind>11||e.owner>7||e.end<e.start||e.radius<0||!Float.isFinite(e.radius)||!Float.isFinite(e.x)||!Float.isFinite(e.y)||e.angle>=36000)throw new IllegalArgumentException("skill effect");
            return e;
        }
    }
    static final class Cast {
        int owner,kind,locked;
        Effect area;
        final List<Effect> zones=new ArrayList<>();
        final Map<Integer,Integer> lives=new HashMap<>(),damage=new HashMap<>();
        final Map<Integer,Integer> targets=new HashMap<>();
    }
    final World w;
    final List<Cast> casts=new ArrayList<>();
    final List<Effect> effects=new ArrayList<>();
    // Every cast carries its own identity, source and affected vehicle life.
    static final class Debuff {
        int id,kind,owner,slot,life,end;
        boolean cleansed;
    }
    static final int DEBUFF_KIND_COUNT=5;
    final List<Debuff> debuffs=new ArrayList<>();
    int nextDebuff=1;
    void debuff(World.Tank t,int kind,int owner,int end) {
        Debuff d=new Debuff();d.id=nextDebuff++;d.kind=kind;d.owner=owner;d.slot=t.slot;d.life=t.life;d.end=end;debuffs.add(d);refresh(t,kind);
    }
    void refresh(World.Tank t,int kind) {
        int end=w.tick,count=0;
        for(Debuff d:debuffs)if(d.slot==t.slot&&d.life==t.life&&d.kind==kind&&!d.cleansed) {end=Math.max(end,d.end);count++;}
        switch(kind) {case 0 -> t.confusionEndTick=end;case 1 -> t.fogEndTick=end;case 2 -> t.fireLockEndTick=end;case 3 -> t.slowEndTick=end;case 4 -> {t.rocketSlowEndTick=end;t.rocketSlowStacks=count;}}
    }
    void expireDebuffs() {
        Set<Integer> changed=new HashSet<>();
        for(Iterator<Debuff> it=debuffs.iterator();it.hasNext();) {
            Debuff d=it.next();World.Tank t=w.tanks.get(d.slot);
            if(w.tick>=d.end||t==null||t.hp<=0||t.life!=d.life) {if(t!=null&&t.life==d.life)changed.add(d.slot*DEBUFF_KIND_COUNT+d.kind);it.remove();}
        }
        for(int key:changed)refresh(w.tanks.get(key/DEBUFF_KIND_COUNT),key%DEBUFF_KIND_COUNT);
    }
    void clearDebuffs(World.Tank t) {debuffs.removeIf(d->d.slot==t.slot);}
    void purify(World.Tank t) {
        Set<Integer> changed=new HashSet<>();
        for(Debuff d:debuffs)if(d.slot==t.slot&&d.life==t.life&&d.owner!=t.slot&&!d.cleansed) {d.cleansed=true;changed.add(d.kind);}
        for(int kind:changed)refresh(t,kind);
        if(t.qPenalty>0) {t.qReadyTick=t.qBaseReadyTick;t.qPenalty=0;t.qPenaltySources.clear();}
    }
    void sabotage(World.Tank t,int owner) {
        if(owner==t.slot)return;
        if(t.qPenalty==0) {t.qBaseReadyTick=t.qReadyTick;t.qPenaltySources.clear();}
        int ready=Math.min(65535,Math.max(w.tick,t.qReadyTick)+SabotageCooldownPenaltyTicks);
        t.qPenalty=ready-t.qBaseReadyTick;t.qReadyTick=ready;t.qPenaltySources.add(owner);
    }
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
                    Effect zone=effect(BOMB,t.slot,w.tick,w.tick+BombDelayTicks,o.x,o.y,BombRadius,0);
                    cast.zones.add(zone);cast.targets.put(zone.id,o.slot);
                    debuff(o,3,t.slot,w.tick+SlowDurationTicks);
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
                for(World.Tank o:w.tanks.values())if(o!=t&&o.hp>0)debuff(o,0,t.slot,w.tick+ConfusionTicks);
                effect(RADAR,t.slot,w.tick,w.tick+24,t.x,t.y,0,0);
                t.qEndTick=w.tick+ConfusionTicks;
            }
            case 3 -> {
                int castId=nextDebuff;
                for(World.Tank o:w.tanks.values())if(o!=t&&o.hp>0) {debuff(o,1,t.slot,w.tick+FogEnemyTicks);debuffs.getLast().id=castId;}
                t.qEndTick=w.tick+FogEnemyTicks;
            }
            case 4 -> t.qEndTick=w.tick+150;
            case 5 -> {t.qStage=1;t.qEndTick=w.tick+BeamChargeTicks;}
            case 6 -> {t.qStage=1;t.qEndTick=w.tick+KamikazeTicks;}
            case 7 -> {
                for(World.Tank o:w.tanks.values())if(o!=t&&o.hp>0) {
                    debuff(o,2,t.slot,w.tick+FireLockTicks);
                    if(o.nation>=0)sabotage(o,t.slot);
                }
                effect(SABOTAGE,t.slot,w.tick,w.tick+24,t.x,t.y,0,0);
                t.qEndTick=w.tick+FireLockTicks;
            }
        }
        t.qReadyTick=w.tick+RoleplayContent.NationCooldowns[t.nation];
        t.qBaseReadyTick=t.qReadyTick;t.qPenalty=0;t.qPenaltySources.clear();
        effect(ACTIVATE,t.slot,w.tick,w.tick+15,t.x,t.y,0,t.nation);
        return Protocol.Error.OK;
    }
    void lock(World.Tank t) {
        // While aiming, qReadyTick can hold a sabotage penalty that must survive locking.
        if(t.qPenalty==0)t.qBaseReadyTick=t.qReadyTick;
        t.qStage=0;t.qBaseReadyTick=Math.min(65535,Math.max(w.tick,t.qBaseReadyTick)+RoleplayContent.NationCooldowns[1]);
        t.qReadyTick=Math.min(65535,Math.max(w.tick,t.qReadyTick)+RoleplayContent.NationCooldowns[1]);t.qPenalty=t.qReadyTick-t.qBaseReadyTick;t.qEndTick=w.tick+KatyushaDelayTicks+KatyushaRainTicks;
        if(t.qPenalty==0)t.qPenaltySources.clear();
        Cast cast=new Cast();cast.owner=t.slot;cast.kind=ROCKET;cast.locked=w.tick;
        cast.area=effect(AREA,t.slot,w.tick,t.qEndTick,t.aimX,t.aimY,KatyushaRadius,0);
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
                Effect cause=effect(KAMIKAZE,t.slot,w.tick,w.tick,t.x,t.y,KamikazeRadius,0);
                breakCircle(t.x,t.y,KamikazeRadius);
                for(World.Tank o:w.tanks.values())if(o!=t&&o.hp>0&&circle(o,t.x,t.y,KamikazeRadius))w.damage(o,t.slot,o.hp,1,0,KAMIKAZE,cause.id);
                w.damage(t,t.slot,t.hp,2,0,KAMIKAZE,cause.id);
            }
            else if(t.nation==5) {
                t.qStage=0;
                double a=Math.toRadians(t.turret/100.0);float x=t.x+(float)Math.cos(a)*GameRules.TankMuzzleOffset,y=t.y+(float)Math.sin(a)*GameRules.TankMuzzleOffset;
                Effect cause=effect(BEAM,t.slot,w.tick,w.tick,x,y,BeamWidth,t.turret);
                for(GameMap.Rect r:w.map.bricks)if(w.bricks[r.id]>0&&beamRect(r.x+r.w/2,r.y+r.h/2,r.w/2,r.h/2,0,x,y,t.turret))w.bricks[r.id]=0;
                for(World.Tank o:w.tanks.values())if(o!=t&&o.hp>0&&beam(o,x,y,t.turret))w.damage(o,t.slot,o.hp,1,0,BEAM,cause.id);
            }
        }
        for(Iterator<Cast> it=casts.iterator();it.hasNext();) {
            Cast cast=it.next();
            for(Iterator<Effect> zones=cast.zones.iterator();zones.hasNext();) {
                Effect e=zones.next();
                if(cast.kind==BOMB&&w.tick>cast.locked)trackBomb(cast,e);
                if(cast.kind==ROCKET&&w.tick>=e.start&&!cast.targets.containsKey(e.id))lockRocket(cast,e);
                if(w.tick<e.end)continue;
                breakCircle(e.x,e.y,e.radius);
                for(World.Tank t:w.tanks.values())if(t.hp>0&&circle(t,e.x,e.y,e.radius)) {
                    if(cast.kind==BOMB) {
                        if(t.slot==cast.owner||!Objects.equals(cast.lives.get(t.slot),t.life)||cast.damage.containsKey(t.slot))continue;
                        cast.damage.put(t.slot,BombDamage);
                        w.damage(t,cast.owner,BombDamage,0,0,BOMB,e.id);
                    }
                    else {
                        int damage=Math.min(RocketDamage,KatyushaDamageCap-cast.damage.getOrDefault(t.slot,0));
                        if(damage>0) {cast.damage.merge(t.slot,damage,Integer::sum);w.damage(t,cast.owner,damage,0,0,ROCKET,e.id);}
                        if(t.hp>0&&w.tick<cast.area.end)debuff(t,4,cast.owner,cast.area.end);
                    }
                }
                zones.remove();
            }
            if(cast.zones.isEmpty())it.remove();
        }
    }
    void lockRocket(Cast cast,Effect e) {
        List<World.Tank> targets=new ArrayList<>();
        for(World.Tank t:w.tanks.values())if(t.slot!=cast.owner&&t.hp>0&&circle(t,cast.area.x,cast.area.y,cast.area.radius))targets.add(t);
        if(targets.isEmpty()) {
            // Keep this rocket's random point inside the area, even if a tank enters later.
            cast.targets.put(e.id,-1);
        }
        else {
            World.Tank target=targets.get(w.random.nextInt(targets.size()));
            e.x=target.x;e.y=target.y;
            // The warning and falling rocket share one fixed point; never follow the tank.
            cast.targets.put(e.id,target.slot);
        }
    }
    void trackBomb(Cast cast,Effect e) {
        Integer slot=cast.targets.get(e.id);if(slot==null)return;
        World.Tank t=w.tanks.get(slot);
        if(t==null||t.hp<=0||!Objects.equals(cast.lives.get(slot),t.life)) {cast.targets.remove(e.id);return;}
        float dx=t.x-e.x,dy=t.y-e.y;double distance=Math.hypot(dx,dy);
        float step=BombTrackingSpeed/30;
        if(distance<=step) {e.x=t.x;e.y=t.y;}
        else {e.x+=dx/distance*step;e.y+=dy/distance*step;}
    }
    static int katyushaSlowEnd(World.Tank t,int tick,Iterable<Effect> effects) {
        int end=0;
        if(t.hp<=0)return end;
        for(Effect e:effects)if(e.kind==AREA&&tick>=e.start&&tick<e.end&&circle(t,e.x,e.y,e.radius))end=Math.max(end,e.end);
        return end;
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
        Set<Integer> ids=new HashSet<>();effects.clear();for(int i=0;i<count;i++) {Effect e=Effect.read(r);if(e.kind>8||!ids.add(e.id))throw new IllegalArgumentException("duplicate effect");effects.add(e);}
    }
    void clear() {casts.clear();effects.clear();debuffs.clear();w.items.clear();for(World.Tank t:w.tanks.values())w.clearEffects(t);w.skillIntents.clear();}
}
