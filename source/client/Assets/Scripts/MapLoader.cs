using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
namespace Tank  {
    public static class MapLoader  {
        public static ClientCore CreateClient(out GameMap initial)  {
            bool largePreview=Array.IndexOf(Environment.GetCommandLineArgs(),"--preview-eight")>=0;
            byte[] raw=File.ReadAllBytes(Path.Combine(Application.streamingAssetsPath,largePreview?"map-large.json":"map.json"));
            initial=JsonUtility.FromJson<GameMap>(System.Text.Encoding.UTF8.GetString(raw));
            byte[] hash;
            using(var sha=SHA256.Create())hash=sha.ComputeHash(raw);
            var client=new ClientCore(initial,hash);
            raw=File.ReadAllBytes(Path.Combine(Application.streamingAssetsPath,largePreview?"map.json":"map-large.json"));
            using(var sha=SHA256.Create())hash=sha.ComputeHash(raw);
            client.AddMap(JsonUtility.FromJson<GameMap>(System.Text.Encoding.UTF8.GetString(raw)),hash);
            string assetError=null;
            foreach(string name in RoleplayContent.RequiredSprites) {
                var sprite=Resources.Load<Texture2D>("ArenaArt/"+name);
                if(sprite==null||!sprite.isReadable) {assetError=name;break;}
            }
            client.SetRoleplayAssets(assetError);
            return client;
        }
    }
}
