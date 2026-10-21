using System.Collections.Generic;
namespace Tank {
    public class RoomInfo  {
        public uint Id;
        public string Name;
        public int Players,Capacity;
        public RoomPhase Phase;
        public RoomMode Mode;
        public bool Locked,CanRejoin;
    }
    public class RoomPlayer  {
        public int Slot;
        public int Nation=-1;
        public string Name;
        public bool Ready;
    }
    public class Score  {
        public int Slot,Kills,Deaths,Rank;
        public int Nation=-1;
        public string Name;
        public uint LastKill;
        public bool Disconnected;
    }
    public class HistoryEntry  {
        public ulong Match,Ended;
        public int Reason,Kills,Deaths,Rank;
    }
    public class MatchDetail  {
        public ulong Match,Started,Ended;
        public int MapId,Reason,OwnSlot;
        public uint? DurationTicks;
        public string RoomModeName,ContentVersion;
        public long DurationSeconds {
            get {
                // Simulation time is authoritative, including early-ended matches.
                if(DurationTicks.HasValue)return (DurationTicks.Value+15L)/30L;
                // Legacy rows have UTC timestamps only; avoid integer truncation and unsigned underflow.
                if(Ended<=Started)return 0;
                return (long)System.Math.Round((Ended-Started)/1000.0,System.MidpointRounding.AwayFromZero);
            }
        }
        public readonly List<Score> Players=new List<Score>();
    }
}
