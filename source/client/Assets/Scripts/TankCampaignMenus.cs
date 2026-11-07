using UnityEngine;
using Tank.Campaign;

namespace Tank {
    public partial class TankApp {
        int campaignPage,campaignChoice=-1,campaignAccountRevision;
        Vector2 campaignScroll;
        Rect campaignPointerArea;
        bool CampaignMouseInMap(){return campaignPointerArea.Contains(new Vector2(Input.mousePosition.x,Screen.height-Input.mousePosition.y));}
        readonly CampaignPresentation campaignPresentation=new CampaignPresentation();
        CampaignAudio campaignAudio;
        void EnsureCampaignAudio(){
            if(campaignAudio!=null)return;
            campaignAudio=gameObject.AddComponent<CampaignAudio>();
            campaignAudio.Bind(()=>client.Campaign.Run,()=>CampaignOpen&&campaignPage==3&&client.Phase==ClientPhase.LOBBY);
        }
        int queuedCampaignAction=-1;
        double nextCampaignRepeat;
        bool campaignInputRelease=true;
        void GateCampaignInputUntilReleased(){queuedCampaignAction=-1;campaignInputRelease=true;nextCampaignRepeat=Time.unscaledTimeAsDouble+.18;}
        bool CampaignControlHeld(){
            return ClientPreferences.Active.Held(ControlAction.Up)||ClientPreferences.Active.Held(ControlAction.Right)||ClientPreferences.Active.Held(ControlAction.Down)||ClientPreferences.Active.Held(ControlAction.Left)||ClientPreferences.Active.Held(ControlAction.CampaignFire)||ClientPreferences.Active.Held(ControlAction.CampaignSkill)||ClientPreferences.Active.Held(ControlAction.CampaignShop)||Input.GetMouseButton(0)||Input.GetMouseButton(1)||Input.GetKey(KeyCode.Escape)||Input.GetKey(KeyCode.Return)||Input.GetKey(KeyCode.KeypadEnter);
        }
        readonly int[] campaignNations={0,1,5};
        bool CampaignOpen {get{return campaignPage!=0;}}
        void SetupCampaignPreview() {
            if(!IsMenuPreview)return;
            string page=UxTestDriver.Arg("--menu-preview","");
            if(!page.StartsWith("campaign"))return;
            campaignInputRelease=false;
            client.Phase=ClientPhase.LOBBY;client.Nickname="Chỉ huy";
            campaignPage=page.StartsWith("campaign-ranking")?2:page=="campaign-shop"||page=="campaign-shop-unavailable"||page.StartsWith("campaign-play")||page.StartsWith("campaign-results")||page=="campaign-victory"||page=="campaign-end"||page=="campaign-motion"||page=="campaign-layout"?3:1;campaignChoice=0;
            client.Campaign.Rankings.Add(new Ranking{Name="Chỉ huy",Score=10000,Gold=1850,DurationMillis=325450,Rank=1,DetailsAvailable=true});
            client.Campaign.Rankings.Add(new Ranking{Name="Đội giải cứu",Score=10000,Gold=1850,DurationMillis=325450,Rank=1,DetailsAvailable=true});
            client.Campaign.Rankings.Add(new Ranking{Name="Thiết giáp",Score=10000,Gold=1850,DurationMillis=410120,Rank=2,DetailsAvailable=true});
            client.Campaign.Rankings.Add(new Ranking{Name="Trinh sát",Score=10000,Gold=1500,DurationMillis=310000,Rank=3,DetailsAvailable=true});
            client.Campaign.Rankings.Add(new Ranking{Name="Hậu cần",Score=2500,Gold=600,Rank=4,DetailsAvailable=true});client.Campaign.TotalGold=1240;
            if(campaignPage==3){
                var run=new RunState{Id="preview",Nation=0,X=6,Y=506,Distance=506,Hp=75,Facing=0,Shot=4,Skill=12,Gold=185,Kills=28,Shields=2,DoubleHits=3,Bosses=1,Bottom=499,TotalGold=1240,ShopAvailable=true,Prices=new[]{30,46,55},NationStatsAvailable=true,SkillCooldown=24,Template=6,Biome=2,RegionName="Ngã tư tiền đồn",Terrain=new byte[247],Message="Tiến về phía bắc. Trùm tiếp theo tại 1.000 m.",SaveStatus=""};
                for(int y=0;y<19;y++)for(int x=0;x<13;x++)if(x<4||x>8)run.Terrain[y*13+x]=(byte)((x+y)%9==0?1:0);
                run.Enemies.Add(new Actor{Id=1,X=6,Y=510,Hp=46,MaxHp=46});run.Enemies.Add(new Actor{Id=2,X=8,Y=512,Hp=180,MaxHp=180,Boss=2});run.Loot.Add(new Pickup{X=6,Y=508,Kind=3});run.Loot.Add(new Pickup{X=5,Y=507,Kind=0});
                if(page.StartsWith("campaign-results")||page=="campaign-victory"){
                    run.Finished=true;run.Victory=page=="campaign-victory";run.Message=run.Victory?"Đã giải cứu thành công!":"Xe đã bị phá hủy. Chiến dịch kết thúc.";
                    run.Hp=run.Victory?75:0;run.SaveStatus=page=="campaign-results-unsaved"?"Chưa lưu được. Bấm Lưu lại để thử lại.":"Đã lưu kết quả.";
                    if(run.Victory){run.Distance=10000;run.Bosses=CampaignClient.BossCount;}
                }
                client.Campaign.Run=run;
                int previewBuffs;
                if(int.TryParse(UxTestDriver.Arg("--preview-buff-count",""),out previewBuffs))run.Shields=run.DoubleHits=Mathf.Clamp(previewBuffs,0,run.MaxBuff);
                if(page=="campaign-play-ready"){run.Shot=0;run.Skill=0;}
                if(page=="campaign-play-pending")client.Campaign.Pending=true;
                string fixture=UxTestDriver.Arg("--campaign-map-fixture","");
                if(!string.IsNullOrEmpty(fixture)){var packet=Protocol.Decode(System.IO.File.ReadAllBytes(fixture),false);client.Campaign.Read(new WireReader(packet.Payload));}
                if(page=="campaign-shop"||page=="campaign-shop-unavailable")campaignShopOpen=true;
                if(page=="campaign-shop-unavailable"){run.Gold=12;run.Hp=run.MaxHp;run.Shields=run.MaxBuff;}
                if(page=="campaign-end")campaignEndConfirm=true;
                string facing=UxTestDriver.Arg("--campaign-preview-facing","");int direction;
                if(int.TryParse(facing,out direction)&&direction>=0&&direction<4)client.Campaign.Run.Facing=direction;
            }
            var args=System.Environment.GetCommandLineArgs();
            int previewNation;
            if(int.TryParse(UxTestDriver.Arg("--preview-nation",""),out previewNation)){
                for(int i=0;i<campaignNations.Length;i++)if(campaignNations[i]==previewNation)campaignChoice=i;
                if(client.Campaign.Run!=null){
                    var sample=client.Campaign.Run;var selected=CampaignNation.For(campaignNations[campaignChoice]);sample.Nation=selected.Id;
                    sample.MaxHp=selected.MaxHp;sample.Armor=selected.Armor;sample.ShotDamage=selected.ShotDamage;sample.MeleeDamage=selected.MeleeDamage;sample.SkillCooldown=selected.SkillCooldown;sample.SkillRange=selected.SkillRange;
                }
            }
            if(page=="campaign-ranking-empty")client.Campaign.Rankings.Clear();
            if(page=="campaign-loading")client.Campaign.Pending=true;
            if(page=="campaign-error"||System.Array.IndexOf(args,"--preview-error")>=0)client.Campaign.Error="Không xử lý được yêu cầu chiến dịch. Vui lòng kiểm tra kết nối và thử lại.";
            if(System.Array.IndexOf(args,"--preview-long-text")>=0){client.Nickname="Chỉ huy xe tăng Việt Nam";if(client.Campaign.Rankings.Count>0)client.Campaign.Rankings[0].Name=client.Nickname;}
            if(System.Array.IndexOf(args,"--preview-wide-names")>=0){client.Nickname=new string('M',24);foreach(var rank in client.Campaign.Rankings)rank.Name=client.Nickname;}
            if(System.Array.IndexOf(args,"--preview-scroll-bottom")>=0){campaignBriefingOpen=true;campaignSkillDetailsOpen=true;campaignHudScroll=campaignScroll=campaignResultScroll=campaignShopScroll=new Vector2(0,10000);}
        }
        void CampaignEntryButtons() {
            if(Button("Chiến dịch",true,true)){campaignPage=1;campaignScroll=Vector2.zero;}
            if(Button("BXH chiến dịch",true,true)){campaignPage=2;campaignScroll=Vector2.zero;if(client.Phase==ClientPhase.LOBBY)client.CampaignCommand(3);}
        }
        void UpdateCampaign() {
            double now=Time.unscaledTimeAsDouble;
            if(campaignAccountRevision!=client.AccountRevision){campaignAccountRevision=client.AccountRevision;campaignPage=0;campaignPresentation.Reset();GateCampaignInputUntilReleased();campaignShopOpen=false;campaignEndConfirm=false;campaignHudScroll=Vector2.zero;}
            // Room responses can arrive after changing the local mode screen.
            // Close presentation only; the server-owned run remains resumable.
            if(client.Phase!=ClientPhase.LOBBY){
                campaignPage=0;campaignShopOpen=false;campaignEndConfirm=false;campaignHudScroll=Vector2.zero;
                campaignPresentation.Reset();GateCampaignInputUntilReleased();return;
            }
            if(CampaignOpen)EnsureCampaignAudio();
            var run=client.Campaign.Run;
            campaignPresentation.Observe(run,now);
            if(!CampaignOpen||client.Phase!=ClientPhase.LOBBY||!GameWindowFocus.IsForeground||AnyDialog||SkillDetailsUi.InputBlocked){GateCampaignInputUntilReleased();return;}
            if(campaignPage!=3||run==null||run.Finished){campaignShopOpen=false;GateCampaignInputUntilReleased();return;}
            if(campaignEndConfirm){GateCampaignInputUntilReleased();if(Input.GetKeyDown(KeyCode.Escape))campaignEndConfirm=false;return;}
            if(campaignShopOpen){GateCampaignInputUntilReleased();if(Input.GetKeyDown(KeyCode.Escape)||ClientPreferences.Active.Pressed(ControlAction.CampaignShop,false))ToggleCampaignShop();return;}
            if(campaignInputRelease){if(CampaignControlHeld())return;campaignInputRelease=false;nextCampaignRepeat=now+.18;return;}
            if(ClientPreferences.Active.Pressed(ControlAction.CampaignShop,CampaignMouseInMap())){ToggleCampaignShop();return;}
            int action=-1;
            if(ClientPreferences.Active.Pressed(ControlAction.Up,CampaignMouseInMap()))action=0;
            else if(ClientPreferences.Active.Pressed(ControlAction.Right,CampaignMouseInMap()))action=1;
            else if(ClientPreferences.Active.Pressed(ControlAction.Down,CampaignMouseInMap()))action=2;
            else if(ClientPreferences.Active.Pressed(ControlAction.Left,CampaignMouseInMap()))action=3;
            else if(ClientPreferences.Active.Pressed(ControlAction.CampaignFire,CampaignMouseInMap()))action=4;
            else if(ClientPreferences.Active.Pressed(ControlAction.CampaignSkill,CampaignMouseInMap()))action=5;
            if(action>=0)queuedCampaignAction=action;
            if(client.Campaign.Pending||campaignPresentation.Busy(now))return;
            action=queuedCampaignAction;queuedCampaignAction=-1;
            if(action<0&&now>=nextCampaignRepeat) {
                if(ClientPreferences.Active.Held(ControlAction.Up,CampaignMouseInMap()))action=0;
                else if(ClientPreferences.Active.Held(ControlAction.Right,CampaignMouseInMap()))action=1;
                else if(ClientPreferences.Active.Held(ControlAction.Down,CampaignMouseInMap()))action=2;
                else if(ClientPreferences.Active.Held(ControlAction.Left,CampaignMouseInMap()))action=3;
            }
            if(action>=0){client.CampaignCommand(1,action);nextCampaignRepeat=now+.18;}
        }
        void DrawCampaignRun(RunState run) {
            EnsureCampaignAudio();
            const int rows=13;
            double now=Time.unscaledTimeAsDouble;
            var layout=CampaignLayout(run,now);float cell=layout.Map.width/rows,bw=layout.Map.width,bh=layout.Map.height,bx=layout.Map.x,by=layout.Map.y;
            campaignPointerArea=layout.Map;
            campaignPresentation.Observe(run,now);
            float bottom=campaignPresentation.CameraBottom(rows,now);
            for(float py=ArenaLayout.Top;py<viewHeight-24;py+=cell)for(float px=0;px<layout.Sidebar.x;px+=cell) {
                GUI.color=new Color(.48f,.57f,.46f);
                CampaignArt.Surroundings(new Rect(px,py,Mathf.Min(cell,layout.Sidebar.x-px),Mathf.Min(cell,viewHeight-24-py)),Mathf.FloorToInt(px/cell),Mathf.FloorToInt(py/cell),run.Biome);
            }
            GUI.color=Color.white;
            Vector2 shake=campaignPresentation.Shake(now);
            ArenaRenderer.Fill(new Rect(bx-3,by-3,bw+6,bh+6),new Color(.06f,.12f,.08f));
            GUI.BeginGroup(new Rect(bx+shake.x,by+shake.y,bw,bh));
            System.Func<Vector2,Rect> tileAt=pos=>new Rect(pos.x*cell,(rows-1-(pos.y-bottom))*cell,cell,cell);
            for(int y=Mathf.FloorToInt(bottom);y<=Mathf.CeilToInt(bottom)+rows;y++)for(int col=0;col<13;col++) {
                CampaignArt.EnvironmentGround(tileAt(new Vector2(col,y)),col,y,campaignPresentation.Surface(col,y),campaignPresentation.SurfaceMask(col,y,false),campaignPresentation.SurfaceMask(col,y,true));
            }
            for(int y=Mathf.CeilToInt(bottom)+rows;y>=Mathf.FloorToInt(bottom);y--)for(int col=0;col<13;col++)
                CampaignArt.EnvironmentObject(tileAt(new Vector2(col,y)),col,y,campaignPresentation.Terrain(col,y),campaignPresentation.Surface(col,y),campaignPresentation.Decoration(col,y));
            foreach(var danger in run.Dangers){
                Rect mark=tileAt(new Vector2(danger.X,danger.Y));
                ArenaRenderer.Fill(new Rect(mark.x+1,mark.y+1,mark.width-2,mark.height-2),new Color(1,.15f,.07f,.50f));
                GUI.Label(mark,"! "+(danger.Due-run.Turn),new GUIStyle(CampaignUI.SmallLight){alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold,fontSize=Mathf.Clamp((int)(cell*.4f),14,24)});
            }
            foreach(var l in run.Loot)CampaignArt.Pickup(tileAt(new Vector2(l.X,l.Y)),l.Kind);
            foreach(var e in campaignPresentation.Actors()) {
                float opacity=campaignPresentation.Opacity(e.Id,now);if(opacity<=0)continue;
                Vector2 pos=campaignPresentation.Enemy(e,now),direction=campaignPresentation.Player(now)-pos;
                int facing=Mathf.Abs(direction.x)>Mathf.Abs(direction.y)?(direction.x>=0?1:3):(direction.y>=0?0:2);
                Rect tile=tileAt(pos);
                Color tint=campaignPresentation.Tint(e.Id,now);
                if(e.Boss==0)tint*=e.Squad%3==1?new Color(1,.86f,.78f):e.Squad%3==2?new Color(.78f,.91f,1):Color.white;
                tint.a=opacity;GUI.color=tint;
                CampaignArt.Enemy(tile,e.Boss,facing,e.Role);GUI.color=Color.white;
                if(run.Enemies.Exists(actor=>actor.Id==e.Id)){
                    ArenaRenderer.Fill(new Rect(tile.x,tile.y,cell*Mathf.Clamp01((float)e.Hp/e.MaxHp),4),e.Boss>0?Color.yellow:Color.red);
                    if(e.Boss>0)GUI.Label(tile,"B"+e.Boss+(e.BossPhase==2?" · II":" · I"),new GUIStyle(CampaignUI.SmallLight){alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold});
                    else {
                        string role=e.Role==EnemyRole.Ranged?"X":e.Role==EnemyRole.Support?"H":"C";
                        var badge=new Rect(tile.xMax-cell*.35f,tile.y+5,cell*.35f,Mathf.Max(16,cell*.32f));
                        ArenaRenderer.Fill(badge,new Color(.04f,.1f,.08f,.85f));
                        GUI.Label(badge,role,new GUIStyle(CampaignUI.SmallLight){alignment=TextAnchor.MiddleCenter,fontSize=Mathf.Clamp((int)(cell*.25f),10,16)});
                        if(e.Guard>0)ArenaRenderer.Fill(new Rect(tile.x,tile.y+4,cell*e.Guard/10f,3),new Color(.35f,.85f,1));
                    }
                }
            }
            Rect player=tileAt(campaignPresentation.Player(now));
            GUI.color=campaignPresentation.Tint(0,now);
            CampaignTank(player,run.Nation,run.Facing);GUI.color=Color.white;
            campaignPresentation.DrawEffects(tileAt,cell,now,CampaignUI.SmallLight);
            Rect mapViewport=new Rect(0,0,bw,bh);
            DrawCampaignRegionBanner(mapViewport,now);
            // Draw health after world objects and combat FX so it remains legible.
            CampaignHealthBar(CampaignHudGeometry.PlayerHealthBounds(player,mapViewport),run.Hp,run.MaxHp);
            GUI.EndGroup();

            bool hudEnabled=GUI.enabled;
            bool hudBlocked=!hudEnabled||campaignShopOpen||campaignEndConfirm||run.Finished&&campaignPresentation.ResultsReady(now);
            GUI.enabled=!hudBlocked;
            DrawCampaignHud(run,now);
            GUI.enabled=hudEnabled;
        }
        // The common header must be drawn before these full-window overlays.
        void DrawCampaignOverlays(){
            if(!CampaignOpen||campaignPage!=3||client.Phase!=ClientPhase.LOBBY)return;
            var run=client.Campaign.Run;if(run==null)return;
            if(run.Finished&&campaignPresentation.ResultsReady(Time.unscaledTimeAsDouble))DrawCampaignResults(run);
            else if(campaignShopOpen)DrawCampaignShop(run);
            else if(campaignEndConfirm)DrawCampaignEndConfirmation();
        }
        void CampaignHealthBar(Rect rect,int hp,int maximum){
            float fraction=Mathf.Clamp01(hp/(float)Mathf.Max(1,maximum));
            ArenaRenderer.Fill(rect,CartoonUi.Ink);
            ArenaRenderer.Fill(new Rect(rect.x+1,rect.y+1,rect.width-2,rect.height-2),CartoonUi.Muted);
            ArenaRenderer.Fill(new Rect(rect.x+1,rect.y+1,(rect.width-2)*fraction,rect.height-2),fraction<=.25f?CartoonUi.Danger:CartoonUi.Ready);
        }
        void CampaignTank(Rect rect,int nation,int facing) {
            CampaignArt.Tank(rect,nation,facing);
        }
    }
}
