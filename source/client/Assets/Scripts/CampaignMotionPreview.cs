using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Tank.Campaign {
    // Explicit QA only. Uses the production renderer and never connects to a server.
    public sealed class CampaignMotionPreview : MonoBehaviour {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Boot(){if(Array.IndexOf(Environment.GetCommandLineArgs(),"--campaign-motion-preview")>=0)new GameObject("Campaign Motion Preview").AddComponent<CampaignMotionPreview>();}
        static RunState Clone(RunState a) {
            var b=new RunState{Id=a.Id,Nation=a.Nation,Turn=a.Turn+1,X=a.X,Y=a.Y,Bottom=a.Bottom,Distance=a.Distance,Hp=a.Hp,Facing=a.Facing,Shot=a.Shot,Skill=a.Skill,Gold=a.Gold,Kills=a.Kills,Shields=a.Shields,DoubleHits=a.DoubleHits,Bosses=a.Bosses,Message="",SaveStatus="",Terrain=new byte[247]};
            foreach(var e in a.Enemies)b.Enemies.Add(new Actor{Id=e.Id,X=e.X,Y=e.Y,Hp=e.Hp,MaxHp=e.MaxHp,Boss=e.Boss});return b;
        }
        static void Ground(RunState s){s.Bottom=s.Y-7;s.Distance=s.Y;for(int row=0;row<19;row++)for(int col=0;col<13;col++)s.Terrain[row*13+col]=(byte)((col<4||col>8)&&(col+row+s.Bottom)%9==0?1:0);}
        static CampaignVisual Event(VisualKind kind,int source,int target,int x,int y,int tx,int ty,int amount=0){return new CampaignVisual{Kind=kind,Source=source,Target=target,X=x,Y=y,TX=tx,TY=ty,Amount=amount};}
        IEnumerator Start() {
            string folder=Tank.UxTestDriver.Arg("--campaign-motion-preview","");Directory.CreateDirectory(folder);
            yield return new WaitForSecondsRealtime(1);
            var app=FindFirstObjectByType<Tank.TankApp>();var core=app.Core;
            int width=Screen.width,height=Screen.height;Screen.SetResolution(width+1,height,FullScreenMode.Windowed);yield return new WaitForSecondsRealtime(.3f);Screen.SetResolution(width,height,FullScreenMode.Windowed);yield return new WaitForSecondsRealtime(.3f);
            var state=new RunState{Id="motion-preview",Nation=0,X=6,Y=506,Distance=506,Hp=100,Facing=0,Gold=185,Message="",SaveStatus="",Terrain=new byte[247]};
            state.Enemies.Add(new Actor{Id=1,X=6,Y=508,Hp=60,MaxHp=60});state.Enemies.Add(new Actor{Id=2,X=8,Y=512,Hp=100,MaxHp=100});Ground(state);core.Campaign.Run=state;
            yield return new WaitForSecondsRealtime(.5f);
            var trace=new List<string>();int frame=0;
            foreach(string stage in new[]{"move","melee","shot","skill","move-pickup","move-shield","shield","heal"}) {
                var next=Clone(state);
                if(stage=="move"){next.Y++;next.Enemies[1].Y--;}
                if(stage=="melee"){
                    next.Enemies[0].Hp-=20;next.Hp-=8;
                    next.Visuals.Add(Event(VisualKind.Melee,0,1,6,507,6,508));next.Visuals.Add(Event(VisualKind.Hit,0,1,6,507,6,508,20));
                    next.Visuals.Add(Event(VisualKind.Melee,1,0,6,508,6,507));next.Visuals.Add(Event(VisualKind.Hit,1,0,6,508,6,507,8));
                }
                if(stage=="shot"){
                    next.Enemies.RemoveAt(0);next.Kills++;next.Shot=10;next.Enemies[0].X=7;
                    next.Visuals.Add(Event(VisualKind.Shot,0,1,6,507,6,508));next.Visuals.Add(Event(VisualKind.Hit,0,1,6,507,6,508,40));next.Visuals.Add(Event(VisualKind.Death,0,1,6,507,6,508));
                }
                if(stage=="skill"){
                    next.Skill=50;next.Enemies[0].Hp-=60;
                    next.Visuals.Add(Event(VisualKind.Skill,0,2,6,507,7,511));next.Visuals.Add(Event(VisualKind.Hit,0,2,6,507,7,511,60));
                    next.Enemies[0].Y--;
                }
                if(stage=="move-pickup"){next.Y++;next.Enemies[0].Y--;}
                if(stage=="move-shield"){next.Y++;next.Shields=1;}
                if(stage=="shield"){
                    next.Facing=1;next.Shields=0;next.Enemies[0].Hp-=20;
                    next.Visuals.Add(Event(VisualKind.Melee,0,2,6,509,7,509));next.Visuals.Add(Event(VisualKind.Hit,0,2,6,509,7,509,20));
                    next.Visuals.Add(Event(VisualKind.Melee,2,0,7,509,6,509));next.Visuals.Add(Event(VisualKind.Block,2,0,7,509,6,509));
                }
                if(stage=="heal"){next.Hp=100;next.Visuals.Add(Event(VisualKind.Heal,0,0,6,509,6,509,8));}
                Ground(next);core.Campaign.Run=next;state=next;
                double start=Time.unscaledTimeAsDouble;
                foreach(float sample in new[]{0f,.045f,.09f,.135f,.19f,.26f,.40f}) {
                    float delay=sample-(float)(Time.unscaledTimeAsDouble-start);if(delay>0)yield return new WaitForSecondsRealtime(delay);
                    yield return new WaitForEndOfFrame();
                    string file=Path.Combine(folder,frame.ToString("D3")+"-"+stage+".png");ScreenCapture.CaptureScreenshot(file);
                    trace.Add(frame+","+stage+","+(Time.unscaledTimeAsDouble-start).ToString("F4",System.Globalization.CultureInfo.InvariantCulture));frame++;
                }
                yield return new WaitForSecondsRealtime(.3f);
            }
            File.WriteAllLines(Path.Combine(folder,"frames.csv"),trace);yield return new WaitForSecondsRealtime(1);Application.Quit();
        }
    }
}
