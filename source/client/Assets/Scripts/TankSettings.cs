using System.IO;
using UnityEngine;
namespace Tank {
    public partial class TankApp {
        const string GithubUrl="https://github.com/BaoITuvagi/Tank_Game";
        const string FacebookUrl="https://www.facebook.com/huy.huu.nguyen.266934";
        bool settingsOpen;
        ClientPreferences settingsDraft;
        Vector2 settingsScroll;
        GUIStyle clearBindingButton,bindingButton,bindingLabel,bindingColumnLabel;
        int captureAction=-1;
        bool captureSecondary;
        string settingsMessage="",settingsLoadWarning="";
        string PreferencesPath=>Path.Combine(Application.persistentDataPath,"settings.json");
        void LoadPreferences(){ClientPreferences.Activate(ClientPreferences.Load(PreferencesPath,out settingsLoadWarning));}
        void OpenPreferences(){settingsDraft=ClientPreferences.Active.Copy();settingsOpen=true;settingsScroll=Vector2.zero;captureAction=-1;settingsMessage=settingsLoadWarning;GUI.FocusControl(null);}
        void ClosePreferences(){settingsOpen=false;captureAction=-1;settingsDraft=null;GUI.FocusControl(null);queuedCampaignAction=-1;nextCampaignRepeat=Time.unscaledTimeAsDouble+.18;}
        void DrawSettingsButton(Rect rect){if(GUI.Button(rect,"Cài đặt",menu.HeaderButton))OpenPreferences();}
        void DrawPreferencesDialog(){
            if(!settingsOpen)return;
            menu.Init();var previousSkin=GUI.skin;GUI.skin=menu.Skin;GUI.enabled=true;
            if(bindingButton==null){
                bindingButton=new GUIStyle(menu.Secondary){fixedWidth=0,fixedHeight=0,margin=new RectOffset(),padding=new RectOffset(8,8,5,5)};
                clearBindingButton=new GUIStyle(bindingButton){padding=new RectOffset(),fontSize=18};
                bindingLabel=new GUIStyle(menu.Text){alignment=TextAnchor.MiddleLeft};
                bindingColumnLabel=new GUIStyle(menu.Hint){alignment=TextAnchor.MiddleCenter};
            }
            var e=Event.current;
            if(e.type==EventType.KeyDown&&e.keyCode==KeyCode.Escape){if(captureAction>=0){captureAction=-1;settingsMessage="Đã hủy gán phím.";}else ClosePreferences();e.Use();if(!settingsOpen){GUI.skin=previousSkin;return;}}
            if(captureAction>=0&&(e.type==EventType.KeyDown||e.type==EventType.MouseDown)){
                KeyCode key=e.type==EventType.MouseDown?(KeyCode)((int)KeyCode.Mouse0+e.button):e.keyCode;
                if(key!=KeyCode.None){if(settingsDraft.TryBind((ControlAction)captureAction,captureSecondary,key,out settingsMessage)){captureAction=-1;settingsMessage="Đã gán phím. Bấm Lưu để áp dụng.";}e.Use();}
            }
            float screenWidth=CampaignOpen?viewWidth:Screen.width,screenHeight=CampaignOpen?viewHeight:Screen.height;
            ArenaRenderer.Fill(new Rect(0,0,screenWidth,screenHeight),new Color(.025f,.045f,.035f,.92f));
            float width=Mathf.Min(880,screenWidth-24),height=Mathf.Min(800,screenHeight-24);
            Rect card=new Rect((screenWidth-width)/2,(screenHeight-height)/2,width,height);menu.Card(card,"CÀI ĐẶT");
            GUILayout.BeginArea(new Rect(card.x+22,card.y+80,card.width-44,card.height-200));
            settingsScroll=GUILayout.BeginScrollView(settingsScroll);
            float contentWidth=MenuScrollContentWidth(card.width-44);
            GUILayout.BeginVertical(GUILayout.Width(contentWidth));
            bool capturing=captureAction>=0;GUI.enabled=!capturing;
            settingsDraft.SoundEnabled=menu.Toggle(settingsDraft.SoundEnabled,"Âm thanh: "+(settingsDraft.SoundEnabled?"BẬT":"TẮT"));
            GUILayout.Space(14);
            Rect links=GUILayoutUtility.GetRect(contentWidth,44,GUILayout.Width(contentWidth),GUILayout.Height(44));
            if(GUI.Button(new Rect(links.x,links.y,(contentWidth-12)/2,44),"GitHub",bindingButton))Application.OpenURL(GithubUrl);
            if(GUI.Button(new Rect(links.x+(contentWidth+12)/2,links.y,(contentWidth-12)/2,44),"Facebook",bindingButton))Application.OpenURL(FacebookUrl);
            if(client.Phase>=ClientPhase.LOBBY){
                GUILayout.Space(12);
                if(GUILayout.Button("Đăng xuất",menu.Danger,GUILayout.Height(44))){
                    ClosePreferences();client.Logout();roomForm=profileForm=joinForm=false;campaignPage=0;
                    GUILayout.EndVertical();GUILayout.EndScrollView();GUILayout.EndArea();GUI.skin=previousSkin;return;
                }
            }
            GUILayout.Space(24);
            GUILayout.Label("GÁN PHÍM ĐIỀU KHIỂN",menu.Section);
            GUILayout.Space(8);
            GUILayout.Label("Mỗi thao tác có 2 ô phím. Bấm ô rồi nhấn phím / nút chuột; Esc hủy. × bỏ gán. Lưu để áp dụng. F11: toàn màn hình.",menu.Hint);
            float nameWidth=contentWidth<640?132:202,clearWidth=32;
            float keyWidth=(contentWidth-nameWidth-112)/2,primaryX=nameWidth+16,secondaryX=primaryX+keyWidth+8+clearWidth+16;
            GUILayout.Space(16);
            Rect columns=GUILayoutUtility.GetRect(contentWidth,22,GUILayout.Width(contentWidth),GUILayout.Height(22));
            GUI.Label(new Rect(columns.x+primaryX,columns.y,keyWidth,22),"Phím chính",bindingColumnLabel);
            GUI.Label(new Rect(columns.x+secondaryX,columns.y,keyWidth,22),"Phím phụ",bindingColumnLabel);
            for(int i=0;i<settingsDraft.Bindings.Length;i++){
                if(i==0||i==4||i==9){GUILayout.Space(i==0?12:24);GUILayout.Label(i==0?"Di chuyển · dùng chung":i==4?"Đấu thường / Nhập vai":"Chiến dịch",menu.Name);GUILayout.Space(8);}
                var binding=settingsDraft.Bindings[i];
                Rect row=GUILayoutUtility.GetRect(contentWidth,48,GUILayout.Width(contentWidth),GUILayout.Height(48));
                GUI.Label(new Rect(row.x,row.y,nameWidth,48),ClientPreferences.ActionName(binding.Action),bindingLabel);
                DrawBindingSlot(binding,false,new Rect(row.x+primaryX,row.y+2,keyWidth,44),new Rect(row.x+primaryX+keyWidth+8,row.y+2,clearWidth,44));
                DrawBindingSlot(binding,true,new Rect(row.x+secondaryX,row.y+2,keyWidth,44),new Rect(row.x+secondaryX+keyWidth+8,row.y+2,clearWidth,44));
                GUILayout.Space(10);
            }
            GUI.enabled=true;
            GUILayout.EndVertical();GUILayout.EndScrollView();GUILayout.EndArea();
            string feedback=captureAction>=0?"Đang chờ phím · "+ClientPreferences.ActionName((ControlAction)captureAction)+" · Esc để hủy\n"+settingsMessage:settingsMessage;
            GUI.Label(new Rect(card.x+24,card.yMax-112,card.width-48,44),feedback,menu.Hint);
            GUI.enabled=captureAction<0;
            float buttonWidth=(width-60)/3;
            if(GUI.Button(new Rect(card.x+20,card.yMax-62,buttonWidth,42),"Mặc định",menu.Secondary)){settingsDraft=ClientPreferences.Defaults();settingsMessage="Đã khôi phục mặc định. Bấm Lưu để áp dụng.";}
            if(GUI.Button(new Rect(card.x+30+buttonWidth,card.yMax-62,buttonWidth,42),"Hủy",menu.Secondary))ClosePreferences();
            if(GUI.Button(new Rect(card.x+40+buttonWidth*2,card.yMax-62,buttonWidth,42),"Lưu",menu.Primary)){
                if(settingsDraft.Save(PreferencesPath,out settingsMessage)){
                    ClientPreferences.Activate(settingsDraft);if(!settingsDraft.SoundEnabled){skillAudio?.Stop();campaignAudio?.Silence();}
                    settingsLoadWarning="";ClosePreferences();
                }
            }
            GUI.enabled=true;GUI.skin=previousSkin;
        }
        void DrawBindingSlot(ControlBinding binding,bool secondary,Rect keyRect,Rect clearRect){
            KeyCode key=secondary?binding.Secondary:binding.Primary;
            string label=captureAction==(int)binding.Action&&captureSecondary==secondary?"Nhấn phím…":ClientPreferences.KeyName(key);
            if(GUI.Button(keyRect,label,bindingButton)){captureAction=(int)binding.Action;captureSecondary=secondary;settingsMessage="";GUI.FocusControl(null);}
            if(GUI.Button(clearRect,"×",clearBindingButton))settingsDraft.TryBind(binding.Action,secondary,KeyCode.None,out settingsMessage);
        }
    }
}
