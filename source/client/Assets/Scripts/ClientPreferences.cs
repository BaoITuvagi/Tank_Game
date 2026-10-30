using System;
using System.IO;
using UnityEngine;
namespace Tank {
    public enum ControlAction { Up,Down,Left,Right,Fire,Sprint,AutoFire,Skill,Shop,CampaignFire,CampaignSkill,CampaignShop }
    [Serializable] public sealed class ControlBinding {
        public ControlAction Action;
        public KeyCode Primary,Secondary;
        public ControlBinding(ControlAction action,KeyCode primary,KeyCode secondary=KeyCode.None){Action=action;Primary=primary;Secondary=secondary;}
    }
    [Serializable] public sealed class ClientPreferences {
        static readonly string[] ActionNames={"Di chuyển lên","Di chuyển xuống","Di chuyển trái","Di chuyển phải","Bắn","Tăng tốc","Bật / tắt tự bắn","Kỹ năng nhập vai","Shop nhập vai","Bắn chiến dịch","Kỹ năng chiến dịch","Tiếp tế chiến dịch"};
        public int Version=1;
        public bool SoundEnabled=true;
        public ControlBinding[] Bindings;
        public static ClientPreferences Active {get;private set;}=Defaults();
        public static void Activate(ClientPreferences value){Active=value;AudioListener.volume=value.SoundEnabled?1:0;}
        public static ClientPreferences Defaults(){return new ClientPreferences{Bindings=new[]{
            new ControlBinding(ControlAction.Up,KeyCode.W,KeyCode.UpArrow),new ControlBinding(ControlAction.Down,KeyCode.S,KeyCode.DownArrow),
            new ControlBinding(ControlAction.Left,KeyCode.A,KeyCode.LeftArrow),new ControlBinding(ControlAction.Right,KeyCode.D,KeyCode.RightArrow),
            new ControlBinding(ControlAction.Fire,KeyCode.Mouse0,KeyCode.Space),new ControlBinding(ControlAction.Sprint,KeyCode.LeftShift,KeyCode.RightShift),
            new ControlBinding(ControlAction.AutoFire,KeyCode.Mouse1),new ControlBinding(ControlAction.Skill,KeyCode.Q),new ControlBinding(ControlAction.Shop,KeyCode.E),
            new ControlBinding(ControlAction.CampaignFire,KeyCode.I),new ControlBinding(ControlAction.CampaignSkill,KeyCode.O),new ControlBinding(ControlAction.CampaignShop,KeyCode.P)
        }};}
        public ClientPreferences Copy(){return JsonUtility.FromJson<ClientPreferences>(JsonUtility.ToJson(this));}
        public static string ActionName(ControlAction action){
            return ActionNames[(int)action];
        }
        public static string KeyName(KeyCode key){
            switch(key){case KeyCode.None:return "Chưa gán";case KeyCode.Mouse0:return "Chuột trái";case KeyCode.Mouse1:return "Chuột phải";case KeyCode.Mouse2:return "Chuột giữa";
                case KeyCode.UpArrow:return "↑";case KeyCode.DownArrow:return "↓";case KeyCode.LeftArrow:return "←";case KeyCode.RightArrow:return "→";
                case KeyCode.LeftShift:return "Shift trái";case KeyCode.RightShift:return "Shift phải";case KeyCode.Space:return "Space";}
            return key.ToString();
        }
        public string Label(ControlAction action){var b=Bindings[(int)action];return b.Secondary==KeyCode.None?KeyName(b.Primary):b.Primary==KeyCode.None?KeyName(b.Secondary):KeyName(b.Primary)+" / "+KeyName(b.Secondary);}
        public string NationDescription(int nation){return RoleplayContent.NationDescriptions[nation].Replace("Q",Label(ControlAction.Skill));}
        static int Context(ControlAction action){return action<=ControlAction.Right?3:action>=ControlAction.CampaignFire?2:1;}
        public static bool Allowed(KeyCode key){return key==KeyCode.None||Enum.IsDefined(typeof(KeyCode),key)&&(int)key>0&&(int)key<330&&key!=KeyCode.Escape&&key!=KeyCode.F11;}
        public bool TryBind(ControlAction action,bool secondary,KeyCode key,out string error){
            error="";
            if((int)action<0||(int)action>=Bindings.Length){error="Thao tác không hợp lệ.";return false;}
            if(!Allowed(key)){error="Phím này không hỗ trợ. Esc và F11 được dành cho giao diện.";return false;}
            if(key!=KeyCode.None)foreach(var other in Bindings){
                if((Context(action)&Context(other.Action))==0)continue;
                bool same=other.Action==action;
                if((!same||secondary)&&other.Primary==key||(!same||!secondary)&&other.Secondary==key){error=KeyName(key)+" đã dùng cho "+ActionName(other.Action)+". Hãy bỏ gán phím cũ trước.";return false;}
            }
            if(secondary)Bindings[(int)action].Secondary=key;else Bindings[(int)action].Primary=key;
            return true;
        }
        public bool IsPressed(ControlAction action,Func<KeyCode,bool> read,bool allowMouse=true){var b=Bindings[(int)action];return Read(b.Primary,read,allowMouse)||Read(b.Secondary,read,allowMouse);}
        static bool Read(KeyCode key,Func<KeyCode,bool> read,bool mouse){return key!=KeyCode.None&&(mouse||key<KeyCode.Mouse0||key>KeyCode.Mouse6)&&read(key);}
        public bool Held(ControlAction action,bool allowMouse=true){return IsPressed(action,Input.GetKey,allowMouse);}
        public bool Pressed(ControlAction action,bool allowMouse=true){return IsPressed(action,Input.GetKeyDown,allowMouse);}
        public int MovementButtons(bool allowMouse=true){return (Held(ControlAction.Up,allowMouse)?1:0)|(Held(ControlAction.Down,allowMouse)?2:0)|(Held(ControlAction.Left,allowMouse)?4:0)|(Held(ControlAction.Right,allowMouse)?8:0);}
        bool Valid(){
            if(Version!=1||Bindings==null||Bindings.Length!=12)return false;
            for(int i=0;i<Bindings.Length;i++)if(Bindings[i]==null||Bindings[i].Action!=(ControlAction)i||!Allowed(Bindings[i].Primary)||!Allowed(Bindings[i].Secondary))return false;
            var check=Defaults();foreach(var b in check.Bindings){b.Primary=KeyCode.None;b.Secondary=KeyCode.None;}
            string error;foreach(var b in Bindings)if(!check.TryBind(b.Action,false,b.Primary,out error)||!check.TryBind(b.Action,true,b.Secondary,out error))return false;
            return true;
        }
        public static ClientPreferences Load(string path,out string warning){
            warning="";if(!File.Exists(path))return Defaults();
            try{var value=JsonUtility.FromJson<ClientPreferences>(File.ReadAllText(path));if(value==null||!value.Valid())throw new InvalidDataException();return value;}
            catch(Exception e) when(e is IOException||e is InvalidDataException||e is UnauthorizedAccessException||e is ArgumentException){warning="Không đọc được cài đặt cũ. Đang dùng thiết lập mặc định.";return Defaults();}
        }
        public bool Save(string path,out string error){
            error="";if(!Valid()){error="Thiết lập phím không hợp lệ.";return false;}
            try{string parent=Path.GetDirectoryName(path);if(!string.IsNullOrEmpty(parent))Directory.CreateDirectory(parent);File.WriteAllText(path+".tmp",JsonUtility.ToJson(this,true));if(File.Exists(path))File.Replace(path+".tmp",path,null);else File.Move(path+".tmp",path);return true;}
            catch(Exception e) when(e is IOException||e is UnauthorizedAccessException||e is ArgumentException){error="Không lưu được cài đặt. Vui lòng kiểm tra quyền ghi rồi thử lại.";return false;}
        }
    }
}
