package tank;
import org.junit.jupiter.api.*;
import java.nio.file.*;
import java.lang.reflect.*;
import java.util.*;
import static org.junit.jupiter.api.Assertions.*;
class GoldenInteropTest {
 @Test void everyOpcodeHasCurrentGoldenPayload()throws Exception{
  Set<Integer> expected=new HashSet<>(),actual=new HashSet<>();
  for(Field field:Protocol.class.getFields())if(field.getType()==int.class&&Modifier.isStatic(field.getModifiers()))expected.add(field.getInt(null));
  for(byte[] frame:Golden.fixtures().values()){var packet=Protocol.decode(frame,false);actual.add(packet.op());assertArrayEquals(frame,Protocol.frame(packet.op(),packet.sequence(),packet.payload()));}
  assertEquals(46,expected.size());assertEquals(expected,actual);
  Golden.write(Path.of("target/interop/golden.tsv"));
 }
}
