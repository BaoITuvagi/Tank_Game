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
            string heading=campaignPage==2?"BẢNG CHỈ HUY":"CHIẾN DỊCH GIẢI CỨU";
            string subtitle=campaignPage==2?"Những chuyến đi được ghi vào chiến sử":"CHỌN CHIẾN XA  /  NHẬN NHIỆM VỤ  /  XUẤT KÍCH";
            float headingHeight=CampaignLabelHeight(CampaignUI.Header,heading,viewWidth-440,34),subtitleHeight=CampaignLabelHeight(CampaignUI.SmallLight,subtitle,viewWidth-440,25);
            float headerHeight=Mathf.Max(90,headingHeight+subtitleHeight+28),contentTop=headerHeight+16;
            CampaignUI.Logo(new Rect(22,10,84,84));
            GUI.Label(new Rect(120,21,viewWidth-440,headingHeight),heading,CampaignUI.Header);
            GUI.Label(new Rect(122,27+headingHeight,viewWidth-440,subtitleHeight),subtitle,CampaignUI.SmallLight);
            CampaignUI.Rule(new Rect(24,headerHeight,viewWidth-48,2),CartoonUi.Gold);
            if(client.Phase!=ClientPhase.LOBBY){DrawCampaignBack();CampaignUI.Notice(new Rect(28,126,viewWidth-56,72),"Đăng nhập để bắt đầu chiến dịch và lưu vàng, điểm số.");if(CampaignUI.Button(new Rect(40,218,220,48),"Về đăng nhập"))campaignPage=0;return;}
            if(campaignPage==2){DrawCampaignRanking(contentTop);DrawCampaignBack();return;}
            bool canResume=client.Campaign.Run!=null&&!client.Campaign.Run.Finished;
            bool needsSave=client.Campaign.Run!=null&&client.Campaign.Run.Finished&&client.Campaign.Run.SaveStatus!="Đã lưu kết quả.";
            string status=client.Campaign.Pending?"Đang chuẩn bị chiến dịch…":!string.IsNullOrEmpty(client.Campaign.Error)?client.Campaign.Error:campaignChoice<0?"Chọn một chiến xa để nhận nhiệm vụ.":canResume?"Bạn đang có chuyến đi dở dang. Chọn Tiếp tục để trở lại chiến trường.":needsSave?"Kết quả chuyến đi cần được lưu trước khi nhận nhiệm vụ mới.":"";
            int buttonCount=canResume||needsSave?3:2;
            float buttonWidth=Mathf.Min(300,(viewWidth-64-(buttonCount-1)*12)/buttonCount),buttonX=(viewWidth-buttonCount*buttonWidth-(buttonCount-1)*12)/2;
            string selected=campaignChoice>=0?CampaignNation.For(campaignNations[campaignChoice]).Name:"chọn xe",startLabel=client.Campaign.Pending?"Đang chuẩn bị…":"Xuất kích · "+selected,resumeLabel=canResume?"Tiếp tục chuyến đi":"Xem / Lưu kết quả";
            float buttonHeight=Mathf.Max(CampaignButtonHeight(startLabel,buttonWidth,54),CampaignButtonHeight("Bảng xếp hạng",buttonWidth,54));
            if(buttonCount==3)buttonHeight=Mathf.Max(buttonHeight,CampaignButtonHeight(resumeLabel,buttonWidth,54));
            float viewportWidth=viewWidth-48,contentWidth=viewportWidth-20,frameWidth=Mathf.Min(1240,contentWidth-8),left=(contentWidth-frameWidth)/2;
            float statusHeight=string.IsNullOrEmpty(status)?0:CampaignLabelHeight(!string.IsNullOrEmpty(client.Campaign.Error)?CampaignUI.ErrorLabel:CampaignUI.SmallLight,status,frameWidth,24);
            bool wide=frameWidth>=800;float railWidth=wide?244:frameWidth,railHeight=wide?326:130;
            float dossierWidth=wide?frameWidth-railWidth-18:frameWidth,dossierHeight=CampaignDossierHeight(dossierWidth),dossierY=wide?4:railHeight+20;
            float firstHeight=wide?Mathf.Max(railHeight,dossierHeight):dossierY+dossierHeight;
            const string missionTitle="HỒ SƠ NHIỆM VỤ · 10.000 M",missionText="Tổ chức Vu-a-lo đã bắt giữ tổng thống Đô-na-tri và gia đình, là 1 chỉ huy đại tài hãy lên đường giải cứu ngài tổng thống";
            float missionWidth=frameWidth,missionTitleHeight=CampaignLabelHeight(CampaignUI.DialogueTitle,missionTitle,missionWidth-40,26),missionTextHeight=CampaignLabelHeight(CampaignUI.DialogueText,missionText,missionWidth-40);
            float guideHeight=missionTitleHeight+missionTextHeight+34;
            float stackHeight=firstHeight+18+guideHeight;
            float statusBlock=statusHeight>0?statusHeight+12:0;
            float availableHeight=viewHeight-contentTop-24,footerHeight=24+statusBlock+buttonHeight;
            float viewportHeight=Mathf.Min(stackHeight+8,Mathf.Max(80,availableHeight-footerHeight));
            float groupTop=contentTop+Mathf.Max(0,(availableHeight-viewportHeight-footerHeight)/2);
            Rect viewport=new Rect((viewWidth-contentWidth)/2,groupTop,viewportWidth,viewportHeight);
            float statusY=viewport.yMax+12,buttonY=viewport.yMax+24+statusBlock,frameX=viewport.x+left;
            campaignScroll=GUI.BeginScrollView(viewport,campaignScroll,new Rect(0,0,contentWidth,Mathf.Max(viewport.height,stackHeight+8)));
            GUI.Label(new Rect(left,4,railWidth,24),"BIÊN ĐỘI THIẾT GIÁP",CampaignUI.SmallLight);
            for(int i=0;i<3;i++){
                Rect choice=wide?new Rect(left,34+i*96,railWidth,86):new Rect(left+i*(frameWidth+12)/3,34,(frameWidth-24)/3,96);
                DrawCampaignNationCard(choice,i);
            }
            DrawCampaignDossier(new Rect(wide?left+railWidth+18:left,dossierY,dossierWidth,dossierHeight),Mathf.Clamp(campaignChoice,0,2));
            Rect briefing=new Rect(left,firstHeight+18,frameWidth,guideHeight);GUI.Box(briefing,GUIContent.none,CampaignUI.Banner);
            CampaignUI.Rule(new Rect(briefing.x+1,briefing.y+1,5,briefing.height-2),CartoonUi.Gold);
            GUI.Label(new Rect(briefing.x+20,briefing.y+14,missionWidth-40,missionTitleHeight),missionTitle,CampaignUI.DialogueTitle);
            GUI.Label(new Rect(briefing.x+20,briefing.y+20+missionTitleHeight,missionWidth-40,missionTextHeight),missionText,CampaignUI.DialogueText);
            GUI.EndScrollView();
            if(client.Campaign.Pending)CartoonUi.Busy(new Rect(frameX,statusY,Mathf.Min(430,frameWidth),statusHeight),status);
            else if(!string.IsNullOrEmpty(status))GUI.Label(new Rect(frameX,statusY,frameWidth,statusHeight),status,!string.IsNullOrEmpty(client.Campaign.Error)?CampaignUI.ErrorLabel:CampaignUI.SmallLight);
            if(CampaignUI.Button(new Rect(buttonX,buttonY,buttonWidth,buttonHeight),startLabel,campaignChoice>=0&&!client.Campaign.Pending&&!canResume&&!needsSave)){client.CampaignCommand(0,campaignNations[campaignChoice]);campaignPage=3;}
            if(CampaignUI.Button(new Rect(buttonX+buttonWidth+12,buttonY,buttonWidth,buttonHeight),"Bảng xếp hạng",!client.Campaign.Pending,true)){campaignPage=2;campaignScroll=Vector2.zero;client.CampaignCommand(3);}
            if((canResume||needsSave)&&CampaignUI.Button(new Rect(buttonX+(buttonWidth+12)*2,buttonY,buttonWidth,buttonHeight),resumeLabel,!client.Campaign.Pending,false,true))campaignPage=3;
            DrawCampaignBack();
        }
        void DrawCampaignBack(){
            DrawSettingsButton(new Rect(viewWidth-294,26,110,46));
            if(CampaignUI.Button(new Rect(viewWidth-174,26,144,CampaignButtonHeight("Về menu",144)),"Về menu",!client.Campaign.Pending,true))campaignPage=0;
        }
        void DrawCampaignNationCard(Rect rect,int index){
            var nation=CampaignNation.For(campaignNations[index]);
            bool enabled=GUI.enabled;GUI.enabled=enabled&&!client.Campaign.Pending;GUI.SetNextControlName("CampaignNation"+index);
            if(GUI.Button(rect,GUIContent.none,GUIStyle.none))campaignChoice=index;GUI.enabled=enabled;
            bool chosen=campaignChoice==index,hovered=!client.Campaign.Pending&&(rect.Contains(Event.current.mousePosition)||GUI.GetNameOfFocusedControl()=="CampaignNation"+index);
            CartoonUi.Panel(rect,chosen?CartoonUi.Gold:hovered?CartoonUi.PanelColor:CartoonUi.PanelDark,8,0);
            CampaignUI.Rule(new Rect(rect.x+1,rect.y+1,chosen?5:2,rect.height-2),chosen?CartoonUi.Cream:CartoonUi.Ready);
            bool compact=rect.width<220;
            CampaignTank(new Rect(rect.x+12,rect.y+(compact?2:8),compact?54:66,compact?54:66),nation.Id,1);
            var nameStyle=chosen?CampaignUI.Text:CampaignUI.Light;float x=compact?rect.x+76:rect.x+88;
            GUI.Label(new Rect(x,rect.y+12,rect.xMax-x-10,28),nation.Name,nameStyle);
            if(compact)GUI.Label(new Rect(rect.x+12,rect.y+57,rect.width-24,32),chosen?"ĐÃ CHỌN":nation.Role,chosen?CampaignUI.Hint:CampaignUI.SmallLight);
            else GUI.Label(new Rect(x,rect.y+40,rect.xMax-x-10,40),nation.Role,chosen?CampaignUI.Hint:CampaignUI.SmallLight);
        }
        float CampaignDossierHeight(float width){
            var nation=CampaignNation.For(campaignNations[Mathf.Clamp(campaignChoice,0,2)]);bool side=width>=580;
            float detailWidth=side?width*.52f-34:width-40;
            float skillTitle=CampaignLabelHeight(CampaignUI.DialogueTitle,ClientPreferences.Active.Label(ControlAction.CampaignSkill)+" · "+nation.SkillName+" · "+nation.SkillCooldown+" lượt",width-48,26),skillText=CampaignLabelHeight(CampaignUI.DialogueText,nation.SkillDescription,width-48);
            float top=side?260:360;
            return top+skillTitle+skillText+48+CampaignLabelHeight(CampaignUI.Hint,"Cận chiến "+nation.MeleeDamage+" · Hồi bắn 5 lượt",detailWidth,24);
        }
        void DrawCampaignDossier(Rect rect,int index){
            var nation=CampaignNation.For(campaignNations[index]);CampaignUI.Panel(rect);bool side=rect.width>=580;
            GUI.Label(new Rect(rect.x+22,rect.y+17,rect.width-44,38),nation.Name.ToUpper(),CampaignUI.Title);
            GUI.Label(new Rect(rect.x+22,rect.y+58,rect.width-44,25),nation.Role,CampaignUI.Hint);
            float portraitWidth=side?rect.width*.45f:rect.width-44,portraitHeight=side?166:140;
            Rect portrait=new Rect(rect.x+22,rect.y+96,portraitWidth,portraitHeight);
            CartoonUi.Panel(portrait,CartoonUi.PanelDark,6,0);
            CampaignUI.Rule(new Rect(portrait.x+16,portrait.yMax-24,portrait.width-32,2),CartoonUi.PanelColor);
            CampaignTank(new Rect(portrait.x+12,portrait.y+2,portrait.width-24,portrait.height-9),nation.Id,1);
            float sx=side?portrait.xMax+24:rect.x+22,sy=side?rect.y+98:portrait.yMax+14,sw=side?rect.xMax-sx-22:rect.width-44;
            UiIcon[] icons={UiIcon.Health,UiIcon.Armor,UiIcon.Damage,UiIcon.Range};string[] values={nation.MaxHp+" HP",nation.Armor+" giáp",nation.ShotDamage+" sát thương","Tầm bắn 4 ô"};
            for(int stat=0;stat<4;stat++){
                float w=sw/2,x=sx+(stat%2)*w,y=sy+(stat/2)*48;
                CartoonUi.Icon(new Rect(x,y+3,21,21),icons[stat],CartoonUi.PanelDark);
                GUI.Label(new Rect(x+30,y,w-32,44),values[stat],CampaignUI.Text);
            }
            string melee="Cận chiến "+nation.MeleeDamage+" · Hồi bắn 5 lượt";
            float meleeY=side?sy+111:sy+104,meleeHeight=CampaignLabelHeight(CampaignUI.Hint,melee,sw,24);
            GUI.Label(new Rect(sx,meleeY,sw,meleeHeight),melee,CampaignUI.Hint);
            float skillY=side?Mathf.Max(portrait.yMax+18,meleeY+meleeHeight+14):meleeY+meleeHeight+18;
            string skillTitle=ClientPreferences.Active.Label(ControlAction.CampaignSkill)+" · "+nation.SkillName+" · "+nation.SkillCooldown+" lượt";float titleHeight=CampaignLabelHeight(CampaignUI.DialogueTitle,skillTitle,rect.width-48,26),textHeight=CampaignLabelHeight(CampaignUI.DialogueText,nation.SkillDescription,rect.width-48);
            CampaignUI.Rule(new Rect(rect.x+22,skillY-10,rect.width-44,1),CartoonUi.Outline);
            GUI.Label(new Rect(rect.x+24,skillY,rect.width-48,titleHeight),skillTitle,CampaignUI.DialogueTitle);
            GUI.Label(new Rect(rect.x+24,skillY+titleHeight+7,rect.width-48,textHeight),nation.SkillDescription,CampaignUI.DialogueText);
        }
        void DrawCampaignRanking(float contentTop){
            int count=client.Campaign.Rankings.Count;
            if(campaignRankingWrapped==null)campaignRankingWrapped=new GUIStyle(CampaignUI.RankingName){wordWrap=true};
            float buttonWidth=Mathf.Min(280,(viewWidth-72)/2),buttonX=(viewWidth-buttonWidth*2-16)/2,buttonHeight=Mathf.Max(CampaignButtonHeight("Đang tải…",buttonWidth,52),CampaignButtonHeight("Chọn xe",buttonWidth,52)),buttonY=viewHeight-18-buttonHeight;
            float width=Mathf.Min(1120,viewWidth-56),height=buttonY-16-contentTop;
            Rect frame=new Rect((viewWidth-width)/2,contentTop,width,height);
            CartoonUi.Panel(frame,CartoonUi.PanelDark,8,0);
            string note=string.IsNullOrEmpty(client.Campaign.Error)?"Kết quả tốt nhất mỗi chỉ huy · Cho phép đồng hạng · Thời gian phút:giây.mili giây · —: chưa có dữ liệu":client.Campaign.Error;
            float noteHeight=CampaignLabelHeight(CampaignUI.SmallLight,note,width-44,24),innerWidth=width-60,nameWidth=Mathf.Max(120,innerWidth-330),contentHeight=0;
            float[] rowHeights=new float[count];
            for(int i=0;i<count;i++){
                var row=client.Campaign.Rankings[i];string name=WrapMenuText(row.Name+(row.Name==client.Nickname?" · BẠN":""),campaignRankingWrapped,nameWidth);
                rowHeights[i]=Mathf.Max(116,CampaignLabelHeight(campaignRankingWrapped,name,nameWidth)+58);contentHeight+=rowHeights[i]+8;
            }
            string empty=client.Campaign.Pending?"Đang tải bảng xếp hạng…":"Chưa có chuyến đi được ghi nhận. Hãy trở thành chỉ huy đầu tiên!";
            float emptyHeight=CampaignLabelHeight(CampaignUI.Light,empty,innerWidth-48,52);
            if(count==0)contentHeight=132+emptyHeight;
            GUI.Label(new Rect(frame.x+22,frame.y+16,width-44,24),"CHIẾN SỬ  /  QUÃNG ĐƯỜNG XA NHẤT",CampaignUI.SmallLight);
            Rect viewport=new Rect(frame.x+20,frame.y+52,width-40,Mathf.Max(50,height-noteHeight-82));
            campaignScroll=GUI.BeginScrollView(viewport,campaignScroll,new Rect(0,0,innerWidth,Mathf.Max(viewport.height,contentHeight)));
            float rowY=0;
            for(int i=0;i<count;i++){
                var row=client.Campaign.Rankings[i];bool own=row.Name==client.Nickname;Rect rect=new Rect(0,rowY,innerWidth,rowHeights[i]);
                CartoonUi.Panel(rect,own?CartoonUi.PanelColor:CartoonUi.Backdrop,5,0);
                if(own)CampaignUI.Rule(new Rect(0,rowY+6,4,rect.height-12),CartoonUi.Ready);
                if(row.Rank<=3)CartoonUi.Medal(new Rect(14,rowY+15,52,62),row.Rank);
                else GUI.Label(new Rect(14,rowY+23,54,40),row.Rank.ToString("00"),CampaignUI.RankingDistance);
                string name=WrapMenuText(row.Name+(own?" · BẠN":""),campaignRankingWrapped,nameWidth);
                float nameHeight=CampaignLabelHeight(campaignRankingWrapped,name,nameWidth,27);
                GUI.Label(new Rect(84,rowY+15,nameWidth,nameHeight),name,campaignRankingWrapped);
                GUI.Label(new Rect(84,rowY+Mathf.Max(76,nameHeight+20),innerWidth-104,26),"Vàng nhận: "+(row.DetailsAvailable?row.Gold.ToString("N0"):"—")+"   /   Thời gian: "+CampaignUI.DurationLabel(row.DurationMillis),CampaignUI.SmallLight);
                GUI.Label(new Rect(innerWidth-226,rowY+14,210,34),row.Score.ToString("N0")+" m",CampaignUI.RankingDistance);
                CampaignUI.Meter(new Rect(innerWidth-226,rowY+56,210,8),row.Score/(float)CampaignClient.GoalDistance,own?CartoonUi.Ready:CartoonUi.Gold);
                rowY+=rect.height+8;
            }
            if(count==0){
                CartoonUi.Icon(new Rect(innerWidth/2-32,28,64,64),UiIcon.Trophy,CartoonUi.Gold);
                GUI.Label(new Rect(24,124,innerWidth-48,emptyHeight),empty,CampaignUI.Light);
            }
            GUI.EndScrollView();
            GUI.Label(new Rect(frame.x+22,frame.yMax-noteHeight-12,width-44,noteHeight),note,CampaignUI.SmallLight);
            if(CampaignUI.Button(new Rect(buttonX,buttonY,buttonWidth,buttonHeight),client.Campaign.Pending?"Đang tải…":"Làm mới",!client.Campaign.Pending,true))client.CampaignCommand(3);
            if(CampaignUI.Button(new Rect(buttonX+buttonWidth+16,buttonY,buttonWidth,buttonHeight),"Chọn xe",!client.Campaign.Pending)){campaignPage=1;campaignScroll=Vector2.zero;}
        }
        void DrawCampaignResults(RunState run){
            ArenaRenderer.Fill(new Rect(0,0,viewWidth,viewHeight),new Color(.035f,.055f,.04f,.9f));
            float width=Mathf.Min(760,viewWidth-32),height=Mathf.Min(700,viewHeight-32);Rect card=new Rect((viewWidth-width)/2,(viewHeight-height)/2,width,height);
            CartoonUi.Panel(card,CartoonUi.PanelDark,10,0);
            CampaignUI.Rule(new Rect(card.x+20,card.y+1,width-40,5),run.Victory?CartoonUi.Ready:CartoonUi.Gold);
            bool saved=run.SaveStatus=="Đã lưu kết quả.",error=!string.IsNullOrEmpty(client.Campaign.Error);
            string status=error?client.Campaign.Error:string.IsNullOrEmpty(run.SaveStatus)?"Đang chờ lưu kết quả…":run.SaveStatus,primary=client.Campaign.Pending?"Đang xử lý…":saved?"Chọn xe / Chơi lại":"Lưu lại";
            float buttonWidth=(width-68)/2,actionHeight=Mathf.Max(CampaignButtonHeight(primary,buttonWidth,54),CampaignButtonHeight("Về menu",buttonWidth,54)),actionY=card.yMax-20-actionHeight;
            Rect viewport=new Rect(card.x+20,card.y+18,width-40,actionY-card.y-36);float bodyWidth=viewport.width-20;
            string title=run.Victory?"GIẢI CỨU THÀNH CÔNG":"KẾT THÚC CHUYẾN ĐI",subtitle=CampaignNation.For(run.Nation).Name+" · "+run.Bosses+" / "+CampaignClient.BossCount+" trùm đã vượt";
            float titleHeight=CampaignLabelHeight(CampaignUI.Outcome,title,bodyWidth-36,38),titleY=106,subtitleY=titleY+titleHeight+6,distanceY=subtitleY+40;
            string distance=run.Distance.ToString("N0")+" m";float distanceHeight=CampaignLabelHeight(CampaignUI.GoldValue,distance,bodyWidth-32,52),meterY=distanceY+distanceHeight+8,statsY=meterY+46,statWidth=(bodyWidth-16)/3;
            string[] values={run.Kills.ToString(),run.Gold.ToString("N0"),run.TotalGold.ToString("N0")};string[] labels={"ĐỊCH ĐÃ HẠ","VÀNG NHẬN","VÀNG TÍCH LŨY"};
            float valueHeight=32,captionHeight=24;
            for(int i=0;i<3;i++){valueHeight=Mathf.Max(valueHeight,CampaignLabelHeight(CampaignUI.RankingDistance,values[i],statWidth-16));captionHeight=Mathf.Max(captionHeight,CampaignLabelHeight(CampaignUI.SmallLight,labels[i],statWidth-16));}
            float statHeight=valueHeight+captionHeight+24,noticeY=statsY+statHeight+16,noticeHeight=CampaignLabelHeight(CampaignUI.DialogueText,status,bodyWidth-32,26)+26;
            campaignResultScroll=GUI.BeginScrollView(viewport,campaignResultScroll,new Rect(0,0,bodyWidth,Mathf.Max(viewport.height,noticeY+noticeHeight+4)));
            CartoonUi.Panel(new Rect(bodyWidth/2-49,8,98,84),CartoonUi.Gold,8,0);
            CartoonUi.Icon(new Rect(bodyWidth/2-30,20,60,58),run.Victory?UiIcon.Trophy:UiIcon.Range,CartoonUi.Ink);
            GUI.Label(new Rect(18,titleY,bodyWidth-36,titleHeight),title,CampaignUI.Outcome);
            GUI.Label(new Rect(18,subtitleY,bodyWidth-36,28),subtitle,CampaignUI.CenteredLight);
            GUI.Label(new Rect(16,distanceY,bodyWidth-32,distanceHeight),distance,CampaignUI.GoldValue);
            GUI.Label(new Rect(16,meterY+20,bodyWidth-32,24),"QUÃNG ĐƯỜNG / 10.000 M",CampaignUI.CenteredLight);
            CampaignUI.Meter(new Rect(42,meterY,bodyWidth-84,10),run.Distance/(float)CampaignClient.GoalDistance,run.Victory?CartoonUi.Ready:CartoonUi.Gold);
            CampaignUI.Rule(new Rect(12,statsY-2,bodyWidth-24,1),CartoonUi.Outline);
            for(int i=0;i<3;i++){
                Rect stat=new Rect(i*(statWidth+8),statsY,statWidth,statHeight);
                GUI.Label(new Rect(stat.x+8,stat.y+10,stat.width-16,valueHeight),values[i],CampaignUI.CenteredValue);
                GUI.Label(new Rect(stat.x+8,stat.y+15+valueHeight,stat.width-16,captionHeight),labels[i],CampaignUI.CenteredLight);
            }
            CampaignUI.Notice(new Rect(0,noticeY,bodyWidth,noticeHeight),status,error,saved);
            GUI.EndScrollView();
            if(CampaignUI.Button(new Rect(card.x+28,actionY,buttonWidth,actionHeight),primary,!client.Campaign.Pending)){if(saved){campaignPage=1;campaignScroll=Vector2.zero;campaignResultScroll=Vector2.zero;}else client.CampaignCommand(4);}
            if(CampaignUI.Button(new Rect(card.x+40+buttonWidth,actionY,buttonWidth,actionHeight),"Về menu",saved&&!client.Campaign.Pending,true))campaignPage=0;
        }
    }
}
