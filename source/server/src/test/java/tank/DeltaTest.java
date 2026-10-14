package tank;
import org.junit.jupiter.api.*;
import java.nio.file.*;
import java.util.*;
import static org.junit.jupiter.api.Assertions.*;
class DeltaTest {
 @Test void roundTripAndInterop()throws Exception{GameMap map=GameMap.load(Path.of("../../source/client/Assets/StreamingAssets/map.json"));World w=new World(map,17);for(int i=0;i<8;i++)w.add(i);Random random=new Random(7);List<String> trace=new ArrayList<>();byte[] previous=w.encode();trace.add("FULL\t"+HexFormat.of().formatHex(previous));for(int tick=1;tick<=300;tick++){Map<Integer,World.Input> inputs=new HashMap<>();for(int slot:w.tanks.keySet())inputs.put(slot,new World.Input(tick,random.nextInt(64),random.nextInt(36000)));w.step(inputs);if(tick==150)w.remove(7);if(tick==200)w.add(7);byte[] current=w.encode(),delta=DeltaCodec.encode(previous,current);assertArrayEquals(current,DeltaCodec.apply(previous,delta));trace.add("DELTA\t"+HexFormat.of().formatHex(delta)+"\t"+HexFormat.of().formatHex(current));previous=current;}Path target=Path.of("target/interop/delta-trace.tsv");Files.createDirectories(target.getParent());Files.write(target,trace);}
 @Test void atomicMalformed(){World w=new World(null,0);byte[] base=w.encode();byte[] bad={1,0,0,1,0,0,0,0};assertThrows(RuntimeException.class,()->DeltaCodec.apply(base,bad));assertArrayEquals(base,w.encode());}
}

