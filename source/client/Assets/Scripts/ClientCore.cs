using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
namespace Tank  {
    public partial class ClientCore : IDisposable  {
        public ClientPhase Phase=ClientPhase.DISCONNECTED;
        public string Message="",Nickname="";
        public ulong Account,Match,StartUs;
        public uint RoomId,LastSnapshot;
        public int Slot=-1,Host=-1,LeadTicks=2;
        public NetworkMode Mode;
        public RoomPhase RoomPhase;
        public readonly List<RoomInfo> Rooms=new List<RoomInfo>();
        public readonly List<RoomPlayer> Players=new List<RoomPlayer>();
        public readonly List<Score> Scores=new List<Score>();
        public readonly List<HistoryEntry> History=new List<HistoryEntry>();
        public readonly List<Snapshot> Snapshots=new List<Snapshot>();
        public Snapshot Latest;
        public TankState Predicted;
        public float RenderOffsetX,RenderOffsetY;
        public double OffsetUs,MinRttUs=double.MaxValue,LastRttUs,LastArrivalUs,LastStateAgeUs,LastJitterUs,RecoveryUs;
        public long RxBytes,TxBytes,RxPackets,TxPackets,Decoded,Invalid,MissingBase,Corrections,PredictionResets,SequenceGaps;
        public double PredictionError;
        public int PingSamples;
        public UdpImpairment Impairment;
        public bool Ended;
        public int EndReason;
        public MatchDetail HistoryDetail;
        public ulong SelectedHistoryMatch;
        public bool HistoryDetailPending;
        uint detailRequest;
        double lastTcpReceived;
        GameMap map;
        byte[] mapHash;
        readonly Dictionary<int,Tuple<GameMap,byte[]>> maps=new Dictionary<int,Tuple<GameMap,byte[]>>();
        public GameMap ActiveMap  {
            get {
                return map;
            }
        }
        public const int AfkGraceSeconds=15;
        public int RoomCapacity=8,RoomSeconds=180,RoomStartingHp=3;
        public string RoomName="",AccountName="";
        public bool RoomLocked;
        public int NameRevision,SettingsRevision;
        public double ResultsUntilUs;
        public bool AfkWarning;
        public double AfkWarnedAt;
        readonly ConcurrentQueue<IncomingPacket> inbox=new ConcurrentQueue<IncomingPacket>();
        readonly ConcurrentQueue<ConnectionError> errors=new ConcurrentQueue<ConnectionError>();
        readonly SortedDictionary<uint,TankState> predictedHistory=new SortedDictionary<uint,TankState>();
        readonly SortedDictionary<uint,InputRecord> history=new SortedDictionary<uint,InputRecord>();
        readonly Dictionary<uint,WorldState> cache=new Dictionary<uint,WorldState>();
        readonly Queue<uint> cacheOrder=new Queue<uint>();
        readonly Dictionary<uint,double> sequences=new Dictionary<uint,double>();
        readonly Dictionary<uint,byte> requests=new Dictionary<uint,byte>();
        TcpClient tcp;
        UdpClient udp;
        NetworkStream stream;
        byte[] token;
        IPEndPoint remote;
        bool leavingMatch;
        volatile bool open;
        uint seq,udpSeq,targetTick,lastSequence;
        double lastHeartbeat,lastList,lastPing,lastAck,lastHandshake,prepareAt,lastResync,lastValidSnapshot,resyncAt,previousArrival;
        bool handshake,prepared;
        TcpSendQueue tcpWriter;
        int connectionEpoch;
        public int AccountRevision  {
            get;
            private set;
        }
        readonly Queue<InputRecord> recent=new Queue<InputRecord>();
        public static double NowUs  {
            get {
                return Stopwatch.GetTimestamp()*1e6/Stopwatch.Frequency;
            }
        }
        double authRetryAt;
        public double AuthenticationRetrySeconds  {
            get {
                return Math.Max(0,(authRetryAt-NowUs)/1e6);
            }
        }
        public bool AuthenticationPending  {
            get {
                foreach(byte op in requests.Values)if(op==Op.Login||op==Op.Register)return true;
                return false;
            }
        }
        public static string CredentialError(string username,string password) {
            if(username==null||!System.Text.RegularExpressions.Regex.IsMatch(username,"\\A[A-Za-z0-9_]{3,24}\\z"))return "Tên tài khoản cần 3-24 ký tự: chữ không dấu A-Z, a-z, số hoặc _. Không dùng dấu cách.";
            if(password==null||password.Length<8||password.Length>64)return "Mật khẩu cần từ 8 đến 64 ký tự.";
            if(System.Text.Encoding.UTF8.GetByteCount(password)>256)return "Mật khẩu vượt quá giới hạn 256 byte UTF-8.";
            return null;
        }
        void Authenticate(byte op,string username,string password) {
            string error=CredentialError(username,password);
            if(error!=null) {
                Message=error;
                return;
            }
            if(AuthenticationPending) {
                Message="Đang xử lý yêu cầu, vui lòng chờ.";
                return;
            }
            if(AuthenticationRetrySeconds>0) {
                Message="RATE_LIMITED";
                return;
            }
            Request(op,new WireWriter().Str(username).Str(password).Done());
        }
        public ClientCore(GameMap m,byte[] hash) {
            map=m;
            mapHash=hash;
            if(m!=null)AddMap(m,hash);
        }
        public void AddMap(GameMap m,byte[] hash) {
            maps[m.mapId]=Tuple.Create(m,hash);
        }
        public void Login(string u,string p) {
            Authenticate(Op.Login,u,p);
        }
        public void Register(string u,string p) {
            Authenticate(Op.Register,u,p);
        }
        public void Create(string name) {
            Request(Op.Create,new WireWriter().Str(name).Done());
        }
        public void Create(string name,int capacity,int seconds) {
            if(capacity<2||capacity>8||seconds<60||seconds>600||seconds%60!=0)throw new ArgumentOutOfRangeException("room settings");
            Request(Op.Create,new WireWriter().Str(name).U8(capacity).U16(seconds).Done());
        }
        public void Join(uint room) {
            Request(Op.Join,new WireWriter().U32(room).Done());
        }
        public void Ready(bool ready) {
            Request(Op.Ready,new WireWriter().U8(ready?1:0).Done());
        }
        public void StartMatch() {
            Request(Op.StartMatch,new byte[0]);
        }
        public void Leave() {
            if(leavingMatch)return;
            leavingMatch=Request(Op.Leave,new byte[0])!=0;
        }
        public void Logout() {
            Request(Op.Logout,new byte[0]);
        }
        public void Refresh() {
            Request(Op.RoomList,new byte[0]);
        }
        public void GetHistory(int offset=0) {
            Request(Op.History,new WireWriter().U16(offset).U8(20).Done());
        }
        public void Kick(int slot) {
            Request(Op.Kick,new WireWriter().U8(slot).Done());
        }
        public void Create(string name,int capacity,int seconds,string password,int startingHp=3) {
            if(startingHp<1||startingHp>30)throw new ArgumentOutOfRangeException("startingHp");
            Request(Op.Create,new WireWriter().Str(name).U8(capacity).U16(seconds).Str(password??"").U8(startingHp).Done());
        }
        public void Join(uint room,string password) {
            Request(Op.Join,new WireWriter().U32(room).Str(password??"").Done());
        }
        public void SetDisplayName(string value) {
            Request(Op.SetDisplayName,new WireWriter().Str(value.Trim()).Done());
        }
        public void ConfigureRoom(string name,int capacity,int seconds,int passwordMode,string password,int startingHp=3) {
            if(startingHp<1||startingHp>30)throw new ArgumentOutOfRangeException("startingHp");
            var w=new WireWriter().Str(name).U8(capacity).U16(seconds).U8(passwordMode);
            if(passwordMode==2)w.Str(password);
            Request(Op.RoomSettings,w.U8(startingHp).Done());
        }
        public void GetHistoryDetail(ulong match) {
            SelectedHistoryMatch=match;
            HistoryDetail=null;
            Message="";
            detailRequest=Request(Op.HistoryDetail,new WireWriter().U64(match).Done());
            HistoryDetailPending=detailRequest!=0;
        }
        public void CloseHistoryDetail() {
            SelectedHistoryMatch=0;
            HistoryDetail=null;
            HistoryDetailPending=false;
        }
        public void BackToRoom() {
            if(Phase==ClientPhase.RESULTS&&RoomId!=0) {
                Phase=ClientPhase.ROOM;
                Message="";
            }
        }
        WireWriter Prefix() {
            return new WireWriter().Bytes(token).U64(Match);
        }
        void SendUdp(byte op,byte[] payload) {
            if(!open)return;
            byte[] f=Protocol.Frame(op,++udpSeq,payload);
            if(f.Length>1200)throw new InvalidDataException("UDP limit");
            int epoch=connectionEpoch;var currentUdp=udp;var endpoint=new IPEndPoint(remote.Address,remote.Port);
            Action deliver=()=> {
                if(!open||epoch!=connectionEpoch)return;
                try {
                    currentUdp.Send(f,f.Length,endpoint);
                    TxBytes+=f.Length;
                    TxPackets++;
                }
                catch(Exception e) {
                    ReportError(e.Message,epoch);
                }
            };
            if(Impairment==null)deliver();
            else Impairment.Dispatch(deliver,true);
        }
        public void Poll() {
            Impairment?.Poll();
            ConnectionError err;
            while(errors.TryDequeue(out err)) {
                if(err.Epoch!=connectionEpoch)continue;
                Dispose();
                Message=err.Text;
                return;
            }
            IncomingPacket incoming;
            int count=0;
            while(count++<512&&inbox.TryDequeue(out incoming))try {
                if(incoming.Epoch!=connectionEpoch)continue;
                Handle(incoming.Packet);
            }
            catch(Exception e) {
                Invalid++;
                Message="Dữ liệu không hợp lệ: "+e.Message;
            }
            double now=NowUs;
            if(!open)return;
            if(now-lastTcpReceived>15e6) {
                Dispose();
                Message="TCP heartbeat quá hạn";
                return;
            }
            if(now-lastHeartbeat>5e6) {
                lastHeartbeat=now;
                Request(Op.Heartbeat,new WireWriter().U64((ulong)now).Done());
            }
            if(Phase==ClientPhase.LOBBY&&now-lastList>2e6) {
                lastList=now;
                Refresh();
            }
            if(Phase==ClientPhase.PREPARING) {
                if(!handshake&&now-lastHandshake>5e5) {
                    lastHandshake=now;
                    SendUdp(Op.Handshake,Prefix().U64(7).Done());
                }
                if(!handshake&&now-prepareAt>1e7) {
                    Dispose();
                    Message="UDP handshake quá hạn";
                    return;
                }
                if(handshake&&PingSamples>=8&&!prepared) {
                    prepared=true;
                    Request(Op.Prepared,new WireWriter().U64(Match).Done());
                }
            }
            if(handshake&&(Phase==ClientPhase.PREPARING||Phase==ClientPhase.PLAYING||StartUs>0)) {
                double interval=PingSamples<8?1e5:1e6;
                if(now-lastPing>interval) {
                    lastPing=now;
                    SendUdp(Op.Ping,Prefix().U64((ulong)now).Done());
                }
                if(now-lastAck>1e5) {
                    lastAck=now;
                    SendUdp(Op.Ack,Prefix().U32(LastSnapshot).Done());
                }
            }
            if(Phase==ClientPhase.PREPARING&&StartUs>0&&now+OffsetUs>=StartUs) {
                Phase=ClientPhase.PLAYING;
                if(Message=="Đang vào lại trận đấu…")Message="";
                LeadTicks=Math.Max(2,Math.Min(12,(int)Math.Ceiling(MinRttUs/2/(1e6/30))+2));
                targetTick=0;
                lastValidSnapshot=now;
            }
            if(Phase==ClientPhase.PLAYING&&now-lastValidSnapshot>3e6) {
                Message="Mạng chậm, đang đồng bộ lại trận đấu…";
                if(now-lastResync>=5e5) {
                    lastResync=now;
                    SendUdp(Op.Resync,Prefix().U32(LastSnapshot).Done());
                }
                if(now-lastValidSnapshot>10e6)Leave();
            }
            ExpireSequences(now);
        }
        static bool Equal(byte[] a,byte[] b) {
            if(a.Length!=b.Length)return false;
            for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;
            return true;
        }
        void ReceiveSnapshot(Packet p,WireReader r) {
            if(Phase!=ClientPhase.PLAYING&&Phase!=ClientPhase.PREPARING)return;
            var s=new Snapshot {
                Match=r.U64(),Id=r.U32(),Tick=r.U32(),Time=r.U64(),Resolved=r.U32(),Remaining=r.U16()
            };
            if(s.Match!=Match)return;
            if(p.Op==Op.Full)s.World=WorldState.Full(r);
            else {
                uint baseId=r.U32();
                WorldState baseWorld;
                if(!cache.TryGetValue(baseId,out baseWorld)) {
                    MissingBase++;
                    double now=NowUs;
                    if(now-lastResync>=5e5) {
                        lastResync=now;
                        if(resyncAt==0)resyncAt=now;
                        SendUdp(Op.Resync,Prefix().U32(baseId).Done());
                    }
                    return;
                }
                s.World=DeltaCodec.Apply(baseWorld,r);
            }
            r.End();
            Decoded++;
            if(Message=="Mạng chậm, đang đồng bộ lại trận đấu…")Message="";
            lastValidSnapshot=NowUs;
            if(resyncAt>0) {
                RecoveryUs=NowUs-resyncAt;
                resyncAt=0;
            }
            if(!cache.ContainsKey(s.Id))cacheOrder.Enqueue(s.Id);
            cache[s.Id]=s.World;
            while(cacheOrder.Count>128)cache.Remove(cacheOrder.Dequeue());
            if(LastSnapshot!=0&&!Protocol.Newer(s.Id,LastSnapshot))return;
            LastSnapshot=s.Id;
            Latest=s;
            LastArrivalUs=NowUs;
            LastStateAgeUs=NowUs+OffsetUs-s.Time;
            if(previousArrival>0)LastJitterUs=Math.Abs(LastArrivalUs-previousArrival-1e6/30);
            previousArrival=LastArrivalUs;
            Snapshots.Add(s);
            if(Snapshots.Count>128)Snapshots.RemoveAt(0);
            Reconcile(s);
        }
        void RecordSequence(uint sequence) {
            if(lastSequence==0) {
                lastSequence=sequence;
                return;
            }
            if(Protocol.Newer(sequence,lastSequence)) {
                uint gap=sequence-lastSequence;
                for(uint i=1;i<Math.Min(gap,256);i++)sequences[lastSequence+i]=NowUs;
                lastSequence=sequence;
            }
            sequences.Remove(sequence);
        }
        void ExpireSequences(double now) {
            var expired=new List<uint>();
            foreach(var x in sequences)if(now-x.Value>5e5)expired.Add(x.Key);
            foreach(uint x in expired) {
                SequenceGaps++;
                sequences.Remove(x);
            }
        }
        void Reconcile(Snapshot s) {
            TankState actual;
            if(!s.World.Tanks.TryGetValue(Slot,out actual))return;
            float oldX=Predicted==null?actual.X:Predicted.X,oldY=Predicted==null?actual.Y:Predicted.Y;
            int oldHp=Predicted==null?actual.Hp:Predicted.Hp;
            Predicted=actual.Copy();
            var remove=new List<uint>();
            foreach(var h in history) {
                if(h.Key<=s.Resolved)remove.Add(h.Key);
                else {
                    Movement.Step(Predicted,h.Value,map,s.World.Bricks);
                    predictedHistory[h.Key]=Predicted.Copy();
                }
            }
            foreach(uint t in remove) {
                history.Remove(t);
                predictedHistory.Remove(t);
            }
            PredictionError=Math.Sqrt(Math.Pow(oldX-Predicted.X,2)+Math.Pow(oldY-Predicted.Y,2));
            if(PredictionError>.1)Corrections++;
            if(PredictionError<=1&&oldHp>0&&actual.Hp>0) {
                RenderOffsetX=oldX-Predicted.X;
                RenderOffsetY=oldY-Predicted.Y;
                smoothRemaining=.1;
            }
            else RenderOffsetX=RenderOffsetY=0;
            if(history.Count>=256||targetTick>s.Tick+30||targetTick+30<s.Tick) {
                history.Clear();
                predictedHistory.Clear();
                recent.Clear();
                targetTick=s.Tick;
                Predicted=actual.Copy();
                PredictionResets++;
            }
        }
        public static uint DesiredInputTick(Snapshot snapshot,double now,double arrival,int lead,double snapshotAgeUs=0) {
            // Server catch-up can skip wall-clock time. Anchor commands to its tick, including
            // the snapshot's network age, and stay inside InputBuffer's 30-tick window.
            int elapsed=(int)Math.Floor(Math.Max(0,Math.Min(4e5,now-arrival+Math.Max(0,snapshotAgeUs)))*30/1e6);
            return snapshot.Tick+(uint)Math.Min(30,elapsed+Math.Max(2,Math.Min(12,lead)));
        }
        public bool GenerateInput(int buttons,int angle) {
            if(Phase!=ClientPhase.PLAYING||Latest==null)return false;
            uint desired=DesiredInputTick(Latest,NowUs,LastArrivalUs,LeadTicks,LastStateAgeUs);
            if(targetTick>=desired)return false;
            if(targetTick==0)targetTick=desired-1;
            if(desired-targetTick>30) {
                history.Clear();
                predictedHistory.Clear();
                recent.Clear();
                targetTick=desired-1;
                PredictionResets++;
            }
            var input=new InputRecord(++targetTick,buttons,angle);
            history[input.Tick]=input;
            if(history.Count>256) {
                history.Clear();
                predictedHistory.Clear();
                PredictionResets++;
            }
            recent.Enqueue(input);
            while(recent.Count>3)recent.Dequeue();
            var list=Mode==NetworkMode.INPUT_REDUNDANCY||Mode==NetworkMode.COMBINED?recent.ToArray():new[] {
                input
            };
            var w=Prefix().U32(LastSnapshot).U8(list.Length);
            for(int i=list.Length-1;i>=0;i--)list[i].Write(w);
            SendUdp(Op.Input,w.Done());
            if(Predicted!=null&&Latest!=null) {
                Movement.Step(Predicted,input,map,Latest.World.Bricks);
                predictedHistory[input.Tick]=Predicted.Copy();
            }
            return true;
        }
        double smoothRemaining;
        public void Smooth(double seconds) {
            float f=smoothRemaining<=seconds?0:(float)((smoothRemaining-seconds)/smoothRemaining);
            RenderOffsetX*=f;
            RenderOffsetY*=f;
            smoothRemaining=Math.Max(0,smoothRemaining-seconds);
        }
        public void Dispose() {
            lock (connectionGate) {
            Interlocked.Increment(ref connectionEpoch);
            open=false;
            tcpWriter?.Dispose();
            tcpWriter=null;
            ClearAccountState();
            try {
                tcp?.Close();
                udp?.Close();
            }
            catch {
            }
            tcp=null;
            udp=null;
            Phase=ClientPhase.DISCONNECTED;
            token=null;
            handshake=false;
            StartUs=0;
            leavingMatch=false;
            RoomId=0;
            Slot=-1;
            Latest=null;
            Predicted=null;
            Snapshots.Clear();
            Players.Clear();
            Rooms.Clear();
            History.Clear();
            CloseHistoryDetail();
            IncomingPacket incoming;
            while(inbox.TryDequeue(out incoming)) {
            }
            ConnectionError staleError;
            while(errors.TryDequeue(out staleError)) {
            }
            history.Clear();
            predictedHistory.Clear();
            recent.Clear();
            requests.Clear();
            }
        }
    }
}
