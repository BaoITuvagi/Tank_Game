using UnityEngine;
namespace Tank  {
    public sealed class ArenaTheme  {
        public readonly Color Paper=CartoonUi.PanelColor,Ink=CartoonUi.Ink,Muted=CartoonUi.Muted,Accent=CartoonUi.Gold;
        public GUISkin Skin;
        internal static Font GlyphFont {get;private set;}
        public GUIStyle Hero,Title,Section,Text,Hint,White,Secondary,Primary,Chip,Header,Name;
        Texture2D grass,sand,road;
        public void Init()  {
            if(Skin!=null)return;
            if(GlyphFont==null)GlyphFont=GUI.skin.font;
            Skin=Object.Instantiate(GUI.skin);
            Skin.font=CartoonUi.BodyFont;
            Skin.settings.cursorColor=Ink;
            Skin.settings.cursorFlashSpeed=.7f;
            Skin.settings.selectionColor=new Color(1,.76f,.29f,.45f);
            Text=CartoonUi.Label(16,CartoonUi.Text);
            Name=new GUIStyle(Text) {
                fontStyle=FontStyle.Bold
            };
            Title=CartoonUi.Label(30,CartoonUi.Text,true);
            Section=CartoonUi.Label(20,CartoonUi.Text,true);
            Hint=CartoonUi.Label(13,Muted);
            White=CartoonUi.Label(15,CartoonUi.Text);
            Header=CartoonUi.Label(30,CartoonUi.Text,true);
            Hero=CartoonUi.Label(64,CartoonUi.Text,true);
            Primary=CartoonUi.Button(Accent,16,40);
            Secondary=CartoonUi.Button(CartoonUi.Cream,15,40);
            Chip=new GUIStyle(Hint) {
                alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold
            };
            Skin.label=Text;
            Skin.button=Primary;
            grass=Resources.Load<Texture2D>("ArenaArt/tileGrass1");
            sand=Resources.Load<Texture2D>("ArenaArt/tileSand1");
            road=Resources.Load<Texture2D>("ArenaArt/tileSand_roadEast");
        }
        public void Background(float width,float height)  {
            ArenaRenderer.Fill(new Rect(0,0,width,height),CartoonUi.Backdrop);
            ArenaRenderer.Fill(new Rect(0,0,width,68),CartoonUi.PanelDark);
        }
        public void Card(Rect rect,string title,string subtitle=null)  {
            CartoonUi.Panel(rect,Paper);
            GUI.Label(new Rect(rect.x+24,rect.y+18,rect.width-48,42),title,Title);
            if(subtitle!=null)GUI.Label(new Rect(rect.x+24,rect.y+65,rect.width-48,42),subtitle,Hint);
        }
        public void HeroScene(Rect rect)  {
            GUI.Label(new Rect(rect.x,rect.y,rect.width,190),"TANK\nBATTLE",Hero);
            var area=new Rect(rect.x,rect.y+270,rect.width,210);
            GUI.BeginGroup(area);
            for(int row=0;row<4;row++)for(int col=0;col<8;col++) {
                GUI.color=new Color(.62f,.72f,.5f);
                GUI.DrawTexture(new Rect(col*64,row*64,64,64),col<3?grass:sand);
            }
            GUI.color=new Color(.82f,.87f,.71f);
            for(int col=0;col<8;col++)GUI.DrawTexture(new Rect(col*64,96,64,64),road);
            GUI.EndGroup();
            GUI.color=Color.white;
            Matrix4x4 saved=GUI.matrix;
            GuiTransforms.Rotate(65,new Vector2(area.x+150,area.y+118));
            GUI.DrawTexture(new Rect(area.x+99,area.y+67,102,102),TankPalette.Body(2));
            GUI.DrawTexture(new Rect(area.x+138,area.y+42,24,86),TankPalette.Barrel(2));
            GUI.matrix=saved;
            GuiTransforms.Rotate(-45,new Vector2(area.x+333,area.y+135));
            GUI.DrawTexture(new Rect(area.x+295,area.y+97,76,76),TankPalette.Body(1));
            GUI.DrawTexture(new Rect(area.x+324,area.y+81,18,64),TankPalette.Barrel(1));
            GUI.matrix=saved;
        }
        public void Dispose() {
            if(Skin!=null)Object.Destroy(Skin);
            Skin=null;
        }
    }
}
