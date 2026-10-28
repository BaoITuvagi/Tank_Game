using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tank.Campaign {
    // Campaign-only cues. Same-turn save/shop responses never replay combat audio.
    public sealed class CampaignAudioTimeline {
        public struct Cue {public string Clip;public double Due;public float Volume;}
        readonly List<Cue> pending=new List<Cue>();
        RunState previous;
        public void Clear(){pending.Clear();previous=null;}
        void Add(string clip,double due,float volume){pending.Add(new Cue{Clip=clip,Due=due,Volume=volume});}
        public void Observe(RunState state,double now,bool active){
            if(!active||state==null){pending.Clear();previous=state;return;}
            if(previous==null||previous.Id!=state.Id||state.Turn<previous.Turn||state.Turn>previous.Turn+1){pending.Clear();previous=state;return;}
            if(state.ShopRevision>previous.ShopRevision&&state.Gold<previous.Gold)Add("purchase",now,.38f);
            if(state.Turn==previous.Turn+1){
                int shots=0,hits=0,deaths=0,blocks=0,heals=0,skills=0,melees=0;
                foreach(var v in state.Visuals){
                    if(v.Kind==VisualKind.Shot&&shots++<3)Add("shot",now+(v.Source==0?0:.1),v.Source==0?.42f:.20f);
                    if(v.Kind==VisualKind.Melee&&melees++<2)Add("hit",now+(v.Source==0?.085:.18),.22f);
                    if(v.Kind==VisualKind.Hit&&hits++<3)Add("hit",now+CampaignPresentation.Impact(v),v.Target==0?.36f:.22f);
                    if(v.Kind==VisualKind.Death&&v.Target>0&&deaths++<2)Add("explosion",now+.08,.46f);
                    if(v.Kind==VisualKind.Block&&blocks++<2)Add("fortress",now+CampaignPresentation.Impact(v),.30f);
                    if(v.Kind==VisualKind.Heal&&heals++<1)Add("heal",now+.23,.30f);
                    if(v.Kind==VisualKind.Skill&&skills++<2)Add(v.Source>0?"airstrike":state.Nation==1?"fortress":state.Nation==5?"pierce":"airstrike",now,v.Source>0?.27f:.48f);
                }
            }
            if(state.Finished&&!previous.Finished)Add(state.Victory?"victory":"explosion",now+.30,.5f);
            previous=state;
        }
        public List<Cue> TakeDue(double now){
            var due=new List<Cue>();
            for(int i=0;i<pending.Count;){
                var cue=pending[i];if(cue.Due>now){i++;continue;}
                pending.RemoveAt(i);if(now-cue.Due<.3)due.Add(cue);
            }
            return due;
        }
    }

    // Own sources/listener are children of TankApp, so destruction cleans them up.
    // No shared audio settings, menu skin, multiplayer or roleplay audio are changed.
    public sealed class CampaignAudio : MonoBehaviour {
        public static readonly string[] ClipNames={"shot","hit","explosion","airstrike","fortress","pierce","heal","purchase","victory","shop_open","shop_close"};
        readonly CampaignAudioTimeline timeline=new CampaignAudioTimeline();
        readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();
        readonly Dictionary<string,double> lastPlayed=new Dictionary<string,double>();
        AudioSource[] voices;
        AudioListener ownedListener;
        Func<RunState> readState;
        Func<bool> isCampaignRun;
        bool wasActive;
        bool preview;
        public int PlayedCount {get;private set;}
        public void Bind(Func<RunState> state,Func<bool> active){readState=state;isCampaignRun=active;preview=Array.IndexOf(Environment.GetCommandLineArgs(),"--campaign-audio-preview")>=0;}
        void Prepare(){
            if(voices!=null)return;
            var audio=new GameObject("Campaign sound effects");audio.transform.SetParent(transform,false);
            // Reuse a scene listener if one exists; otherwise create an isolated one.
            if(FindFirstObjectByType<AudioListener>()==null)ownedListener=audio.AddComponent<AudioListener>();
            voices=new AudioSource[8];
            for(int i=0;i<voices.Length;i++){
                var source=audio.AddComponent<AudioSource>();source.playOnAwake=false;source.loop=false;
                source.spatialBlend=0;source.dopplerLevel=0;source.priority=i<2?64:128;voices[i]=source;
            }
            foreach(string name in ClipNames){var clip=Resources.Load<AudioClip>("CampaignAudio/"+name);if(clip==null)Debug.LogError("Missing campaign sound: "+name);else clips[name]=clip;}
        }
        void Update(){
            if(readState==null)return;
            bool active=isCampaignRun()&&(Tank.GameWindowFocus.IsForeground||preview);
            double now=Time.unscaledTimeAsDouble;
            timeline.Observe(readState(),now,active);
            if(!active){
                if(wasActive&&voices!=null)foreach(var source in voices)source.Stop();
                if(ownedListener!=null)ownedListener.enabled=false;
                wasActive=false;return;
            }
            Prepare();if(ownedListener!=null)ownedListener.enabled=true;wasActive=true;
            foreach(var cue in timeline.TakeDue(now))Play(cue.Clip,cue.Volume,now);
        }
        void Play(string name,float volume,double now){
            if(!wasActive)return;
            double last;if(lastPlayed.TryGetValue(name,out last)&&now-last<.045)return;
            AudioClip clip;if(!clips.TryGetValue(name,out clip))return;
            bool ui=name=="purchase"||name=="victory"||name.StartsWith("shop_");
            AudioSource voice=null;
            for(int i=ui?0:2;i<(ui?2:voices.Length);i++)if(!voices[i].isPlaying){voice=voices[i];break;}
            if(voice==null&&ui){voice=voices[0];voice.Stop();}
            if(voice==null)return;
            voice.clip=clip;voice.volume=volume;voice.Play();lastPlayed[name]=now;PlayedCount++;
            if(preview)Debug.Log("Campaign sound: "+name);
        }
        public void ShopToggle(bool open){if(wasActive)Play(open?"shop_open":"shop_close",.27f,Time.unscaledTimeAsDouble);}
        void OnDisable(){
            timeline.Clear();if(voices!=null)foreach(var source in voices)if(source!=null)source.Stop();
            if(ownedListener!=null)ownedListener.enabled=false;wasActive=false;
        }
    }
}
