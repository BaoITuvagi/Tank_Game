package tank;

import java.util.*;
import java.util.concurrent.ConcurrentLinkedQueue;
import static tank.Protocol.*;

/** The room thread owns timers and members. Only identity allocation crosses to SQL. */
final class BotLobby {
    final Room room;
    final Random random=new Random();
    final Set<Long> excluded=new HashSet<>();
    final Queue<Database.Account> arrivals=new ConcurrentLinkedQueue<>();
    final Map<Integer,Long> readyAt=new HashMap<>();
    volatile long nextFill;
    volatile boolean pending,closed;
    BotLobby(Room room){this.room=room;}
    long delay(){return room.server.cfg.integer("bots.fill.delay.seconds",15)*1_000_000L;}
    boolean hasHuman(){return room.members.values().stream().anyMatch(m->!m.session.bot);}
    boolean eligible(){return !closed&&room.alive&&!room.stopRequested&&room.phase==RoomState.WAITING&&!room.access.locked&&hasHuman()&&room.server.cfg.botsEnabled();}
    void tick(long now) {
        Database.Account account;
        while((account=arrivals.poll())!=null) {
            final var incoming=account;
            pending=false;
            boolean nameCollision=room.members.values().stream().anyMatch(m->m.displayName.equalsIgnoreCase(incoming.name()));
            if(nameCollision)excluded.add(account.id());
            if(!eligible()||room.members.size()>=room.capacity||nameCollision) {
                room.server.bots.release(account);continue;
            }
            var session=room.server.new Session(account);room.join(session,0);
            var member=room.member(session);
            if(member==null){room.server.bots.release(account);continue;}
            member.bot=new BotBrain(room,member,random.nextLong());
            readyAt.put(member.slot,now+700_000+random.nextInt(1_200_001));
            int min=room.server.cfg.integer("bots.join.min.seconds",1),max=room.server.cfg.integer("bots.join.max.seconds",3);
            nextFill=now+min*1_000_000L+random.nextInt((max-min)*1_000_000+1);
            System.out.println("BOT_JOIN room="+room.id+" slot="+member.slot+" name="+member.displayName);
        }
        if(!eligible()){nextFill=0;return;}
        for(var member:room.members.values())if(member.session.bot&&!member.ready&&now>=readyAt.getOrDefault(member.slot,0L)) {
            if(room.roleplay!=null&&member.roleplay.nation<0) {
                if(room.members.values().stream().anyMatch(m->!m.session.bot&&m.roleplay.nation<0))continue;
                var free=new ArrayList<Integer>();for(int nation=0;nation<8;nation++){final int n=nation;if(room.members.values().stream().noneMatch(m->m.roleplay.nation==n))free.add(n);}
                if(free.isEmpty())continue;
                room.roleplay.selectNation(member.session,free.get(random.nextInt(free.size())),0);
            }
            room.ready(member.session,true,0);
        }
        if(room.members.size()>=room.capacity){nextFill=0;return;}
        if(nextFill==0)nextFill=now+delay();
        if(pending||now<nextFill)return;
        pending=true;
        var unavailable=new HashSet<>(excluded);for(var m:room.members.values())unavailable.add(m.account.id());
        room.server.db.submit(()->room.server.bots.acquire(Set.copyOf(unavailable))).whenComplete((result,error)-> {
            synchronized(this) {
                if(closed||!room.alive||room.stopRequested){room.server.bots.release(result);pending=false;return;}
                if(error!=null||result==null){pending=false;nextFill=Server.nowUs()+delay();return;}
                arrivals.offer(result);
            }
        });
    }
    void removed(Room.Member member,boolean kicked) {
        if(!member.session.bot)return;
        if(kicked){excluded.add(member.account.id());nextFill=Server.nowUs()+delay();}
        readyAt.remove(member.slot);member.session.open=false;room.server.bots.release(member.account);
    }
    void clear(){for(var m:new ArrayList<>(room.members.values()))if(m.session.bot)room.leave(m.session,4);}
    synchronized void close() {
        closed=true;Database.Account a;while((a=arrivals.poll())!=null)room.server.bots.release(a);
        for(var m:room.members.values())if(m.session.bot){m.session.open=false;room.server.bots.release(m.account);}
    }
}
