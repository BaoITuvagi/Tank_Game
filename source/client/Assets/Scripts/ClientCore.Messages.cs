using System;
using System.IO;
namespace Tank  {
    public partial class ClientCore  {
        void Handle(Packet p) {
            if(p.Op==255) {
                Phase=ClientPhase.AUTHENTICATING;
                Message="Kết nối thành công";
                return;
            }
            if(p.Op==Op.HandshakeAck||p.Op==Op.Pong||p.Op==Op.Full||p.Op==Op.Delta)RecordSequence(p.Sequence);
            if(IsResponse(p.Op)&&p.Sequence!=0&&!requests.ContainsKey(p.Sequence))return;
            var r=new WireReader(p.Payload);
            byte request=0;
            if(p.Op>=Op.RegisterResult&&p.Op!=Op.HandshakeAck&&p.Op!=Op.Pong&&p.Op!=Op.Full&&p.Op!=Op.Delta&&p.Sequence!=0&&requests.TryGetValue(p.Sequence,out request))requests.Remove(p.Sequence);
            switch(p.Op) {
                case Op.RegisterResult:case Op.LoginResult:case Op.LogoutResult:case Op.ActionResult:case Op.RoomListResult:case Op.HistoryResult:case Op.HistoryDetailResult:case Op.DisplayNameResult: {
                    int status=r.U16();
                    if(p.Op==Op.HistoryDetailResult) {
                        if(p.Sequence!=detailRequest||SelectedHistoryMatch==0)return;
                        HistoryDetailPending=false;
                    }
                    if(status!=0) {
                        if(request==Op.Leave)leavingMatch=false;
                        Message=r.Str();
                        r.End();
                        if(status==(int)ErrorCode.RATE_LIMITED&&(p.Op==Op.RegisterResult||p.Op==Op.LoginResult))authRetryAt=NowUs+12e6;
                        return;
                    }
                    if(p.Op==Op.HistoryDetailResult) {
                        var detail=new MatchDetail {
                            Match=r.U64(),MapId=r.U16(),Started=r.U64(),Ended=r.U64(),Reason=r.U8(),OwnSlot=-1
                        };
                        int n=r.U8();
                        if(n<1||n>8||detail.Match!=SelectedHistoryMatch)throw new InvalidDataException("match detail");
                        for(int i=0;i<n;i++) {
                            var score=new Score {
                                Slot=r.U8(),Name=r.Str(96),Kills=r.U16(),Deaths=r.U16(),Rank=r.U8(),Disconnected=r.Bool()
                            };
                            if(r.Bool())detail.OwnSlot=score.Slot;
                            detail.Players.Add(score);
                        }
                        if(r.Remaining>0)detail.DurationTicks=r.U32();
                        HistoryDetail=detail;
                    }
                    else if(p.Op==Op.LoginResult) {
                        ClearAccountState();
                        Account=r.U64();
                        Nickname=r.Str(96);
                        token=r.Bytes(16);
                        AccountName=r.Remaining>0?r.Str(24):Nickname;
                        Phase=ClientPhase.LOBBY;
                        Message="Đăng nhập thành công";
                    }
                    else if(p.Op==Op.DisplayNameResult) {
                        Nickname=r.Str(96);
                        NameRevision++;
                        foreach(var player in Players)if(player.Slot==Slot)player.Name=Nickname;
                        Message="";
                    }
                    else if(p.Op==Op.ActionResult&&request==Op.RoomSettings) {
                        SettingsRevision++;
                        Message="";
                    }
                    else if(p.Op==Op.LogoutResult) {
                        ClearAccountState();
                        Phase=ClientPhase.AUTHENTICATING;
                        Message="Đã đăng xuất";
                    }
                    else if(p.Op==Op.RegisterResult) {
                        Message="Đăng ký thành công";
                    }
                    else if(p.Op==Op.RoomListResult) {
                        Rooms.Clear();
                        int n=r.U16();
                        if(n>40)throw new InvalidDataException("rooms");
                        for(int i=0;i<n;i++)Rooms.Add(new RoomInfo {
                            Id=r.U32(),Name=r.Str(48),Players=r.U8(),Capacity=r.U8(),Phase=(RoomPhase)r.U8()
                        });
                        if(r.Remaining>0) {
                            if(r.Remaining!=Rooms.Count&&r.Remaining!=2*Rooms.Count)throw new InvalidDataException("room flags");
                            foreach(var room in Rooms)room.Locked=r.Bool();
                            if(r.Remaining>0)foreach(var room in Rooms)room.CanRejoin=r.Bool();
                        }
                    }
                    else if(p.Op==Op.HistoryResult) {
                        History.Clear();
                        int n=r.U8();
                        if(n>20)throw new InvalidDataException("history");
                        for(int i=0;i<n;i++)History.Add(new HistoryEntry {
                            Match=r.U64(),Ended=r.U64(),Reason=r.U8(),Kills=r.U16(),Deaths=r.U16(),Rank=r.U8()
                        });
                    }
                    r.End();
                    break;
                }
                case Op.RoomState: {
                    uint incomingRoom=r.U32();
                    bool entering=incomingRoom!=RoomId||Phase==ClientPhase.LOBBY;
                    RoomId=incomingRoom;
                    RoomName=r.Str(48);
                    RoomPhase=(RoomPhase)r.U8();
                    Host=r.U8();
                    int n=r.U8();
                    if(n>8)throw new InvalidDataException("members");
                    Players.Clear();
                    for(int i=0;i<n;i++) {
                        var player=new RoomPlayer {
                            Slot=r.U8(),Name=r.Str(96),Ready=r.Bool()
                        };
                        Players.Add(player);
                        if(player.Name==Nickname)Slot=player.Slot;
                    }
                    if(r.Remaining>0) {
                        RoomCapacity=r.U8();
                        RoomSeconds=r.U16();
                        if(RoomCapacity<2||RoomCapacity>8||RoomSeconds<0||RoomSeconds>600)throw new InvalidDataException("room settings");
                    }
                    if(r.Remaining>0) {
                        RoomLocked=r.Bool();
                        Slot=r.U8();
                        if(!Players.Exists(member=>member.Slot==Slot))throw new InvalidDataException("own slot");
                    }
                    RoomStartingHp=r.Remaining>0?r.U8():3;
                    if(RoomStartingHp<1||RoomStartingHp>30)throw new InvalidDataException("starting HP");
                    r.End();
                    if(entering) {
                        Message="";
                        AfkWarning=false;
                    }
                    if(RoomPhase==RoomPhase.WAITING) {
                        Phase=ClientPhase.ROOM;
                        StartUs=0;
                        handshake=false;
                    }
                    break;
                }
                case Op.Prepare: {
                    leavingMatch=false;
                    Match=r.U64();
                    RoomId=r.U32();
                    Slot=r.U8();
                    int mapId=r.U16();
                    byte[] hash=r.Bytes(32);
                    Mode=(NetworkMode)r.U8();
                    int rate=r.U8(),port=r.U16();
                    bool rejoining=r.Remaining>0&&r.Bool();
                    r.End();
                    Tuple<GameMap,byte[]> selected;
                    if(!maps.TryGetValue(mapId,out selected)||rate!=30||!Equal(hash,selected.Item2)) {
                        Message="Map không khớp server";
                        Leave();
                        return;
                    }
                    map=selected.Item1;
                    mapHash=selected.Item2;
                    AfkWarning=false;
                    Message=rejoining?"Đang vào lại trận đấu…":"";
                    remote.Port=port;
                    Phase=ClientPhase.PREPARING;
                    PingSamples=0;
                    MinRttUs=double.MaxValue;
                    OffsetUs=0;
                    StartUs=0;
                    handshake=prepared=false;
                    Ended=false;
                    LastSnapshot=0;
                    cache.Clear();
                    cacheOrder.Clear();
                    history.Clear();
                    predictedHistory.Clear();
                    recent.Clear();
                    Snapshots.Clear();
                    Latest=null;
                    Predicted=null;
                    prepareAt=NowUs;
                    break;
                }
                case Op.HandshakeAck: {
                    if(r.U64()!=Match)return;
                    r.U64();
                    if(r.U8()!=Slot)throw new InvalidDataException("slot");
                    r.U64();
                    r.End();
                    handshake=true;
                    break;
                }
                case Op.Pong: {
                    if(r.U64()!=Match)return;
                    ulong sent=r.U64(),received=r.U64(),serverSent=r.U64();
                    r.End();
                    double now=NowUs,rtt=now-sent-(serverSent-received);
                    if(rtt<0)return;
                    LastRttUs=rtt;
                    double offset=((double)received-sent+(double)serverSent-now)/2;
                    if(rtt<MinRttUs) {
                        MinRttUs=rtt;
                        if(PingSamples<8)OffsetUs=offset;
                    }
                    if(PingSamples>=8)OffsetUs=OffsetUs*.9+offset*.1;
                    PingSamples++;
                    break;
                }
                case Op.Start:if(r.U64()!=Match)return;
                StartUs=r.U64();
                r.End();
                break;
                case Op.Full:case Op.Delta:ReceiveSnapshot(p,r);
                break;
                case Op.End: {
                    if(r.U64()!=Match)return;
                    EndReason=r.U8();
                    int n=r.U8();
                    if(n>8)throw new InvalidDataException("scores");
                    Scores.Clear();
                    for(int i=0;i<n;i++)Scores.Add(new Score {
                        Slot=r.U8(),Name=r.Str(96),Kills=r.U16(),Deaths=r.U16(),LastKill=r.U32(),Rank=r.U8(),Disconnected=r.Bool()
                    });
                    r.End();
                    Phase=ClientPhase.RESULTS;
                    ResultsUntilUs=NowUs+10e6;
                    Ended=true;
                    StartUs=0;
                    break;
                }
                case Op.Removed: {
                    uint room=r.U32();
                    int slot=r.U8();
                    int reason=r.U8();
                    r.End();
                    if(room==RoomId&&slot==Slot) {
                        Phase=token==null?ClientPhase.AUTHENTICATING:ClientPhase.LOBBY;
                        Slot=-1;
                        RoomId=0;
                        leavingMatch=false;
                        Latest=null;
                        Predicted=null;
                        Snapshots.Clear();
                        Players.Clear();
                        handshake=false;
                        StartUs=0;
                        AfkWarning=false;
                        Message=reason==3?"Bạn đã về sảnh vì không hoạt động trong 15 giây sau cảnh báo AFK.":reason==4?"Chủ phòng đã mời bạn ra.":reason==2?"Bạn đã về sảnh vì kết nối UDP quá hạn. Các người chơi còn lại tiếp tục trận.":"Đã rời phòng.";
                    }
                    break;
                }
                case Op.Notice:int noticeCode=r.U16();
                Message=r.Str();
                r.End();
                if(noticeCode==100) {
                    AfkWarning=true;
                    AfkWarnedAt=NowUs;
                }
                if(noticeCode==101)AfkWarning=false;
                break;
                case Op.HeartbeatAck:r.U64();
                r.End();
                break;
                default:throw new InvalidDataException("opcode");
            }
        }
    }
}
