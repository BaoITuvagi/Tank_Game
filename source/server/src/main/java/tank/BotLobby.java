package tank;

import java.util.*;
import java.util.concurrent.ConcurrentLinkedQueue;
import static tank.Protocol.*;
import tank.Protocol.Error;

/** Manual lobby seats. Allocation is asynchronous; the room owns admission. */
final class BotLobby {
    final Room room;
    final Random random=new Random();
    final Set<Integer> pending=new HashSet<>();
    final Map<Integer,Long> readyAt=new HashMap<>();
    final Queue<Runnable> arrivals=new ConcurrentLinkedQueue<>();
    volatile boolean closed;
    static final class Addition {
        final int slot,generation;final List<Integer> sequences=new ArrayList<>();boolean finished;Error error;
        Addition(int slot,int generation,int seq){this.slot=slot;this.generation=generation;sequences.add(seq);}
    }
    final LinkedHashMap<String,Addition> requests=new LinkedHashMap<>();
    void completed(Server.Session actor,Addition request,Error error){
        request.finished=true;request.error=error;pending.remove(request.slot);
        for(int seq:request.sequences){if(error==null)actor.ok(ACTION_RESULT,seq);else actor.error(ACTION_RESULT,seq,error);}
        room.broadcast();
    }
    BotLobby(Room room){this.room=room;}
    boolean hasHuman(){return room.members.values().stream().anyMatch(m->!m.botOrigin&&!m.disconnected&&m.session.open);}
    void add(Server.Session actor,int slot,int requestId,int seq) {
        if(room.member(actor)==null||actor.slot!=room.host){actor.error(ACTION_RESULT,seq,Error.NOT_HOST);return;}
        String key=actor.account.id()+":"+requestId;Addition previous=requests.get(key);
        if(previous!=null){
            if(previous.slot!=slot){actor.error(ACTION_RESULT,seq,Error.INVALID_REQUEST);return;}
            if(previous.finished){if(previous.error==null)actor.ok(ACTION_RESULT,seq);else actor.error(ACTION_RESULT,seq,previous.error);}
            else if(previous.sequences.size()<16)previous.sequences.add(seq);
            return;
        }
        if(requestId==0||closed||room.phase!=RoomState.WAITING||slot<0||slot>=room.capacity||room.members.containsKey(slot)||!pending.add(slot)) {
            actor.error(ACTION_RESULT,seq,Error.INVALID_STATE);return;
        }
        Addition request=new Addition(slot,room.lobbyRevision,seq);requests.put(key,request);
        if(requests.size()>128){var old=requests.entrySet().iterator();while(requests.size()>128&&old.hasNext())if(old.next().getValue().finished)old.remove();}
        room.broadcast();
        room.server.db.submit(()->room.server.bots.acquire(Set.of())).whenComplete((account,error)->{
            synchronized(this) {
                if(closed){room.server.bots.release(account);return;}
                arrivals.add(()->{
                    if(error!=null||account==null){completed(actor,request,Error.DATABASE_UNAVAILABLE);return;}
                    if(request.generation!=room.lobbyRevision||closed||room.phase!=RoomState.WAITING||!room.alive||room.stopRequested||room.member(actor)==null||actor.slot!=room.host||slot>=room.capacity||room.members.containsKey(slot)) {
                        room.server.bots.release(account);completed(actor,request,Error.INVALID_STATE);return;
                    }
                    var session=room.server.new Session(account);
                    room.admit(session,slot);
                    room.member(session).bot=new BotBrain(room,room.member(session),random.nextLong());
                    readyAt.put(slot,Server.nowUs()+700_000+random.nextInt(1_200_001));
                    completed(actor,request,null);
                });
            }
        });
    }
    void tick(long now) {
        Runnable action;while((action=arrivals.poll())!=null)action.run();
        if(room.phase!=RoomState.WAITING)return;
        for(var member:room.members.values())if(member.botOrigin&&!member.ready&&now>=readyAt.getOrDefault(member.slot,0L)) {
            if(room.roleplay!=null&&member.roleplay.nation<0) {
                if(room.members.values().stream().anyMatch(m->!m.botOrigin&&m.roleplay.nation<0))continue;
                var free=new ArrayList<Integer>();for(int nation=0;nation<8;nation++){final int n=nation;if(room.members.values().stream().noneMatch(m->m.roleplay.nation==n))free.add(n);}
                if(free.isEmpty())continue;
                room.roleplay.selectNation(member.session,free.get(random.nextInt(free.size())),0);
            }
            room.ready(member.session,true,0);
        }
    }
    void removed(Room.Member member,boolean kicked) {
        if(!member.botOrigin)return;
        readyAt.remove(member.slot);member.session.open=false;room.server.bots.release(member.account);
    }
    void clear(){for(var m:new ArrayList<>(room.members.values()))if(m.botOrigin)room.leave(m.session,4);}
    synchronized void close() {
        closed=true;Runnable action;while((action=arrivals.poll())!=null)action.run();
        for(var m:room.members.values())if(m.botOrigin){m.session.open=false;room.server.bots.release(m.account);}
    }
}
