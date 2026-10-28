using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
namespace Tank {
    // Runtime PCM voices use the existing WinMM backend; no Unity audio module is required.
    sealed class SkillSounds : IDisposable {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [StructLayout(LayoutKind.Sequential,Pack=2)] struct WaveFormat {
            public ushort Tag,Channels;public uint Samples,Bytes;public ushort Align,Bits,Extra;
        }
        [StructLayout(LayoutKind.Sequential)] struct WaveHeader {
            public IntPtr Data;public uint Length,Recorded;public UIntPtr User;public uint Flags,Loops;public IntPtr Next;public UIntPtr Reserved;
        }
        [DllImport("winmm.dll")] static extern uint waveOutOpen(out IntPtr handle,uint device,ref WaveFormat format,IntPtr callback,IntPtr instance,uint flags);
        [DllImport("winmm.dll")] static extern uint waveOutPrepareHeader(IntPtr handle,IntPtr header,uint size);
        [DllImport("winmm.dll")] static extern uint waveOutUnprepareHeader(IntPtr handle,IntPtr header,uint size);
        [DllImport("winmm.dll")] static extern uint waveOutWrite(IntPtr handle,IntPtr header,uint size);
        [DllImport("winmm.dll")] static extern uint waveOutReset(IntPtr handle);
        [DllImport("winmm.dll")] static extern uint waveOutClose(IntPtr handle);
        sealed class Voice {
            public IntPtr Handle,Header;
            public GCHandle Buffer;
            public bool Prepared;
        }
        readonly Voice[] voices=new Voice[6];
        readonly Dictionary<int,byte[]> clips=new Dictionary<int,byte[]>();
        readonly uint headerSize=(uint)Marshal.SizeOf(typeof(WaveHeader));
        bool disposed;
        public SkillSounds() {
            var format=new WaveFormat{Tag=1,Channels=1,Samples=22050,Bytes=44100,Align=2,Bits=16};
            for(int i=0;i<voices.Length;i++) {
                IntPtr handle;
                if(waveOutOpen(out handle,uint.MaxValue,ref format,IntPtr.Zero,IntPtr.Zero,0)!=0)continue;
                voices[i]=new Voice{Handle=handle,Header=Marshal.AllocHGlobal((int)headerSize)};
            }
            for(int kind=0;kind<32;kind++) {
                byte[] original=Synthesize(kind);
                for(int level=0;level<8;level++) {
                    var data=new byte[original.Length];float gain=(level+1)/8f;
                    for(int i=0;i<data.Length;i+=2) {short sample=(short)(original[i]|original[i+1]<<8);sample=(short)(sample*gain);data[i]=(byte)sample;data[i+1]=(byte)(sample>>8);}
                    clips[(kind<<3)|level]=data;
                }
            }
        }
        static byte[] Synthesize(int kind) {
            double duration=kind==0?.035:kind<3?.08:kind==3||kind==4||kind==8?.65:kind==13||kind==25?.55:kind<15?.35:.18;
            int count=(int)(22050*duration);var data=new byte[count*2];var noise=new Random(901+kind);
            for(int i=0;i<count;i++) {
                double t=i/22050.0,p=i/(double)count,frequency=kind==0?440:kind==1?660:kind==2?180:180+(kind%8)*77;
                double tone=Math.Sin(2*Math.PI*(frequency*t+(kind%2==0?420:-180)*t*t));
                double grain=noise.NextDouble()*2-1,amplitude=.1;
                if(kind>=11&&kind<15) {tone=.75*grain+.25*Math.Sin(2*Math.PI*65*t);amplitude=.12;}
                if(kind>=3&&kind<11)tone=.65*tone+.25*Math.Sin(2*Math.PI*frequency*1.5*t)+.1*grain;
                // Engine, siren, radar, fog, barrage, charge, alarm and electrical sabotage.
                if(kind==3)tone=.35*grain+.45*Math.Sin(2*Math.PI*75*t)*Math.Sin(2*Math.PI*8*t)+.2*Math.Sin(2*Math.PI*(900*t-200*t*t));
                if(kind==4)tone=Math.Sin(2*Math.PI*650*t-80*Math.Cos(2*Math.PI*2*t));
                if(kind==5)tone=Math.Sin(2*Math.PI*1250*t)*Math.Exp(-p*5);
                if(kind==6||kind==19||kind==20)tone=.35*grain*(1-p)+.45*Math.Sin(2*Math.PI*(kind==20?360:180)*t)*Math.Sin(Math.PI*p);
                if(kind==7||kind==21)tone=.65*grain*Math.Exp(-p*4)+.35*Math.Sin(2*Math.PI*125*t);
                if(kind==8)tone=.7*Math.Sin(2*Math.PI*(250*t+1600*t*t))+.3*Math.Sin(2*Math.PI*(500*t+3000*t*t));
                if(kind==9||kind==17)tone=Math.Sin(2*Math.PI*(i/1764%2==0?850:1150)*t);
                if(kind==10)tone=(.6*grain+.4*Math.Sin(2*Math.PI*1600*t))*(Math.Sin(2*Math.PI*32*t)>0?1:.15);
                if(kind==12||kind==13)tone=.6*grain*Math.Exp(-p*3)+.4*Math.Sin(2*Math.PI*(kind==13?42:65)*t);
                if(kind==14)tone=.7*Math.Sin(2*Math.PI*(1500*t-1200*t*t))+.3*grain*Math.Exp(-p*5);
                if(kind==24)tone=.7*grain*Math.Exp(-p*8)+.3*Math.Sin(2*Math.PI*(140*t-200*t*t));
                if(kind==25||kind==29)tone=.7*grain*Math.Exp(-p*3)+.3*Math.Sin(2*Math.PI*(kind==25?48:90)*t);
                if(kind==26)tone=Math.Sin(2*Math.PI*2100*t)*Math.Exp(-p*8)+.2*grain;
                if(kind==27||kind==28)tone=(.65*grain+.35*Math.Sin(2*Math.PI*130*t))*Math.Exp(-p*5);
                if(kind==30)tone=Math.Sin(2*Math.PI*(400*t+700*t*t))*Math.Exp(-p*3);
                if(kind==31)tone=.65*Math.Sin(2*Math.PI*(700*t+1800*t*t))+.25*Math.Sin(2*Math.PI*1400*t);
                double envelope=Math.Min(1,p*18)*Math.Pow(1-p,kind>=11&&kind<15?2:1);
                short sample=(short)(short.MaxValue*amplitude*tone*envelope);data[i*2]=(byte)sample;data[i*2+1]=(byte)(sample>>8);
            }
            return data;
        }
        bool Release(Voice voice,bool stop) {
            if(!voice.Prepared)return true;
            if(stop&&waveOutReset(voice.Handle)!=0)return false;
            if(waveOutUnprepareHeader(voice.Handle,voice.Header,headerSize)!=0)return false;
            voice.Prepared=false;if(voice.Buffer.IsAllocated)voice.Buffer.Free();return true;
        }
        public void Poll() {
            if(disposed)return;
            foreach(var v in voices)if(v!=null&&v.Prepared&&(Marshal.PtrToStructure<WaveHeader>(v.Header).Flags&1)!=0)Release(v,false);
        }
        void Play(int kind,bool priority=false,float gain=1) {
            if(disposed)return;Poll();Voice voice=null;
            for(int i=priority?0:2;i<(priority?2:voices.Length);i++)if(voices[i]!=null&&!voices[i].Prepared) {voice=voices[i];break;}
            if(voice==null&&priority) {voice=voices[0]??voices[1];if(voice!=null&&!Release(voice,true))voice=null;}
            if(voice==null)return;
            int key=(kind<<3)|Math.Max(0,Math.Min(7,(int)Math.Ceiling(gain*8)-1));
            voice.Buffer=GCHandle.Alloc(clips[key],GCHandleType.Pinned);
            var header=new WaveHeader{Data=voice.Buffer.AddrOfPinnedObject(),Length=(uint)clips[key].Length};
            Marshal.StructureToPtr(header,voice.Header,false);
            if(waveOutPrepareHeader(voice.Handle,voice.Header,headerSize)!=0) {voice.Buffer.Free();return;}
            voice.Prepared=true;
            if(waveOutWrite(voice.Handle,voice.Header,headerSize)!=0)Release(voice,true);
        }
        public void Press() {Play(0,true);}
        public void Confirm(bool success) {Play(success?1:2,true);}
        public void Activate(int nation) {Play(3+Math.Max(0,Math.Min(7,nation)));}
        public void Impact(int kind) {Play(kind==SkillEffect.Beam?14:kind==SkillEffect.Kamikaze?13:kind==SkillEffect.Bomb?12:11);}
        public void Countdown(int nation,int seconds) {Play(nation==5?16:seconds==1?17:15,true);}
        public void Fog(bool start) {Play(start?19:20,true);}
        public void Barrage() {Play(21);}
        public void PlayWorld(int clip,float gain,bool priority) {if(clip>=0&&clip<32&&gain>0)Play(clip,priority,gain);}
        public void Stop() {foreach(var voice in voices)if(voice!=null)Release(voice,true);}
        public void Dispose() {
            if(disposed)return;disposed=true;
            foreach(var voice in voices)if(voice!=null) {
                // Retain memory if a device refuses to release an in-flight buffer.
                if(!Release(voice,true))continue;
                if(waveOutClose(voice.Handle)==0)Marshal.FreeHGlobal(voice.Header);
            }
        }
#else
        public void Press() {}
        public void Confirm(bool success) {}
        public void Activate(int nation) {}
        public void Impact(int kind) {}
        public void Countdown(int nation,int seconds) {}
        public void Fog(bool start) {}
        public void Barrage() {}
        public void PlayWorld(int clip,float gain,bool priority) {}
        public void Stop() {}
        public void Poll() {}
        public void Dispose() {}
#endif
    }
}
