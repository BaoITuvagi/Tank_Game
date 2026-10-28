using UnityEngine;

namespace Tank {
    /// <summary>Presentation for network notices. Does not pause or change game input/state.</summary>
    public sealed class AdminNoticeOverlay : MonoBehaviour {
        readonly AdminNoticeQueue notices=new AdminNoticeQueue();
        readonly MenuTheme theme=new MenuTheme();
        GUIStyle titleStyle,textStyle,hintStyle,iconStyle;
        Vector2 scroll;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() {
            if(FindFirstObjectByType<AdminNoticeOverlay>()==null)
                new GameObject("Admin network notices").AddComponent<AdminNoticeOverlay>();
        }
        void Awake() {DontDestroyOnLoad(gameObject);}
        void OnEnable() {AdminNoticeChannel.Received+=Receive;}
        void OnDisable() {AdminNoticeChannel.Received-=Receive;}
        void Receive(AdminNotice notice) {
            if(notices.Current==null)scroll=Vector2.zero;
            notices.Enqueue(notice);
        }
        void Fill(Rect rect,Color color) {
            Color previous=GUI.color;
            GUI.color=color;
            GUI.DrawTexture(rect,Texture2D.whiteTexture);
            GUI.color=previous;
        }
        void InitStyles() {
            theme.Init();
            if(titleStyle!=null)return;
            titleStyle=new GUIStyle(theme.Section){fontSize=22,richText=false};
            textStyle=new GUIStyle(theme.Text){fontSize=17,wordWrap=true,richText=false};
            hintStyle=new GUIStyle(theme.Hint){richText=false};
            iconStyle=new GUIStyle(theme.Title){alignment=TextAnchor.MiddleCenter,richText=false};
        }
        void OnGUI() {
            AdminNotice notice=notices.Current;
            if(notice==null||Screen.width<=0||Screen.height<=0)return;
            Matrix4x4 previousMatrix=GUI.matrix;
            GUISkin previousSkin=GUI.skin;
            Color previousColor=GUI.color;
            bool previousEnabled=GUI.enabled;
            int previousDepth=GUI.depth;
            try {
                InitStyles();
                float uiScale=Mathf.Min(1f,Screen.width/380f,Screen.height/300f);
                float canvasWidth=Screen.width/uiScale,canvasHeight=Screen.height/uiScale;
                GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(uiScale,uiScale,1));
                GUI.skin=theme.Skin;
                GUI.color=Color.white;
                GUI.enabled=true;
                GUI.depth=-1000;

                float width=Mathf.Min(640,canvasWidth-32),height=Mathf.Min(400,canvasHeight-32);
                Rect box=new Rect((canvasWidth-width)/2,(canvasHeight-height)/2,width,height);
                Color accent=notice.Code==202?new Color(.69f,.24f,.22f):notice.Code==201?new Color(.65f,.44f,.12f):theme.Accent;
                Fill(new Rect(box.x+5,box.y+7,box.width,box.height),new Color(0,0,0,.35f));
                Fill(box,theme.Paper);
                Fill(new Rect(box.x,box.y,box.width,5),accent);
                Fill(new Rect(box.x+20,box.y+24,42,42),accent);
                iconStyle.normal.textColor=Color.white;
                GUI.Label(new Rect(box.x+20,box.y+24,42,42),notice.Code==200?"i":"!",iconStyle);
                string title=notice.Code==202?"Lỗi từ quản trị viên":notice.Code==201?"Cảnh báo từ quản trị viên":"Thông báo từ quản trị viên";
                titleStyle.fontSize=width<500?17:22;
                GUI.Label(new Rect(box.x+76,box.y+20,width-96,52),title,titleStyle);
                string subtitle="THÔNG BÁO MÁY CHỦ";
                if(notices.PendingCount>0)subtitle+=" · Còn "+notices.PendingCount+" thông báo";
                GUI.Label(new Rect(box.x+24,box.y+80,width-48,24),subtitle,hintStyle);

                Rect viewport=new Rect(box.x+24,box.y+116,width-48,height-194);
                float textWidth=viewport.width-22;
                float textHeight=Mathf.Max(viewport.height,textStyle.CalcHeight(new GUIContent(notice.Text),textWidth));
                scroll=GUI.BeginScrollView(viewport,scroll,new Rect(0,0,textWidth,textHeight));
                GUI.Label(new Rect(0,0,textWidth,textHeight),notice.Text,textStyle);
                GUI.EndScrollView();
                if(notices.DroppedCount>0)
                    GUI.Label(new Rect(box.x+24,box.yMax-72,width-48,22),"Đã lược bớt "+notices.DroppedCount+" tin cũ vì hàng đợi đầy.",hintStyle);
                if(GUI.Button(new Rect(box.xMax-160,box.yMax-52,136,36),notices.PendingCount>0?"Tin tiếp theo":"Đóng",theme.Primary)) {
                    notices.Dismiss();scroll=Vector2.zero;
                }
            }
            finally {
                GUI.matrix=previousMatrix;
                GUI.skin=previousSkin;
                GUI.color=previousColor;
                GUI.enabled=previousEnabled;
                GUI.depth=previousDepth;
            }
        }
    }
}
