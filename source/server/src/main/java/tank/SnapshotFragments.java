package tank;
import java.util.*;
import static tank.Protocol.*;

/** Application-level fragmentation; each datagram remains independently framed below MTU. */
final class SnapshotFragments {
    static final int CHUNK=1100,MAX_BYTES=32768;
    static List<byte[]> split(long match,int snapshot,int op,byte[] payload) {
        if((op!=FULL&&op!=DELTA)||payload.length>MAX_BYTES||payload.length<30)throw new IllegalArgumentException("snapshot fragments");
        List<byte[]> parts=new ArrayList<>();int count=(payload.length+CHUNK-1)/CHUNK;
        for(int index=0;index<count;index++)parts.add(new Writer().u64(match).u32(snapshot).u8(op).u8(index).u8(count).u16(payload.length).bytes(Arrays.copyOfRange(payload,index*CHUNK,Math.min(payload.length,(index+1)*CHUNK))).done());
        return parts;
    }
}
