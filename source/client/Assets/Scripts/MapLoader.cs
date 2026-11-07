using System;
using System.IO;
using UnityEngine;
namespace Tank  {
    public static class MapLoader  {
        public static ClientCore CreateClient(out GameMap initial)  {
            MapCatalog.Load(Application.streamingAssetsPath);
            var defaultMap=MapCatalog.Find(MapCatalog.DefaultId);
            initial=defaultMap.Map;
            var client=new ClientCore(initial,defaultMap.Hash);
            foreach(var entry in MapCatalog.Entries)client.AddMap(entry.Map,entry.Hash);
            string assetError=null;
            foreach(string name in RoleplayContent.RequiredSprites) {
                var sprite=Resources.Load<Texture2D>("ArenaArt/"+name);
                if(sprite==null||!sprite.isReadable) {assetError=name;break;}
            }
            client.SetRoleplayAssets(assetError);
            client.SetCombatAssets(CombatFxCatalog.Preload());
            TankPalette.PreloadCombatMasks();
            return client;
        }
    }
}
