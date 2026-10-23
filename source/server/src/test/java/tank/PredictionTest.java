package tank;
import java.nio.file.*;
import java.util.*;
import org.junit.jupiter.api.*;
import static org.junit.jupiter.api.Assertions.*;
class PredictionTest {
 @Test void sharedMovementTrace()throws Exception{GameMap map=GameMap.load(Path.of("../../source/client/Assets/StreamingAssets/map.json"));World world=new World(map,1);world.add(0);World.Tank t=world.tanks.get(0);Random random=new Random(19);List<String> rows=new ArrayList<>();for(int i=0;i<600;i++){t.x=i<200?28: i<400?1+GameRules.TankHalfSize*(float)Math.sqrt(2)+.1f:17.5f;t.y=i<200?15:i<400?5:12-GameRules.TankHalfSize*(float)Math.sqrt(2)-.1f;t.body=random.nextInt(36000);t.turret=random.nextInt(36000);var before=new Protocol.Writer();World.writeTank(before,t);World.Input input=new World.Input(i+1,random.nextInt(64),random.nextInt(36000));World.movement(t,input,map,world.bricks);var after=new Protocol.Writer();World.writeTank(after,t);rows.add(HexFormat.of().formatHex(before.done())+"\t"+input.buttons()+"\t"+input.angle()+"\t"+HexFormat.of().formatHex(after.done()));assertFalse(map.blocked(t.x,t.y,world.bricks));}Files.createDirectories(Path.of("target/interop"));Files.write(Path.of("target/interop/movement-trace.tsv"),rows);}
}
