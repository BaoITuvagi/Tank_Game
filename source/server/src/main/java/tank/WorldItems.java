package tank;
import java.util.*;
import static tank.GameRules.*;
import static tank.Protocol.*;

/** Match-owned pickups. The extension is repeated in deltas so a lost packet never replays an event. */
final class WorldItems {
    static final int MEDICAL=0,ARMOR=1,HE=2,PURIFICATION=3,INVINCIBILITY=4,MIRROR=5;
    static final int NORMAL_BULLET=0,HE_BULLET=1,MIRROR_BULLET=2,MIRROR_CHILD=3,HE_MIRROR_BULLET=4,HE_MIRROR_CHILD=5;
    static final int MIRROR_DAMAGE=(BulletDamage*MirrorDamagePercent+50)/100,GERMAN_MIRROR_DAMAGE=GermanBulletDamage+MIRROR_DAMAGE;
    static final int PICKUP=9,EXPLOSION=10,SPLIT=11;
    static final class Item {
        int id,type,expires;
        float x,y;
        void write(Writer out) {validate();out.u32(id).u8(type).f32(x).f32(y).u16(expires);}
        void validate() {
            if(id<=0||type<0||type>=ItemTypeCount||expires<0||expires>65535||!Float.isFinite(x)||!Float.isFinite(y))throw new IllegalArgumentException("item");
        }
        static Item read(Reader r) {Item i=new Item();i.id=r.u32();i.type=r.u8();i.x=r.f32();i.y=r.f32();i.expires=r.u16();i.validate();return i;}
    }
    final World w;
    final TreeMap<Integer,Item> items=new TreeMap<>();
    final List<NationSkills.Effect> effects=new ArrayList<>();
    final Map<Integer,List<float[]>> paths=new HashMap<>();
    int nextId=1,nextEffect=1;
    final int[] nextSpawn=new int[ItemTypeCount];
    final GameMap.Point[] previousSpawns=new GameMap.Point[ItemTypeCount];
    int matchTicks=5400,lastWave;
    boolean roleplay;
    WorldItems(World world) {w=world;configure(false);}
    void configure(boolean value) {
        configure(value,5400);
    }
    void configure(boolean value,int duration) {
        roleplay=value;matchTicks=duration;lastWave=0;Arrays.fill(nextSpawn,value?Integer.MAX_VALUE:ItemFirstSpawnTicks);
    }
    int interval() {return RoleplayItemIntervalTicks;}
    int window() {return RoleplayItemWindowTicks;}
    int wave() {
        int wave=w.tick/interval();
        return roleplay&&wave>0&&w.tick<matchTicks&&w.tick-wave*interval()<window()?wave:0;
    }
    float spawnRadius() {return Math.min(w.map.width,w.map.height)*RoleplayItemRadiusPercent/100f;}
    void spawnWave(int wave) {
        items.clear();
        List<Integer> types=new ArrayList<>();for(int type=0;type<ItemTypeCount;type++)types.add(type);
        Collections.shuffle(types,w.random);
        for(int n=0;n<RoleplayMaxItems;n++) {
            for(int attempt=0;attempt<512;attempt++) {
                double angle=w.random.nextDouble()*Math.PI*2,radius=Math.sqrt(w.random.nextDouble())*spawnRadius();
                GameMap.Point p=new GameMap.Point();p.x=w.map.width/2f+(float)(Math.cos(angle)*radius);p.y=w.map.height/2f+(float)(Math.sin(angle)*radius);
                if(!available(p,null))continue;
                Item i=place(types.get(n),p);i.expires=Math.min(matchTicks,wave*interval()+window());break;
            }
        }
    }
    void path(World.Tank t,float x,float y,float endX,float endY) {paths.computeIfAbsent(t.slot,key->new ArrayList<>()).add(new float[]{x,y,endX,endY});}
    void beginTick() {
        paths.clear();effects.removeIf(e->w.tick>e.end+24);
        boolean[] present=new boolean[ItemTypeCount];
        for(Iterator<Item> it=items.values().iterator();it.hasNext();) {
            Item i=it.next();
            if(i.expires>0&&w.tick>=i.expires) {scheduleRespawn(i);it.remove();}
            else present[i.type]=true;
        }
        if(roleplay) {
            int wave=wave();
            if(wave>0&&wave!=lastWave) {lastWave=wave;spawnWave(wave);}
            return;
        }
        if(w.map.itemSpawns==null)return;
        for(int type=0;type<ItemTypeCount;type++) {
            if(present[type]||w.tick<nextSpawn[type]||items.size()>=MaxItems)continue;
            spawn(type,previousSpawns[type]);
        }
    }
    void scheduleRespawn(Item i) {
        if(roleplay)return;
        GameMap.Point p=new GameMap.Point();p.x=i.x;p.y=i.y;
        nextSpawn[i.type]=w.tick+ItemRespawnTicks;previousSpawns[i.type]=p;
    }
    Item spawn(int type,GameMap.Point previous) {
        List<GameMap.Point> candidates=new ArrayList<>();
        for(GameMap.Point p:w.map.itemSpawns) {
            if(available(p,previous))candidates.add(p);
        }
        if(candidates.isEmpty())return null;
        GameMap.Point p=candidates.get(w.random.nextInt(candidates.size()));
        return place(type,p);
    }
    boolean available(GameMap.Point p,GameMap.Point previous) {
        if(w.map.blocked(p.x,p.y,w.bricks)||w.map.water(p.x,p.y)||previous!=null&&p.x==previous.x&&p.y==previous.y)return false;
        for(World.Tank t:w.tanks.values())if(t.hp>0&&Math.hypot(t.x-p.x,t.y-p.y)<ItemTankClearance)return false;
        for(Item i:items.values())if(Math.abs(i.x-p.x)<2*ItemHalfSize&&Math.abs(i.y-p.y)<2*ItemHalfSize)return false;
        return true;
    }
    Item place(int type,GameMap.Point p) {
        Item i=new Item();i.id=nextId++;i.type=type;i.x=p.x;i.y=p.y;items.put(i.id,i);return i;
    }
    boolean touches(World.Tank t,Item i) {
        if(NationSkills.circle(t,i.x,i.y,ItemHalfSize))return true;
        for(float[] p:paths.getOrDefault(t.slot,List.of())) {
            World.Tank start=t.copy();start.x=p[0];start.y=p[1];
            if(World.dashContact(start,p[2]-p[0],p[3]-p[1],i.x,i.y,ItemHalfSize,ItemHalfSize,0)<1)return true;
        }
        return false;
    }
    void pickup() {
        for(Iterator<Item> it=items.values().iterator();it.hasNext();) {
            Item i=it.next();World.Tank winner=null;double distance=Double.POSITIVE_INFINITY;
            for(World.Tank t:w.tanks.values())if(t.hp>0&&touches(t,i)) {
                double d=Math.hypot(t.x-i.x,t.y-i.y);
                if(d<distance) {winner=t;distance=d;}
            }
            if(winner!=null) {apply(winner,i.type);scheduleRespawn(i);it.remove();}
        }
    }
    void apply(World.Tank t,int type) {
        if(t.hp<=0)return;
        switch(type) {
            case MEDICAL -> t.hp=Math.min(t.maxHp,t.hp+(t.maxHp*MedicalHealPercent+99)/100);
            case ARMOR -> t.armorEndTick=w.tick+ArmorTicks;
            case HE -> t.heEndTick=w.tick+HighExplosiveTicks;
            case PURIFICATION -> w.skills.purify(t);
            case INVINCIBILITY -> t.invincibilityEndTick=w.tick+InvincibilityTicks;
            case MIRROR -> t.mirrorShots=MirrorShots;
            default -> throw new IllegalArgumentException("item type");
        }
        event(PICKUP,t.slot,t.x,t.y,1.5f,type);
    }
    void event(int kind,int owner,float x,float y,float radius,int type) {
        event(nextEffect++,kind,owner,x,y,radius,type);
    }
    void event(int id,int kind,int owner,float x,float y,float radius,int type) {
        if(effects.size()>=MaxItemEffects)effects.remove(0);
        NationSkills.Effect e=new NationSkills.Effect();e.id=id;e.kind=kind;e.owner=owner;e.start=e.end=w.tick;e.x=x;e.y=y;e.radius=radius;e.angle=type;effects.add(e);
    }
    int reservedBullets() {
        int count=w.bullets.size();for(World.Bullet b:w.bullets.values())if(isMirrorParent(b.kind))count+=2;return count;
    }
    static boolean hasHe(int kind) {return kind==HE_BULLET||kind==HE_MIRROR_BULLET||kind==HE_MIRROR_CHILD;}
    static boolean isMirrorParent(int kind) {return kind==MIRROR_BULLET||kind==HE_MIRROR_BULLET;}
    static boolean isMirrorChild(int kind) {return kind==MIRROR_CHILD||kind==HE_MIRROR_CHILD;}
    static boolean validDamage(int kind,int damage) {
        if(kind<NORMAL_BULLET||kind>HE_MIRROR_CHILD)return false;
        return isMirrorChild(kind)?damage==MIRROR_DAMAGE||damage==GERMAN_MIRROR_DAMAGE:damage==BulletDamage||damage==GermanBulletDamage;
    }
    static float size(World.Bullet b) {return (hasHe(b.kind)?HighExplosiveSizePercent/100f:1)*(isMirrorChild(b.kind)?MirrorSizePercent/100f:1);}
    void explode(World.Bullet b,int nx,int ny,GameMap.Rect wall) {
        explode(b,nx,ny,wall,nextEffect++);
    }
    void explode(World.Bullet b,int nx,int ny,GameMap.Rect wall,int eventId) {
        // Offset towards the incoming side: an intact impact wall must still block the far side.
        float x=b.x+nx*.001f,y=b.y+ny*.001f;
        if(wall!=null) {
            if(nx<0)x=Math.min(x,wall.x-.001f);else if(nx>0)x=Math.max(x,wall.x+wall.w+.001f);
            if(ny<0)y=Math.min(y,wall.y-.001f);else if(ny>0)y=Math.max(y,wall.y+wall.h+.001f);
        }
        event(eventId,EXPLOSION,b.owner,x,y,HighExplosiveRadius,0);
        for(World.Tank t:w.tanks.values())if(t.hp>0&&NationSkills.circle(t,x,y,HighExplosiveRadius)&&w.visible(x,y,t.x,t.y))w.damage(t,b.owner,Math.round(BulletDamage*HighExplosiveDamagePercent/100f),0,0,EXPLOSION,eventId);
    }
    void split(World.Bullet parent,int heading,int nx,int ny) {
        w.bullets.remove(parent.id);
        event(SPLIT,parent.owner,parent.x,parent.y,1,heading);
        for(int offset:new int[]{0,-MirrorAngle,MirrorAngle}) {
            World.Bullet child=parent.copy();child.id=w.nextBullet++;child.kind=hasHe(parent.kind)?HE_MIRROR_CHILD:MIRROR_CHILD;child.bounces=1;child.heading=Math.floorMod(heading+offset,36000);child.damage=MIRROR_DAMAGE+(parent.damage==GermanBulletDamage?GermanBulletDamage:0);
            // Separate each child from the impacted surface using its actual extents.
            double a=Math.toRadians(child.heading/100.0);float size=size(child);
            float ex=(float)(Math.abs(Math.cos(a))*World.BULLET_HALF_LENGTH+Math.abs(Math.sin(a))*World.BULLET_HALF_WIDTH)*size;
            float ey=(float)(Math.abs(Math.sin(a))*World.BULLET_HALF_LENGTH+Math.abs(Math.cos(a))*World.BULLET_HALF_WIDTH)*size;
            child.x+=nx*(ex+.001f);child.y+=ny*(ey+.001f);w.bullets.put(child.id,child);
        }
    }
    void clear() {items.clear();effects.clear();paths.clear();roleplay=false;Arrays.fill(nextSpawn,Integer.MAX_VALUE);Arrays.fill(previousSpawns,null);}
    byte[] encode() {
        if(items.size()>MaxItems||effects.size()>MaxItemEffects)throw new IllegalArgumentException("item count");
        Writer out=new Writer().u8(w.tanks.size());
        for(World.Tank t:w.tanks.values()) {
            for(int end:new int[]{t.armorEndTick,t.heEndTick,t.invincibilityEndTick,t.qBaseReadyTick})if(end<0||end>65535)throw new IllegalArgumentException("item deadline");
            if(t.mirrorShots<0||t.mirrorShots>MirrorShots)throw new IllegalArgumentException("mirror shots");
            out.u8(t.slot).u16(t.armorEndTick).u16(t.heEndTick).u16(t.invincibilityEndTick).u8(t.mirrorShots).u16(t.qBaseReadyTick);
        }
        out.u8(items.size());for(Item i:items.values())i.write(out);
        out.u16(effects.size());for(NationSkills.Effect e:effects)e.write(out);
        return out.done();
    }
    void read(Reader r) {
        int n=r.u8();if(n!=w.tanks.size())throw new IllegalArgumentException("item tank count");
        Set<Integer> slots=new HashSet<>();
        for(int j=0;j<n;j++) {
            int slot=r.u8();World.Tank t=w.tanks.get(slot);if(t==null||!slots.add(slot))throw new IllegalArgumentException("item tank");
            t.armorEndTick=r.u16();t.heEndTick=r.u16();t.invincibilityEndTick=r.u16();t.mirrorShots=r.u8();t.qBaseReadyTick=r.u16();
            if(t.mirrorShots>MirrorShots)throw new IllegalArgumentException("mirror shots");
        }
        n=r.u8();if(n>MaxItems)throw new IllegalArgumentException("items");items.clear();
        for(int j=0;j<n;j++) {Item i=Item.read(r);if(items.put(i.id,i)!=null)throw new IllegalArgumentException("duplicate item");}
        n=r.u16();if(n>MaxItemEffects)throw new IllegalArgumentException("item effects");effects.clear();Set<Integer> ids=new HashSet<>();
        for(int j=0;j<n;j++) {NationSkills.Effect e=NationSkills.Effect.read(r);if(e.kind<PICKUP||e.kind>SPLIT||e.kind==PICKUP&&e.angle>=ItemTypeCount||!ids.add(e.id))throw new IllegalArgumentException("item effect");effects.add(e);}
    }
}
