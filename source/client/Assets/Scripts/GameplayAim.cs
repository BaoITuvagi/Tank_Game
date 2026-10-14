using System;
namespace Tank  {
    public static class GameplayAim  {
        // Screen input uses pixels; the arena uses scaled GUI coordinates and map units.
        public static int Angle(float screenX,float screenYFromTop,float uiScale,float arenaX,float arenaY,float unitScale,float mapHeight,TankState tank) {
            if(tank==null)return 0;
            float wx=(screenX/uiScale-arenaX)/unitScale,wy=mapHeight-(screenYFromTop/uiScale-arenaY)/unitScale;
            return ((int)Math.Round(Math.Atan2(wy-tank.Y,wx-tank.X)*180/Math.PI*100)+36000)%36000;
        }
    }
}
