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
        public static int MovementButtons(bool up,bool down,bool left,bool right,bool arrowUp,bool arrowDown,bool arrowLeft,bool arrowRight) {
            return (up||arrowUp?1:0)|(down||arrowDown?2:0)|(left||arrowLeft?4:0)|(right||arrowRight?8:0);
        }
        public static InputSample Resolve(bool controlsEnabled,int buttons,int aim,int frozenAim) {
            return controlsEnabled?new InputSample(buttons,aim):new InputSample(0,frozenAim);
        }
    }
}
