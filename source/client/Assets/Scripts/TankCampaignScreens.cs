using UnityEngine;
using Tank.Campaign;

namespace Tank {
    public partial class TankApp {
        void DrawCampaign(){
            CampaignUI.Ensure();
            Vector2 visible=CampaignUI.VisibleSize();viewWidth=visible.x;viewHeight=visible.y;
            if(campaignPage==3&&client.Phase==ClientPhase.LOBBY&&client.Campaign.Run!=null){DrawCampaignRun(client.Campaign.Run);return;}
            CampaignUI.Background(new Rect(0,0,viewWidth,viewHeight));
            GUI.Label(new Rect(28,20,viewWidth-240,40),campaignPage==2?"BẢNG CHỈ HUY":"CHIẾN DỊCH GIẢI CỨU",CampaignUI.Header);
            GUI.Label(new Rect(30,63,viewWidth-260,32),campaignPage==2?"Quãng đường ↓ · Vàng thực nhận ↓ · Thời gian ↑ · Cho phép đồng hạng":"Chọn xe theo lối chơi của bạn · Đích đến 10.000 m",CampaignUI.SmallLight);
            if(client.Phase!=ClientPhase.LOBBY){DrawCampaignBack();CampaignUI.Notice(new Rect(28,126,viewWidth-56,92),"Đăng nhập để bắt đầu chiến dịch và lưu vàng, điểm số.");if(CampaignUI.Button(new Rect(40,238,220,44),"Về đăng nhập"))campaignPage=0;return;}
            if(campaignPage==2){DrawCampaignRanking();DrawCampaignBack();return;}
            int columns=viewWidth>=1120?3:1;
            const float cardHeight=544,gap=20,padding=24;
            float viewportWidth=viewWidth-48,contentWidth=viewportWidth-20;
            float frameWidth=Mathf.Min(columns==3?1160:410,contentWidth-12);
            float cardWidth=(frameWidth-padding*2-gap*(columns-1))/columns;
            int rows=(3+columns-1)/columns;
            float frameHeight=82+rows*cardHeight+(rows-1)*gap;
            float guideWidth=Mathf.Min(860,contentWidth-12),guideHeight=190;
            float stackHeight=frameHeight+24+guideHeight;
            Rect viewport=new Rect(24,108,viewportWidth,Mathf.Max(140,viewHeight-190));
            float top=Mathf.Max(6,(viewport.height-stackHeight)/2);
            campaignScroll=GUI.BeginScrollView(viewport,campaignScroll,new Rect(0,0,contentWidth,Mathf.Max(viewport.height,top+stackHeight+6)));
            Rect frame=new Rect((contentWidth-frameWidth)/2,top,frameWidth,frameHeight);
            CampaignUI.Background(frame);GUI.Box(frame,GUIContent.none,CampaignUI.Focus);
            GUI.Label(new Rect(frame.x+24,frame.y+18,frame.width-48,28),"CHỌN XE XUẤT PHÁT",CampaignUI.Light);
            for(int i=0;i<3;i++)DrawCampaignNationCard(new Rect(frame.x+padding+i%columns*(cardWidth+gap),frame.y+64+i/columns*(cardHeight+gap),cardWidth,cardHeight),i);
            string briefing="Vu-a-lo đã bắt Tổng thống Đô-na-tri và gia đình. Chọn xe, vượt các chốt địch và tiến tới 10.000 m để giải cứu họ. Cứ 200 m, một tên trùm sẽ chặn đường.";
            string controls="WASD / mũi tên: di chuyển · I: bắn (hồi 5 lượt)\nO: kỹ năng quốc gia · P: cửa hàng tiếp tế";
            CampaignUI.Dialogue(new Rect((contentWidth-guideWidth)/2,frame.yMax+24,guideWidth,guideHeight),"SỞ CHỈ HUY · NHIỆM VỤ GIẢI CỨU",briefing+"\n\nHƯỚNG DẪN\n"+controls,2);
            GUI.EndScrollView();
            if(!string.IsNullOrEmpty(client.Campaign.Error))GUI.Label(new Rect((viewWidth-Mathf.Min(860,viewWidth-48))/2,viewHeight-82,Mathf.Min(860,viewWidth-48),22),client.Campaign.Error,CampaignUI.SmallLight);
            bool canResume=client.Campaign.Run!=null&&!client.Campaign.Run.Finished;
            int buttonCount=canResume?3:2;
            float buttonWidth=Mathf.Min(270,(viewWidth-64-(buttonCount-1)*16)/buttonCount);
            float buttonX=(viewWidth-buttonCount*buttonWidth-(buttonCount-1)*16)/2;
            string selected=campaignChoice>=0?CampaignNation.For(campaignNations[campaignChoice]).Name:"chọn xe";
            if(CampaignUI.Button(new Rect(buttonX,viewHeight-60,buttonWidth,44),"Bắt đầu · "+selected,campaignChoice>=0&&!client.Campaign.Pending)){client.CampaignCommand(0,campaignNations[campaignChoice]);campaignPage=3;}
            if(CampaignUI.Button(new Rect(buttonX+buttonWidth+16,viewHeight-60,buttonWidth,44),"Bảng xếp hạng",!client.Campaign.Pending,true)){campaignPage=2;campaignScroll=Vector2.zero;client.CampaignCommand(3);}
            if(canResume&&CampaignUI.Button(new Rect(buttonX+(buttonWidth+16)*2,viewHeight-60,buttonWidth,44),"Tiếp tục chuyến đi",!client.Campaign.Pending,true))campaignPage=3;
            DrawCampaignBack();
        }
        void DrawCampaignBack(){
            if(CampaignUI.Button(new Rect(viewWidth-170,24,142,40),"Về menu",!client.Campaign.Pending,true))campaignPage=0;
        }
        void DrawCampaignNationCard(Rect rect,int index){
            var nation=CampaignNation.For(campaignNations[index]);
            bool enabled=GUI.enabled;GUI.enabled=enabled&&!client.Campaign.Pending;
            GUI.SetNextControlName("CampaignNation"+index);
            if(GUI.Button(rect,new GUIContent("","Chọn xe "+nation.Name),GUIStyle.none))campaignChoice=index;
            GUI.enabled=enabled;
            bool chosen=campaignChoice==index;
            bool highlighted=!client.Campaign.Pending&&(rect.Contains(Event.current.mousePosition)||GUI.GetNameOfFocusedControl()=="CampaignNation"+index);
            if(chosen){
                Color color=GUI.backgroundColor;GUI.backgroundColor=new Color(1f,.62f,.10f);
                GUI.Box(new Rect(rect.x-5,rect.y-5,rect.width+10,rect.height+10),GUIContent.none,CampaignUI.Card);
                GUI.backgroundColor=new Color(1f,.91f,.65f);
                CampaignUI.Panel(rect);GUI.backgroundColor=color;
            }else CampaignUI.Panel(rect,highlighted);
            float x=rect.x+24,w=rect.width-48;
            GUI.Label(new Rect(x,rect.y+22,w,30),nation.Name,CampaignUI.Title);
            GUI.Label(new Rect(x,rect.y+60,w,42),nation.Role,CampaignUI.Hint);
            float avatarY=96,statsY=208,shotY=280,skillY=348;
            float avatarX=rect.center.x-58;
            GUI.Box(new Rect(avatarX,rect.y+avatarY,116,80),GUIContent.none,CampaignUI.Slot);
            CampaignTank(new Rect(avatarX+12,rect.y+avatarY+8,92,64),nation.Id,1);
            GUI.Label(new Rect(x,rect.y+statsY,w,30),nation.MaxHp+" HP",CampaignUI.Title);
            GUI.Label(new Rect(x,rect.y+statsY+36,w,24),"Giáp "+nation.Armor+" / đòn   ·   Cận chiến "+nation.MeleeDamage,CampaignUI.Hint);
            GUI.Label(new Rect(x,rect.y+shotY,w,26),"I · "+nation.ShotDamage+" sát thương · tầm 4 ô",CampaignUI.Text);
            GUI.Label(new Rect(x,rect.y+shotY+30,w,24),"Hồi bắn 5 lượt",CampaignUI.Hint);
            CampaignUI.Dialogue(new Rect(x-6,rect.y+skillY,w+12,148),"O · "+nation.SkillName,nation.SkillDescription,index==2?3:index);
            GUI.Label(new Rect(x,rect.yMax-34,w,22),"Hồi kỹ năng "+nation.SkillCooldown+" lượt",CampaignUI.Hint);
            if(chosen){
                GUI.Box(new Rect(rect.xMax-124,rect.y+16,108,30),GUIContent.none,CampaignUI.Primary);
                GUI.Label(new Rect(rect.xMax-124,rect.y+16,108,30),"✓ ĐÃ CHỌN",new GUIStyle(CampaignUI.Text){alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold});
            }
        }

