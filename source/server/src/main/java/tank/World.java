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
            t.x=x;
            t.y=y;
            return t;
        }
    }
    public static class Bullet  {
        public int id,owner,heading,bounces,born;
        public float x,y;
        public Bullet copy() {
            Bullet b=new Bullet();
            b.id=id;
            b.owner=owner;
            b.heading=heading;
            b.bounces=bounces;
            b.born=born;
            b.x=x;
            b.y=y;
            return b;
        }
    }
    public final TreeMap<Integer,Tank> tanks=new TreeMap<>();
    public final TreeMap<Integer,Bullet> bullets=new TreeMap<>();
    public byte[] bricks=new byte[64];
    public int tick,nextBullet=1;
    public long contactsExceeded,bulletCap;
    final GameMap map;
    final Random random;
    final Map<Integer,Tank> previousTanks=new HashMap<>();
    final int startingHp;
    public World(GameMap m,long seed) {
        this(m,seed,DEFAULT_HP);
    }
    public World(GameMap m,long seed,int hp) {
        if(hp<1||hp>MAX_START_HP)throw new IllegalArgumentException("starting HP");
        map=m;
        random=new Random(seed);
        startingHp=hp;
        if(m!=null)for(var r:m.bricks)bricks[r.id]=5;
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
        if(tick==0)spawnInitial(t);
        else spawn(t);
    }
    public void remove(int slot) {
        tanks.remove(slot);
        bullets.values().removeIf(b->b.owner==slot);
    }
    // Initial positions use four corners, then a second position near each corner for 5-8 players.
    // Respawns keep the existing farthest-safe-point policy.
    void spawnInitial(Tank t) {
        int index=tanks.size()-1;
        GameMap.Point corner=map.spawns[index%4];
        float px=corner.x,py=corner.y;
        if(index>=4)px+=px<map.width/2?12:-12;
        if(map.blocked(px,py,bricks))throw new IllegalStateException("blocked initial corner");
        reset(t,px,py);
    }
    void reset(Tank t,float px,float py) {
        t.x=px;
        t.y=py;
        t.hp=t.maxHp=startingHp;
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
        int diff=Math.floorMod(desired-current+18000,36000)-18000;
        return Math.floorMod(current+Math.max(-400,Math.min(400,diff)),36000);
    }
    public static void movement(Tank t,Input in,GameMap m,byte[] hp) {
        if(t.hp==0)return;
        int dx=((in.buttons&8)!=0?1:0)-((in.buttons&4)!=0?1:0),dy=((in.buttons&1)!=0?1:0)-((in.buttons&2)!=0?1:0);
        boolean held=(in.buttons&32)!=0,sprinting=held&&(dx!=0||dy!=0)&&t.energy>0;
        if(held) {
            t.energyRecovery=ENERGY_RECOVERY_DELAY;
            if(sprinting)t.energy--;
        }
        else if(t.energyRecovery>0)t.energyRecovery--;
        else t.energy=Math.min(MAX_ENERGY,t.energy+1);
        float norm=dx!=0&&dy!=0?(float)(1/Math.sqrt(2)):1;
        float speed=TANK_SPEED*(m.water(t.x,t.y)?.5f:1)*(sprinting?SPRINT_MULTIPLIER:1)/30;
        float x=t.x+dx*norm*speed,y=t.y+dy*norm*speed;
        int desired=dx!=0||dy!=0?Math.floorMod((int)Math.round(Math.toDegrees(Math.atan2(dy,dx))*100),36000):t.body;
        int movingBody=m.blocked(t.x,t.y,desired,hp)?t.body:desired;
        if(!m.blocked(x,t.y,movingBody,hp))t.x=x;
        if(!m.blocked(t.x,y,movingBody,hp))t.y=y;
        t.body=m.blocked(t.x,t.y,desired,hp)?movingBody:desired;
        t.turret=turn(t.turret,in.angle);
    }
    public void step(Map<Integer,Input> inputs) {
        tick++;
        previousTanks.clear();
        for(Tank t:tanks.values())if(t.hp>0)previousTanks.put(t.slot,t.copy());
        for(Tank t:tanks.values()) {
            if(t.hp==0) {
                if(tick>=t.respawn)spawn(t);
                continue;
            }
            movement(t,inputs.getOrDefault(t.slot,new Input(tick,0,t.turret)),map,bricks);
            Input in=inputs.get(t.slot);
            if(in!=null&&(in.buttons&16)!=0&&tick>=t.cooldown) {
                t.cooldown=tick+30;
                if(bullets.size()>=32) {
                    bulletCap++;
                    continue;
                }
                Bullet b=new Bullet();
                b.id=nextBullet++;
                b.owner=t.slot;
                b.heading=t.turret;
                b.born=tick;
                double a=Math.toRadians(b.heading/100.0);
                b.x=t.x+(float)Math.cos(a)*.6f;
                b.y=t.y+(float)Math.sin(a)*.6f;
                bullets.put(b.id,b);
            }
        }
        for(Bullet b:new ArrayList<>(bullets.values())) {
            if(tick-b.born>=BULLET_LIFETIME_TICKS) {
                bullets.remove(b.id);
                continue;
            }
            advance(b);
        }
        previousTanks.clear();
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
            double radius=TANK_HALF_SIZE*(Math.abs(ax*tx+ay*ty)+Math.abs(-ax*ty+ay*tx))+BULLET_HALF_LENGTH*Math.abs(ax*bx+ay*by)+BULLET_HALF_WIDTH*Math.abs(-ax*by+ay*bx);
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
        float remaining=BULLET_STEP;
        for(int contact=0;contact<4;contact++) {
            double angle=Math.toRadians(b.heading/100.0);
            float dx=(float)Math.cos(angle)*remaining,dy=(float)Math.sin(angle)*remaining;
            Hit h=null;
            for(GameMap.Rect[] rs:List.of(map.walls,map.bricks))for(var r:rs) {
                if(r.kind==1&&bricks[r.id]==0)continue;
                h=choose(h,sweep(b.x,b.y,dx,dy,r.x-.08f,r.y-.08f,r.x+r.w+.08f,r.y+r.h+.08f,r,null,r.kind*100+r.id));
            }
            for(Tank t:tanks.values()) {
                if(t.hp==0||(t.slot==b.owner&&tick-b.born<6))continue;
                h=choose(h,tankHit(b,dx,dy,t,1-remaining/BULLET_STEP,remaining/BULLET_STEP));
            }
            if(h==null) {
                b.x+=dx;
                b.y+=dy;
                return;
            }
            b.x+=dx*h.time;
            b.y+=dy*h.time;
            if(h.tank!=null) {
                damage(h.tank,b.owner);
                bullets.remove(b.id);
                return;
            }
            if(h.wall.kind==1&&bricks[h.wall.id]>0)bricks[h.wall.id]--;
            if(b.bounces>=2) {
                bullets.remove(b.id);
                return;
            }
            b.bounces++;
            if(h.time==0) {
                if(h.nx<0)b.x=h.wall.x-.08f;
                else if(h.nx>0)b.x=h.wall.x+h.wall.w+.08f;
                if(h.ny<0)b.y=h.wall.y-.08f;
                else if(h.ny>0)b.y=h.wall.y+h.wall.h+.08f;
            }
            if(h.nx!=0)dx=-dx;
            if(h.ny!=0)dy=-dy;
            b.heading=Math.floorMod((int)Math.round(Math.toDegrees(Math.atan2(dy,dx))*100),36000);
            b.x+=h.nx*.0001f;
            b.y+=h.ny*.0001f;
            remaining*=1-h.time;
            if(remaining<=1e-6)return;
        }
        contactsExceeded++;
        bullets.remove(b.id);
    }
    void damage(Tank t,int owner) {
        if(t.hp<=0)return;
        if(--t.hp==0) {
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
    public static void writeTank(Writer w,Tank t) {
        w.u8(t.slot).f32(t.x).f32(t.y).u16(t.body).u16(t.turret).u8(t.hp).u16(t.kills).u16(t.deaths).u8(t.status).u32(t.respawn).u32(t.shield).u8(t.maxHp).u8(t.energy).u8(t.energyRecovery).u32(t.lastKill);
    }
    public static Tank readTank(Reader r) {
        Tank t=new Tank();
        t.slot=r.u8();
        t.x=r.f32();
        t.y=r.f32();
        t.body=r.u16();
        t.turret=r.u16();
        t.hp=r.u8();
        t.kills=r.u16();
        t.deaths=r.u16();
        t.status=r.u8();
        t.respawn=r.u32();
        t.shield=r.u32();
        t.maxHp=r.u8();
        t.energy=r.u8();
        t.energyRecovery=r.u8();
        t.lastKill=r.u32();
        if(t.slot>7||t.body>=36000||t.turret>=36000||t.maxHp<1||t.maxHp>MAX_START_HP||t.hp>t.maxHp||t.energy>MAX_ENERGY||t.energyRecovery>ENERGY_RECOVERY_DELAY||t.status>1)throw new IllegalArgumentException("tank");
        return t;
    }
    public static void writeBullet(Writer w,Bullet b) {
        w.u32(b.id).u8(b.owner).f32(b.x).f32(b.y).u16(b.heading).u8(b.bounces);
    }
    public static Bullet readBullet(Reader r) {
        Bullet b=new Bullet();
        b.id=r.u32();
        b.owner=r.u8();
        b.x=r.f32();
        b.y=r.f32();
        b.heading=r.u16();
        b.bounces=r.u8();
        if(b.owner>7||b.heading>=36000||b.bounces>2)throw new IllegalArgumentException("bullet");
        return b;
    }
    public byte[] encode() {
        Writer w=new Writer().u8(tanks.size());
        tanks.values().forEach(t->writeTank(w,t));
        w.u8(bullets.size());
        bullets.values().forEach(b->writeBullet(w,b));
        return w.bytes(bricks).done();
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
        if(n>32)throw new IllegalArgumentException("bullets");
        for(int i=0;i<n;i++) {
            Bullet b=readBullet(r);
            if(s.bullets.put(b.id,b)!=null)throw new IllegalArgumentException("duplicate bullet");
        }
        s.bricks=r.bytes(64);
        for(byte b:s.bricks)if(b<0||b>5)throw new IllegalArgumentException("brick");
        r.end();
        return s;
    }
}
