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
        readonly int[] campaignNations={0,1,5};
        bool CampaignOpen {get{return campaignPage!=0;}}
        void SetupCampaignPreview() {
            if(!IsMenuPreview)return;
            string page=UxTestDriver.Arg("--menu-preview","");
            if(!page.StartsWith("campaign"))return;
            client.Phase=ClientPhase.LOBBY;client.Nickname="Chỉ huy";
            campaignPage=page.StartsWith("campaign-ranking")?2:page=="campaign-shop"||page=="campaign-shop-unavailable"||page=="campaign-play"||page.StartsWith("campaign-results")||page=="campaign-victory"||page=="campaign-end"||page=="campaign-motion"||page=="campaign-layout"?3:1;campaignChoice=0;
            client.Campaign.Rankings.Add(new Ranking{Name="Chỉ huy",Score=10000,Gold=1850,DurationMillis=325450,Rank=1,DetailsAvailable=true});
            client.Campaign.Rankings.Add(new Ranking{Name="Đội giải cứu",Score=10000,Gold=1850,DurationMillis=325450,Rank=1,DetailsAvailable=true});
            client.Campaign.Rankings.Add(new Ranking{Name="Thiết giáp",Score=10000,Gold=1850,DurationMillis=410120,Rank=2,DetailsAvailable=true});
            client.Campaign.Rankings.Add(new Ranking{Name="Trinh sát",Score=10000,Gold=1500,DurationMillis=310000,Rank=3,DetailsAvailable=true});
            client.Campaign.Rankings.Add(new Ranking{Name="Hậu cần",Score=2500,Gold=600,Rank=4,DetailsAvailable=true});client.Campaign.TotalGold=1240;
            if(campaignPage==3){
                var run=new RunState{Id="preview",Nation=0,X=6,Y=506,Distance=506,Hp=75,Facing=0,Shot=4,Skill=12,Gold=185,Kills=28,Shields=2,DoubleHits=3,Bosses=1,Bottom=499,TotalGold=1240,ShopAvailable=true,Prices=new[]{30,46,55},NationStatsAvailable=true,SkillCooldown=24,Terrain=new byte[247],Message="Tiến về phía bắc. Trùm tiếp theo tại 1.000 m.",SaveStatus=""};
                for(int y=0;y<19;y++)for(int x=0;x<13;x++)if(x<4||x>8)run.Terrain[y*13+x]=(byte)((x+y)%9==0?1:0);
                run.Enemies.Add(new Actor{Id=1,X=6,Y=510,Hp=46,MaxHp=46});run.Enemies.Add(new Actor{Id=2,X=8,Y=512,Hp=180,MaxHp=180,Boss=2});run.Loot.Add(new Pickup{X=6,Y=508,Kind=3});run.Loot.Add(new Pickup{X=5,Y=507,Kind=0});
                if(page.StartsWith("campaign-results")||page=="campaign-victory"){
                    run.Finished=true;run.Victory=page=="campaign-victory";run.Message=run.Victory?"Đã giải cứu thành công!":"Xe đã bị phá hủy. Chiến dịch kết thúc.";
                    run.Hp=run.Victory?75:0;run.SaveStatus=page=="campaign-results-unsaved"?"Chưa lưu được. Bấm Lưu lại để thử lại.":"Đã lưu kết quả.";
                    if(run.Victory){run.Distance=10000;run.Bosses=CampaignClient.BossCount;}
                }
                client.Campaign.Run=run;
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
                if(client.Campaign.Run!=null)client.Campaign.Run.Nation=campaignNations[campaignChoice];
            }
            if(page=="campaign-ranking-empty")client.Campaign.Rankings.Clear();
            if(page=="campaign-loading")client.Campaign.Pending=true;
            if(page=="campaign-error"||System.Array.IndexOf(args,"--preview-error")>=0)client.Campaign.Error="Không xử lý được yêu cầu chiến dịch. Vui lòng kiểm tra kết nối và thử lại.";
            if(System.Array.IndexOf(args,"--preview-long-text")>=0){client.Nickname="Chỉ huy xe tăng Việt Nam";if(client.Campaign.Rankings.Count>0)client.Campaign.Rankings[0].Name=client.Nickname;}
            if(System.Array.IndexOf(args,"--preview-wide-names")>=0){client.Nickname=new string('M',24);foreach(var rank in client.Campaign.Rankings)rank.Name=client.Nickname;}
            if(System.Array.IndexOf(args,"--preview-scroll-bottom")>=0)campaignScroll=campaignResultScroll=campaignShopScroll=new Vector2(0,10000);
        }
        void CampaignEntryButtons() {
            if(Button("Chiến dịch",true,true)){campaignPage=1;campaignScroll=Vector2.zero;}
            if(Button("BXH chiến dịch",true,true)){campaignPage=2;campaignScroll=Vector2.zero;if(client.Phase==ClientPhase.LOBBY)client.CampaignCommand(3);}
        }
        void UpdateCampaign() {
            if(CampaignOpen)EnsureCampaignAudio();
            double now=Time.unscaledTimeAsDouble;
            if(campaignAccountRevision!=client.AccountRevision){campaignAccountRevision=client.AccountRevision;campaignPage=0;campaignPresentation.Reset();queuedCampaignAction=-1;campaignShopOpen=false;campaignEndConfirm=false;}
            var run=client.Campaign.Run;
            campaignPresentation.Observe(run,now);
            if(!CampaignOpen||client.Phase!=ClientPhase.LOBBY||!GameWindowFocus.IsForeground||AnyDialog){queuedCampaignAction=-1;return;}
            if(campaignPage!=3||run==null||run.Finished){campaignShopOpen=false;return;}
            if(campaignEndConfirm){queuedCampaignAction=-1;if(Input.GetKeyDown(KeyCode.Escape))campaignEndConfirm=false;return;}
            if(ClientPreferences.Active.Pressed(ControlAction.CampaignShop,!campaignShopOpen&&CampaignMouseInMap())){ToggleCampaignShop();return;}
            if(campaignShopOpen){queuedCampaignAction=-1;if(Input.GetKeyDown(KeyCode.Escape))ToggleCampaignShop();return;}
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
            const float mapTop=100,mapFooter=96;
            float playHeight=viewHeight-mapTop-mapFooter;
            float cell=Mathf.Max(1,Mathf.Min((viewWidth-32)/13,playHeight/rows));
            float bw=13*cell,bh=rows*cell,bx=(viewWidth-bw)/2,by=mapTop+(playHeight-bh)/2;
            campaignPointerArea=new Rect(bx,by,bw,bh);
            double now=Time.unscaledTimeAsDouble;
            campaignPresentation.Observe(run,now);
            float bottom=campaignPresentation.CameraBottom(rows,now);
            for(float py=mapTop;py<viewHeight-mapFooter;py+=cell)for(float px=0;px<viewWidth;px+=cell) {
                GUI.color=new Color(.48f,.57f,.46f);
                CampaignArt.Surroundings(new Rect(px,py,cell,Mathf.Min(cell,viewHeight-mapFooter-py)),Mathf.FloorToInt(px/cell),Mathf.FloorToInt(py/cell),run.Biome);
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
            CampaignHealthBar(new Rect(player.x+3,player.y,cell-6,5),run.Hp,run.MaxHp);
            campaignPresentation.DrawEffects(tileAt,cell,now,CampaignUI.SmallLight);
            GUI.EndGroup();

            DrawCampaignHud(run,now);
            string feedback=client.Campaign.Error;
            if(string.IsNullOrEmpty(feedback)&&run.Turn>0&&!run.Finished&&now-campaignPresentation.Started<3.5)feedback=run.Message;
            if(!string.IsNullOrEmpty(feedback)) {
                float sideWidth=bx-28;
                bool beside=sideWidth>=240;
                float noticeWidth=beside?Mathf.Min(320,sideWidth):bw-16;
                float noticeHeight=CampaignLabelHeight(CampaignUI.DialogueText,feedback,noticeWidth-32)+20;
                Rect notice=new Rect(beside?14:bx+8,beside?by+12:by+8,noticeWidth,noticeHeight);
                CampaignUI.Notice(notice,feedback,!string.IsNullOrEmpty(client.Campaign.Error));
            }
            if(run.Finished&&campaignPresentation.ResultsReady(now))DrawCampaignResults(run);
            else if(campaignShopOpen)DrawCampaignShop(run);
            else if(campaignEndConfirm)DrawCampaignEndConfirmation();
        }
        void CampaignHealthBar(Rect rect,int hp,int maximum){
            float fraction=hp/(float)Mathf.Max(1,maximum);
            CampaignUI.Meter(rect,fraction,fraction<=.25f?new Color(1,.4f,.3f):new Color(.55f,1,.55f));
        }
        void CampaignTank(Rect rect,int nation,int facing) {
            CampaignArt.Tank(rect,nation,facing);
        }
    }
}
