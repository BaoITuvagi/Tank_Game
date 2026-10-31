package tank;
import java.nio.file.*;
import java.util.*;
import static tank.Protocol.*;
/** Current and legacy optional payloads, generated outside authored documentation. */
public final class Golden  {
    public static Map<String,byte[]> fixtures() {
        Map<String,byte[]> vectors=new TreeMap<>();
        Map<Integer,byte[]> v=new TreeMap<>();
        byte[] token=new byte[16],hash=new byte[32];
        String nickname="Xe tăng Việt Nam";
        byte[] contentHash=HexFormat.of().parseHex(RoleplayContent.Hash);
        v.put(CHAT_SEND,new Writer().u64(1).str("Xin chào <b>!</b>").done());
        v.put(CHAT_MESSAGES,new Writer().u64(1).u8(1).u32(1).u8(7).u8(2).str(nickname).str("Xin chào <b>!</b>").done());
        v.put(SELECT_NATION,new Writer().u8(0).done());
        v.put(USE_SKILL,new Writer().u64(1).u32(8).u8(0).f32(6.8f).f32(53.2f).done());
        v.put(SHOP,new Writer().u8(0).u64(0).done());
        v.put(SHOP_RESULT,new Writer().u16(0).u8(2).u64(150).u64(1).u8(1).u8(3).u32(2).bytes(new byte[]{1,1,0,0,0,0}).done());
        byte[] loadout=new Writer().u64(1).u32(1).bytes(contentHash).u8(1).u8(0).u8(0).done();
        try {
            v.put(MATCH_LOADOUT,new Writer().bytes(loadout).bytes(java.security.MessageDigest.getInstance("SHA-256").digest(loadout)).done());
        }
        catch(java.security.NoSuchAlgorithmException e) {throw new IllegalStateException(e);}
        for(int op:new int[] {
            REGISTER,LOGIN
        })v.put(op,new Writer().str("tank").str("password").done());
        for(int op:new int[] {
            REGISTER_RESULT,LOGOUT_RESULT,ACTION_RESULT
        })v.put(op,new Writer().u16(0).done());
        v.put(LOGIN_RESULT,new Writer().u16(0).u64(1).str(nickname).bytes(token).str("tank").done());
        for(int op:new int[] {
            LOGOUT,ROOM_LIST,LEAVE_ROOM,START_MATCH
        })v.put(op,new byte[0]);
        v.put(ROOM_LIST_RESULT,new Writer().u16(0).u16(1).u32(1).str("Phòng").u8(2).u8(8).u8(2).u8(1).u8(1).done());
        v.put(CREATE_ROOM,new Writer().str("Phòng").u8(8).u16(600).str("friends").u16(500).done());
        v.put(JOIN_ROOM,new Writer().u32(1).str("friends").done());
        v.put(SET_READY,new Writer().u8(1).done());
        v.put(KICK_PLAYER,new Writer().u8(1).done());
        v.put(ROOM_STATE,new Writer().u32(1).str("Phòng").u8(0).u8(0).u8(1).u8(0).str(nickname).u8(1).u8(8).u16(600).u8(1).u8(0).u16(500).done());
        v.put(PREPARE,prepare(hash,true));
        v.put(PREPARED,new Writer().u64(1).done());
        v.put(START,new Writer().u64(1).u64(1000000).done());
        v.put(END,new Writer().u64(1).u8(0).u8(1).u8(0).str(nickname).u16(0).u16(0).u32(-1).u8(1).u8(0).done());
        v.put(REMOVED,new Writer().u32(1).u8(0).u16(100).done());
        v.put(NOTICE,new Writer().u16(100).str("AFK: hãy di chuyển").done());
        v.put(HISTORY,new Writer().u16(0).u8(20).done());
        v.put(HISTORY_RESULT,new Writer().u16(0).u8(1).u64(1).u64(1000).u8(0).u16(0).u16(0).u8(1).done());
        v.put(SET_DISPLAY_NAME,new Writer().str(nickname).done());
        v.put(DISPLAY_NAME_RESULT,new Writer().u16(0).str(nickname).done());
        v.put(ROOM_SETTINGS,new Writer().str("Phòng mới").u8(8).u16(600).u8(2).str("friends").u16(500).done());
        v.put(HISTORY_DETAIL,new Writer().u64(1).done());
        v.put(HISTORY_DETAIL_RESULT,new Writer().u16(0).u64(1).u16(1).u64(1000).u64(181000).u8(0).u8(1).u8(0).str(nickname).u16(2).u16(1).u8(1).u8(0).u8(1).done());
        for(int op:new int[] {
            HEARTBEAT,HEARTBEAT_ACK
        })v.put(op,new Writer().u64(1).done());
        for(int op:new int[] {
            HANDSHAKE,PING
        })v.put(op,new Writer().bytes(token).u64(1).u64(7).done());
        v.put(HANDSHAKE_ACK,new Writer().u64(1).u64(7).u8(0).u64(1000).done());
        v.put(PONG,new Writer().u64(1).u64(7).u64(1000).u64(1001).done());
        v.put(INPUT,new Writer().bytes(token).u64(1).u32(1).u8(3).u32(4).u8(63).u16(35999).u32(3).u8(16).u16(32768).u32(2).u8(0).u16(0).done());
        for(int op:new int[] {
            ACK,RESYNC
        })v.put(op,new Writer().bytes(token).u64(1).u32(1).done());
        World world=new World(null,0);
        World.Tank t=new World.Tank();
        t.x=1;
        t.y=2;
        t.turret=32768;
        world.tanks.put(0,t);
        v.put(FULL,new Writer().u64(1).u32(1).u32(1).u64(1000).u32(1).u16(5399).bytes(world.encode()).done());
        v.put(DELTA,new Writer().u64(1).u32(2).u32(2).u64(2000).u32(2).u16(5398).u32(1).bytes(DeltaCodec.encode(world.encode(),world.encode())).done());
        v.put(SNAPSHOT_PART,SnapshotFragments.split(1,1,FULL,v.get(FULL)).getFirst());
        v.forEach((op,payload)->add(vectors,op,"",payload));
        add(vectors,PREPARED,"_roleplay_loading",new Writer().u64(1).u8(PrepStage.LOADING.ordinal()).u32(RoleplayContent.Version).bytes(contentHash).done());
        add(vectors,PREPARED,"_roleplay_loadout",new Writer().u64(1).u8(PrepStage.SYNC_LOADOUT.ordinal()).u32(1).bytes(Arrays.copyOfRange(v.get(MATCH_LOADOUT),loadout.length,loadout.length+32)).done());
        add(vectors,ACTION_RESULT,"_skill",new Writer().u16(0).u64(1).u32(8).u8(USE_SKILL).u8(0).u32(100).u16(850).done());
        add(vectors,SHOP,"_buy",new Writer().u8(1).u64(1).u8(1).u8(2).done());
        add(vectors,SHOP,"_use",new Writer().u8(2).u64(1).u32(2).u8(1).done());
        add(vectors,SHOP_RESULT,"_error",new Writer().u16(1).str("Không đủ vàng.").done());
        add(vectors,ACTION_RESULT,"_skill_rejected",new Writer().u16(Protocol.Error.COOLDOWN.ordinal()).str("COOLDOWN").u64(1).u32(9).u8(USE_SKILL).u8(0).u32(101).u16(850).done());
        add(vectors,HISTORY_DETAIL_RESULT,"_duration",new Writer().bytes(v.get(HISTORY_DETAIL_RESULT)).u32(5400).done());
        add(vectors,CREATE_ROOM,"_legacy",new Writer().str("Phòng").done());
        add(vectors,JOIN_ROOM,"_legacy",new Writer().u32(1).done());
        add(vectors,LOGIN_RESULT,"_legacy",new Writer().u16(0).u64(1).str("tank").bytes(token).done());
        add(vectors,ROOM_LIST_RESULT,"_legacy",new Writer().u16(0).u16(0).done());
        add(vectors,ROOM_STATE,"_legacy",new Writer().u32(1).str("Phòng").u8(0).u8(0).u8(1).u8(0).str("tank").u8(1).done());
        add(vectors,PREPARE,"_new_match",prepare(hash,false));
        add(vectors,ROOM_SETTINGS,"_keep_password",new Writer().str("Phòng").u8(4).u16(180).u8(0).u16(100).done());
        add(vectors,ROOM_SETTINGS,"_remove_password",new Writer().str("Phòng").u8(4).u16(180).u8(1).done());
        for(int op:new int[] {
            LOGIN_RESULT,DISPLAY_NAME_RESULT,ROOM_LIST_RESULT,HISTORY_RESULT,HISTORY_DETAIL_RESULT,ACTION_RESULT
        }) add(vectors,op,"_error",new Writer().u16(Protocol.Error.INVALID_REQUEST.ordinal()).str("INVALID_REQUEST").done());
        return vectors;
    }
    static byte[] prepare(byte[] hash,boolean rejoin) {
        Writer w=new Writer().u64(1).u32(1).u8(0).u16(1).bytes(hash).u8(3).u8(30).u16(8889);
        if(rejoin)w.u8(1);
        return w.done();
    }
    static void add(Map<String,byte[]> v,int op,String suffix,byte[] payload) {
        v.put(String.format("op_%02x%s",op,suffix),frame(op,0x01020304,payload));
    }
    public static void write(Path path)throws Exception {
        Files.createDirectories(path.toAbsolutePath().getParent());
        List<String> lines=new ArrayList<>();
        fixtures().forEach((name,bytes)->lines.add(name+"\t"+HexFormat.of().formatHex(bytes)));
        Files.write(path,lines);
    }
    public static void verify(Path path)throws Exception {
        Map<String,byte[]> expected=fixtures();
        Set<String> seen=new HashSet<>();
        for(String line:Files.readAllLines(path)) {
            String[] parts=line.split("\t");
            if(!seen.add(parts[0])||!Arrays.equals(expected.get(parts[0]),HexFormat.of().parseHex(parts[1])))throw new IllegalArgumentException("Golden mismatch: "+parts[0]);
        }
        if(!seen.equals(expected.keySet()))throw new IllegalArgumentException("Missing golden variants");
        System.out.println("Java verified all C# golden frames: "+seen.size()+" variants / 48 opcodes");
    }
    public static void main(String[] args)throws Exception {
        if(args.length==2&&args[0].equals("--verify"))verify(Path.of(args[1]));
        else write(Path.of(args[0]));
    }
}
