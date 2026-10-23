using System;
using System.IO;
namespace Tank  {
    public partial class ClientCore  {
        void Handle(Packet p) {
            if(p.Op==Op.SnapshotPart) {RecordSequence(p.Sequence);p=fragments.Add(p,Match,LastSnapshot,NowUs);if(p==null)return;}
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
            if(p.Op==Tank.Campaign.CampaignClient.Response){Campaign.Read(r);return;}
            if(p.Op==Op.ActionResult&&request==Tank.Campaign.CampaignClient.Request){Campaign.Pending=false;Campaign.Error="Server chưa hỗ trợ chiến dịch. Hãy cập nhật server và client cùng phiên bản.";return;}
            switch(p.Op) {
                case Op.RegisterResult:case Op.LoginResult:case Op.LogoutResult:case Op.ActionResult:case Op.RoomListResult:case Op.HistoryResult:case Op.HistoryDetailResult:case Op.DisplayNameResult: {
                    if(p.Op==Op.ActionResult&&IsRoleplayAction(request)) {RoleplayResult(r,request,p.Sequence);break;}
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
                        if(r.Remaining>0) {uint duration=r.U32();detail.DurationTicks=duration==uint.MaxValue?(uint?)null:duration;}
                        if(r.Remaining>0) {
                            if(r.U8()!=1)throw new InvalidDataException("history metadata version");
                            detail.RoomModeName=r.Str();detail.ContentVersion=r.Str();int count=r.U8();
                            if(count!=detail.Players.Count)throw new InvalidDataException("history loadout count");
                            var seen=new System.Collections.Generic.HashSet<int>();
                            for(int i=0;i<count;i++) {
                                int slot=r.U8(),nation=r.U8();var player=detail.Players.Find(score=>score.Slot==slot);
                                if(player==null||!seen.Add(slot)||nation!=255&&nation>7)throw new InvalidDataException("history loadout");
                                player.Nation=nation==255?-1:nation;
                            }
                        }
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
                            if(r.Remaining!=Rooms.Count&&r.Remaining!=2*Rooms.Count&&r.Remaining!=3*Rooms.Count)throw new InvalidDataException("room flags");
                            foreach(var room in Rooms)room.Locked=r.Bool();
                            if(r.Remaining>0)foreach(var room in Rooms)room.CanRejoin=r.Bool();
                            if(r.Remaining>0)foreach(var room in Rooms) {room.Mode=(RoomMode)r.U8();if((int)room.Mode>1)throw new InvalidDataException("room mode");}
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
                    RoomStartingHp=r.Remaining>0?r.U16():GameRules.DefaultHp;
                    if(RoomStartingHp<1||RoomStartingHp>GameRules.MaxStartHp)throw new InvalidDataException("starting HP");
                    RoomMode=RoomMode.NORMAL;
                    if(r.Remaining>0) {
                        RoomMode=(RoomMode)r.U8();PrepareStage=(PrepStage)r.U8();PrepareDeadline=r.U64();ulong preparingMatch=r.U64();r.U32();
                        if(RoomMode!=RoomMode.ROLEPLAY||(int)PrepareStage>3)throw new InvalidDataException("roleplay room");
                        foreach(var player in Players) {int nation=r.U8();if(nation!=255&&nation>7)throw new InvalidDataException("nation");player.Nation=nation==255?-1:nation;}
                        if(Phase==ClientPhase.PREPARING&&Match!=0&&preparingMatch!=Match)throw new InvalidDataException("preparation match");
                    }
                    r.End();
                    if(entering) {
                        Message="";
                        AfkWarning=false;
                    }
                    if(RoomPhase==RoomPhase.WAITING) {
                        if(Phase!=ClientPhase.RESULTS) {Phase=ClientPhase.ROOM;ClearRoleplayMatch();}
                        StartUs=0;
                        handshake=false;
                    }
                    break;
                }
                case Op.Prepare: {
                    leavingMatch=false;
                    PreparationError="";
                    ClearRoleplayMatch();
                    Match=r.U64();
                    RoomId=r.U32();
                    Slot=r.U8();
                    int mapId=r.U16();
                    byte[] hash=r.Bytes(32);
                    Mode=(NetworkMode)r.U8();
                    int rate=r.U8(),port=r.U16();
                    bool rejoining=r.Remaining>0&&r.Bool();
                    RoomMode=r.Remaining>0?(RoomMode)r.U8():RoomMode.NORMAL;
                    int contentVersion=0;byte[] content=null;uint requestHighWater=0;
                    if(RoomMode==RoomMode.ROLEPLAY) {contentVersion=r.U16();content=r.Bytes(32);requestHighWater=r.U32();}
                    else if(RoomMode!=RoomMode.NORMAL)throw new InvalidDataException("room mode");
                    r.End();
                    if(RoomMode==RoomMode.ROLEPLAY&&(contentVersion!=RoleplayContent.Version||!Equal(content,ContentHash()))) {
                        FailPreparation("Phiên bản nội dung không khớp server");return;
                    }
                    if(RoomMode==RoomMode.ROLEPLAY&&!roleplayAssetsReady) {FailPreparation("Thiếu hoặc không đọc được sprite: "+roleplayAssetError);return;}
                    roleplayRequestId=requestHighWater;
                    Tuple<GameMap,byte[]> selected;
                    if(!maps.TryGetValue(mapId,out selected)||rate!=30||!Equal(hash,selected.Item2)) {
                        FailPreparation("Map không khớp server");
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
                    LastRttUs=LastPongUs=inputRttUs=0;
                    LastArrivalUs=LastStateAgeUs=LastJitterUs=previousArrival=0;
                    LeadTicks=2;
                    OffsetUs=0;
                    StartUs=0;
                    handshake=prepared=false;
                    Ended=false;
                    Scores.Clear();
                    LastSnapshot=0;fragments.Clear();
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
                    LastPongUs=now;
                    UpdateInputLead(rtt);
                    double offset=((double)received-sent+(double)serverSent-now)/2;
                    if(rtt<MinRttUs) {
                        MinRttUs=rtt;
                        if(PingSamples<8)OffsetUs=offset;
                    }
                    if(PingSamples>=8)OffsetUs=OffsetUs*.9+offset*.1;
                    PingSamples++;
                    break;
                }
                case Op.MatchLoadout:ReceiveLoadout(r);break;
                case Op.Start:if(r.U64()!=Match||Phase!=ClientPhase.PREPARING||RoomMode==RoomMode.ROLEPLAY&&!LoadoutReady)return;
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
                    foreach(var score in Scores) {MatchLoadout loadout;if(Loadouts.TryGetValue(score.Slot,out loadout)) {score.Nation=loadout.Nation;}}
                    r.End();
                    Phase=ClientPhase.RESULTS;
                    Array.Clear(pendingSkills,0,pendingSkills.Length);
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
                        ClearRoleplayMatch();
                        StartUs=0;
                        AfkWarning=false;
                        Message=!string.IsNullOrEmpty(PreparationError)?PreparationError:reason==3?"Bạn đã về sảnh vì không hoạt động trong 15 giây sau cảnh báo AFK.":reason==4?"Chủ phòng đã mời bạn ra.":reason==2?"Bạn đã về sảnh vì kết nối UDP quá hạn. Các người chơi còn lại tiếp tục trận.":"Đã rời phòng.";
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
