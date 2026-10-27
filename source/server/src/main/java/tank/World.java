package tank;
import java.util.*;
import static tank.Protocol.*;
public class World  {
    public static final int BULLET_LIFETIME_TICKS=GameRules.BulletLifetimeTicks;
    // 5 seconds at 30 Hz.
    public static final int DEFAULT_HP=GameRules.DefaultHp,MAX_START_HP=GameRules.MaxStartHp,MAX_ENERGY=GameRules.MaxEnergy,ENERGY_RECOVERY_DELAY=GameRules.EnergyRecoveryDelay,SPAWN_SHIELD_TICKS=GameRules.SpawnShieldTicks;
    public static final float TANK_HALF_SIZE=GameRules.TankHalfSize,BULLET_HALF_LENGTH=GameRules.BulletHalfLength,BULLET_HALF_WIDTH=GameRules.BulletHalfWidth;
    public static final float TANK_SPEED=GameRules.TankSpeed,BULLET_STEP=GameRules.BulletStep,SPRINT_MULTIPLIER=GameRules.SprintMultiplier;
    public static class Tank  {
        public int slot,body,turret,hp=DEFAULT_HP,maxHp=DEFAULT_HP,energy=MAX_ENERGY,energyRecovery,kills,deaths,status=1,respawn,shield,lastKill=-1,cooldown;
        public int qReadyTick,qEndTick,slowEndTick,protectionFlags;
        public int rocketSlowEndTick,rocketSlowStacks;
        public int qStartTick,confusionEndTick,fireLockEndTick,fogEndTick,qStage,life;
        public int armorEndTick,heEndTick,invincibilityEndTick,mirrorShots,qBaseReadyTick;
        int qPenalty;
        final Set<Integer> qPenaltySources=new HashSet<>();
        public float aimX,aimY;
        // Reliable MATCH_LOADOUT supplies these; they do not occupy snapshot bytes.
        public int nation=-1;
        public int usQTick=-1,usSprintTicks,usDeathsAtQ;
        public int desiredAim,desiredButtons;
        public float x,y;
        public Tank copy() {
            Tank t=new Tank();
            t.slot=slot;
            t.body=body;
            t.turret=turret;
            t.hp=hp;
            t.maxHp=maxHp;
            t.energy=energy;
            t.energyRecovery=energyRecovery;
            t.kills=kills;
            t.deaths=deaths;
            t.status=status;
            t.respawn=respawn;
            t.shield=shield;
            t.lastKill=lastKill;
            t.cooldown=cooldown;
            t.qReadyTick=qReadyTick;
            t.qEndTick=qEndTick;
            t.slowEndTick=slowEndTick;
            t.rocketSlowEndTick=rocketSlowEndTick;t.rocketSlowStacks=rocketSlowStacks;
            t.protectionFlags=protectionFlags;
            t.nation=nation;
            t.qStartTick=qStartTick;t.confusionEndTick=confusionEndTick;t.fireLockEndTick=fireLockEndTick;t.fogEndTick=fogEndTick;t.qStage=qStage;t.life=life;t.aimX=aimX;t.aimY=aimY;
            t.armorEndTick=armorEndTick;t.heEndTick=heEndTick;t.invincibilityEndTick=invincibilityEndTick;t.mirrorShots=mirrorShots;t.qBaseReadyTick=qBaseReadyTick;t.qPenalty=qPenalty;
            t.qPenaltySources.addAll(qPenaltySources);
            t.usQTick=usQTick;t.usSprintTicks=usSprintTicks;t.usDeathsAtQ=usDeathsAtQ;
            t.desiredAim=desiredAim;t.desiredButtons=desiredButtons;
            t.x=x;
            t.y=y;
            return t;
        }
    }
    public static class Bullet  {
        public int id,owner,heading,bounces,born;
        public int speedPercent=100,damage=GameRules.BulletDamage;
        public int kind;
        public int remainingLifetime=BULLET_LIFETIME_TICKS;
        public float x,y;
        public Bullet copy() {
            Bullet b=new Bullet();
            b.id=id;
            b.owner=owner;
            b.heading=heading;
            b.bounces=bounces;
            b.born=born;
            b.speedPercent=speedPercent;b.damage=damage;
            b.kind=kind;
            b.remainingLifetime=remainingLifetime;
            b.x=x;
            b.y=y;
            return b;
        }
    }
    public final TreeMap<Integer,Tank> tanks=new TreeMap<>();
    public final TreeMap<Integer,Bullet> bullets=new TreeMap<>();
    public byte[] bricks=new byte[64];
    public int tick,nextBullet=1,nextLife=1;
    final NationSkills skills=new NationSkills(this);
    final WorldItems items=new WorldItems(this);
    final CombatEvents combat=new CombatEvents();
    final WorldBattlefield battlefield;
    final byte[] combatBrickHp=new byte[64];
    public long contactsExceeded,bulletCap;
    final GameMap map;
    final Random random;
    final Map<Integer,Tank> previousTanks=new HashMap<>();
    final int startingHp;
    final ArrayDeque<Runnable> skillIntents=new ArrayDeque<>();
    java.util.function.Consumer<String> telemetry=message->{};
    public World(GameMap m,long seed) {
        this(m,seed,DEFAULT_HP);
    }
    public World(GameMap m,long seed,int hp) {
        if(hp<1||hp>MAX_START_HP)throw new IllegalArgumentException("starting HP");
        map=m;
        random=new Random(seed);
        battlefield=new WorldBattlefield(this,seed);
        startingHp=hp;
        if(m!=null)for(var r:m.bricks)bricks[r.id]=(byte)GameRules.BrickMaxHp;
    }
    public record Input(int tick,int buttons,int angle) {
        public Input {
            if((buttons&~63)!=0||angle<0||angle>=36000)throw new IllegalArgumentException("input");
        }
        public void write(Writer w) {
            w.u32(tick).u8(buttons).u16(angle);
        }
    }
    public void add(int slot) {
        Tank t=new Tank();
        t.slot=slot;
        tanks.put(slot,t);
        spawn(t);
    }
    public void remove(int slot) {
        tanks.remove(slot);
        bullets.values().removeIf(b->b.owner==slot);
    }
    // Match-only layout: choose evenly spaced perimeter targets, then shuffle their owners.
    // Respawns keep the existing farthest-safe-point policy.
    String arrangeInitialSpawns(String previousLayout) {
        List<GameMap.Point> candidates=new ArrayList<>();
        for(var p:map.spawns)if(!map.blocked(p.x,p.y,bricks))candidates.add(p);
        if(candidates.size()<tanks.size())for(float py=3;py<map.height-3;py+=3)for(float px=3;px<map.width-3;px+=3) {
            if(map.blocked(px,py,bricks))continue;
            GameMap.Point p=new GameMap.Point();p.x=px;p.y=py;candidates.add(p);
        }
        List<GameMap.Point> best=null;
        double bestScore=-Double.MAX_VALUE,phase=random.nextDouble()*Math.PI*2;
        int count=tanks.size();
        for(int trial=0;trial<32;trial++) {
            List<GameMap.Point> chosen=new ArrayList<>();double error=0,minDistance=Double.MAX_VALUE;
            for(int i=0;i<count;i++) {
                double a=phase+trial*Math.PI*2/(32*count)+i*Math.PI*2/count,c=Math.cos(a),s=Math.sin(a),edge=Math.max(Math.abs(c),Math.abs(s));
                double tx=map.width/2+map.width*.425*c/edge,ty=map.height/2+map.height*.425*s/edge;
                GameMap.Point nearest=null;double distance=Double.MAX_VALUE;
                for(var p:candidates) {
                    boolean overlaps=false;
                    for(var q:chosen)if(Math.hypot(p.x-q.x,p.y-q.y)<2*TANK_HALF_SIZE*Math.sqrt(2)) {overlaps=true;break;}
                    if(overlaps)continue;
                    double d=Math.hypot(p.x-tx,p.y-ty);
                    if(d<distance) {distance=d;nearest=p;}
                }
                if(nearest==null)break;
                for(var p:chosen)minDistance=Math.min(minDistance,Math.hypot(p.x-nearest.x,p.y-nearest.y));
                chosen.add(nearest);error+=distance;
            }
            double score=minDistance-error/count;
            if(chosen.size()==count&&score>bestScore) {best=chosen;bestScore=score;}
        }
        if(best==null)throw new IllegalStateException("no initial spawn layout");
        Collections.shuffle(best,random);
        String layout=best.stream().map(p->p.x+","+p.y).collect(java.util.stream.Collectors.joining(";"));
        // Small rooms have few optimal layouts; avoid repeating the last assignment by chance.
        if(layout.equals(previousLayout)&&best.size()>1)Collections.rotate(best,1);
        int i=0;for(Tank t:tanks.values()) {GameMap.Point p=best.get(i++);t.x=t.aimX=p.x;t.y=t.aimY=p.y;}
        return best.stream().map(p->p.x+","+p.y).collect(java.util.stream.Collectors.joining(";"));
    }
    void reset(Tank t,float px,float py) {
        clearEffects(t);
        t.x=px;
        t.y=py;
        t.hp=t.maxHp=startingHp;
        t.life=nextLife++;
        t.energy=MAX_ENERGY;
        t.energyRecovery=0;
        t.status=1;
        t.shield=tick+SPAWN_SHIELD_TICKS;
        t.cooldown=tick;
    }
    void spawn(Tank t) {
        List<GameMap.Point> points=new ArrayList<>(List.of(map.spawns));
        Collections.shuffle(points,random);
        GameMap.Point best=null;
        double bestDist=-1;
        for(var p:points) {
            if(map.blocked(p.x,p.y,t.body,bricks))continue;
            double min=Double.MAX_VALUE;
            for(var o:tanks.values())if(o!=t&&o.hp>0)min=Math.min(min,Math.hypot(p.x-o.x,p.y-o.y));
            if(min>bestDist) {
                best=p;
                bestDist=min;
            }
        }
        if(best==null)throw new IllegalStateException("no spawn");
        reset(t,best.x,best.y);
    }
    public static int turn(int current,int desired) {
        return turn(current,desired,GameRules.BaseTurretTurn);
    }
    public static int turn(int current,int desired,int limit) {
        int diff=Math.floorMod(desired-current+18000,36000)-18000;
        return Math.floorMod(current+Math.max(-limit,Math.min(limit,diff)),36000);
    }
    public static void movement(Tank t,Input in,GameMap m,byte[] hp) {
        movement(t,in,m,hp,in.tick);
    }
    public static void movement(Tank t,Input in,GameMap m,byte[] hp,int simulationTick) {
        movement(t,in,m,hp,simulationTick,List.of());
    }
    public static void movement(Tank t,Input in,GameMap m,byte[] hp,int simulationTick,Iterable<NationSkills.Effect> effects) {
        if(t.hp==0)return;
        int dx=((in.buttons&8)!=0?1:0)-((in.buttons&4)!=0?1:0),dy=((in.buttons&1)!=0?1:0)-((in.buttons&2)!=0?1:0);
        boolean held=(in.buttons&32)!=0,sprinting=held&&(dx!=0||dy!=0)&&t.energy>0;
        if(simulationTick<t.confusionEndTick) {dx=-dx;dy=-dy;}
        if(held) {
            t.energyRecovery=ENERGY_RECOVERY_DELAY;
            if(sprinting)t.energy--;
        }
        else if(t.energyRecovery>0)t.energyRecovery--;
        else t.energy=Math.min(MAX_ENERGY,t.energy+1);
        float norm=dx!=0&&dy!=0?(float)(1/Math.sqrt(2)):1;
        float multiplier=sprinting?SPRINT_MULTIPLIER:1;
        multiplier=Math.min(GameRules.MaxTankSpeedPercent/100f,multiplier);
        float water=m.water(t.x,t.y)?.5f:1;
        float slow=simulationTick<t.slowEndTick?GameRules.SlowSpeedPercent/100f:1;
        if(NationSkills.katyushaSlowEnd(t,simulationTick,effects)>simulationTick)slow*=GameRules.KatyushaSlowSpeedPercent/100f;
        if(simulationTick<t.rocketSlowEndTick)for(int hit=0;hit<t.rocketSlowStacks;hit++)slow*=GameRules.RocketSlowSpeedPercent/100f;
        float speed=TANK_SPEED*water*multiplier*slow/30;
        float x=t.x+dx*norm*speed,y=t.y+dy*norm*speed;
        int desired=dx!=0||dy!=0?Math.floorMod((int)Math.round(Math.toDegrees(Math.atan2(dy,dx))*100),36000):t.body;
        int movingBody=m.blocked(t.x,t.y,desired,hp)?t.body:desired;
        if(!m.blocked(x,t.y,movingBody,hp))t.x=x;
        if(!m.blocked(t.x,y,movingBody,hp))t.y=y;
        t.body=m.blocked(t.x,t.y,desired,hp)?movingBody:desired;
        if(simulationTick>=t.confusionEndTick)t.turret=turn(t.turret,in.angle);
    }
    public void step(Map<Integer,Input> inputs) {
        tick++;
        combat.prune(tick);
        System.arraycopy(bricks,0,combatBrickHp,0,64);
        battlefield.beginTick();
        items.beginTick();
        skills.expireDebuffs();
        for(Tank t:tanks.values()) {Input input=inputs.get(t.slot);t.desiredAim=input==null?t.turret:input.angle;t.desiredButtons=input==null?0:input.buttons;}
        Runnable intent;
        while((intent=skillIntents.poll())!=null)intent.run();
        for(Tank t:tanks.values()) {
            if(tick>=t.qEndTick)t.protectionFlags&=~1;
        }
        previousTanks.clear();
        for(Tank t:tanks.values())if(t.hp>0)previousTanks.put(t.slot,t.copy());
        for(Tank t:tanks.values()) {
            if(t.hp==0) {
                if(tick>=t.respawn)spawn(t);
                continue;
            }
            float oldX=t.x,oldY=t.y;
            movement(t,inputs.getOrDefault(t.slot,new Input(tick,0,t.turret)),map,bricks,tick,skills.effects);
            items.path(t,oldX,oldY,t.x,oldY);items.path(t,t.x,oldY,t.x,t.y);
        }
        items.pickup();
        battlefield.damage();
        for(Tank t:tanks.values()) {
            if(t.hp<=0||!previousTanks.containsKey(t.slot))continue;
            Input in=inputs.get(t.slot);
            if(in!=null&&(in.buttons&16)!=0&&tick>=t.cooldown&&tick>=t.fireLockEndTick) {
                t.cooldown=tick+GameRules.BaseShotIntervalTicks;
                boolean barrage=t.nation==4&&tick<t.qEndTick;
                int count=barrage?3:1;
                boolean he=tick<t.heEndTick,mirror=t.mirrorShots>0;
                int kind=mirror?(he?WorldItems.HE_MIRROR_BULLET:WorldItems.MIRROR_BULLET):(he?WorldItems.HE_BULLET:WorldItems.NORMAL_BULLET);
                if(items.reservedBullets()+count*(mirror?3:1)>GameRules.MaxBullets) {bulletCap++;continue;}
                int shotBulletId=nextBullet;
                for(int shot=0;shot<count;shot++) {
                    Bullet b=new Bullet();b.id=nextBullet++;b.owner=t.slot;b.heading=Math.floorMod(t.turret+(shot==1?-1200:shot==2?1200:0),36000);
                    b.born=tick;b.damage=barrage?GameRules.GermanBulletDamage:GameRules.BulletDamage;
                    b.kind=kind;
                    double a=Math.toRadians(b.heading/100.0);
                    b.x=t.x+(float)Math.cos(a)*GameRules.BulletSpawnOffset;b.y=t.y+(float)Math.sin(a)*GameRules.BulletSpawnOffset;
                    bullets.put(b.id,b);
                    telemetry.accept("kind=shot slot="+t.slot+" nation="+t.nation+" tick="+tick+" damage="+b.damage+" speed="+b.speedPercent+" bounces="+GameRules.DefaultBulletBounceLimit+" lifetime="+BULLET_LIFETIME_TICKS);
                }
                if(mirror)t.mirrorShots--;
                double muzzle=Math.toRadians(t.turret/100.0);
                combat.add(tick,CombatEvents.SHOT,t,null,shotBulletId,65535,0,t.x+(float)Math.cos(muzzle)*GameRules.TankMuzzleOffset,t.y+(float)Math.sin(muzzle)*GameRules.TankMuzzleOffset,t.turret,0,0,0);
            }
        }
        skills.tick();
        for(Bullet b:new ArrayList<>(bullets.values())) {
            b.remainingLifetime=Math.max(0,BULLET_LIFETIME_TICKS-(tick-b.born));
            if(b.remainingLifetime==0) {
                bullets.remove(b.id);
                continue;
            }
            advance(b);
        }
        previousTanks.clear();
        if(map!=null)for(GameMap.Rect brick:map.bricks)if(combatBrickHp[brick.id]>0&&bricks[brick.id]==0)
            combat.add(tick,CombatEvents.BRICK_DESTROYED,null,null,0,brick.id,2,brick.x+brick.w/2,brick.y+brick.h/2,0,0,0,0);
    }
    record Hit(double time,int nx,int ny,GameMap.Rect wall,Tank tank,int key) {
    }
    static Hit sweep(float x,float y,float dx,float dy,float minX,float minY,float maxX,float maxY,GameMap.Rect wall,Tank tank,int key) {
        if(x>minX&&x<maxX&&y>minY&&y<maxY) {
            float left=x-minX,right=maxX-x,bottom=y-minY,top=maxY-y;
            float near=Math.min(Math.min(left,right),Math.min(bottom,top));
            return new Hit(0,left==near?-1:right==near?1:0,bottom==near?-1:top==near?1:0,wall,tank,key);
        }
        double tx1=Double.NEGATIVE_INFINITY,tx2=Double.POSITIVE_INFINITY,ty1=tx1,ty2=tx2;
        if(Math.abs(dx)<1e-12) {
            if(x<minX||x>maxX)return null;
        }
        else {
            tx1=(minX-x)/dx;
            tx2=(maxX-x)/dx;
            if(tx1>tx2) {
                double z=tx1;
                tx1=tx2;
                tx2=z;
            }
        }
        if(Math.abs(dy)<1e-12) {
            if(y<minY||y>maxY)return null;
        }
        else {
            ty1=(minY-y)/dy;
            ty2=(maxY-y)/dy;
            if(ty1>ty2) {
                double z=ty1;
                ty1=ty2;
                ty2=z;
            }
        }
        double enter=Math.max(tx1,ty1),exit=Math.min(tx2,ty2);
        if(enter>exit+1e-6||exit<0||enter>1||enter< -1e-6)return null;
        return new Hit(Math.max(0,enter),Math.abs(tx1-enter)<1e-6?(dx>0?-1:1):0,Math.abs(ty1-enter)<1e-6?(dy>0?-1:1):0,wall,tank,key);
    }
    static Hit choose(Hit a,Hit b) {
        if(b==null)return a;
        if(a==null||b.time<a.time-1e-6)return b;
        if(Math.abs(b.time-a.time)<=1e-6) {
            if(a.wall==null&&b.wall!=null)return b;
            if((a.wall==null)==(b.wall==null)&&b.key<a.key)return b;
        }
        return a;
    }
    // Continuous SAT between the rendered oriented rectangles. Relative motion catches
    // a moving target that crosses a projectile between simulation ticks.
    Hit tankHit(Bullet b,float dx,float dy,Tank t,float progress,float duration) {
        Tank previous=b.born==tick?null:previousTanks.get(t.slot);
        float moveX=previous==null?0:t.x-previous.x,moveY=previous==null?0:t.y-previous.y;
        float startX=t.x-moveX*(1-progress),startY=t.y-moveY*(1-progress);
        double body=Math.toRadians(t.body/100.0),heading=Math.toRadians(b.heading/100.0);
        double tx=Math.cos(body),ty=Math.sin(body),bx=Math.cos(heading),by=Math.sin(heading);
        double enter=0,exit=1;
        double[][] axes= {
            {
                tx,ty
            }, {
                -ty,tx
            }, {
                bx,by
            }, {
                -by,bx
            }
        };
        for(double[] axis:axes) {
            double ax=axis[0],ay=axis[1];
            double radius=TANK_HALF_SIZE*(Math.abs(ax*tx+ay*ty)+Math.abs(-ax*ty+ay*tx))+WorldItems.size(b)*(BULLET_HALF_LENGTH*Math.abs(ax*bx+ay*by)+BULLET_HALF_WIDTH*Math.abs(-ax*by+ay*bx));
            double center=(b.x-startX)*ax+(b.y-startY)*ay,velocity=(dx-moveX*duration)*ax+(dy-moveY*duration)*ay;
            if(Math.abs(velocity)<1e-12) {
                if(Math.abs(center)>radius+1e-6)return null;
                continue;
            }
            double a=(-radius-center)/velocity,z=(radius-center)/velocity;
            if(a>z) {
                double swap=a;
                a=z;
                z=swap;
            }
            enter=Math.max(enter,a);
            exit=Math.min(exit,z);
            if(enter>exit+1e-6)return null;
        }
        if(exit<0||enter>1)return null;
        return new Hit(Math.max(0,enter),0,0,null,t,t.slot);
    }
    void advance(Bullet b) {
        float step=BULLET_STEP*Math.min(GameRules.MaxBulletSpeedPercent,b.speedPercent)/100f,remaining=step;
        for(int contact=0;contact<4;contact++) {
            double angle=Math.toRadians(b.heading/100.0);
            float dx=(float)Math.cos(angle)*remaining,dy=(float)Math.sin(angle)*remaining;
            Hit h=null;
            float extentX=.08f,extentY=.08f;
            if(WorldItems.hasHe(b.kind)||WorldItems.isMirrorChild(b.kind)) {
                float size=WorldItems.size(b);
                extentX=(float)(Math.abs(Math.cos(angle))*BULLET_HALF_LENGTH+Math.abs(Math.sin(angle))*BULLET_HALF_WIDTH)*size;
                extentY=(float)(Math.abs(Math.sin(angle))*BULLET_HALF_LENGTH+Math.abs(Math.cos(angle))*BULLET_HALF_WIDTH)*size;
            }
            for(GameMap.Rect[] rs:List.of(map.walls,map.bricks))for(var r:rs) {
                if(r.kind==1&&bricks[r.id]==0)continue;
                h=choose(h,sweep(b.x,b.y,dx,dy,r.x-extentX,r.y-extentY,r.x+r.w+extentX,r.y+r.h+extentY,r,null,r.kind*100+r.id));
            }
            for(Tank t:tanks.values()) {
                if(t.hp==0||(t.slot==b.owner&&tick-b.born<6))continue;
                h=choose(h,tankHit(b,dx,dy,t,1-remaining/step,remaining/step));
            }
            if(h==null) {
                b.x+=dx;
                b.y+=dy;
                return;
            }
            b.x+=dx*h.time;
            b.y+=dy*h.time;
            int explosionId=WorldItems.hasHe(b.kind)?items.nextEffect++:0;
            if(h.tank!=null) {
                if(explosionId==0)contact(b,h,CombatEvents.IMPACT);
                damage(h.tank,b.owner,b.damage,0,b.id,explosionId==0?0:WorldItems.EXPLOSION,explosionId);
                if(WorldItems.hasHe(b.kind))items.explode(b,0,0,null,explosionId);
                bullets.remove(b.id);
                return;
            }
            if(h.wall.kind==1&&bricks[h.wall.id]>0)bricks[h.wall.id]--;
            if(WorldItems.hasHe(b.kind))items.explode(b,h.nx,h.ny,h.wall,explosionId);
            if(b.bounces>=GameRules.DefaultBulletBounceLimit) {
                if(explosionId==0)contact(b,h,CombatEvents.IMPACT);
                bullets.remove(b.id);
                return;
            }
            b.bounces++;
            if(explosionId==0&&!WorldItems.isMirrorParent(b.kind))contact(b,h,CombatEvents.BOUNCE);
            if(h.time==0) {
                if(h.nx<0)b.x=h.wall.x-extentX;
                else if(h.nx>0)b.x=h.wall.x+h.wall.w+extentX;
                if(h.ny<0)b.y=h.wall.y-extentY;
                else if(h.ny>0)b.y=h.wall.y+h.wall.h+extentY;
            }
            if(h.nx!=0)dx=-dx;
            if(h.ny!=0)dy=-dy;
            b.heading=Math.floorMod((int)Math.round(Math.toDegrees(Math.atan2(dy,dx))*100),36000);
            if(WorldItems.isMirrorParent(b.kind)) {items.split(b,b.heading,h.nx,h.ny);return;}
            b.x+=h.nx*.0001f;
            b.y+=h.ny*.0001f;
            remaining*=1-h.time;
            if(remaining<=1e-6)return;
        }
        contactsExceeded++;
        bullets.remove(b.id);
    }
    void contact(Bullet b,Hit h,int kind) {
        int angle=h.nx==0&&h.ny==0?0:Math.floorMod((int)Math.round(Math.toDegrees(Math.atan2(h.ny,h.nx))*100),36000);
        combat.add(tick,kind,tanks.get(b.owner),h.tank,b.id,h.wall!=null&&h.wall.kind==1?h.wall.id:65535,h.wall==null?0:h.wall.kind==1?2:1,b.x,b.y,angle,0,0,0);
    }
    void damage(Tank t,int owner) {damage(t,owner,GameRules.BulletDamage);}
    void damage(Tank t,int owner,int amount) {
        damage(t,owner,amount,0);
    }
    // 0 = ordinary damage, 1 = instant kill, 2 = mandatory sacrifice.
    void damage(Tank t,int owner,int amount,int kind) {
        damage(t,owner,amount,kind,0,0,0);
    }
    void damage(Tank t,int owner,int amount,int kind,int bulletId,int causeKind,int causeId) {
        if(t.hp<=0)return;
        if(kind!=2&&tick<t.invincibilityEndTick)return;
        if(kind==0&&tick<t.armorEndTick)amount=Math.round(amount*(100-GameRules.ArmorReductionPercent)/100f);
        int previousHp=t.hp;
        t.hp=Math.max(0,t.hp-Math.max(0,amount));
        if(t.hp<previousHp)combat.add(tick,CombatEvents.DAMAGE,tanks.get(owner),t,bulletId,65535,0,t.x,t.y,0,previousHp-t.hp,causeKind,causeId);
        if(t.hp==0) {
            combat.add(tick,CombatEvents.DEATH,tanks.get(owner),t,bulletId,65535,0,t.x,t.y,0,t.maxHp,causeKind,causeId);
            clearEffects(t);
            t.status=0;
            t.deaths++;
            t.respawn=tick+90;
            Tank killer=tanks.get(owner);
            if(killer!=null&&owner!=t.slot) {
                killer.kills++;
                killer.lastKill=tick;
            }
        }
    }
    void clearEffects(Tank t) {
        skills.clearDebuffs(t);
        t.armorEndTick=t.heEndTick=t.invincibilityEndTick=tick;t.mirrorShots=0;
        t.qEndTick=t.slowEndTick=tick;
        t.rocketSlowEndTick=tick;t.rocketSlowStacks=0;
        t.protectionFlags=0;
        t.qStage=0;t.qStartTick=tick;t.confusionEndTick=t.fireLockEndTick=t.fogEndTick=tick;
    }
    public Protocol.Error useSkill(int slot,int ability) {
        Tank t=tanks.get(slot);
        if(t==null||t.hp==0||t.nation<0||t.nation>7||ability!=0)return Protocol.Error.INVALID_STATE;
        return skills.use(t);
    }
    public void skillAim(int slot,float x,float y) {
        Tank t=tanks.get(slot);if(t!=null&&Float.isFinite(x)&&Float.isFinite(y))skills.aim(t,x,y);
    }
    boolean visible(float x,float y,float targetX,float targetY) {
        for(GameMap.Rect[] rectangles:List.of(map.walls,map.bricks))for(GameMap.Rect r:rectangles) {
            if(r.kind==1&&bricks[r.id]==0)continue;
            if(sweep(x,y,targetX-x,targetY-y,r.x,r.y,r.x+r.w,r.y+r.h,r,null,0)!=null)return false;
        }
        return true;
    }
    // Continuous SAT for the entire dash path, using the same oriented tank size.
    static double dashContact(Tank t,float dx,float dy,float cx,float cy,double halfX,double halfY,int body) {
        double a=Math.toRadians(t.body/100.0),b=Math.toRadians(body/100.0),ac=Math.cos(a),as=Math.sin(a),bc=Math.cos(b),bs=Math.sin(b);
        double enter=0,exit=1;
        for(double[] axis:new double[][]{{ac,as},{-as,ac},{bc,bs},{-bs,bc}}) {
            double ax=axis[0],ay=axis[1];
            double radius=TANK_HALF_SIZE*(Math.abs(ax*ac+ay*as)+Math.abs(-ax*as+ay*ac))+halfX*Math.abs(ax*bc+ay*bs)+halfY*Math.abs(-ax*bs+ay*bc)-GameRules.CollisionEpsilon;
            double center=(t.x-cx)*ax+(t.y-cy)*ay,velocity=dx*ax+dy*ay;
            if(Math.abs(velocity)<1e-12) {
                if(Math.abs(center)>=radius)return 1;
            }
            else {
                double from=(-radius-center)/velocity,to=(radius-center)/velocity;
                if(from>to) {double swap=from;from=to;to=swap;}
                enter=Math.max(enter,from);
                exit=Math.min(exit,to);
                if(enter>exit)return 1;
            }
        }
        return exit<0||enter>1?1:Math.max(0,enter);
    }
    boolean dash(Tank t) {
        float distance=GameRules.DashDistance*(map.water(t.x,t.y)?GameRules.DashWaterPercent/100f:1);
        double angle=Math.toRadians(t.body/100.0),c=Math.cos(angle),s=Math.sin(angle);
        float dx=(float)c*distance,dy=(float)s*distance;
        double fraction=1,extent=TANK_HALF_SIZE*(Math.abs(c)+Math.abs(s));
        if(dx>0)fraction=Math.min(fraction,(map.width-extent-t.x)/dx);
        if(dx<0)fraction=Math.min(fraction,(extent-t.x)/dx);
        if(dy>0)fraction=Math.min(fraction,(map.height-extent-t.y)/dy);
        if(dy<0)fraction=Math.min(fraction,(extent-t.y)/dy);
        for(GameMap.Rect[] rectangles:List.of(map.walls,map.bricks))for(GameMap.Rect r:rectangles) {
            if(r.kind==1&&bricks[r.id]==0)continue;
            fraction=Math.min(fraction,dashContact(t,dx,dy,r.x+r.w/2,r.y+r.h/2,r.w/2,r.h/2,0));
        }
        for(Tank other:tanks.values())if(other!=t&&other.hp>0)fraction=Math.min(fraction,dashContact(t,dx,dy,other.x,other.y,TANK_HALF_SIZE,TANK_HALF_SIZE,other.body));
        double travel=Math.max(0,distance*fraction-(fraction<1?.0001:0));
        if(travel<=.00001)return false;
        items.path(t,t.x,t.y,t.x+(float)c*(float)travel,t.y+(float)s*(float)travel);
        t.x+=(float)c*(float)travel;
        t.y+=(float)s*(float)travel;
        return true;
    }
    public static void writeTank(Writer w,Tank t) {
        validate(t);
        w.u8(t.slot).f32(t.x).f32(t.y).u16(t.body).u16(t.turret).u16(t.hp).u16(t.kills).u16(t.deaths).u8(t.status).u32(t.respawn).u32(t.shield).u16(t.maxHp).u8(t.energy).u8(t.energyRecovery).u32(t.lastKill).u16(t.qReadyTick).u16(t.qEndTick).u16(t.slowEndTick).u8(t.protectionFlags).u16(t.qStartTick).u16(t.confusionEndTick).u16(t.fireLockEndTick).u16(t.fogEndTick).u8(t.qStage).f32(t.aimX).f32(t.aimY).u32(t.life).u16(t.rocketSlowEndTick).u8(t.rocketSlowStacks);
    }
    public static Tank readTank(Reader r) {
        Tank t=new Tank();
        t.slot=r.u8();
        t.x=r.f32();
        t.y=r.f32();
        t.body=r.u16();
        t.turret=r.u16();
        t.hp=r.u16();
        t.kills=r.u16();
        t.deaths=r.u16();
        t.status=r.u8();
        t.respawn=r.u32();
        t.shield=r.u32();
        t.maxHp=r.u16();
        t.energy=r.u8();
        t.energyRecovery=r.u8();
        t.lastKill=r.u32();
        t.qReadyTick=r.u16();
        t.qEndTick=r.u16();
        t.slowEndTick=r.u16();
        t.protectionFlags=r.u8();
        t.qStartTick=r.u16();t.confusionEndTick=r.u16();t.fireLockEndTick=r.u16();t.fogEndTick=r.u16();t.qStage=r.u8();t.aimX=r.f32();t.aimY=r.f32();t.life=r.u32();
        t.rocketSlowEndTick=r.u16();t.rocketSlowStacks=r.u8();
        validate(t);
        return t;
    }
    public static void validate(Tank t) {
        if(t.slot<0||t.slot>7||t.body<0||t.body>=36000||t.turret<0||t.turret>=36000||t.maxHp<1||t.maxHp>MAX_START_HP||t.hp<0||t.hp>t.maxHp||t.energy<0||t.energy>MAX_ENERGY||t.energyRecovery<0||t.energyRecovery>ENERGY_RECOVERY_DELAY||t.status<0||t.status>1||!Float.isFinite(t.x)||!Float.isFinite(t.y))throw new IllegalArgumentException("tank");
        for(int deadline:new int[]{t.qReadyTick,t.qEndTick,t.slowEndTick,t.qStartTick,t.confusionEndTick,t.fireLockEndTick,t.fogEndTick,t.rocketSlowEndTick})if(deadline<0||deadline>65535)throw new IllegalArgumentException("skill deadline");
        if(t.rocketSlowStacks<0||t.rocketSlowStacks>9)throw new IllegalArgumentException("rocket slow stacks");
        if(t.qStage<0||t.qStage>1||t.life<0||!Float.isFinite(t.aimX)||!Float.isFinite(t.aimY))throw new IllegalArgumentException("skill state");
        if((t.protectionFlags&~1)!=0)throw new IllegalArgumentException("protection flags");
    }
    public static void writeBullet(Writer w,Bullet b) {
        validate(b);
        w.u32(b.id).u8(b.owner).f32(b.x).f32(b.y).u16(b.heading).u8(b.bounces).u8(b.speedPercent).u16(b.remainingLifetime).u8(b.damage).u8(b.kind);
    }
    public static Bullet readBullet(Reader r) {
        Bullet b=new Bullet();
        b.id=r.u32();
        b.owner=r.u8();
        b.x=r.f32();
        b.y=r.f32();
        b.heading=r.u16();
        b.bounces=r.u8();
        b.speedPercent=r.u8();
        b.remainingLifetime=r.u16();b.damage=r.u8();b.kind=r.u8();
        validate(b);
        return b;
    }
    public static void validate(Bullet b) {
        if(!WorldItems.validDamage(b.kind,b.damage)||b.owner<0||b.owner>7||b.heading<0||b.heading>=36000||b.bounces<0||b.bounces>GameRules.DefaultBulletBounceLimit||b.speedPercent<100||b.speedPercent>GameRules.MaxBulletSpeedPercent||b.remainingLifetime<0||b.remainingLifetime>GameRules.BulletLifetimeTicks||!Float.isFinite(b.x)||!Float.isFinite(b.y))throw new IllegalArgumentException("bullet");
    }
    public byte[] encode() {
        if(tanks.size()>8||bullets.size()>GameRules.MaxBullets)throw new IllegalArgumentException("world count");
        Writer w=new Writer().u8(tanks.size());
        tanks.values().forEach(t->writeTank(w,t));
        w.u8(bullets.size());
        bullets.values().forEach(b->writeBullet(w,b));
        return w.bytes(bricks).bytes(skills.encode()).bytes(items.encode()).bytes(combat.encode()).bytes(battlefield.encode()).done();
    }
    public static World decode(byte[] data) {
        World s=new World(null,0);
        Reader r=new Reader(data);
        int n=r.u8();
        if(n>8)throw new IllegalArgumentException("tanks");
        for(int i=0;i<n;i++) {
            Tank t=readTank(r);
            if(s.tanks.put(t.slot,t)!=null)throw new IllegalArgumentException("duplicate tank");
        }
        n=r.u8();
        if(n>GameRules.MaxBullets)throw new IllegalArgumentException("bullets");
        for(int i=0;i<n;i++) {
            Bullet b=readBullet(r);
            if(s.bullets.put(b.id,b)!=null)throw new IllegalArgumentException("duplicate bullet");
        }
        s.bricks=r.bytes(64);
        for(byte b:s.bricks)if(b<0||b>GameRules.BrickMaxHp)throw new IllegalArgumentException("brick");
        s.skills.read(r);
        s.items.read(r);
        s.combat.read(r);
        s.battlefield.read(r);
        r.end();
        return s;
    }
}