        void DrawCampaignRanking(){
            int count=client.Campaign.Rankings.Count;
            float width=Mathf.Min(1120,viewWidth-48),height=Mathf.Min(Mathf.Max(280,count*84+138),viewHeight-210);
            Rect frame=new Rect((viewWidth-width)/2,108+Mathf.Max(0,(viewHeight-210-height)/2),width,height);
            CampaignUI.Background(frame);GUI.Box(frame,GUIContent.none,CampaignUI.Focus);
            float left=frame.x+24,innerWidth=width-68;
            float[] stops={0,.10f,.38f,.58f,.78f,1};
            string[] headings={"HẠNG","CHỈ HUY","QUÃNG ĐƯỜNG","VÀNG THỰC NHẬN","THỜI GIAN"};
            for(int i=0;i<5;i++)GUI.Label(new Rect(left+innerWidth*stops[i]+14,frame.y+18,innerWidth*(stops[i+1]-stops[i])-20,40),headings[i],CampaignUI.SmallLight);
            Rect viewport=new Rect(left,frame.y+66,width-48,Mathf.Max(70,height-112));
            campaignScroll=GUI.BeginScrollView(viewport,campaignScroll,new Rect(0,0,innerWidth,Mathf.Max(80,count*84)));
            for(int i=0;i<count;i++){
                var row=client.Campaign.Rankings[i];Rect rect=new Rect(0,i*84,innerWidth,72);
                GUI.Box(rect,GUIContent.none,CampaignUI.RankingRow);
                string[] values={row.Rank.ToString("00"),row.Name,row.Score+" m",row.DetailsAvailable?row.Gold.ToString():"—",CampaignUI.DurationLabel(row.DurationMillis)};
                for(int col=0;col<5;col++)GUI.Label(new Rect(innerWidth*stops[col]+14,rect.y+10,innerWidth*(stops[col+1]-stops[col])-24,52),values[col],col<2?CampaignUI.RankingName:CampaignUI.RankingText);
            }
            if(count==0)GUI.Label(new Rect(14,16,innerWidth-28,48),client.Campaign.Pending?"Đang tải bảng xếp hạng…":"Chưa có chuyến đi được ghi nhận.",CampaignUI.Light);
            GUI.EndScrollView();
            GUI.Label(new Rect(left,frame.yMax-34,width-48,24),string.IsNullOrEmpty(client.Campaign.Error)?"Kết quả tốt nhất của mỗi chỉ huy · Thời gian: phút:giây.mili giây · —: chưa có dữ liệu":client.Campaign.Error,CampaignUI.SmallLight);
            float buttonWidth=Mathf.Min(240,(viewWidth-72)/2),buttonX=(viewWidth-buttonWidth*2-16)/2;
            if(CampaignUI.Button(new Rect(buttonX,viewHeight-60,buttonWidth,44),"Làm mới",!client.Campaign.Pending))client.CampaignCommand(3);
            if(CampaignUI.Button(new Rect(buttonX+buttonWidth+16,viewHeight-60,buttonWidth,44),"Chọn xe",!client.Campaign.Pending,true)){campaignPage=1;campaignScroll=Vector2.zero;}
        }
        void DrawCampaignResults(RunState run){
            ArenaRenderer.Fill(new Rect(0,0,viewWidth,viewHeight),new Color(0,0,0,.72f));
            float width=Mathf.Min(580,viewWidth-32),height=Mathf.Min(480,viewHeight-32);Rect card=new Rect((viewWidth-width)/2,(viewHeight-height)/2,width,height);
            CampaignUI.Panel(card);GUI.Label(new Rect(card.x+28,card.y+24,width-56,38),run.Victory?"GIẢI CỨU THÀNH CÔNG":"KẾT THÚC CHUYẾN ĐI",CampaignUI.Title);
            GUI.Label(new Rect(card.x+28,card.y+78,width-56,32),CampaignNation.For(run.Nation).Name+" · "+run.Kills+" địch đã hạ",CampaignUI.Hint);
            GUI.Label(new Rect(card.x+28,card.y+127,width-56,50),run.Distance+" m",new GUIStyle(CampaignUI.Title){fontSize=36});
            GUI.Label(new Rect(card.x+28,card.y+192,width-56,60),"Vàng nhận: "+run.Gold+"\nTổng vàng tích lũy: "+run.TotalGold,CampaignUI.Text);
            CampaignUI.Notice(new Rect(card.x+20,card.y+274,width-40,64),string.IsNullOrEmpty(client.Campaign.Error)?run.SaveStatus:client.Campaign.Error);
            bool saved=run.SaveStatus=="Đã lưu kết quả.";
            if(CampaignUI.Button(new Rect(card.x+28,card.yMax-116,width-56,42),saved?"Chọn xe / Chơi lại":"Lưu lại",!client.Campaign.Pending)){if(saved){campaignPage=1;campaignScroll=Vector2.zero;}else client.CampaignCommand(4);}
            if(CampaignUI.Button(new Rect(card.x+28,card.yMax-64,width-56,40),"Về menu",saved&&!client.Campaign.Pending,true))campaignPage=0;
        }
    }
}
