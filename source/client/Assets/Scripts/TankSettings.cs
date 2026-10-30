using System.IO;
using UnityEngine;
namespace Tank {
    public partial class TankApp {
        const string GithubUrl="https://github.com/BaoITuvagi/Tank_Game";
        const string FacebookUrl="https://www.facebook.com/huy.huu.nguyen.266934";
        bool settingsOpen;
        ClientPreferences settingsDraft;
        string[] primaryBindingText,secondaryBindingText;
        Vector2 settingsScroll;
        GUIStyle bindingButton,bindingField,bindingLabel,bindingColumnLabel;
        string settingsMessage="",settingsLoadWarning="";
        string PreferencesPath=>Path.Combine(Application.persistentDataPath,"settings.json");
        void LoadPreferences(){ClientPreferences.Activate(ClientPreferences.Load(PreferencesPath,out settingsLoadWarning));}
        void ResetBindingText(){
            primaryBindingText=new string[settingsDraft.Bindings.Length];secondaryBindingText=new string[settingsDraft.Bindings.Length];
            for(int i=0;i<settingsDraft.Bindings.Length;i++){primaryBindingText[i]=ClientPreferences.BindingText(settingsDraft.Bindings[i].Primary);secondaryBindingText[i]=ClientPreferences.BindingText(settingsDraft.Bindings[i].Secondary);}
        }
        void OpenPreferences(){settingsDraft=ClientPreferences.Active.Copy();ResetBindingText();settingsOpen=true;settingsScroll=Vector2.zero;settingsMessage=settingsLoadWarning;GUI.FocusControl(null);}
        void ClosePreferences(){settingsOpen=false;settingsDraft=null;primaryBindingText=secondaryBindingText=null;fieldFocusRequest=null;GUI.FocusControl(null);queuedCampaignAction=-1;nextCampaignRepeat=Time.unscaledTimeAsDouble+.18;}
        void DrawSettingsButton(Rect rect,GUIStyle style=null){if(GUI.Button(rect,"Cài đặt",style??menu.HeaderButton))OpenPreferences();}
        void SavePreferences(){
            if(!settingsDraft.TryUpdateBindingText(primaryBindingText,secondaryBindingText,out settingsMessage)||!settingsDraft.Save(PreferencesPath,out settingsMessage))return;
            ClientPreferences.Activate(settingsDraft);if(!settingsDraft.SoundEnabled){skillAudio?.Stop();campaignAudio?.Silence();}
            settingsLoadWarning="";ClosePreferences();
        }
        void DrawPreferencesDialog(){
            if(!settingsOpen)return;
            if(primaryBindingText==null)ResetBindingText();
            menu.Init();var previousSkin=GUI.skin;GUI.skin=menu.Skin;GUI.enabled=true;
            if(bindingButton==null){
                bindingButton=new GUIStyle(menu.Secondary){fixedWidth=0,fixedHeight=0,margin=new RectOffset(),padding=new RectOffset(8,8,5,5)};
                bindingField=new GUIStyle(menu.Skin.textField){fixedWidth=0,fixedHeight=0,margin=new RectOffset(),padding=new RectOffset(12,12,5,5)};
                bindingLabel=new GUIStyle(menu.Text){alignment=TextAnchor.MiddleLeft};
                bindingColumnLabel=new GUIStyle(menu.Hint){alignment=TextAnchor.MiddleCenter};
            }
            var e=Event.current;
            if(e.type==EventType.KeyDown&&e.keyCode==KeyCode.Escape){ClosePreferences();e.Use();GUI.skin=previousSkin;return;}
            float screenWidth=CampaignOpen?viewWidth:Screen.width,screenHeight=CampaignOpen?viewHeight:Screen.height;
            ArenaRenderer.Fill(new Rect(0,0,screenWidth,screenHeight),new Color(.025f,.045f,.035f,.92f));
            float width=Mathf.Min(880,screenWidth-24),height=Mathf.Min(800,screenHeight-24);
            Rect card=new Rect((screenWidth-width)/2,(screenHeight-height)/2,width,height);menu.Card(card,"CÀI ĐẶT");
            float feedbackHeight=string.IsNullOrEmpty(settingsMessage)?0:Mathf.Clamp(menu.Hint.CalcHeight(new GUIContent(settingsMessage),card.width-48),24,96);
            GUILayout.BeginArea(new Rect(card.x+22,card.y+80,card.width-44,card.height-174-feedbackHeight));
            settingsScroll=GUILayout.BeginScrollView(settingsScroll);
            float contentWidth=MenuScrollContentWidth(card.width-44);
            GUILayout.BeginVertical(GUILayout.Width(contentWidth));
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
            GUILayout.Label("Nhập tên phím vào ô, ví dụ: W, Space, Shift trái, Chuột trái, Mouse3. Xóa nội dung để bỏ gán. Mỗi thao tác cần ít nhất 1 phím. Bấm Lưu để kiểm tra và áp dụng.",menu.Hint);
            float nameWidth=contentWidth<640?132:202;
            float keyWidth=(contentWidth-nameWidth-32)/2,primaryX=nameWidth+16,secondaryX=primaryX+keyWidth+16;
            GUILayout.Space(16);
            Rect columns=GUILayoutUtility.GetRect(contentWidth,22,GUILayout.Width(contentWidth),GUILayout.Height(22));
            GUI.Label(new Rect(columns.x+primaryX,columns.y,keyWidth,22),"Phím chính",bindingColumnLabel);
            GUI.Label(new Rect(columns.x+secondaryX,columns.y,keyWidth,22),"Phím phụ",bindingColumnLabel);
            for(int i=0;i<settingsDraft.Bindings.Length;i++){
                if(i==0||i==4||i==9){GUILayout.Space(i==0?12:24);GUILayout.Label(i==0?"Di chuyển · dùng chung":i==4?"Đấu thường / Nhập vai":"Chiến dịch",menu.Name);GUILayout.Space(8);}
                Rect row=GUILayoutUtility.GetRect(contentWidth,48,GUILayout.Width(contentWidth),GUILayout.Height(48));
                GUI.Label(new Rect(row.x,row.y,nameWidth,48),ClientPreferences.ActionName((ControlAction)i),bindingLabel);
                DrawBindingSlot(i,false,new Rect(row.x+primaryX,row.y+2,keyWidth,44));
                DrawBindingSlot(i,true,new Rect(row.x+secondaryX,row.y+2,keyWidth,44));
                GUILayout.Space(10);
            }
            GUILayout.EndVertical();GUILayout.EndScrollView();GUILayout.EndArea();
            if(feedbackHeight>0)GUI.Label(new Rect(card.x+24,card.yMax-78-feedbackHeight,card.width-48,feedbackHeight),settingsMessage,menu.Hint);
            float buttonWidth=(width-60)/3;
            if(GUI.Button(new Rect(card.x+20,card.yMax-62,buttonWidth,42),"Mặc định",menu.Secondary)){settingsDraft=ClientPreferences.Defaults();ResetBindingText();settingsMessage="Đã khôi phục mặc định. Bấm Lưu để áp dụng.";GUI.FocusControl(null);}
            if(GUI.Button(new Rect(card.x+30+buttonWidth,card.yMax-62,buttonWidth,42),"Hủy",menu.Secondary))ClosePreferences();
            bool save=GUI.Button(new Rect(card.x+40+buttonWidth*2,card.yMax-62,buttonWidth,42),"Lưu",menu.Primary)||fieldSubmitRequested;
            fieldSubmitRequested=false;
            if(save&&settingsOpen)SavePreferences();
            GUI.enabled=true;GUI.skin=previousSkin;
        }
        static string BindingControlName(int action,bool secondary){return "binding-"+action+(secondary?"-secondary":"-primary");}
        void DrawBindingSlot(int action,bool secondary,Rect keyRect){
            string key=BindingControlName(action,secondary);
            string next=!secondary?BindingControlName(action,true):action+1<settingsDraft.Bindings.Length?BindingControlName(action+1,false):null;
            HandleFieldEnter(key,next);
            GUI.SetNextControlName(key);
            var texts=secondary?secondaryBindingText:primaryBindingText;
            texts[action]=GUI.TextField(keyRect,texts[action],32,bindingField);
            FocusRequestedField(key);
        }
    }
}
