package tank;
import java.nio.file.*;
import java.security.MessageDigest;
import java.util.*;
import org.junit.jupiter.api.Test;
import static org.junit.jupiter.api.Assertions.*;

/** Approved rectangular layout/hitbox baseline. The pre-layout trace remains archived separately. */
class CombatGameplayTraceTest {
    List<String> trace(boolean recording)throws Exception {
        List<String> rows=new ArrayList<>();
        MessageDigest sha=MessageDigest.getInstance("SHA-256");
        for(int scenario=0;scenario<3;scenario++) {
            GameMap map=GameMap.load(Path.of("../../source/client/Assets/StreamingAssets/"+(scenario==2?"map-large.json":"map.json")));
            World w=new World(map,731+scenario,300);
            w.combat.enabled=recording;
            w.items.configure(scenario!=0);
            for(int slot=0;slot<8;slot++) {w.add(slot);if(scenario!=0)w.tanks.get(slot).nation=slot;}
            for(int tick=1;tick<=1200;tick++) {
                if(tick%180==1)for(var t:w.tanks.values())if(t.hp>0&&scenario!=0) {
                    w.skillAim(t.slot,30+(t.slot%2)*8,25+(t.slot%3)*5);w.useSkill(t.slot,0);
                }
                if(tick%150==0)for(var t:w.tanks.values())if(t.hp>0)w.items.apply(t,(tick/150+t.slot)%6);
                Map<Integer,World.Input> inputs=new HashMap<>();
                for(int slot=0;slot<8;slot++)inputs.put(slot,new World.Input(tick,16|((tick/60+slot)%2==0?32:0)|(1<<((tick/90+slot)%4)),(tick*97+slot*4500)%36000));
                w.step(inputs);
                Protocol.Writer out=new Protocol.Writer().u32(w.tick).u32(w.nextBullet).u32(w.nextLife).u8(w.tanks.size());
                for(var t:w.tanks.values()) {
                    World.writeTank(out,t);
                    out.u32(t.cooldown).u32(t.qPenalty).u32(t.qBaseReadyTick).u32(t.desiredAim).u32(t.desiredButtons);
                }
                out.u8(w.bullets.size());for(var b:w.bullets.values()) {World.writeBullet(out,b);out.u32(b.born);}
                out.bytes(w.bricks).bytes(w.skills.encode()).bytes(w.items.encode());
                rows.add(scenario+":"+tick+":"+HexFormat.of().formatHex(sha.digest(out.done())));
            }
            rows.add("rng:"+scenario+":"+w.random.nextLong());
        }
        return rows;
    }
    @Test void matchesApprovedLayoutGameplayAtEveryTick()throws Exception {
        var actual=trace(true);Path fixture=Path.of("src/test/resources/combat-gameplay-trace.sha256");
        if(Boolean.getBoolean("tank.recordTrace")) {Files.createDirectories(fixture.getParent());Files.write(fixture,actual);}
        assertEquals(Files.readAllLines(fixture),actual);
        assertEquals(actual,trace(false));
    }
}
