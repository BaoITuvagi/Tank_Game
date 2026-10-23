using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
namespace Tank  {
    public partial class ClientCore  {
        readonly object connectionGate = new object();
        sealed class IncomingPacket {
            public readonly int Epoch;
            public readonly Packet Packet;
            public IncomingPacket(int epoch,Packet packet){Epoch=epoch;Packet=packet;}
        }
        sealed class ConnectionError {
            public readonly int Epoch;
            public readonly string Text;
            public ConnectionError(int epoch,string text){Epoch=epoch;Text=text;}
        }
        void ReportError(string error,int epoch){errors.Enqueue(new ConnectionError(epoch,error));}

        public async void Connect(string host,int tcpPort,int udpPort)  {
            Dispose();
            int epoch=connectionEpoch;
            Phase=ClientPhase.CONNECTING;
            Message="Đang kết nối";
            TcpClient candidate=null;
            try  {
                var addresses=await Dns.GetHostAddressesAsync(host);
                if(epoch!=connectionEpoch)return;
                var address=Array.Find(addresses,a=>a.AddressFamily==AddressFamily.InterNetwork);
                if(address==null)throw new IOException("IPv4 required");
                candidate=new TcpClient();
                var connect=candidate.ConnectAsync(address,tcpPort);
                if(await Task.WhenAny(connect,Task.Delay(5000))!=connect)throw new IOException("Kết nối quá hạn");
                await connect;
                if(epoch!=connectionEpoch) {
                    candidate.Close();
                    return;
                }
                lock (connectionGate) {
                if(epoch!=connectionEpoch){candidate.Close();return;}
                candidate.NoDelay=true;
                tcp=candidate;
                stream=candidate.GetStream();
                udp=new UdpClient(0);
                remote=new IPEndPoint(address,udpPort);
                open=true;
                lastTcpReceived=NowUs;
                tcpWriter=new TcpSendQueue(stream,error=> {
                    if(open&&epoch==connectionEpoch)ReportError(error,epoch);
                });
                inbox.Enqueue(new IncomingPacket(epoch,new Packet {
                    Op=255,Payload=new byte[0]
                }));
                ReadTcp(epoch,stream);
                ReadUdp(epoch,udp);
                }
            }
            catch(Exception e)  {
                candidate?.Close();
                if(epoch==connectionEpoch)ReportError(e.Message,epoch);
            }
        }
        async void ReadTcp(int epoch,NetworkStream currentStream)  {
            try  {
                while(open&&epoch==connectionEpoch)  {
                    var packet=await Protocol.ReadAsync(currentStream).ConfigureAwait(false);
                    if(!open||epoch!=connectionEpoch)return;
                    lastTcpReceived=NowUs;
                    if(inbox.Count>=512)throw new IOException("Hàng đợi nhận đầy");
                    inbox.Enqueue(new IncomingPacket(epoch,packet));
                }
            }
            catch(Exception e) {
                if(open&&epoch==connectionEpoch)ReportError("Mất kết nối: "+e.Message,epoch);
            }
        }
        async void ReadUdp(int epoch,UdpClient currentUdp)  {
            while(open&&epoch==connectionEpoch)try  {
                var result=await currentUdp.ReceiveAsync().ConfigureAwait(false);
                if(!open||epoch!=connectionEpoch)return;
                if(!result.RemoteEndPoint.Equals(remote))continue;
                byte[] raw=result.Buffer;
                Interlocked.Add(ref RxBytes,raw.Length);
                Interlocked.Increment(ref RxPackets);
                Packet packet=Protocol.Decode(raw,true);
                Action deliver=()=> {
                    if(!open||epoch!=connectionEpoch)return;
                    if(inbox.Count<512)inbox.Enqueue(new IncomingPacket(epoch,packet));
                    else Interlocked.Increment(ref Invalid);
                };
                if(Impairment==null)deliver();
                else Impairment.Dispatch(deliver,false);
            }
            catch(ObjectDisposedException) {
                return;
            }
            catch(SocketException) {
                // An unreachable UDP peer must not tear down a healthy TCP session.
                if(!open||epoch!=connectionEpoch)return;
                await Task.Delay(100).ConfigureAwait(false);
            }
            catch(Exception) {
                if(open&&epoch==connectionEpoch)Interlocked.Increment(ref Invalid);
            }
        }
        public uint Request(byte op,byte[] payload)  {
            if(!open||tcpWriter==null)return 0;
            uint id=++seq;
            byte[] frame=Protocol.Frame(op,id,payload);
            if(!tcpWriter.TryEnqueue(frame)) {
                ReportError("Hàng đợi gửi TCP đầy hoặc đã đóng",connectionEpoch);
                return 0;
            }
            requests[id]=op;
            return id;
        }
        static bool IsResponse(byte op)  {
            if(op==Tank.Campaign.CampaignClient.Response)return true;
            return op==Op.RegisterResult||op==Op.LoginResult||op==Op.LogoutResult||op==Op.ActionResult|| op==Op.RoomListResult||op==Op.HistoryResult||op==Op.HistoryDetailResult||op==Op.DisplayNameResult||op==Op.HeartbeatAck;
        }
        void ClearAccountState()  {
            Campaign=new Tank.Campaign.CampaignClient();
            ClearRoleplayMatch();RoomMode=RoomMode.NORMAL;PreparationError="";
            Account=Match=StartUs=0;
            Nickname=AccountName=RoomName="";
            token=null;
            RoomId=LastSnapshot=0;
            Slot=Host=-1;
            RoomLocked=false;
            RoomPhase=RoomPhase.WAITING;
            RoomCapacity=8;
            RoomSeconds=180;
            RoomStartingHp=GameRules.DefaultHp;
            Ended=false;
            EndReason=0;
            AfkWarning=false;
            AfkWarnedAt=0;
            handshake=prepared=leavingMatch=false;
            Rooms.Clear();
            Players.Clear();
            Scores.Clear();
            History.Clear();
            CloseHistoryDetail();
            detailRequest=0;
            Latest=null;
            Predicted=null;
            Snapshots.Clear();
            cache.Clear();fragments.Clear();
            cacheOrder.Clear();
            history.Clear();
            predictedHistory.Clear();
            recent.Clear();
            sequences.Clear();
            requests.Clear();
            lastSequence=targetTick=0;
            RenderOffsetX=RenderOffsetY=0;
            smoothRemaining=0;
            OffsetUs=0;
            MinRttUs=double.MaxValue;
            PingSamples=0;
            LastRttUs=LastPongUs=inputRttUs=0;
            LastArrivalUs=LastStateAgeUs=LastJitterUs=0;
            LeadTicks=2;
            resyncAt=previousArrival=0;
            AccountRevision++;
        }
    }
}
