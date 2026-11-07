using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
namespace Tank {
    /// <summary>Names, files and ordering come from the same manifest as the server.</summary>
    public static class MapCatalog {
        public const int DefaultId=2,FortressId=3,RiverId=4;
        [Serializable] public sealed class Entry {
            public int id;
            public string name,file,description;
            [NonSerialized] public GameMap Map;
            [NonSerialized] public byte[] Hash;
        }
        [Serializable] sealed class Manifest {public int defaultId;public Entry[] maps;}
        public static IReadOnlyList<Entry> Entries {get;private set;}=Array.AsReadOnly(new Entry[0]);
        public static void Load(string directory) {
            var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Path.Combine(directory,"multiplayer-maps.json"),Encoding.UTF8));
            if(manifest==null||manifest.defaultId!=DefaultId||manifest.maps==null||manifest.maps.Length!=3)throw new InvalidDataException("map catalog");
            var ids=new HashSet<int>();var files=new HashSet<string>();
            foreach(var entry in manifest.maps) {
                if(entry==null||!IsSelectable(entry.id)||string.IsNullOrWhiteSpace(entry.name)||entry.description==null||entry.file==null
                    ||!System.Text.RegularExpressions.Regex.IsMatch(entry.file,"\\A[A-Za-z0-9_-]+\\.json\\z")||!files.Add(entry.file)||!ids.Add(entry.id))throw new InvalidDataException("map catalog entry");
                byte[] raw=File.ReadAllBytes(Path.Combine(directory,entry.file));
                entry.Map=JsonUtility.FromJson<GameMap>(Encoding.UTF8.GetString(raw));
                Validate(entry.Map,entry.id);
                using(var sha=SHA256.Create())entry.Hash=sha.ComputeHash(raw);
            }
            if(!ids.Contains(DefaultId))throw new InvalidDataException("default map missing");
            Entries=Array.AsReadOnly(manifest.maps);
        }
        static bool Finite(float v) {return !float.IsNaN(v)&&!float.IsInfinity(v);}
        static void Validate(GameMap map,int id) {
            if(map==null||map.mapId!=id||!Finite(map.width+map.height)||map.width<40||map.height<40||map.width>128||map.height>128
                ||map.walls==null||map.bricks==null||map.water==null||map.spawns==null||map.bricks.Length>64||map.spawns.Length<2)throw new InvalidDataException("map limits");
            for(int i=0;i<map.bricks.Length;i++)if(map.bricks[i]==null||map.bricks[i].id!=i)throw new InvalidDataException("brick IDs");
            foreach(var group in new[]{map.walls,map.bricks,map.water})foreach(var r in group)
                if(r==null||!Finite(r.x+r.y+r.w+r.h)||r.w<=0||r.h<=0||r.x<0||r.y<0||r.x+r.w>map.width||r.y+r.h>map.height)throw new InvalidDataException("rectangle");
            var hp=new byte[64];for(int i=0;i<hp.Length;i++)hp[i]=(byte)GameRules.BrickMaxHp;
            foreach(var p in map.spawns)if(p==null||!Finite(p.x+p.y)||p.x<.4f||p.y<.4f||p.x>map.width-.4f||p.y>map.height-.4f||map.Blocked(p.x,p.y,hp))throw new InvalidDataException("spawn");
            map.ValidateItemSpawns();
        }
        public static Entry Find(int id) {foreach(var entry in Entries)if(entry.id==id)return entry;return null;}
        public static bool IsSelectable(int id) {return id==DefaultId||id==FortressId||id==RiverId;}
        public static string Name(int id) {var entry=Find(id);return entry==null?"Bản đồ "+id:entry.name;}
    }
}
