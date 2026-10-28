using UnityEngine;
using Tank.Campaign;

namespace Tank {
    public partial class TankApp {
        Vector2 campaignResultScroll;
        GUIStyle campaignRankingWrapped;
        static float CampaignLabelHeight(GUIStyle style,string text,float width,float minimum=0){
            return Mathf.Max(minimum,Mathf.Ceil(style.CalcHeight(new GUIContent(text??""),Mathf.Max(1,width)))+2);
        }
        static float CampaignButtonHeight(string text,float width,float minimum=46){
            return Mathf.Max(minimum,CampaignLabelHeight(CampaignUI.Primary,text,width),CampaignLabelHeight(CampaignUI.Quiet,text,width),CampaignLabelHeight(CampaignUI.Disabled,text,width));
        }
        void DrawCampaign(){
            CampaignUI.Ensure();
            Vector2 visible=CampaignUI.VisibleSize();viewWidth=visible.x;viewHeight=visible.y;
            if(campaignPage==3&&client.Phase==ClientPhase.LOBBY&&client.Campaign.Run!=null){DrawCampaignRun(client.Campaign.Run);return;}
            CampaignUI.Background(new Rect(0,0,viewWidth,viewHeight));
            string heading=campaignPage==2?"BẢNG CHỈ HUY":"CHIẾN DỊCH GIẢI CỨU",subtitle=campaignPage==2?"Quãng đường · Vàng thực nhận · Thời gian · Cho phép đồng hạng":"Ba chiến xa, ba lối chơi · Một nhiệm vụ giải cứu";
            float headingHeight=CampaignLabelHeight(CampaignUI.Header,heading,viewWidth-300,35),subtitleHeight=CampaignLabelHeight(CampaignUI.SmallLight,subtitle,viewWidth-320,24);
            float headerHeight=Mathf.Max(78,headingHeight+subtitleHeight+28),contentTop=headerHeight+28;
            CartoonUi.Panel(new Rect(18,14,viewWidth-36,headerHeight),CartoonUi.PanelDark,18,4);
            CartoonUi.Icon(new Rect(36,32,36,36),campaignPage==2?UiIcon.Trophy:UiIcon.Range,CartoonUi.Gold);
            GUI.Label(new Rect(86,25,viewWidth-300,headingHeight),heading,CampaignUI.Header);
            GUI.Label(new Rect(88,29+headingHeight,viewWidth-320,subtitleHeight),subtitle,CampaignUI.SmallLight);
            if(client.Phase!=ClientPhase.LOBBY){DrawCampaignBack();CampaignUI.Notice(new Rect(28,126,viewWidth-56,72),"Đăng nhập để bắt đầu chiến dịch và lưu vàng, điểm số.");if(CampaignUI.Button(new Rect(40,218,220,44),"Về đăng nhập"))campaignPage=0;return;}
            if(campaignPage==2){DrawCampaignRanking(contentTop);DrawCampaignBack();return;}
            int columns=viewWidth>=1000?3:1;
            const float gap=18;
            float viewportWidth=viewWidth-48,contentWidth=viewportWidth-20;
            float frameWidth=Mathf.Min(columns==3?1240:440,contentWidth-12),cardWidth=(frameWidth-gap*(columns-1))/columns;
            float cardHeight=416;
            for(int i=0;i<3;i++)cardHeight=Mathf.Max(cardHeight,CampaignNationCardHeight(cardWidth,i));
            int rows=(3+columns-1)/columns;
            float cardsHeight=rows*cardHeight+(rows-1)*gap,missionWidth=columns==3?frameWidth*.63f:frameWidth-32;
            const string missionTitle="HỒ SƠ NHIỆM VỤ · 10.000 M",missionText="Vu-a-lo đã bắt Tổng thống Đô-na-tri và gia đình. Vượt các chốt địch để giải cứu họ. Mỗi 200 m, một tên trùm sẽ chặn đường.",controls="WASD / Mũi tên: di chuyển\nI: bắn · O: kỹ năng · P: tiếp tế";
            float missionTitleHeight=CampaignLabelHeight(CampaignUI.DialogueTitle,missionTitle,missionWidth-24,25),missionTextHeight=CampaignLabelHeight(CampaignUI.DialogueText,missionText,missionWidth-24);
            float controlsWidth=columns==3?frameWidth-missionWidth-16:frameWidth-36,controlsHeight=CampaignLabelHeight(CampaignUI.DialogueText,controls,controlsWidth,40);
            float guideHeight=columns==3?Mathf.Max(missionTitleHeight+missionTextHeight+32,controlsHeight+66):missionTitleHeight+missionTextHeight+controlsHeight+44,stackHeight=cardsHeight+18+guideHeight;
            bool canResume=client.Campaign.Run!=null&&!client.Campaign.Run.Finished;
            bool needsSave=client.Campaign.Run!=null&&client.Campaign.Run.Finished&&client.Campaign.Run.SaveStatus!="Đã lưu kết quả.";
            string status=client.Campaign.Pending?"Đang chuẩn bị chiến dịch…":!string.IsNullOrEmpty(client.Campaign.Error)?client.Campaign.Error:campaignChoice<0?"Chọn một chiến xa để nhận nhiệm vụ.":canResume?"Bạn đang có chuyến đi dở dang. Chọn Tiếp tục để trở lại chiến trường.":needsSave?"Kết quả chuyến đi cần được lưu trước khi nhận nhiệm vụ mới.":"";
            int buttonCount=canResume||needsSave?3:2;
            float buttonWidth=Mathf.Min(280,(viewWidth-64-(buttonCount-1)*16)/buttonCount),buttonX=(viewWidth-buttonCount*buttonWidth-(buttonCount-1)*16)/2;
            string selected=campaignChoice>=0?CampaignNation.For(campaignNations[campaignChoice]).Name:"chọn xe",startLabel=client.Campaign.Pending?"Đang chuẩn bị…":"Bắt đầu · "+selected,resumeLabel=canResume?"Tiếp tục chuyến đi":"Xem / Lưu kết quả";
            float buttonHeight=Mathf.Max(CampaignButtonHeight(startLabel,buttonWidth),CampaignButtonHeight("Bảng xếp hạng",buttonWidth));
            if(buttonCount==3)buttonHeight=Mathf.Max(buttonHeight,CampaignButtonHeight(resumeLabel,buttonWidth));
            float buttonY=viewHeight-16-buttonHeight,statusHeight=string.IsNullOrEmpty(status)?0:CampaignLabelHeight(!string.IsNullOrEmpty(client.Campaign.Error)?CampaignUI.ErrorLabel:CampaignUI.SmallLight,status,viewWidth-64,client.Campaign.Pending?28:24),statusY=buttonY-8-statusHeight;
            Rect viewport=new Rect(24,contentTop,viewportWidth,Mathf.Max(80,statusY-12-contentTop));
            float top=Mathf.Max(4,(viewport.height-stackHeight)/2),left=(contentWidth-frameWidth)/2;
            campaignScroll=GUI.BeginScrollView(viewport,campaignScroll,new Rect(0,0,contentWidth,Mathf.Max(viewport.height,top+stackHeight+4)));
            for(int i=0;i<3;i++)DrawCampaignNationCard(new Rect(left+i%columns*(cardWidth+gap),top+i/columns*(cardHeight+gap),cardWidth,cardHeight),i);
            Rect briefing=new Rect(left,top+cardsHeight+18,frameWidth,guideHeight);
            GUI.Box(briefing,GUIContent.none,CampaignUI.Banner);
            GUI.Label(new Rect(briefing.x+18,briefing.y+12,missionWidth-24,missionTitleHeight),missionTitle,CampaignUI.DialogueTitle);
            GUI.Label(new Rect(briefing.x+18,briefing.y+18+missionTitleHeight,missionWidth-24,missionTextHeight),missionText,CampaignUI.DialogueText);
            float controlsX=columns==3?briefing.x+missionWidth:briefing.x+18,controlsY=columns==3?briefing.y+16:briefing.y+28+missionTitleHeight+missionTextHeight;
            GUI.Label(new Rect(controlsX,controlsY,controlsWidth,controlsHeight),controls,CampaignUI.DialogueText);
            if(columns==3)CartoonUi.Badge(new Rect(controlsX,controlsY+controlsHeight+10,controlsWidth-2,24),"GIỮ HƯỚNG XE KHI BẮN",CartoonUi.Cream);
            GUI.EndScrollView();
            if(client.Campaign.Pending)CartoonUi.Busy(new Rect(32,statusY,Mathf.Min(430,viewWidth-64),statusHeight),status);
            else if(!string.IsNullOrEmpty(status))GUI.Label(new Rect(32,statusY,viewWidth-64,statusHeight),status,!string.IsNullOrEmpty(client.Campaign.Error)?CampaignUI.ErrorLabel:CampaignUI.SmallLight);
            if(CampaignUI.Button(new Rect(buttonX,buttonY,buttonWidth,buttonHeight),startLabel,campaignChoice>=0&&!client.Campaign.Pending&&!canResume&&!needsSave)){client.CampaignCommand(0,campaignNations[campaignChoice]);campaignPage=3;}
            if(CampaignUI.Button(new Rect(buttonX+buttonWidth+16,buttonY,buttonWidth,buttonHeight),"Bảng xếp hạng",!client.Campaign.Pending,true)){campaignPage=2;campaignScroll=Vector2.zero;client.CampaignCommand(3);}
            if((canResume||needsSave)&&CampaignUI.Button(new Rect(buttonX+(buttonWidth+16)*2,buttonY,buttonWidth,buttonHeight),resumeLabel,!client.Campaign.Pending,false,true))campaignPage=3;
            DrawCampaignBack();
        }
        void DrawCampaignBack(){
            if(CampaignUI.Button(new Rect(viewWidth-174,30,138,CampaignButtonHeight("Về menu",138)),"Về menu",!client.Campaign.Pending,true))campaignPage=0;
        }
        float CampaignNationCardHeight(float width,int index){
            var nation=CampaignNation.For(campaignNations[index]);float w=width-36;
            float top=18+CampaignLabelHeight(CampaignUI.Title,nation.Name,w-110,30)+4+CampaignLabelHeight(CampaignUI.Hint,nation.Role,w,24)+10+110;
            float stats=12+20+4+CampaignLabelHeight(CampaignUI.StatValue,"140",w/4,25)+2+CampaignLabelHeight(CampaignUI.StatCaption,"GIÁP",w/4,20);
            float melee=8+CampaignLabelHeight(CampaignUI.Hint,"Cận chiến "+nation.MeleeDamage+" · Hồi bắn 5 lượt",w,22);
            float skill=12+8+CampaignLabelHeight(CampaignUI.DialogueTitle,"O · "+nation.SkillName+" · "+nation.SkillCooldown+" lượt",w-20,25)+6+CampaignLabelHeight(CampaignUI.DialogueText,nation.SkillDescription,w-20)+12;
            return top+stats+melee+skill+16;
        }
        void DrawCampaignNationCard(Rect rect,int index){
            var nation=CampaignNation.For(campaignNations[index]);
            bool enabled=GUI.enabled;GUI.enabled=enabled&&!client.Campaign.Pending;
            GUI.SetNextControlName("CampaignNation"+index);
            if(GUI.Button(rect,GUIContent.none,GUIStyle.none))campaignChoice=index;
            GUI.enabled=enabled;
            bool chosen=campaignChoice==index,highlighted=!client.Campaign.Pending&&(rect.Contains(Event.current.mousePosition)||GUI.GetNameOfFocusedControl()=="CampaignNation"+index);
            CampaignUI.Panel(rect,chosen||highlighted);
            float x=rect.x+18,w=rect.width-36;
            float titleHeight=CampaignLabelHeight(CampaignUI.Title,nation.Name,w-110,30),roleY=rect.y+18+titleHeight+4,roleHeight=CampaignLabelHeight(CampaignUI.Hint,nation.Role,w,24),portraitY=roleY+roleHeight+10;
            GUI.Label(new Rect(x,rect.y+18,w-110,titleHeight),nation.Name,CampaignUI.Title);
            if(chosen)CartoonUi.Badge(new Rect(rect.xMax-112,rect.y+19,94,26),"ĐÃ CHỌN",CartoonUi.Ready);
            GUI.Label(new Rect(x,roleY,w,roleHeight),nation.Role,CampaignUI.Hint);
            CartoonUi.Panel(new Rect(x,portraitY,w,110),CartoonUi.PanelDark,14,3);
            CampaignTank(new Rect(rect.center.x-76,portraitY+2,152,106),nation.Id,1);
            UiIcon[] icons={UiIcon.Health,UiIcon.Armor,UiIcon.Damage,UiIcon.Range};
            string[] values={nation.MaxHp.ToString(),nation.Armor.ToString(),nation.ShotDamage.ToString(),"4 ô"};
            string[] captions={"HP","GIÁP","BẮN","TẦM"};
            float statWidth=w/4,statsY=portraitY+122,valueHeight=CampaignLabelHeight(CampaignUI.StatValue,"140",statWidth,25),captionHeight=CampaignLabelHeight(CampaignUI.StatCaption,"GIÁP",statWidth,20);
            for(int stat=0;stat<4;stat++){
                float sx=x+stat*statWidth;
                CartoonUi.Icon(new Rect(sx+statWidth/2-10,statsY,20,20),icons[stat],CartoonUi.PanelDark);
                GUI.Label(new Rect(sx,statsY+24,statWidth,valueHeight),values[stat],CampaignUI.StatValue);
                GUI.Label(new Rect(sx,statsY+26+valueHeight,statWidth,captionHeight),captions[stat],CampaignUI.StatCaption);
            }
            string melee="Cận chiến "+nation.MeleeDamage+" · Hồi bắn 5 lượt",skillTitle="O · "+nation.SkillName+" · "+nation.SkillCooldown+" lượt";
            float meleeY=statsY+26+valueHeight+captionHeight+8,meleeHeight=CampaignLabelHeight(CampaignUI.Hint,melee,w,22),skillTitleHeight=CampaignLabelHeight(CampaignUI.DialogueTitle,skillTitle,w-20,25),skillTextHeight=CampaignLabelHeight(CampaignUI.DialogueText,nation.SkillDescription,w-20);
            GUI.Label(new Rect(x,meleeY,w,meleeHeight),melee,CampaignUI.Hint);
            Rect skill=new Rect(x-2,meleeY+meleeHeight+12,w+4,8+skillTitleHeight+6+skillTextHeight+12);
            GUI.Box(skill,GUIContent.none,CampaignUI.Banner);
            GUI.Label(new Rect(skill.x+12,skill.y+8,skill.width-24,skillTitleHeight),skillTitle,CampaignUI.DialogueTitle);
            GUI.Label(new Rect(skill.x+12,skill.y+14+skillTitleHeight,skill.width-24,skillTextHeight),nation.SkillDescription,CampaignUI.DialogueText);
        }
        void DrawCampaignRanking(float contentTop){
            int count=client.Campaign.Rankings.Count;
            if(campaignRankingWrapped==null)campaignRankingWrapped=new GUIStyle(CampaignUI.RankingName){wordWrap=true};
            float buttonWidth=Mathf.Min(260,(viewWidth-72)/2),buttonX=(viewWidth-buttonWidth*2-16)/2,buttonHeight=Mathf.Max(CampaignButtonHeight("Đang tải…",buttonWidth),CampaignButtonHeight("Chọn xe",buttonWidth)),buttonY=viewHeight-16-buttonHeight;
            float width=Mathf.Min(1240,viewWidth-48),height=buttonY-16-contentTop;
            Rect frame=new Rect((viewWidth-width)/2,contentTop,width,height);
            CartoonUi.Panel(frame,CartoonUi.PanelDark,18,5);
            float left=frame.x+20,innerWidth=width-60;
            float[] stops={0,.09f,.40f,.60f,.79f,1};
            string[] headings={"HẠNG","CHỈ HUY","QUÃNG ĐƯỜNG","VÀNG NHẬN","THỜI GIAN"};
            float headingsHeight=28;
            for(int i=0;i<5;i++)headingsHeight=Mathf.Max(headingsHeight,CampaignLabelHeight(CampaignUI.SmallLight,headings[i],innerWidth*(stops[i+1]-stops[i])-16));
            for(int i=0;i<5;i++)GUI.Label(new Rect(left+innerWidth*stops[i]+12,frame.y+16,innerWidth*(stops[i+1]-stops[i])-16,headingsHeight),headings[i],CampaignUI.SmallLight);
            string note=string.IsNullOrEmpty(client.Campaign.Error)?"Kết quả tốt nhất mỗi chỉ huy · Thời gian phút:giây.mili giây · —: chưa có dữ liệu":client.Campaign.Error;
            float noteHeight=CampaignLabelHeight(CampaignUI.SmallLight,note,width-40,24),contentHeight=0;
            float[] rowHeights=new float[count];
            for(int i=0;i<count;i++){
                var row=client.Campaign.Rankings[i];string name=WrapMenuText(row.Name+(row.Name==client.Nickname?" · BẠN":""),campaignRankingWrapped,innerWidth*(stops[2]-stops[1])-20);
                rowHeights[i]=Mathf.Max(64,CampaignLabelHeight(campaignRankingWrapped,name,innerWidth*(stops[2]-stops[1])-20)+24);contentHeight+=rowHeights[i]+10;
            }
            string empty=client.Campaign.Pending?"Đang tải bảng xếp hạng…":"Chưa có chuyến đi được ghi nhận. Hãy trở thành chỉ huy đầu tiên!";
            float emptyHeight=CampaignLabelHeight(CampaignUI.Light,empty,innerWidth-32,52);
            if(count==0)contentHeight=84+emptyHeight+16;
            Rect viewport=new Rect(left,frame.y+24+headingsHeight,width-40,Mathf.Max(50,height-headingsHeight-noteHeight-54));
            campaignScroll=GUI.BeginScrollView(viewport,campaignScroll,new Rect(0,0,innerWidth,Mathf.Max(viewport.height,contentHeight)));
            float rowY=0;
            for(int i=0;i<count;i++){
                var row=client.Campaign.Rankings[i];Rect rect=new Rect(0,rowY,innerWidth,rowHeights[i]);
                bool own=row.Name==client.Nickname;
                CartoonUi.Panel(rect,own?CartoonUi.PanelColor:CartoonUi.Backdrop,12,3);
                Color medal=row.Rank==1?CartoonUi.Gold:row.Rank==2?CartoonUi.Cream:row.Rank==3?CartoonUi.Sand:CartoonUi.Muted;
                CartoonUi.Badge(new Rect(10,rect.y+14,innerWidth*.09f-20,34),row.Rank.ToString("00"),medal);
                string[] values={WrapMenuText(row.Name+(own?" · BẠN":""),campaignRankingWrapped,innerWidth*(stops[2]-stops[1])-20),row.Score.ToString("N0")+" m",row.DetailsAvailable?row.Gold.ToString("N0"):"—",CampaignUI.DurationLabel(row.DurationMillis)};
                for(int col=1;col<5;col++)GUI.Label(new Rect(innerWidth*stops[col]+12,rect.y+12,innerWidth*(stops[col+1]-stops[col])-20,rect.height-24),values[col-1],col==1?campaignRankingWrapped:CampaignUI.RankingText);
                rowY+=rect.height+10;
            }
            if(count==0){
                CartoonUi.Icon(new Rect(innerWidth/2-24,22,48,48),UiIcon.Trophy,CartoonUi.Gold);
                GUI.Label(new Rect(16,84,innerWidth-32,emptyHeight),empty,CampaignUI.Light);
            }
            GUI.EndScrollView();
            GUI.Label(new Rect(left,frame.yMax-noteHeight-12,width-40,noteHeight),note,CampaignUI.SmallLight);
            if(CampaignUI.Button(new Rect(buttonX,buttonY,buttonWidth,buttonHeight),client.Campaign.Pending?"Đang tải…":"Làm mới",!client.Campaign.Pending))client.CampaignCommand(3);
            if(CampaignUI.Button(new Rect(buttonX+buttonWidth+16,buttonY,buttonWidth,buttonHeight),"Chọn xe",!client.Campaign.Pending,true)){campaignPage=1;campaignScroll=Vector2.zero;}
        }
        void DrawCampaignResults(RunState run){
            ArenaRenderer.Fill(new Rect(0,0,viewWidth,viewHeight),new Color(.04f,.08f,.14f,.84f));
            float width=Mathf.Min(680,viewWidth-32),height=Mathf.Min(620,viewHeight-32);Rect card=new Rect((viewWidth-width)/2,(viewHeight-height)/2,width,height);
            CampaignUI.Panel(card);
            bool saved=run.SaveStatus=="Đã lưu kết quả.",error=!string.IsNullOrEmpty(client.Campaign.Error);
            string status=error?client.Campaign.Error:string.IsNullOrEmpty(run.SaveStatus)?"Đang chờ lưu kết quả…":run.SaveStatus,primary=client.Campaign.Pending?"Đang xử lý…":saved?"Chọn xe / Chơi lại":"Lưu lại";
            float actionHeight=CampaignButtonHeight(primary,width-56),backHeight=CampaignButtonHeight("Về menu",width-56,42),backY=card.yMax-18-backHeight,actionY=backY-10-actionHeight;
            Rect viewport=new Rect(card.x+20,card.y+20,width-40,actionY-card.y-34);float bodyWidth=viewport.width-20;
            string title=run.Victory?"GIẢI CỨU THÀNH CÔNG":"KẾT THÚC CHUYẾN ĐI",subtitle=CampaignNation.For(run.Nation).Name+" · "+run.Bosses+" / "+CampaignClient.BossCount+" trùm đã vượt",distance=run.Distance.ToString("N0")+" / 10.000 m";
            float titleHeight=CampaignLabelHeight(CampaignUI.Light,title,bodyWidth-116,30),subtitleHeight=CampaignLabelHeight(CampaignUI.SmallLight,subtitle,bodyWidth-116,24),bannerHeight=Mathf.Max(110,titleHeight+subtitleHeight+42);
            float distanceY=bannerHeight+16,distanceHeight=CampaignLabelHeight(CampaignUI.Title,distance,bodyWidth-16,36),meterY=distanceY+distanceHeight+8,statsY=meterY+32,statWidth=(bodyWidth-16)/3;
            string[] values={run.Kills.ToString(),run.Gold.ToString("N0"),run.TotalGold.ToString("N0")};
            string[] labels={"ĐỊCH ĐÃ HẠ","VÀNG NHẬN","VÀNG TÍCH LŨY"};
            float valueHeight=29,captionHeight=22;
            for(int i=0;i<3;i++){valueHeight=Mathf.Max(valueHeight,CampaignLabelHeight(CampaignUI.StatValue,values[i],statWidth-16));captionHeight=Mathf.Max(captionHeight,CampaignLabelHeight(CampaignUI.StatCaption,labels[i],statWidth-16));}
            float statHeight=valueHeight+captionHeight+26,noticeY=statsY+statHeight+16,noticeHeight=CampaignLabelHeight(CampaignUI.DialogueText,status,bodyWidth-32,26)+26;
            campaignResultScroll=GUI.BeginScrollView(viewport,campaignResultScroll,new Rect(0,0,bodyWidth,Mathf.Max(viewport.height,noticeY+noticeHeight+4)));
            CartoonUi.Panel(new Rect(0,0,bodyWidth,bannerHeight),CartoonUi.PanelDark,16,4);
            CartoonUi.Icon(new Rect(18,(bannerHeight-62)/2,62,62),run.Victory?UiIcon.Trophy:UiIcon.Range,run.Victory?CartoonUi.Gold:CartoonUi.Sand);
            GUI.Label(new Rect(96,18,bodyWidth-116,titleHeight),title,CampaignUI.Light);
            GUI.Label(new Rect(96,26+titleHeight,bodyWidth-116,subtitleHeight),subtitle,CampaignUI.SmallLight);
            GUI.Label(new Rect(8,distanceY,bodyWidth-16,distanceHeight),distance,CampaignUI.Title);
            CampaignUI.Meter(new Rect(8,meterY,bodyWidth-16,12),run.Distance/(float)CampaignClient.GoalDistance,run.Victory?CartoonUi.Ready:CartoonUi.Gold);
            for(int i=0;i<3;i++){
                Rect stat=new Rect(i*(statWidth+8),statsY,statWidth,statHeight);
                CartoonUi.Panel(stat,CartoonUi.Sand,12,3);
                GUI.Label(new Rect(stat.x+8,stat.y+9,stat.width-16,valueHeight),values[i],CampaignUI.StatValue);
                GUI.Label(new Rect(stat.x+8,stat.y+15+valueHeight,stat.width-16,captionHeight),labels[i],CampaignUI.StatCaption);
            }
            CampaignUI.Notice(new Rect(0,noticeY,bodyWidth,noticeHeight),status,error,saved);
            GUI.EndScrollView();
            if(CampaignUI.Button(new Rect(card.x+28,actionY,width-56,actionHeight),primary,!client.Campaign.Pending)){if(saved){campaignPage=1;campaignScroll=Vector2.zero;campaignResultScroll=Vector2.zero;}else client.CampaignCommand(4);}
            if(CampaignUI.Button(new Rect(card.x+28,backY,width-56,backHeight),"Về menu",saved&&!client.Campaign.Pending,true))campaignPage=0;
        }
    }
}
