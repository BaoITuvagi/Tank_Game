using UnityEngine;
using Tank.Campaign;

namespace Tank {
    public partial class TankApp {
        void DrawCampaign(){
            CampaignUI.Ensure();
            if(campaignPage==3&&client.Phase==ClientPhase.LOBBY&&client.Campaign.Run!=null){DrawCampaignRun(client.Campaign.Run);return;}
            CampaignUI.Background(new Rect(0,0,viewWidth,viewHeight));
            GUI.Label(new Rect(28,20,viewWidth-240,40),campaignPage==2?"BẢNG CHỈ HUY":"CHIẾN DỊCH GIẢI CỨU",CampaignUI.Header);
            GUI.Label(new Rect(30,63,viewWidth-260,32),campaignPage==2?"Quãng đường xa nhất của các chuyến giải cứu":"Chọn xe theo lối chơi của bạn · Đích đến 10.000 m",CampaignUI.SmallLight);
            if(CampaignUI.Button(new Rect(viewWidth-170,24,142,40),"Về menu",!client.Campaign.Pending,true))campaignPage=0;
            if(client.Phase!=ClientPhase.LOBBY){CampaignUI.Notice(new Rect(28,126,viewWidth-56,92),"Đăng nhập để bắt đầu chiến dịch và lưu vàng, điểm số.");if(CampaignUI.Button(new Rect(40,238,220,44),"Về đăng nhập"))campaignPage=0;return;}
            if(campaignPage==2){DrawCampaignRanking();return;}
            Rect viewport=new Rect(24,108,viewWidth-48,Mathf.Max(140,viewHeight-258));
            int columns=viewWidth>=960?3:1;float width=(viewport.width-20-(columns-1)*16)/columns,cardHeight=columns==1?300:462;
            int rows=(3+columns-1)/columns;
            campaignScroll=GUI.BeginScrollView(viewport,campaignScroll,new Rect(0,0,viewport.width-20,rows*(cardHeight+16)));
            for(int i=0;i<3;i++)DrawCampaignNationCard(new Rect(i%columns*(width+16),i/columns*(cardHeight+16),width,cardHeight),i);
            GUI.EndScrollView();
            string tip=string.IsNullOrEmpty(client.Campaign.Error)?"WASD / mũi tên: di chuyển   •   I: bắn (hồi 5 lượt)   •   O: kỹ năng quốc gia   •   P: tiếp tế":client.Campaign.Error;
            CampaignUI.Notice(new Rect(28,viewHeight-136,viewWidth-56,62),tip);
            float buttonWidth=Mathf.Min(270,(viewWidth-80)/3);
            string selected=campaignChoice>=0?CampaignNation.For(campaignNations[campaignChoice]).Name:"chọn xe";
            if(CampaignUI.Button(new Rect(28,viewHeight-60,buttonWidth,44),"Bắt đầu · "+selected,campaignChoice>=0&&!client.Campaign.Pending)){client.CampaignCommand(0,campaignNations[campaignChoice]);campaignPage=3;}
            if(CampaignUI.Button(new Rect(40+buttonWidth,viewHeight-60,buttonWidth,44),"Bảng xếp hạng",!client.Campaign.Pending,true)){campaignPage=2;campaignScroll=Vector2.zero;client.CampaignCommand(3);}
            if(client.Campaign.Run!=null&&!client.Campaign.Run.Finished&&CampaignUI.Button(new Rect(52+buttonWidth*2,viewHeight-60,buttonWidth,44),"Tiếp tục chuyến đi",!client.Campaign.Pending,true))campaignPage=3;
        }
        void DrawCampaignNationCard(Rect rect,int index){
            var nation=CampaignNation.For(campaignNations[index]);bool chosen=campaignChoice==index;
            CampaignUI.Panel(rect,chosen);float x=rect.x+20,w=rect.width-40;
            GUI.Label(new Rect(x,rect.y+18,w,30),nation.Name,CampaignUI.Title);
            GUI.Label(new Rect(x,rect.y+54,w,36),nation.Role,CampaignUI.Hint);
            if(chosen)GUI.Label(new Rect(rect.xMax-108,rect.y+23,90,22),"ĐÃ CHỌN",CampaignUI.Hint);
            GUI.Box(new Rect(x,rect.y+98,104,100),GUIContent.none,CampaignUI.Slot);
            CampaignTank(new Rect(x+12,rect.y+110,80,76),nation.Id,0);
            GUI.Label(new Rect(x+120,rect.y+99,w-120,32),nation.MaxHp+" HP",CampaignUI.Title);
            GUI.Label(new Rect(x+120,rect.y+137,w-120,58),"Giáp "+nation.Armor+" / đòn\nCận chiến "+nation.MeleeDamage,CampaignUI.Hint);
            if(rect.width>=600){
                GUI.Label(new Rect(x,rect.y+214,230,30),"I · "+nation.ShotDamage+" sát thương · 4 ô",CampaignUI.Text);
                GUI.Label(new Rect(x,rect.y+245,230,24),"Hồi bắn 5 lượt",CampaignUI.Hint);
                float sx=x+244,sw=w-244;
                CampaignUI.Notice(new Rect(sx,rect.y+94,sw,130),"");
                GUI.Label(new Rect(sx+14,rect.y+108,sw-28,26),"O · "+nation.SkillName,CampaignUI.Text);
                GUI.Label(new Rect(sx+14,rect.y+141,sw-28,78),nation.SkillDescription,CampaignUI.Hint);
                GUI.Label(new Rect(sx,rect.y+232,sw,24),"Hồi kỹ năng "+nation.SkillCooldown+" lượt",CampaignUI.Hint);
                if(CampaignUI.Button(new Rect(sx,rect.y+258,sw,32),chosen?"Đã chọn "+nation.Name:"Chọn "+nation.Name,!client.Campaign.Pending,false,chosen))campaignChoice=index;
                return;
            }
            GUI.Label(new Rect(x,rect.y+214,w,30),"I · "+nation.ShotDamage+" sát thương · tầm 4 ô",CampaignUI.Text);
            GUI.Label(new Rect(x,rect.y+245,w,24),"Hồi bắn 5 lượt",CampaignUI.Hint);
            CampaignUI.Notice(new Rect(x-4,rect.y+278,w+8,112),"");
            GUI.Label(new Rect(x+10,rect.y+288,w-20,24),"O · "+nation.SkillName,CampaignUI.Text);
            GUI.Label(new Rect(x+10,rect.y+316,w-20,68),nation.SkillDescription,CampaignUI.Hint);
            GUI.Label(new Rect(x,rect.y+397,w,22),"Hồi kỹ năng "+nation.SkillCooldown+" lượt",CampaignUI.Hint);
            if(CampaignUI.Button(new Rect(x,rect.y+422,w,34),chosen?"Đã chọn "+nation.Name:"Chọn "+nation.Name,!client.Campaign.Pending,false,chosen))campaignChoice=index;
        }
        void DrawCampaignRanking(){
            CampaignUI.Panel(new Rect(24,108,viewWidth-48,viewHeight-190));
            GUI.Label(new Rect(48,128,viewWidth-96,30),"Tổng vàng tích lũy: "+client.Campaign.TotalGold,CampaignUI.Title);
            Rect viewport=new Rect(44,176,viewWidth-88,Mathf.Max(80,viewHeight-280));
            campaignScroll=GUI.BeginScrollView(viewport,campaignScroll,new Rect(0,0,viewport.width-20,Mathf.Max(90,client.Campaign.Rankings.Count*48+46)));
            GUI.Label(new Rect(12,4,68,28),"HẠNG",CampaignUI.Hint);GUI.Label(new Rect(98,4,viewport.width-260,28),"CHỈ HUY",CampaignUI.Hint);GUI.Label(new Rect(viewport.width-146,4,120,28),"QUÃNG ĐƯỜNG",CampaignUI.Hint);
            for(int i=0;i<client.Campaign.Rankings.Count;i++){var row=client.Campaign.Rankings[i];float y=42+i*48;GUI.Label(new Rect(16,y,60,30),(i+1).ToString("00"),CampaignUI.Title);GUI.Label(new Rect(98,y,viewport.width-270,30),row.Name,CampaignUI.Text);GUI.Label(new Rect(viewport.width-146,y,120,30),row.Score+" m",CampaignUI.Text);}
            if(client.Campaign.Rankings.Count==0)GUI.Label(new Rect(12,50,viewport.width-40,40),client.Campaign.Pending?"Đang tải…":"Chưa có chuyến đi được ghi nhận.",CampaignUI.Text);
            GUI.EndScrollView();
            if(CampaignUI.Button(new Rect(28,viewHeight-60,210,44),"Làm mới",!client.Campaign.Pending))client.CampaignCommand(3);
            if(CampaignUI.Button(new Rect(252,viewHeight-60,210,44),"Chọn xe",!client.Campaign.Pending,true)){campaignPage=1;campaignScroll=Vector2.zero;}
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
