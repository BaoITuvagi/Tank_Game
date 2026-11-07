package tank;
import com.google.gson.Gson;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.util.*;

/** The shared manifest is the source of selectable multiplayer maps and display names. */
public final class MapCatalog {
    public static final int DEFAULT_ID=2,FORTRESS_ID=3,RIVER_ID=4;
    public static final class Entry {
        public int id;
        public String name,file,description;
    }
    private static final class Manifest {int defaultId;Entry[] maps;}
    private final Map<Integer,GameMap> maps;
    private final List<Entry> entries;
    private MapCatalog(Map<Integer,GameMap> maps,List<Entry> entries) {
        this.maps=Map.copyOf(maps);this.entries=List.copyOf(entries);
    }
    public static MapCatalog load(Path directory,GameMap defaultMap)throws Exception {
        var manifest=new Gson().fromJson(Files.readString(directory.resolve("multiplayer-maps.json"),StandardCharsets.UTF_8),Manifest.class);
        if(manifest==null||manifest.defaultId!=DEFAULT_ID||manifest.maps==null||manifest.maps.length!=3)throw new IllegalArgumentException("map catalog");
        var maps=new LinkedHashMap<Integer,GameMap>();var entries=new ArrayList<Entry>();var files=new HashSet<String>();
        for(var entry:manifest.maps) {
            if(entry==null||(entry.id!=DEFAULT_ID&&entry.id!=FORTRESS_ID&&entry.id!=RIVER_ID)||entry.name==null||entry.name.isBlank()||entry.description==null||entry.file==null
                ||!entry.file.matches("[A-Za-z0-9_-]+\\.json")||!files.add(entry.file)||maps.containsKey(entry.id))throw new IllegalArgumentException("map catalog entry");
            var map=entry.id==DEFAULT_ID&&defaultMap!=null?defaultMap:GameMap.load(directory.resolve(entry.file));
            map.validate();
            if(map.mapId!=entry.id||map.hash==null||map.hash.length!=32)throw new IllegalArgumentException("map catalog identity");
            maps.put(entry.id,map);entries.add(entry);
        }
        if(!maps.containsKey(DEFAULT_ID))throw new IllegalArgumentException("default map missing");
        return new MapCatalog(maps,entries);
    }
    public GameMap get(int id) {return maps.get(id);}
    public boolean contains(int id) {return maps.containsKey(id);}
    public List<Entry> entries() {return entries;}
}
