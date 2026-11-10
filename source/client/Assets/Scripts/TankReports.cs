using UnityEngine;
namespace Tank {
    public partial class TankApp {
        int reportTarget=-1,reportReasons;
        ulong reportMatch;
        string reportName="",reportDetails="",reportError="";
        Vector2 reportScroll;
        GUIStyle reportTextArea,reportOption;
        string reportToast="";
        double reportToastUntil;
        void UpdateReportDialog() {
            if(reportTarget<0)return;
            if(client.ReportContextMatch!=reportMatch){reportTarget=-1;return;}
            if(!client.HasReported(reportTarget))return;
            reportToast=client.ReportMessage;reportToastUntil=ClientCore.NowUs+6e6;reportTarget=-1;
        }
        void DrawTakeControlOverlay() {
            if(!client.NeedsTakeover)return;
            float width=Mathf.Min(440,viewWidth-40);Rect box=new Rect((viewWidth-width)/2,82,width,96);
            CartoonUi.Panel(box,CartoonUi.PanelDark,8,0);
            GUI.Label(new Rect(box.x+12,box.y+8,width-24,32),"Xe đang chờ bạn hoặc do AI điều khiển.",menu.Hint);
            if(GUI.Button(new Rect(box.x+12,box.y+44,width-24,40),"Tiếp quản xe",menu.Primary))client.TakeControl();
        }
        void DrawReportToast() {
            if(string.IsNullOrEmpty(reportToast)||ClientCore.NowUs>=reportToastUntil)return;
            float width=Mathf.Min(700,viewWidth-48),height=menu.Hint.CalcHeight(new GUIContent(reportToast),width-32)+28;
            Rect box=new Rect((viewWidth-width)/2,86,width,height);
            CartoonUi.Panel(box,CartoonUi.PanelColor,8,0);GUI.Label(new Rect(box.x+16,box.y+14,width-32,height-28),reportToast,menu.Hint);
        }
        void OpenReport(Score score) {
            if(!client.CanReport(score.Slot))return;
            reportTarget=score.Slot;reportMatch=client.ReportContextMatch;reportName=score.Name;
            reportReasons=0;reportDetails=reportError="";reportScroll=Vector2.zero;client.ReportMessage="";GUI.FocusControl(null);
        }
        void DrawReportDialog() {
            if(client.ReportContextMatch!=reportMatch){reportTarget=-1;return;}
            bool sent=client.HasReported(reportTarget);
            if(Event.current.type==EventType.KeyDown&&Event.current.keyCode==KeyCode.Escape){reportTarget=-1;GUI.FocusControl(null);Event.current.Use();return;}
            ArenaRenderer.Fill(new Rect(0,0,viewWidth,viewHeight),new Color(0,0,0,.7f));
            float w=Mathf.Min(650,viewWidth-48),h=Mathf.Min(610,viewHeight-100);
            Rect card=new Rect((viewWidth-w)/2,(viewHeight-h)/2,w,h);menu.Card(card,"Tố cáo người chơi");
            GUILayout.BeginArea(new Rect(card.x+24,card.y+74,w-48,h-154));
            reportScroll=GUILayout.BeginScrollView(reportScroll);
            float contentWidth=MenuScrollContentWidth(w-48);
            GUILayout.BeginVertical(GUILayout.Width(contentWidth));
            GUILayout.Label(reportName,menu.Section);
            GUILayout.Label("Trận #"+reportMatch+" · Chọn hành vi bạn đã quan sát. Quản trị viên sẽ xem xét đơn, không tự động xử phạt.",menu.Hint);
            GUILayout.Space(12);
            bool enabled=GUI.enabled;GUI.enabled=enabled&&!client.ReportPending&&!sent;
            if(reportOption==null)reportOption=new GUIStyle(menu.Secondary){fixedHeight=0,fontSize=16,wordWrap=true,margin=new RectOffset(),padding=new RectOffset(12,12,8,10)};
            float optionWidth=(contentWidth-8)/2;
            for(int row=0;row<3;row++) {
                float optionHeight=48;
                for(int col=0;col<2;col++){int index=row*2+col;optionHeight=Mathf.Max(optionHeight,reportOption.CalcHeight(new GUIContent(ClientCore.ReportReasons[index]),optionWidth));}
                GUILayout.BeginHorizontal();
                for(int col=0;col<2;col++) {
                    int index=row*2+col,bit=1<<index;bool selected=(reportReasons&bit)!=0;
                    if(col>0)GUILayout.Space(8);
                    bool value=GUILayout.Toggle(selected,ClientCore.ReportReasons[index],reportOption,GUILayout.Width(optionWidth),GUILayout.Height(optionHeight));
                    if(value)reportReasons|=bit;else reportReasons&=~bit;
                }
                GUILayout.EndHorizontal();GUILayout.Space(5);
            }
            GUILayout.Space(10);GUILayout.Label("Mô tả sự việc (tùy chọn; bắt buộc với Lý do khác)",menu.Text);
            if(reportTextArea==null)reportTextArea=new GUIStyle(GUI.skin.textArea){font=menu.Text.font,fontSize=16,wordWrap=true,padding=new RectOffset(12,12,10,10)};
            GUI.SetNextControlName("report-details");reportDetails=GUILayout.TextArea(reportDetails,500,reportTextArea,GUILayout.MinHeight(92));
            GUILayout.Label(reportDetails.Length+" / 500 ký tự",menu.Hint);
            GUI.enabled=enabled;
            string feedback=string.IsNullOrEmpty(reportError)?client.ReportMessage:reportError;
            if(!string.IsNullOrEmpty(feedback))GUILayout.Label(feedback,menu.Hint);
            if(client.ReportPending)GUILayout.Label("Đang gửi đơn tới server…",menu.Hint);
            GUILayout.EndVertical();GUILayout.EndScrollView();GUILayout.EndArea();
            GUILayout.BeginArea(new Rect(card.x+24,card.yMax-62,w-48,46));GUILayout.BeginHorizontal();
            if(GUILayout.Button(sent?"Đóng":"Hủy",menu.Secondary,GUILayout.Height(42))){reportTarget=-1;GUI.FocusControl(null);}
            GUI.enabled=enabled&&!client.ReportPending&&!sent;
            if(GUILayout.Button("Gửi tố cáo",menu.Primary,GUILayout.Height(42))) {
                reportError=ClientCore.ReportValidationError(reportReasons,reportDetails);
                if(reportError=="")client.ReportPlayer(reportTarget,reportReasons,reportDetails);
            }
            GUI.enabled=enabled;GUILayout.EndHorizontal();GUILayout.EndArea();
        }
    }
}
