using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
namespace Tank {
    public sealed class MatchLoadout {
        public int Slot,Nation;
    }
    public partial class ClientCore {
        public RoomMode RoomMode;
        public PrepStage PrepareStage;
        public ulong PrepareDeadline;
        public readonly Dictionary<int,MatchLoadout> Loadouts=new Dictionary<int,MatchLoadout>();
        public bool LoadoutReady;
        public string SkillNotice="",PreparationError="";
        public int SkillFeedbackRevision,SkillPressRevision;
        public bool LastSkillSucceeded;
        bool roleplayAssetsReady;
        string roleplayAssetError="Chưa kiểm tra sprite bắt buộc";
        uint roleplayRequestId,loadoutRevision;
        byte[] loadoutHash;
        sealed class PendingSkill {
            public uint Id,Sequence;
            public double Since;
            public bool ResyncRequested;
        }
        readonly PendingSkill[] pendingSkills=new PendingSkill[1];
        readonly int[] confirmedReady=new int[1];
        readonly uint[] confirmedTick=new uint[1];
        public void SetRoleplayAssets(string error) {
            roleplayAssetError=error;roleplayAssetsReady=string.IsNullOrEmpty(error);
        }
        static byte[] ContentHash() {
            var hash=new byte[32];for(int i=0;i<hash.Length;i++)hash[i]=Convert.ToByte(RoleplayContent.Hash.Substring(i*2,2),16);return hash;
        }
        static bool IsRoleplayAction(byte action) {
            return action==Op.UseSkill;
        }
        public void SelectNation(int nation) {
            if(RoomMode==RoomMode.ROLEPLAY&&Phase==ClientPhase.ROOM&&nation>=0&&nation<8)Request(Op.SelectNation,new WireWriter().U8(nation).Done());
        }
        public void Create(string name,int capacity,int seconds,string password,int startingHp,RoomMode roomMode) {
            if(capacity<2||capacity>8||seconds<60||seconds>600||seconds%60!=0||startingHp<1||startingHp>GameRules.MaxStartHp||(int)roomMode<0||(int)roomMode>1)throw new ArgumentOutOfRangeException("room settings");
            Request(Op.Create,new WireWriter().Str(name).U8(capacity).U16(seconds).Str(password??"").U16(startingHp).U8((int)roomMode).Done());
        }
        public bool SkillPending(int slot) {return slot==0&&pendingSkills[slot]!=null;}
        public int SkillReadyTick(int slot) {
            if(slot!=0||Latest==null)return 0;
            TankState tank;if(!Latest.World.Tanks.TryGetValue(Slot,out tank))return 0;
            return confirmedTick[slot]>Latest.Tick?confirmedReady[slot]:tank.QReadyTick;
        }
        public uint EstimatedSkillTick {
            get {return Latest==null?0:Latest.Tick+(uint)Math.Min(30,Math.Max(0,(NowUs-LastArrivalUs)*30/1e6));}
        }
        public bool UseSkill(int slot) {
            TankState tank;
            if(slot!=0||RoomMode!=RoomMode.ROLEPLAY||Phase!=ClientPhase.PLAYING||!LoadoutReady||SkillPending(slot)||Latest==null||!Latest.World.Tanks.TryGetValue(Slot,out tank)||tank.Hp==0||(!(tank.Nation==1&&tank.QStage==1)&&EstimatedSkillTick<SkillReadyTick(slot)))return false;
            uint id=++roleplayRequestId,sequence=Request(Op.UseSkill,new WireWriter().U64(Match).U32(id).U8(slot).F32(SkillAimX).F32(SkillAimY).Done());
            if(sequence==0)return false;
            pendingSkills[slot]=new PendingSkill{Id=id,Sequence=sequence,Since=NowUs};
            SkillPressRevision++;
            SkillNotice="Đang chờ server xác nhận Q";
            return true;
        }
        void PollRoleplay(double now) {
            for(int slot=0;slot<1;slot++) {
                var pending=pendingSkills[slot];
                if(pending==null||pending.ResyncRequested||now-pending.Since<=2e6)continue;
                pending.ResyncRequested=true;SkillNotice="Đồng bộ kỹ năng chậm — đang yêu cầu trạng thái mới";
                if(handshake)SendUdp(Op.Resync,Prefix().U32(LastSnapshot).Done());
            }
        }
        void ClearRoleplayMatch() {
            var stale=new List<uint>();foreach(var request in requests)if(IsRoleplayAction(request.Value)||request.Value==Op.Prepared)stale.Add(request.Key);
            foreach(uint request in stale)requests.Remove(request);
            Loadouts.Clear();LoadoutReady=false;loadoutRevision=roleplayRequestId=0;loadoutHash=null;
            Array.Clear(pendingSkills,0,pendingSkills.Length);Array.Clear(confirmedReady,0,confirmedReady.Length);Array.Clear(confirmedTick,0,confirmedTick.Length);
            SkillNotice="";PrepareDeadline=0;PrepareStage=PrepStage.LOADING;
        }
        void FailPreparation(string error) {
            PreparationError=error;Message=error;Leave();
        }
        void RoleplayResult(WireReader r,byte action,uint sequence) {
            int status=r.U16();string error=status==0?"":r.Str();
            if(r.Remaining==0) {
                for(int slot=0;slot<1;slot++)if(pendingSkills[slot]!=null&&pendingSkills[slot].Sequence==sequence)pendingSkills[slot]=null;
                Message=error;return;
            }
            ulong match=r.U64();uint id=r.U32();int acknowledgedAction=r.U8(),slotId=r.U8();uint tick=r.U32();int ready=r.U16();r.End();
            if(match!=Match||acknowledgedAction!=action)return;
            if(action!=Op.UseSkill) {Message=error;return;}
            if(slotId!=0||pendingSkills[slotId]==null||pendingSkills[slotId].Id!=id)return;
            pendingSkills[slotId]=null;
            if(status==0) {confirmedTick[slotId]=tick;confirmedReady[slotId]=ready;}
            LastSkillSucceeded=status==0;SkillFeedbackRevision++;
            SkillNotice=status==0?"Q đã được kích hoạt":SkillError(error);
        }
        static string SkillError(string error) {
            switch(error) {
                case "COOLDOWN":return "Kỹ năng đang hồi";
                case "NO_EFFECT":return "Kỹ năng chưa có tác dụng tại vị trí/trạng thái hiện tại";
                case "RATE_LIMITED":return "Thao tác quá nhanh, thử lại sau";
                default:return "Server từ chối kỹ năng: "+error;
            }
        }
        void ReceiveLoadout(WireReader r) {
            ulong match=r.U64();uint revision=r.U32();byte[] content=r.Bytes(32);int count=r.U8();
            if(count<1||count>8)throw new InvalidDataException("loadout count");
            var incoming=new Dictionary<int,MatchLoadout>();var nations=new HashSet<int>();int previous=-1;
            var canonical=new WireWriter().U64(match).U32(revision).Bytes(content).U8(count);
            for(int i=0;i<count;i++) {
                var p=new MatchLoadout{Slot=r.U8(),Nation=r.U8()};
                if(p.Slot<=previous||p.Slot>7||p.Nation>7||!nations.Add(p.Nation))throw new InvalidDataException("loadout entry");
                previous=p.Slot;incoming.Add(p.Slot,p);canonical.U8(p.Slot).U8(p.Nation);
            }
            byte[] hash=r.Bytes(32);r.End();byte[] expected;
            using(var sha=SHA256.Create())expected=sha.ComputeHash(canonical.Done());
            if(match!=Match||RoomMode!=RoomMode.ROLEPLAY||Phase!=ClientPhase.PREPARING)return;
            if(!Equal(content,ContentHash())||!Equal(hash,expected)||!incoming.ContainsKey(Slot)||revision==0) {FailPreparation("Bộ kỹ năng hoặc phiên bản nội dung không khớp server");return;}
            if(revision<loadoutRevision)return;
            Loadouts.Clear();foreach(var p in incoming)Loadouts.Add(p.Key,p.Value);
            loadoutRevision=revision;loadoutHash=hash;LoadoutReady=true;
            PrepareStage=PrepStage.SYNC_LOADOUT;
            Request(Op.Prepared,new WireWriter().U64(Match).U8((int)PrepStage.SYNC_LOADOUT).U32(revision).Bytes(hash).Done());
        }
        void AttachLoadout(WorldState world) {
            if(RoomMode!=RoomMode.ROLEPLAY)return;
            foreach(var tank in world.Tanks.Values) {
                MatchLoadout loadout;
                if(!Loadouts.TryGetValue(tank.Slot,out loadout))throw new InvalidDataException("missing reliable loadout");
                tank.Nation=loadout.Nation;
            }
        }
    }
}
