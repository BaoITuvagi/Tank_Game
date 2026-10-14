namespace Tank  {
    public struct InputSample  {
        public int Buttons,Aim;
        public InputSample(int buttons,int aim) {
            Buttons=buttons;
            Aim=aim;
        }
    }
    public sealed class AutoFireControl  {
        public bool Enabled  {
            get;
            private set;
        }
        public void Update(bool playing,bool focused,bool toggle) {
            if(!playing||!focused)Enabled=false;
            else if(toggle)Enabled=!Enabled;
        }
        public int Apply(int buttons) {
            return Enabled?buttons|16:buttons;
        }
    }
    public static class GameplayInput  {
        public static InputSample Resolve(bool controlsEnabled,int buttons,int aim,int frozenAim) {
            return controlsEnabled?new InputSample(buttons,aim):new InputSample(0,frozenAim);
        }
    }
}
