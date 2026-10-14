package tank;
import java.util.*;
public final class InputBuffer  {
    final Map<Integer,World.Input> inputs=new HashMap<>();
    public long onTime,missing,late,duplicate,conflict,dropped;
    public synchronized void put(World.Input in,int resolved) {
        if(in.tick()<=resolved) {
            late++;
            return;
        }
        if(in.tick()>resolved+30) {
            dropped++;
            return;
        }
        World.Input old=inputs.get(in.tick());
        if(old!=null) {
            if(old.equals(in))duplicate++;
            else conflict++;
            return;
        }
        if(inputs.size()>=64) {
            dropped++;
            return;
        }
        inputs.put(in.tick(),in);
    }
    public synchronized World.Input resolve(int tick,int angle) {
        World.Input in=inputs.remove(tick);
        inputs.keySet().removeIf(t->t<tick);
        if(in==null) {
            missing++;
            return new World.Input(tick,0,angle);
        }
        onTime++;
        return in;
    }
    public synchronized int depth() {
        return inputs.size();
    }
}
