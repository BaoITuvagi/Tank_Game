package tank;
import com.google.gson.Gson;
import java.nio.file.*;
import java.security.*;
import java.util.*;
public class GameMap  {
    public int mapId;
    public float width,height;
    public Rect[] walls,bricks,water;
    public Point[] spawns;
    public Point[] itemSpawns;
    public static class Point  {
        public float x,y;
    }
    public static class Rect  {
        public int id,kind;
        public float x,y,w,h;
        public boolean contains(float px,float py) {
            return px>=x&&px<x+w&&py>=y&&py<y+h;
        }
    }
    public transient byte[] hash;
    public static GameMap load(Path p)throws Exception  {
        byte[] raw=Files.readAllBytes(p);
        GameMap m=new Gson().fromJson(new String(raw,java.nio.charset.StandardCharsets.UTF_8),GameMap.class);
        m.hash=MessageDigest.getInstance("SHA-256").digest(raw);
        m.validate();
        return m;
    }
    public void validate() {
        if(!Float.isFinite(width+height)||width<40||height<40||width>128||height>128||bricks.length>64||spawns.length<2)throw new IllegalArgumentException("map limits");
        for(int i=0;i<bricks.length;i++)if(bricks[i].id!=i)throw new IllegalArgumentException("brick IDs");
        for(Rect[] rs:List.of(walls,bricks,water))for(Rect r:rs)if(!Float.isFinite(r.x+r.y+r.w+r.h)||r.w<=0||r.h<=0||r.x<0||r.y<0||r.x+r.w>width||r.y+r.h>height)throw new IllegalArgumentException("rectangle");
        byte[] hp=new byte[64];
        Arrays.fill(hp,(byte)GameRules.BrickMaxHp);
        for(Point s:spawns)if(!Float.isFinite(s.x+s.y)||s.x<.4||s.y<.4||s.x>width-.4||s.y>height-.4||blocked(s.x,s.y,hp))throw new IllegalArgumentException("spawn");
        if(itemSpawns==null||itemSpawns.length<3||itemSpawns.length>64)throw new IllegalArgumentException("item spawns");
        Set<String> points=new HashSet<>();
        for(Point p:itemSpawns) {
            if(!Float.isFinite(p.x+p.y)||blocked(p.x,p.y,hp)||water(p.x,p.y)||!points.add(p.x+":"+p.y))throw new IllegalArgumentException("item spawn");
            for(Rect r:water)if(overlap(p.x,p.y,r,GameRules.ItemHalfSize))throw new IllegalArgumentException("wet item spawn");
        }
    }
    public boolean blocked(float x,float y,byte[] hp) {
        return blocked(x,y,0,hp);
    }
    public boolean blocked(float x,float y,int body,byte[] hp) {
        double angle=Math.toRadians(body/100.0),c=Math.cos(angle),s=Math.sin(angle);
        double extent=GameRules.TankHalfSize*(Math.abs(c)+Math.abs(s));
        if(width>0&&height>0&&(x<extent-GameRules.CollisionEpsilon||y<extent-GameRules.CollisionEpsilon||x>width-extent+GameRules.CollisionEpsilon||y>height-extent+GameRules.CollisionEpsilon))return true;
        for(Rect r:walls)if(bodyOverlap(x,y,c,s,r))return true;
        for(Rect r:bricks)if(hp[r.id]>0&&bodyOverlap(x,y,c,s,r))return true;
        return false;
    }
    static boolean bodyOverlap(float x,float y,double c,double s,Rect r) {
        double dx=x-r.x-r.w/2.0,dy=y-r.y-r.h/2.0,h=GameRules.TankHalfSize,e=GameRules.CollisionEpsilon;
        double ac=Math.abs(c),as=Math.abs(s),rw=r.w/2.0,rh=r.h/2.0;
        if(Math.abs(dx)>=h*(ac+as)+rw-e||Math.abs(dy)>=h*(ac+as)+rh-e)return false;
        return Math.abs(dx*c+dy*s)<h+rw*ac+rh*as-e&&Math.abs(-dx*s+dy*c)<h+rw*as+rh*ac-e;
    }
    public static boolean overlap(float x,float y,Rect r,float radius) {
        return x+radius>r.x&&x-radius<r.x+r.w&&y+radius>r.y&&y-radius<r.y+r.h;
    }
    public boolean water(float x,float y) {
        for(Rect r:water)if(r.contains(x,y))return true;
        return false;
    }
}
