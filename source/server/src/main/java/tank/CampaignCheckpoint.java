package tank;

import com.google.gson.*;
import java.util.*;

/** Explicit versioned snapshots; no Java object deserialization or client-authored state. */
final class CampaignCheckpoint {
    static final class RandomState extends Random {
        long state;
        RandomState(long seed){super(0);setSeed(seed);}
        @Override public synchronized void setSeed(long seed){state=(seed^0x5DEECE66DL)&((1L<<48)-1);}
        @Override protected synchronized int next(int bits){state=(state*0x5DEECE66DL+11)&((1L<<48)-1);return (int)(state>>>(48-bits));}
    }
    static final Gson JSON=new Gson();
    static final String[] FIELDS={"id","x","y","distance","hp","turn","facing","shot","skill","gold","kills","doubleHits","shields","bosses","shopRevision","finished","victory","message","generated","nextEnemy","nextSquad"};
    static String encode(CampaignRun run) {
        try {
            JsonObject data=new JsonObject();data.addProperty("version",1);data.addProperty("seed",run.seed);data.addProperty("nation",run.nation);data.addProperty("rng",run.random.state);data.addProperty("elapsed",run.elapsedMillis());
            for(String name:FIELDS){var f=CampaignRun.class.getDeclaredField(name);data.add(name,JSON.toJsonTree(f.get(run)));}
            data.add("enemies",JSON.toJsonTree(run.enemies));data.add("loot",JSON.toJsonTree(run.loot));data.add("dangers",JSON.toJsonTree(run.dangers));return JSON.toJson(data);
        }catch(ReflectiveOperationException e){throw new IllegalStateException(e);}
    }
    static CampaignRun decode(String json) {
        try {
            var data=JsonParser.parseString(json).getAsJsonObject();
            if(data.get("version").getAsInt()!=1)throw new IllegalArgumentException("Unsupported campaign save.");
            CampaignRun run=new CampaignRun(data.get("nation").getAsInt(),data.get("seed").getAsLong());
            for(String name:FIELDS){var f=CampaignRun.class.getDeclaredField(name);if(!data.has(name))throw new IllegalArgumentException("Incomplete campaign save.");f.set(run,JSON.fromJson(data.get(name),f.getType()));}
            run.random.state=data.get("rng").getAsLong();run.accumulatedMillis=data.get("elapsed").getAsLong();run.startedNanos=System.nanoTime();run.completedMillis=run.finished?run.accumulatedMillis:-1;
            run.enemies.clear();run.enemies.addAll(Arrays.asList(JSON.fromJson(data.get("enemies"),CampaignRun.Enemy[].class)));
            run.loot.clear();run.loot.addAll(Arrays.asList(JSON.fromJson(data.get("loot"),CampaignRun.Loot[].class)));
            run.dangers.clear();run.dangers.addAll(Arrays.asList(JSON.fromJson(data.get("dangers"),CampaignRun.Danger[].class)));
            run.visuals.clear();
            if(run.hp<0||run.hp>run.stats.maxHp()||run.gold<0||run.turn<0||run.x<0||run.x>=CampaignRun.WIDTH||run.y<0||run.y>CampaignRun.GOAL)throw new IllegalArgumentException("Invalid campaign save.");
            return run;
        }catch(Exception e){throw new IllegalArgumentException("Bản lưu chiến dịch không đọc được; dữ liệu được giữ nguyên.",e);}
    }
}
