using UnityEngine;
namespace Tank  {
    public sealed class MenuTheme  {
        public readonly Color Paper=new Color(.94f,.94f,.88f),Ink=new Color(.12f,.2f,.19f),Muted=new Color(.36f,.45f,.41f),Accent=new Color(.18f,.51f,.36f);
        public GUISkin Skin;
        public GUIStyle Hero,Title,Section,Text,Hint,White,Secondary,Primary,Chip,Header,Name;
        Texture2D dark,light,hover,field,fieldFocus;
        Texture2D grass,sand,road;
        Texture2D Solid(Color color) {
            var t=new Texture2D(1,1) {
                hideFlags=HideFlags.HideAndDontSave
            };
            t.SetPixel(0,0,color);
            t.Apply();
            return t;
        }
        Texture2D InputBackground(Color border) {
            var t=new Texture2D(32,32) {
                hideFlags=HideFlags.HideAndDontSave
            };
            var pixels=new Color[1024];
            for(int y=0;y<32;y++)for(int x=0;x<32;x++)pixels[y*32+x]=x<2||y<2||x>=30||y>=30?border:new Color(.99f,.99f,.96f);
            t.SetPixels(pixels);
            t.Apply();
            return t;
        }
        public void Init()  {
            if(Skin!=null)return;
            Skin=Object.Instantiate(GUI.skin);
            dark=Solid(Accent);
            hover=Solid(new Color(.23f,.62f,.43f));
            light=Solid(new Color(.84f,.89f,.81f));
            field=InputBackground(new Color(.65f,.72f,.65f));
            fieldFocus=InputBackground(Accent);
            Skin.settings.cursorColor=Ink;
            Skin.settings.cursorFlashSpeed=.7f;
            Skin.settings.selectionColor=new Color(.3f,.65f,.9f,.45f);
            Text=new GUIStyle(Skin.label) {
                fontSize=16,wordWrap=true,padding=new RectOffset(0,0,0,0),clipping=TextClipping.Clip
            };
            Text.normal.textColor=Ink;
            Name=new GUIStyle(Text) {
                fontStyle=FontStyle.Bold
            };
            Title=new GUIStyle(Text) {
                fontSize=30,fontStyle=FontStyle.Bold
            };
            Section=new GUIStyle(Text) {
                fontSize=20,fontStyle=FontStyle.Bold
            };
            Hint=new GUIStyle(Text) {
                fontSize=13
            };
            Hint.normal.textColor=Muted;
            White=new GUIStyle(Text) {
                fontSize=15
            };
            White.normal.textColor=new Color(.82f,.91f,.82f);
            Header=new GUIStyle(White) {
                fontSize=30,fontStyle=FontStyle.Bold
            };
            Hero=new GUIStyle(White) {
                fontSize=64,fontStyle=FontStyle.Bold
            };
            Primary=new GUIStyle(Skin.button) {
                fontSize=16,fontStyle=FontStyle.Bold,fixedHeight=40,padding=new RectOffset(12,12,8,8),margin=new RectOffset(0,0,6,6)
            };
            Primary.normal.background=dark;
            Primary.hover.background=hover;
            Primary.active.background=dark;
            Primary.onNormal.background=dark;
            Primary.onHover.background=hover;
            Primary.onActive.background=dark;
            Primary.normal.textColor=Primary.hover.textColor=Primary.active.textColor=Primary.onNormal.textColor=Color.white;
            Secondary=new GUIStyle(Primary);
            Secondary.normal.background=light;
            Secondary.hover.background=field;
            Secondary.normal.textColor=Secondary.hover.textColor=Ink;
            Secondary.onNormal.background=dark;
            Secondary.onNormal.textColor=Color.white;
            Chip=new GUIStyle(Hint) {
                alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold
            };
            Skin.label=Text;
            Skin.button=Primary;
            Skin.textField=new GUIStyle(Skin.textField) {
                fontSize=16,fixedHeight=40,padding=new RectOffset(12,12,8,8),margin=new RectOffset(0,0,5,7)
            };
            Skin.textField.normal.background=field;
            Skin.textField.focused.background=fieldFocus;
            Skin.textField.border=new RectOffset(2,2,2,2);
            Skin.textField.wordWrap=false;
            Skin.textField.alignment=TextAnchor.MiddleLeft;
            Skin.textField.normal.textColor=Skin.textField.focused.textColor=Ink;
            Skin.box=new GUIStyle(Skin.box) {
                normal= {
                    background=light,textColor=Ink
                },padding=new RectOffset(12,12,10,10)
            };
            grass=Resources.Load<Texture2D>("ArenaArt/tileGrass1");
            sand=Resources.Load<Texture2D>("ArenaArt/tileSand1");
            road=Resources.Load<Texture2D>("ArenaArt/tileSand_roadEast");
        }
        public void Background(float width,float height)  {
            ArenaRenderer.Fill(new Rect(0,0,width,height),new Color(.09f,.19f,.16f));
            for(float py=0;py<height;py+=160)for(float px=0;px<width;px+=160) {
                GUI.color=new Color(.14f,.28f,.2f,.18f);
                GUI.DrawTexture(new Rect(px,py,160,160),grass);
            }
            GUI.color=Color.white;
            ArenaRenderer.Fill(new Rect(0,0,width,64),new Color(.07f,.14f,.12f,.95f));
        }
        public void Card(Rect rect,string title,string subtitle=null)  {
            ArenaRenderer.Fill(new Rect(rect.x+4,rect.y+5,rect.width,rect.height),new Color(0,0,0,.2f));
            ArenaRenderer.Fill(rect,Paper);
            ArenaRenderer.Fill(new Rect(rect.x,rect.y,rect.width,4),Accent);
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
    }
}
