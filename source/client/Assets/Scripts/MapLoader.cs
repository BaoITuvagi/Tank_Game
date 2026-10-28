using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
namespace Tank  {
    public static class MapLoader  {
        public static ClientCore CreateClient(out GameMap initial)  {
            byte[] raw=File.ReadAllBytes(Path.Combine(Application.streamingAssetsPath,"map-large.json"));
            initial=JsonUtility.FromJson<GameMap>(System.Text.Encoding.UTF8.GetString(raw));
            initial.ValidateItemSpawns();
            byte[] hash;
            using(var sha=SHA256.Create())hash=sha.ComputeHash(raw);
            var client=new ClientCore(initial,hash);
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
