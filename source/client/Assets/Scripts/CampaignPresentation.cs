using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tank.Campaign {
    // Purely visual timeline. The authoritative RunState is never interpolated or mutated.
    public sealed class CampaignPresentation {
        public const float TurnSeconds=.24f;
        public RunState Current {get;private set;}
        public RunState Previous {get;private set;}
        public double Started {get;private set;}
        public sealed class Effect {public CampaignVisual Visual;public double Started;public int Nation;}
        readonly List<Effect> effects=new List<Effect>();
        public IReadOnlyList<Effect> Effects {get{return effects;}}
        public bool Busy(double now){return Current!=null&&Previous!=null&&now-Started<TurnSeconds;}
        public bool ResultsReady(double now){return !Busy(now)&&(Previous==null||now-Started>=.65);}
        public void Reset(){Current=Previous=null;effects.Clear();}
        public void Observe(RunState state,double now) {
            effects.RemoveAll(e=>now-e.Started>.85);
            if(state==null){Reset();return;}
            if(Current==null||Current.Id!=state.Id||state.Turn<Current.Turn||state.Turn>Current.Turn+1){Current=state;Previous=null;effects.Clear();Started=now-1;return;}
            if(Current.Turn==state.Turn){Current=state;return;}
            Previous=Current;Current=state;Started=now;
            foreach(var visual in state.Visuals)effects.Add(new Effect{Visual=visual,Started=now,Nation=state.Nation});
        }
        static float Ease(double elapsed,float duration){float t=Mathf.Clamp01((float)elapsed/duration);return t*t*(3-2*t);}
        public Vector2 Player(double now){return Position(0,new Vector2(Current.X,Current.Y),now);}
        public Vector2 Enemy(Actor actor,double now){return Position(actor.Id,new Vector2(actor.X,actor.Y),now);}
        Vector2 Position(int id,Vector2 target,double now) {
            if(Previous==null)return target;
            Vector2 from=target;
            if(id==0)from=new Vector2(Previous.X,Previous.Y);
            else {var old=Previous.Enemies.Find(e=>e.Id==id);if(old!=null)from=new Vector2(old.X,old.Y);}
            double age=now-Started;
            Vector2 result=Vector2.Lerp(from,target,Ease(age-(id==0?0:.08),id==0?.14f:.16f));
            foreach(var v in Current.Visuals) {
                float delay=v.Source==0?0:.10f;
                if(v.Kind==VisualKind.Melee&&v.Source==id) {
                    float t=(float)(age-delay)/.14f;
                    if(t>0&&t<1)result+=new Vector2(v.TX-v.X,v.TY-v.Y).normalized*(Mathf.Sin(t*Mathf.PI)*.25f);
                }
                if(v.Kind==VisualKind.Hit&&v.Target==id) {
                    float t=(float)(age-Impact(v))/.12f;
                    if(t>0&&t<1)result+=new Vector2(v.TX-v.X,v.TY-v.Y).normalized*(Mathf.Sin(t*Mathf.PI)*.09f);
                }
            }
            return result;
        }
        public static float Impact(CampaignVisual v){return v.Kind==VisualKind.Heal?.23f:v.Source==0?.085f:.18f;}
        public Color Tint(int id,double now) {
            foreach(var e in effects)if(e.Visual.Kind==VisualKind.Hit&&e.Visual.Target==id){
                float t=(float)(now-e.Started)-Impact(e.Visual);
                if(t>=0&&t<.045f)return new Color(3,3,3,1);
                if(t>=.045f&&t<.15f)return Color.Lerp(new Color(1,.25f,.25f),Color.white,(t-.045f)/.105f);
            }
            return Color.white;
        }
        public List<Actor> Actors() {
            var actors=new List<Actor>(Current.Enemies);
            if(Previous!=null)foreach(var old in Previous.Enemies)if(Current.Enemies.Find(e=>e.Id==old.Id)==null&&Current.Visuals.Exists(v=>v.Kind==VisualKind.Death&&v.Target==old.Id))actors.Add(old);
            return actors;
        }
        public float Opacity(int id,double now) {
            if(Current.Enemies.Exists(e=>e.Id==id))return 1;
            return 1-Mathf.Clamp01(((float)(now-Started)-.085f)/.18f);
        }
        public float CameraBottom(int rows,double now) {
            float target=Mathf.Clamp(Current.Y-4,Current.Bottom,Current.Bottom+19-rows);
            if(Previous==null)return target;
            float from=Mathf.Clamp(Previous.Y-4,Previous.Bottom,Previous.Bottom+19-rows);
            return Mathf.Lerp(from,target,Ease(now-Started,.14f));
        }
        public byte Terrain(int x,int y) {
            if(x<0||x>=13||y<0||y>10000)return 1;
            if(y>=Current.Bottom&&y<Current.Bottom+19)return Current.Terrain[(y-Current.Bottom)*13+x];
            if(Previous!=null&&y>=Previous.Bottom&&y<Previous.Bottom+19)return Previous.Terrain[(y-Previous.Bottom)*13+x];
            return 0;
        }
        public Vector2 Shake(double now) {
            foreach(var e in effects)if(e.Visual.Kind==VisualKind.Hit&&e.Visual.Target==0){
                float t=(float)(now-e.Started)-Impact(e.Visual);
                if(t>=0&&t<.16f)return new Vector2(Mathf.Sin(t*120),Mathf.Cos(t*100))*(2.5f*(1-t/.16f));
            }
            return Vector2.zero;
        }
        static void Fill(Rect rect,Color color){ArenaRenderer.Fill(rect,color);}
        static void Line(Vector2 a,Vector2 b,float width,Color color) {
            Matrix4x4 previous=GUI.matrix;Vector2 delta=b-a;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg,a);
            Fill(new Rect(a.x,a.y-width/2,delta.magnitude,width),color);GUI.matrix=previous;
        }
        static void Ring(Vector2 center,float radius,Color color) {
            for(int i=0;i<16;i++){float angle=i*Mathf.PI/8;Vector2 p=center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;Fill(new Rect(p.x-2,p.y-2,4,4),color);}
        }
        static void Sparks(Vector2 center,float cell,float t,Color color,int seed) {
            for(int i=0;i<7;i++){float angle=i*2.4f+seed;Vector2 p=center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*cell*t*.65f;float size=Mathf.Max(2,cell*.10f*(1-t));Fill(new Rect(p.x-size/2,p.y-size/2,size,size),color);}
        }
        public void DrawEffects(Func<Vector2,Rect> tileAt,float cell,double now,GUIStyle text) {
            var labelStacks=new Dictionary<int,int>();
            foreach(var effect in effects) {
                var v=effect.Visual;float age=(float)(now-effect.Started),delay=v.Source==0?0:.10f;
                Vector2 a=tileAt(new Vector2(v.X,v.Y)).center,b=tileAt(new Vector2(v.TX,v.TY)).center;
                if(v.Kind==VisualKind.Melee){
                    float t=(age-delay-.045f)/.13f;
                    if(t>=0&&t<1){Color color=new Color(1,.92f,.63f,1-t);Vector2 offset=new Vector2(cell*.22f,-cell*.22f);Line(b-offset,b+offset,Mathf.Max(2,cell*.07f),color);Sparks(b,cell,t,color,v.Target);}
                }
                if(v.Kind==VisualKind.Shot&&age>=delay&&age<delay+.14f){
                    float shotAge=age-delay;
                    float t=Mathf.Clamp01(shotAge/.105f);Vector2 head=Vector2.Lerp(a,b,t),dir=(b-a).normalized;
                    Line(head-dir*cell*.35f,head,Mathf.Max(2,cell*.06f),new Color(1,.77f,.22f));
                    Fill(new Rect(head.x-3,head.y-3,6,6),Color.white);
                    if(shotAge<.06f)Sparks(a+dir*cell*.35f,cell,shotAge/.08f,new Color(1,.9f,.4f),0);
                }
                if(v.Kind==VisualKind.Skill&&age<.30f){
                    float t=Mathf.Clamp01(age/.30f);Color color=effect.Nation==5?new Color(.3f,.9f,1,1-t):new Color(1,.65f,.12f,1-t);
                    if(v.Source>0){Ring(b,cell*(.2f+t*.5f),new Color(1,.25f,.08f,1-t));Sparks(b,cell,t,new Color(1,.6f,.15f,1-t),v.Source);continue;}
                    if(effect.Nation==5){Line(a,b,cell*.18f*(1-t),color);Line(a,b,Mathf.Max(1,cell*.045f*(1-t)),new Color(1,1,1,1-t));}
                    else {Ring(b,cell*(.25f+t*(effect.Nation==0?2:3)),color);for(int i=0;i<(effect.Nation==0?1:3);i++){Vector2 start=b+new Vector2((i-1)*cell,-cell*2.5f);Line(Vector2.Lerp(start,b,Mathf.Clamp01(age/.09f)),b,3,color);}}
                    Sparks(b,cell,t,color,v.Target);
                }
                if(v.Kind==VisualKind.Hit||v.Kind==VisualKind.Block||v.Kind==VisualKind.Heal){
                    float t=age-Impact(v);if(t<0||t>.65f)continue;
                    Color color=v.Kind==VisualKind.Block?new Color(.4f,.85f,1):v.Kind==VisualKind.Heal?new Color(.4f,1,.5f):v.Target==0?new Color(1,.38f,.3f):new Color(1,.88f,.35f);
                    color.a=1-Mathf.Clamp01((t-.35f)/.3f);
                    if(t<.18f){if(v.Kind==VisualKind.Block)Ring(b,cell*.48f,color);else Sparks(b,cell,t/.18f,color,v.Target);}
                    string label=v.Kind==VisualKind.Block?"BLOCK":(v.Kind==VisualKind.Heal?"+":"-")+v.Amount;
                    var style=new GUIStyle(text){fontSize=Mathf.Clamp((int)(cell*.39f),14,25),alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold,wordWrap=false};
                    int stack;labelStacks.TryGetValue(v.Target,out stack);labelStacks[v.Target]=stack+1;
                    float offset=stack==0?0:(stack%2==1?-1:1)*cell*.32f;
                    Rect rect=new Rect(b.x-cell+offset,b.y-cell*.95f-cell*t*.7f-stack*cell*.18f,cell*2,cell*.7f);
                    style.normal.textColor=new Color(0,0,0,color.a);GUI.Label(new Rect(rect.x+1,rect.y+2,rect.width,rect.height),label,style);
                    style.normal.textColor=color;GUI.Label(rect,label,style);
                }
                if(v.Kind==VisualKind.Death){float t=(age-.08f)/.32f;if(t>=0&&t<1){Sparks(b,cell,t,new Color(1,.5f,.1f,1-t),v.Target);Ring(b,cell*t*.6f,new Color(1,.8f,.25f,1-t));}}
            }
            if(Previous!=null){
                float age=(float)(now-Started);if(age>=0&&age<.18f&&(Previous.X!=Current.X||Previous.Y!=Current.Y)){
                    Vector2 p=tileAt(new Vector2(Previous.X,Previous.Y)).center;
                    Sparks(p,cell*.35f,age/.18f,new Color(.8f,.75f,.54f,1-age/.18f),Current.Turn);
                }
            }
        }
    }
}
