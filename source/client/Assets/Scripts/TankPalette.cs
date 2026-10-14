using UnityEngine;
namespace Tank  {
    public static class TankPalette  {
        public static readonly string[] Names= {
            "Xanh dương","Đỏ","Xanh lá","Vàng","Tím","Cam","Ngọc lam","Hồng"
        };
        public static readonly Color[] Colors= {
            new Color(.2f,.64f,1),new Color(1,.27f,.3f),new Color(.28f,.87f,.4f),new Color(1,.83f,.25f),new Color(.67f,.4f,1),new Color(1,.52f,.18f),new Color(.15f,.88f,.85f),new Color(1,.41f,.72f)
        };
        static Texture2D[] bodies=new Texture2D[8],barrels=new Texture2D[8];
        public static Texture2D Body(int slot) {
            return Texture(slot,false);
        }
        public static Texture2D Barrel(int slot) {
            return Texture(slot,true);
        }
        static Texture2D Texture(int slot,bool barrel)  {
            slot=Mathf.Clamp(slot,0,7);
            var cache=barrel?barrels:bodies;
            if(cache[slot]!=null)return cache[slot];
            var source=Resources.Load<Texture2D>("ArenaArt/"+(barrel?"tankDark_barrel2_outline":"tankBody_dark_outline"));
            var pixels=source.GetPixels();
            Color tint=Colors[slot];
            for(int i=0;i<pixels.Length;i++)  {
                Color p=pixels[i];
                float value=Mathf.Max(p.r,Mathf.Max(p.g,p.b));
                // Retain outlines and tread detail; recolor the armor while preserving its highlights.
                if(p.a>0&&value>.27f) {
                    Color armor=tint*Mathf.Clamp(value*2.25f,.45f,1.3f);
                    if(value>.65f)armor=Color.Lerp(armor,Color.white,(value-.65f)*1.5f);
                    armor.a=p.a;
                    pixels[i]=armor;
                }
            }
            var texture=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false) {
                filterMode=FilterMode.Bilinear,hideFlags=HideFlags.HideAndDontSave
            };
            texture.SetPixels(pixels);
            texture.Apply(false,true);
            cache[slot]=texture;
            return texture;
        }
    }
}
