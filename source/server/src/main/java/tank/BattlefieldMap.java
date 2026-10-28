package tank;
import java.util.*;
import static tank.GameRules.*;

/** Authored fire and airstrike targets. */
public final class BattlefieldMap {
    public Zone[] fireZones,airstrikeZones;
    public static class Zone {public int id;public float x,y;}
    public Zone zone(int kind,int id) {for(Zone z:kind==WorldBattlefield.FIRE?fireZones:airstrikeZones)if(z.id==id)return z;throw new IllegalArgumentException("battlefield zone");}
    public void validate(GameMap map) {
        if(fireZones==null||airstrikeZones==null)throw new IllegalArgumentException("battlefield targets");
        Set<Integer> ids=new HashSet<>();
        for(int kind:new int[]{WorldBattlefield.FIRE,WorldBattlefield.AIRSTRIKE}) {
            Zone[] zones=kind==WorldBattlefield.FIRE?fireZones:airstrikeZones;ids.clear();
            if(zones.length<1||zones.length>64)throw new IllegalArgumentException("battlefield zones");
            for(Zone z:zones) {
                float r=BattlefieldRadius;
                if(z==null||z.id<0||z.id>63||!ids.add(z.id)||!Float.isFinite(z.x)||!Float.isFinite(z.y)||z.x-r<0||z.y-r<0||z.x+r>map.width||z.y+r>map.height)throw new IllegalArgumentException("battlefield zone");
                for(GameMap.Point p:map.spawns)if(Math.hypot(z.x-p.x,z.y-p.y)<r+BattlefieldSpawnProtectionRadius+TankHalfSize*Math.sqrt(2))throw new IllegalArgumentException("battlefield spawn protection");
                if(kind==WorldBattlefield.FIRE)for(GameMap.Rect[] rs:List.of(map.walls,map.bricks,map.water))for(GameMap.Rect rect:rs)
                    if(GameMap.overlap(z.x,z.y,rect,r))throw new IllegalArgumentException("blocked fire zone");
            }
        }
    }
}
