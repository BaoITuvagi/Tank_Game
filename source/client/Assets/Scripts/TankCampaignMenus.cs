using UnityEngine;
using Tank.Campaign;

namespace Tank {
    public partial class TankApp {
        int campaignPage,campaignChoice=-1,campaignAccountRevision;
        Vector2 campaignScroll;
        readonly CampaignPresentation campaignPresentation=new CampaignPresentation();
        int queuedCampaignAction=-1;
        double nextCampaignRepeat;
        readonly int[] campaignNations={0,1,5};
        readonly string[] campaignNames={"Mỹ","Liên Xô","Ý"};
        readonly string[] campaignSkills={"Oanh tạc chiến lược\n60 sát thương trong bán kính 2 ô quanh mục tiêu gần nhất.","Bão hỏa tiễn\n45 sát thương trong bán kính 3 ô quanh mục tiêu gần nhất.","Siêu pháo tụ lực\n120 sát thương lên mục tiêu gần nhất."};
        bool CampaignOpen {get{return campaignPage!=0;}}
        void SetupCampaignPreview() {
            if(!IsMenuPreview)return;
            string page=UxTestDriver.Arg("--menu-preview","");
            if(!page.StartsWith("campaign"))return;
            client.Phase=ClientPhase.LOBBY;client.Nickname="Chỉ huy";
            campaignPage=page=="campaign-ranking"?2:page=="campaign-play"||page=="campaign-results"||page=="campaign-motion"||page=="campaign-layout"?3:1;campaignChoice=0;
            client.Campaign.Rankings.Add(new Ranking{Name="Chỉ huy Việt",Score=10000});
            client.Campaign.Rankings.Add(new Ranking{Name="Đội giải cứu",Score=2500});client.Campaign.TotalGold=1240;
            if(campaignPage==3){
                var run=new RunState{Id="preview",Nation=0,X=6,Y=506,Distance=506,Hp=75,Facing=0,Shot=4,Skill=32,Gold=185,Kills=28,Shields=2,DoubleHits=3,Bosses=1,Bottom=499,TotalGold=1240,Terrain=new byte[247],Message="Tiến về phía bắc. Trùm tiếp theo tại 1.000 m.",SaveStatus=""};
                for(int y=0;y<19;y++)for(int x=0;x<13;x++)if(x<4||x>8)run.Terrain[y*13+x]=(byte)((x+y)%9==0?1:0);
                run.Enemies.Add(new Actor{Id=1,X=6,Y=510,Hp=46,MaxHp=46});run.Enemies.Add(new Actor{Id=2,X=8,Y=512,Hp=180,MaxHp=180,Boss=2});run.Loot.Add(new Pickup{X=6,Y=508,Kind=3});run.Loot.Add(new Pickup{X=5,Y=507,Kind=0});
                if(page=="campaign-results"){run.Finished=true;run.Message="Xe đã bị phá hủy. Chiến dịch kết thúc.";run.Hp=0;run.SaveStatus="Đã lưu kết quả.";}
                client.Campaign.Run=run;
                string fixture=UxTestDriver.Arg("--campaign-map-fixture","");
                if(!string.IsNullOrEmpty(fixture)){var packet=Protocol.Decode(System.IO.File.ReadAllBytes(fixture),false);client.Campaign.Read(new WireReader(packet.Payload));}
                string facing=UxTestDriver.Arg("--campaign-preview-facing","");int direction;
                if(int.TryParse(facing,out direction)&&direction>=0&&direction<4)client.Campaign.Run.Facing=direction;
            }
        }
        void CampaignEntryButtons() {
            if(Button("Chiến dịch",true,true)){campaignPage=1;campaignScroll=Vector2.zero;}
            if(Button("BXH chiến dịch",true,true)){campaignPage=2;campaignScroll=Vector2.zero;if(client.Phase==ClientPhase.LOBBY)client.CampaignCommand(3);}
        }
        void UpdateCampaign() {
            double now=Time.unscaledTimeAsDouble;
            if(campaignAccountRevision!=client.AccountRevision){campaignAccountRevision=client.AccountRevision;campaignPage=0;campaignPresentation.Reset();queuedCampaignAction=-1;}
            var run=client.Campaign.Run;
            campaignPresentation.Observe(run,now);
            if(!CampaignOpen||client.Phase!=ClientPhase.LOBBY||!GameWindowFocus.IsForeground||AnyDialog){queuedCampaignAction=-1;return;}
            if(campaignPage!=3||run==null||run.Finished)return;
            int action=-1;
            if(Input.GetKeyDown(KeyCode.W)||Input.GetKeyDown(KeyCode.UpArrow))action=0;
            else if(Input.GetKeyDown(KeyCode.D)||Input.GetKeyDown(KeyCode.RightArrow))action=1;
            else if(Input.GetKeyDown(KeyCode.S)||Input.GetKeyDown(KeyCode.DownArrow))action=2;
            else if(Input.GetKeyDown(KeyCode.A)||Input.GetKeyDown(KeyCode.LeftArrow))action=3;
            else if(Input.GetKeyDown(KeyCode.I))action=4;
            else if(Input.GetKeyDown(KeyCode.J))action=5;
            if(action>=0)queuedCampaignAction=action;
            if(client.Campaign.Pending||campaignPresentation.Busy(now))return;
            action=queuedCampaignAction;queuedCampaignAction=-1;
            if(action<0&&now>=nextCampaignRepeat) {
                if(Input.GetKey(KeyCode.W)||Input.GetKey(KeyCode.UpArrow))action=0;
                else if(Input.GetKey(KeyCode.D)||Input.GetKey(KeyCode.RightArrow))action=1;
                else if(Input.GetKey(KeyCode.S)||Input.GetKey(KeyCode.DownArrow))action=2;
                else if(Input.GetKey(KeyCode.A)||Input.GetKey(KeyCode.LeftArrow))action=3;
            }
            if(action>=0){client.CampaignCommand(1,action);nextCampaignRepeat=now+.18;}
        }
        void DrawCampaign() {
            if(campaignPage==3&&client.Phase==ClientPhase.LOBBY&&client.Campaign.Run!=null){DrawCampaignRun(client.Campaign.Run);return;}
            GUI.Label(new Rect(24,17,500,40),"TANK BATTLE · CHIẾN DỊCH",menu.Header);
            Rect area=Content;
            menu.Card(area,campaignPage==2?"Bảng xếp hạng chiến dịch":campaignPage==3?"Chiến dịch giải cứu":"Chọn chỉ huy");
            if(client.Phase!=ClientPhase.LOBBY) {
                GUILayout.BeginArea(new Rect(area.x+24,area.y+84,area.width-48,area.height-108));
                GUILayout.Label("Đăng nhập tài khoản để chơi và lưu vàng, điểm chiến dịch.",menu.Text);
                if(Button("Về đăng nhập"))campaignPage=0;
                GUILayout.EndArea();return;
            }
            GUILayout.BeginArea(new Rect(area.x+24,area.y+78,area.width-48,area.height-102));
            campaignScroll=GUILayout.BeginScrollView(campaignScroll);
            if(campaignPage==2){
                GUILayout.Label("Tổng vàng chiến dịch: "+client.Campaign.TotalGold,menu.Name);
                GUILayout.BeginHorizontal();GUILayout.Label("Hạng",menu.Name,GUILayout.Width(60));GUILayout.Label("Tên người chơi",menu.Name,GUILayout.ExpandWidth(true));GUILayout.Label("Điểm (m)",new GUIStyle(menu.Name){alignment=TextAnchor.MiddleRight},GUILayout.Width(100));GUILayout.EndHorizontal();
                for(int i=0;i<client.Campaign.Rankings.Count;i++) {
                    var row=client.Campaign.Rankings[i];
                    GUILayout.BeginHorizontal();GUILayout.Label((i+1).ToString(),GUILayout.Width(60));GUILayout.Label(row.Name,GUILayout.ExpandWidth(true));GUILayout.Label(row.Score.ToString(),new GUIStyle(menu.Text){alignment=TextAnchor.MiddleRight},GUILayout.Width(100));GUILayout.EndHorizontal();
                }
                if(client.Campaign.Rankings.Count==0)GUILayout.Label(client.Campaign.Pending?"Đang tải…":"Chưa có kết quả chiến dịch.");
                if(Button("Làm mới",!client.Campaign.Pending))client.CampaignCommand(3);
            }else {
                GUILayout.Label("Tổng thống Đô-na-tri và gia đình đã bị tổ chức Vu-a-lo bắt cóc. Hãy lên đường giải cứu!",menu.Text);
                float skillHeight=0;
                for(int i=0;i<3;i++)skillHeight=Mathf.Max(skillHeight,menu.Hint.CalcHeight(new GUIContent("J · "+campaignSkills[i]+"\nTự chọn mục tiêu trong 12 ô · hồi 50 lượt"),(area.width-80)/3-24));
                GUILayout.Space(16);GUILayout.BeginHorizontal();
                for(int i=0;i<3;i++) {
                    GUILayout.BeginVertical(GUI.skin.box,GUILayout.Width((area.width-80)/3));
                    int nation=campaignNations[i];
                    GUILayout.Label(campaignNames[i],menu.Section);
                    Rect picture=GUILayoutUtility.GetRect(80,96,GUILayout.ExpandWidth(true));
                    CampaignTank(new Rect(picture.center.x-36,picture.y,72,88),nation,0);
                    GUILayout.Label("100 HP · Công cơ bản 20\nI: đạn 40 · hồi 10 lượt",menu.Text);
                    GUILayout.Label("J · "+campaignSkills[i]+"\nTự chọn mục tiêu trong 12 ô · hồi 50 lượt",menu.Hint,GUILayout.Height(skillHeight));
                    if(GUILayout.Button((campaignChoice==i?"✓ Đã chọn ":"Chọn xe ")+campaignNames[i],campaignChoice==i?menu.Primary:menu.Secondary))campaignChoice=i;
                    GUILayout.EndVertical();
                }
                GUILayout.EndHorizontal();
                if(Button("Vào chiến dịch",campaignChoice>=0&&!client.Campaign.Pending)) {client.CampaignCommand(0,campaignNations[campaignChoice]);campaignPage=3;}
                if(client.Campaign.Run!=null&&!client.Campaign.Run.Finished&&Button("Tiếp tục chuyến đi"))campaignPage=3;
            }
            GUILayout.Label(client.Campaign.Error,menu.Hint);
            if(Button("Về menu",!client.Campaign.Pending,true))campaignPage=0;
            GUILayout.EndScrollView();GUILayout.EndArea();
        }
        void DrawCampaignRun(RunState run) {
            const int rows=13;
            float cell=Mathf.Max(1,Mathf.Min((viewWidth-32)/13,(viewHeight-128)/rows));
            float bw=13*cell,bh=rows*cell,bx=(viewWidth-bw)/2,by=64+(viewHeight-128-bh)/2;
            double now=Time.unscaledTimeAsDouble;
            campaignPresentation.Observe(run,now);
            float bottom=campaignPresentation.CameraBottom(rows,now);
            for(float py=64;py<viewHeight-64;py+=cell)for(float px=0;px<viewWidth;px+=cell) {
                GUI.color=new Color(.48f,.57f,.46f);
                CampaignArt.Surroundings(new Rect(px,py,cell,Mathf.Min(cell,viewHeight-64-py)),Mathf.FloorToInt(px/cell),Mathf.FloorToInt(py/cell));
            }
            GUI.color=Color.white;
            Vector2 shake=campaignPresentation.Shake(now);
            ArenaRenderer.Fill(new Rect(bx-3,by-3,bw+6,bh+6),new Color(.06f,.12f,.08f));
            GUI.BeginGroup(new Rect(bx+shake.x,by+shake.y,bw,bh));
            System.Func<Vector2,Rect> tileAt=pos=>new Rect(pos.x*cell,(rows-1-(pos.y-bottom))*cell,cell,cell);
            for(int y=Mathf.FloorToInt(bottom);y<=Mathf.CeilToInt(bottom)+rows;y++)for(int col=0;col<13;col++) {
                CampaignArt.Ground(tileAt(new Vector2(col,y)),col,y,campaignPresentation.Terrain(col,y));
            }
            foreach(var l in run.Loot)CampaignArt.Pickup(tileAt(new Vector2(l.X,l.Y)),l.Kind);
            foreach(var e in campaignPresentation.Actors()) {
                float opacity=campaignPresentation.Opacity(e.Id,now);if(opacity<=0)continue;
                Vector2 pos=campaignPresentation.Enemy(e,now),direction=campaignPresentation.Player(now)-pos;
                int facing=Mathf.Abs(direction.x)>Mathf.Abs(direction.y)?(direction.x>=0?1:3):(direction.y>=0?0:2);
                Rect tile=tileAt(pos);
                Color tint=campaignPresentation.Tint(e.Id,now);tint.a=opacity;GUI.color=tint;
                CampaignArt.Enemy(tile,e.Boss,facing);GUI.color=Color.white;
                if(run.Enemies.Exists(actor=>actor.Id==e.Id)){
                    ArenaRenderer.Fill(new Rect(tile.x,tile.y,cell*Mathf.Clamp01((float)e.Hp/e.MaxHp),4),e.Boss>0?Color.yellow:Color.red);
                    if(e.Boss>0)GUI.Label(tile,"B"+e.Boss,menu.Chip);
                }
            }
            Rect player=tileAt(campaignPresentation.Player(now));
            GUI.color=campaignPresentation.Tint(0,now);
            CampaignTank(player,run.Nation,run.Facing);GUI.color=Color.white;
            CampaignHealthBar(new Rect(player.x+3,player.y,cell-6,5),run.Hp);
            campaignPresentation.DrawEffects(tileAt,cell,now,menu.White);
            GUI.EndGroup();

            ArenaRenderer.Fill(new Rect(0,0,viewWidth,60),new Color(.04f,.1f,.08f,.97f));
            ArenaRenderer.Fill(new Rect(0,viewHeight-60,viewWidth,60),new Color(.04f,.1f,.08f,.97f));
            float healthWidth=Mathf.Clamp(viewWidth*.22f,120,250);
            CampaignHealthBar(new Rect(18,12,healthWidth,18),run.Hp);
            GUI.Label(new Rect(18,33,healthWidth,22),"HP "+run.Hp+" / 100",menu.White);
            GUI.Label(new Rect(healthWidth+38,10,180,26),run.Distance+" / 10.000 m",menu.White);
            GUI.Label(new Rect(healthWidth+38,33,180,22),"Vàng "+run.Gold+" · Trùm "+run.Bosses+"/20",menu.White);
            float footerWidth=Mathf.Min(580,viewWidth-32),footerX=(viewWidth-footerWidth)/2;
            bool enabled=GUI.enabled;
            GUI.enabled=enabled&&!client.Campaign.Pending&&!run.Finished&&!campaignPresentation.Busy(now);
            if(GUI.Button(new Rect(footerX,viewHeight-50,(footerWidth-12)/2,40),"I · Bắn"+(run.Shot>0?" · "+run.Shot:""),run.Shot==0?menu.Primary:menu.Secondary)&&run.Shot==0)client.CampaignCommand(1,4);
            if(GUI.Button(new Rect(footerX+(footerWidth+12)/2,viewHeight-50,(footerWidth-12)/2,40),"J · Kỹ năng"+(run.Skill>0?" · "+run.Skill:""),run.Skill==0?menu.Primary:menu.Secondary)&&run.Skill==0)client.CampaignCommand(1,5);
            if(GUI.Button(new Rect(viewWidth-172,10,154,40),"Kết thúc & lưu",menu.Secondary))client.CampaignCommand(2);
            GUI.enabled=enabled;
            if(viewWidth>=900)GUI.Label(new Rect(healthWidth+230,12,viewWidth-healthWidth-420,40),"×2 "+run.DoubleHits+" · Khiên "+run.Shields+" · Hạ địch "+run.Kills,menu.White);
            string feedback=client.Campaign.Error;
            if(string.IsNullOrEmpty(feedback)&&run.Turn>0&&!run.Finished)feedback=run.Message;
            if(!string.IsNullOrEmpty(feedback)) {
                Rect notice=new Rect(bx+8,by+8,bw-16,48);
                ArenaRenderer.Fill(notice,new Color(.04f,.1f,.08f,.92f));
                GUI.Label(new Rect(notice.x+10,notice.y+7,notice.width-20,34),feedback,menu.White);
            }
            if(run.Finished&&campaignPresentation.ResultsReady(now))DrawCampaignResults(run);
        }
        void CampaignHealthBar(Rect rect,int hp) {
            ArenaRenderer.Fill(rect,new Color(.12f,.16f,.15f));
            ArenaRenderer.Fill(new Rect(rect.x+1,rect.y+1,(rect.width-2)*Mathf.Clamp01(hp/100f),Mathf.Max(1,rect.height-2)),hp<=25?new Color(.95f,.28f,.25f):new Color(.25f,.86f,.55f));
        }
        void DrawCampaignResults(RunState run) {
            ArenaRenderer.Fill(new Rect(0,0,viewWidth,viewHeight),new Color(0,0,0,.7f));
            float width=Mathf.Min(520,viewWidth-32),height=Mathf.Min(410,viewHeight-32);
            Rect card=new Rect((viewWidth-width)/2,(viewHeight-height)/2,width,height);
            menu.Card(card,run.Victory?"Giải cứu thành công!":"Kết thúc chuyến đi");
            GUILayout.BeginArea(new Rect(card.x+24,card.y+80,card.width-48,card.height-104));
            campaignScroll=GUILayout.BeginScrollView(campaignScroll);
            GUILayout.Label("Điểm: "+run.Distance+" m\nVàng nhận: "+run.Gold+"\nTổng vàng: "+run.TotalGold+"\n"+run.SaveStatus,menu.Text);
            GUILayout.Space(14);
            if(run.SaveStatus=="Đã lưu kết quả.") {
                if(Button("Chọn xe / Chơi lại"))campaignPage=1;
                if(Button("Về menu",true,true))campaignPage=0;
            }else if(Button("Lưu lại",!client.Campaign.Pending,true))client.CampaignCommand(4);
            GUILayout.Label(client.Campaign.Error,menu.Hint);
            GUILayout.EndScrollView();GUILayout.EndArea();
        }
        void CampaignTank(Rect rect,int nation,int facing) {
            CampaignArt.Tank(rect,nation,facing);
        }
    }
}
