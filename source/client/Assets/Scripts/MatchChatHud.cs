using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
namespace Tank {
    /// <summary>Secondary HUD surface. Input focus is independent of modal dialogs.</summary>
    public sealed class MatchChatHud {
        const string InputName="match-chat-input";
        public bool Typing {get;private set;}
        public bool InputBlocked {get {return Typing||releaseInput;}}
        public bool Expanded {get;private set;}
        bool releaseInput,focusInput,followEnd=true,initialized;
        ulong match;
        uint readId,cachedId;
        float cachedWidth=-1,contentHeight;
        Vector2 scroll;
        string draft="";
        Rect inputRect;
        GUIStyle text,name,input,button,header,hint;
        struct Run {public string Text;public Rect Rect;public int Palette;}
        readonly List<Run> runs=new List<Run>();
        public void Sync(ClientCore client,float screenHeight) {
            if(!initialized||match!=client.Chat.Match) {
                initialized=true;match=client.Chat.Match;Expanded=screenHeight>=720;readId=cachedId=0;
                draft="";Typing=focusInput=releaseInput=false;followEnd=true;scroll=Vector2.zero;cachedWidth=-1;inputRect=default(Rect);
            }
            if(client.Phase!=ClientPhase.PLAYING)Cancel();
        }
        public void BeforeInput(bool foreground,bool modal) {
            if(!foreground||modal){Cancel();return;}
            if(releaseInput&&!Input.GetKey(KeyCode.Return)&&!Input.GetKey(KeyCode.KeypadEnter)&&!Input.GetKey(KeyCode.Escape)&&!Input.GetMouseButton(0))releaseInput=false;
            if(Expanded&&Input.GetMouseButtonDown(0)&&inputRect.Contains(new Vector2(Input.mousePosition.x,Screen.height-Input.mousePosition.y)))Typing=true;
        }
        public void Cancel() {if(Typing)releaseInput=true;Typing=focusInput=false;draft="";}
        public void EndGUI(bool modal) {
            if(modal||Typing&&!focusInput&&GUI.GetNameOfFocusedControl()!=InputName)Cancel();
            if(!Typing&&GUI.GetNameOfFocusedControl()==InputName)GUI.FocusControl(null);
        }
        public int Unread(MatchChatState state) {int count=0;foreach(var message in state.Messages)if(message.Id>readId)count++;return count;}
        void Styles() {
            if(text!=null)return;
            text=CartoonUi.Label(15,CartoonUi.Text);text.richText=false;text.wordWrap=false;
            name=new GUIStyle(text);hint=CartoonUi.Label(13,CartoonUi.Muted);hint.richText=false;
            input=new GUIStyle(GUI.skin.textField){font=CartoonUi.BodyFont,fontSize=15,richText=false,padding=new RectOffset(7,7,5,5)};
            input.normal.textColor=input.focused.textColor=CartoonUi.Text;
            input.normal.background=input.focused.background=CartoonUi.SurfaceStyle(CartoonUi.Backdrop,5).normal.background;
            button=new GUIStyle(input){fontSize=16,fixedHeight=30,wordWrap=false,alignment=TextAnchor.MiddleCenter,padding=new RectOffset(5,5,2,2)};
            foreach(var state in new[]{button.normal,button.hover,button.active,button.focused,button.onNormal,button.onHover,button.onActive,button.onFocused}) {
                state.background=input.normal.background;state.textColor=CartoonUi.Text;
            }
            button.hover.textColor=button.focused.textColor=CartoonUi.Gold;
            button.active.textColor=CartoonUi.Ready;
            header=new GUIStyle(button){alignment=TextAnchor.UpperLeft,padding=new RectOffset(3,3,3,3)};
        }
        public void Toggle(ClientCore client) {Expanded=!Expanded;if(!Expanded){Cancel();GUI.FocusControl(null);}}
        public void Tabs(Rect rect,ClientCore client) {
            Styles();
            if(GUI.Button(new Rect(rect.x,rect.y,rect.width*.5f-3,30),"Xếp hạng",button)&&Expanded)Toggle(client);
            int unread=Unread(client.Chat);
            if(GUI.Button(new Rect(rect.x+rect.width*.5f+3,rect.y,rect.width*.5f-3,30),"Chat"+(unread>0?" · "+unread:""),header))Toggle(client);
        }
        public void Draw(Rect rect,ClientCore client,bool showHeader=true) {
            Styles();
            CartoonUi.Panel(rect,CartoonUi.Backdrop,6);
            float top=rect.y+5;
            if(showHeader) {
                int unread=Unread(client.Chat);
                if(GUI.Button(new Rect(rect.x+5,top,rect.width-10,30),"CHAT"+(unread>0?" · "+unread:""),header))Toggle(client);
                top+=35;
            }
            if(!Expanded){inputRect=default(Rect);return;}
            float errorHeight=string.IsNullOrEmpty(client.ChatError)?0:Mathf.Min(44,hint.CalcHeight(new GUIContent(client.ChatError),rect.width-16)+4);
            float entryY=rect.yMax-37;
            bool unseen=!followEnd&&Unread(client.Chat)>0;
            var viewport=new Rect(rect.x+8,top,rect.width-16,Mathf.Max(12,entryY-top-7-errorHeight-(unseen?26:0)));
            Rebuild(client.Chat,viewport.width-18);
            float max=Mathf.Max(0,contentHeight-viewport.height);
            if(followEnd)scroll.y=max;
            scroll=GUI.BeginScrollView(viewport,scroll,new Rect(0,0,viewport.width-18,contentHeight));
            foreach(var run in runs) {
                if(run.Rect.yMax<scroll.y||run.Rect.y>scroll.y+viewport.height)continue;
                if(run.Palette>=0){name.normal.textColor=TankPalette.Colors[run.Palette];GUI.Label(run.Rect,run.Text,name);}
                else GUI.Label(run.Rect,run.Text,text);
            }
            if(client.Chat.Messages.Count==0)GUI.Label(new Rect(0,0,viewport.width-20,40),"Chưa có tin nhắn.",hint);
            GUI.EndScrollView();
            followEnd=scroll.y>=max-3;
            if(followEnd&&GUI.enabled&&Event.current.type==EventType.Repaint)readId=client.Chat.LastId;
            if(unseen&&GUI.Button(new Rect(rect.x+8,viewport.yMax+2,rect.width-16,24),"Tin mới ↓",button)){followEnd=true;scroll.y=contentHeight;}
            if(errorHeight>0)GUI.Label(new Rect(rect.x+8,entryY-errorHeight-2,rect.width-16,errorHeight),client.ChatError,hint);
            inputRect=new Rect(rect.x+7,entryY,rect.width-67,30);
            var e=Event.current;
            bool focused=GUI.GetNameOfFocusedControl()==InputName;
            if(GUI.enabled&&focused&&e.type==EventType.KeyDown) {
                if(e.keyCode==KeyCode.Escape){Cancel();GUI.FocusControl(null);e.Use();}
                else if((e.keyCode==KeyCode.Return||e.keyCode==KeyCode.KeypadEnter)&&!e.alt&&!e.control&&!e.command&&!e.shift&&Input.compositionString.Length==0){Send(client);e.Use();}
            }
            GUI.SetNextControlName(InputName);
            draft=GUI.TextField(inputRect,draft,400,input);
            if(focusInput&&e.type==EventType.Repaint){GUI.FocusControl(InputName);focusInput=false;}
            if(GUI.GetNameOfFocusedControl()==InputName&&GUI.enabled)Typing=true;
            if(GUI.enabled&&e.type==EventType.Repaint&&GUI.GetNameOfFocusedControl()==InputName) {
                var editor=(TextEditor)GUIUtility.GetStateObject(typeof(TextEditor),GUIUtility.keyboardControl);
                if(editor.cursorIndex==editor.selectIndex&&Time.unscaledTime%1f<.6f) {
                    Vector2 position=input.GetCursorPixelPosition(inputRect,new GUIContent(draft),Mathf.Clamp(editor.cursorIndex,0,draft.Length));
                    float cx=Mathf.Clamp(position.x-editor.scrollOffset.x,inputRect.x+input.padding.left,inputRect.xMax-input.padding.right-2);
                    ArenaRenderer.Fill(new Rect(cx,inputRect.center.y-9,2,18),CartoonUi.Text);
                }
            }
            if(draft.Length==0&&!Typing&&e.type==EventType.Repaint)GUI.Label(new Rect(inputRect.x+7,inputRect.y+6,inputRect.width-14,20),"Bấm để trò chuyện…",hint);
            bool enabled=GUI.enabled;GUI.enabled=enabled&&!client.ChatPending;
            if(GUI.Button(new Rect(rect.xMax-55,entryY,48,30),"Gửi",button))Send(client);
            GUI.enabled=enabled;
            if(Typing&&e.type==EventType.MouseDown&&!inputRect.Contains(e.mousePosition)){Cancel();GUI.FocusControl(null);}
        }
        void Send(ClientCore client) {
            if(client.SendChat(draft)){Cancel();GUI.FocusControl(null);followEnd=true;}
            else {Typing=true;focusInput=true;}
        }
        void Rebuild(MatchChatState state,float width) {
            if(cachedId==state.LastId&&Mathf.Approximately(cachedWidth,width))return;
            cachedId=state.LastId;cachedWidth=width;runs.Clear();float y=0;
            foreach(var message in state.Messages) {
                float x=0;Append(message.Name+": ",message.Palette,width,ref x,ref y);Append(message.Text,-1,width,ref x,ref y);y+=26;
            }
            contentHeight=y;
        }
        void Append(string value,int palette,float width,ref float x,ref float y) {
            string line="";float left=x;
            var words=value.Split(' ');
            for(int i=0;i<words.Length;i++) {
                string token=words[i]+(i<words.Length-1?" ":"");
                float wordWidth=text.CalcSize(new GUIContent(words[i])).x;
                if(x>0&&x+wordWidth>width&&wordWidth<=width) {
                    if(line.Length>0)runs.Add(new Run{Text=line,Rect=new Rect(left,y,x-left+2,24),Palette=palette});
                    line="";x=left=0;y+=21;
                }
                var elements=StringInfo.GetTextElementEnumerator(token);
                while(elements.MoveNext()) {
                    string element=elements.GetTextElement();float size=text.CalcSize(new GUIContent(element)).x;
                    if(x>0&&x+size>width) {
                        if(line.Length>0)runs.Add(new Run{Text=line,Rect=new Rect(left,y,x-left+2,24),Palette=palette});
                        line="";x=left=0;y+=21;
                    }
                    if(x==0&&element==" ")continue;
                    line+=element;x+=size;
                }
            }
            if(line.Length>0)runs.Add(new Run{Text=line,Rect=new Rect(left,y,Mathf.Max(1,x-left+2),24),Palette=palette});
        }
    }
}
