package tank;
import java.nio.charset.StandardCharsets;
import java.util.*;
import static tank.Protocol.*;
import tank.Protocol.Error;

/** Owned by the room thread; never persisted with match results. */
final class MatchChat {
    static final int LIMIT=100,MAX_CHARS=200,MAX_BYTES=800,BATCH=10;
    record Message(int id,int slot,int palette,String name,String text) {
        void write(Writer w) {w.u32(id).u8(slot).u8(palette).str(name).str(text);}
    }
    final Room room;
    final ArrayDeque<Message> messages=new ArrayDeque<>();
    int nextId;
    MatchChat(Room room) {this.room=room;}
    static String normalize(String raw) {
        if(raw==null||raw.getBytes(StandardCharsets.UTF_8).length>MAX_BYTES)throw new IllegalArgumentException("chat length");
        String text=raw.replace('\r',' ').replace('\n',' ').replace('\t',' ').strip();
        if(text.isEmpty()||text.codePointCount(0,text.length())>MAX_CHARS)throw new IllegalArgumentException("chat length");
        if(text.codePoints().anyMatch(Character::isISOControl))throw new IllegalArgumentException("chat control");
        return text;
    }
    void send(Server.Session s,long match,String raw,int seq) {
        Room.Member m=room.member(s);
        if(s.room!=room||m==null||m.session!=s||!s.open||m.rejoining||room.phase!=RoomState.RUNNING||room.match!=match) {
            s.error(ACTION_RESULT,seq,Error.INVALID_STATE);return;
        }
        String text;
        try {text=normalize(raw);}catch(IllegalArgumentException e){s.error(ACTION_RESULT,seq,Error.INVALID_REQUEST);return;}
        long now=System.nanoTime();
        if(m.lastChat!=0&&now-m.lastChat<1_000_000_000L){s.error(ACTION_RESULT,seq,Error.RATE_LIMITED);return;}
        m.lastChat=now;
        int palette=room.roomMode==RoomMode.ROLEPLAY?m.roleplay.nation:m.slot;
        Message message=new Message(++nextId,m.slot,palette,m.displayName,text);
        messages.addLast(message);if(messages.size()>LIMIT)messages.removeFirst();
        s.ok(ACTION_RESULT,seq);
        byte[] payload=payload(List.of(message));
        for(Room.Member target:room.members.values())if(!target.rejoining&&target.session.open)target.session.event(CHAT_MESSAGES,payload);
    }
    byte[] payload(List<Message> batch) {
        Writer w=new Writer().u64(room.match).u8(batch.size());
        for(Message message:batch)message.write(w);
        return w.done();
    }
    void replay(Server.Session s) {
        List<Message> all=new ArrayList<>(messages);
        for(int i=0;i<all.size();i+=BATCH)s.event(CHAT_MESSAGES,payload(all.subList(i,Math.min(i+BATCH,all.size()))));
    }
    void clear() {messages.clear();nextId=0;for(Room.Member m:room.members.values())m.lastChat=0;}
}
