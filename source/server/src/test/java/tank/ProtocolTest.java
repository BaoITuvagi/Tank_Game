package tank;
import org.junit.jupiter.api.Test;
import java.io.*;
import java.util.*;
import static org.junit.jupiter.api.Assertions.*;
class ProtocolTest {
 @Test void golden(){byte[] f=Protocol.frame(1,0x01020304,new Protocol.Writer().str("tank").str("password").done());assertEquals("544210010102030400100000000474616e6b000870617373776f7264",HexFormat.of().formatHex(f));assertEquals(1,Protocol.decode(f,false).op());}
 @Test void threeItemClientsAreRejectedBeforeLogin() {
  byte[] login=Protocol.frame(Protocol.LOGIN,1,new Protocol.Writer().str("tank").str("password").done());login[2]=6;
  assertThrows(IllegalArgumentException.class,()->Protocol.read(new ByteArrayInputStream(login)));
  assertThrows(IllegalArgumentException.class,()->Protocol.decode(login,false));
 }
 @Test void previousCombatProtocolIsRejectedOnBothTransports() {
  byte[] frame=Protocol.frame(Protocol.LOGIN,1,new Protocol.Writer().str("tank").str("password").done());frame[2]=8;
  assertThrows(IllegalArgumentException.class,()->Protocol.read(new ByteArrayInputStream(frame)));
  assertThrows(IllegalArgumentException.class,()->Protocol.decode(frame,false));
  assertThrows(IllegalArgumentException.class,()->Protocol.decode(frame,true));
 }
 @Test void framing()throws Exception{byte[] f=Protocol.frame(73,-1,new Protocol.Writer().u64(7).done());InputStream split=new FilterInputStream(new ByteArrayInputStream(f)){public int read(byte[] b,int o,int l)throws IOException{return super.read(b,o,Math.min(1,l));}};assertEquals(-1,Protocol.read(split).sequence());assertThrows(IllegalArgumentException.class,()->Protocol.decode(Arrays.copyOf(f,13),true));assertTrue(Protocol.newer(1,-1));assertFalse(Protocol.newer(-1,1));}
 @Test void unsigned(){for(int a:new int[]{0,18000,32768,35999})assertEquals(a,new Protocol.Reader(new Protocol.Writer().u16(a).done()).u16());assertThrows(IllegalArgumentException.class,()->new Protocol.Reader(new byte[]{2}).bool());}
}
