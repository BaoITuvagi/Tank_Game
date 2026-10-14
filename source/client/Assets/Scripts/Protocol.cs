using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
namespace Tank  {
    public enum NetworkMode  {
        BASELINE, INPUT_REDUNDANCY, DELTA, COMBINED
    }
    public enum RoomPhase  {
        WAITING, PREPARING, RUNNING, RESULTS
    }
    public enum ClientPhase  {
        DISCONNECTED, CONNECTING, AUTHENTICATING, LOBBY, ROOM, PREPARING, PLAYING, RESULTS
    }
    public enum ErrorCode  {
        OK, INVALID_REQUEST, UNAUTHENTICATED, INVALID_CREDENTIALS, USERNAME_EXISTS, ALREADY_LOGGED_IN, ROOM_MISSING, ROOM_FULL, ROOM_BUSY, NOT_HOST, NOT_READY, INVALID_STATE, RATE_LIMITED, DATABASE_UNAVAILABLE, MAP_MISMATCH, ROOM_PASSWORD_REQUIRED, ROOM_PASSWORD_INCORRECT
    }
    public static class Op  {
        public const byte Register=1,RegisterResult=2,Login=3,LoginResult=4,Logout=5,LogoutResult=6,RoomList=7,RoomListResult=8,Create=9,Join=10,Leave=11,Ready=12,StartMatch=13,Kick=14,ActionResult=15,Handshake=16,HandshakeAck=17,Ping=18,Pong=19,Input=32,Ack=33,Resync=34,Full=48,Delta=49,RoomState=64,Prepare=65,Prepared=66,Start=67,End=68,Removed=69,Notice=70,History=71,HistoryResult=72,Heartbeat=73,HeartbeatAck=74,SetDisplayName=75,DisplayNameResult=76,RoomSettings=77,HistoryDetail=78,HistoryDetailResult=79;
    }
    public class Packet  {
        public byte Op;
        public uint Sequence;
        public byte[] Payload;
    }
    public sealed class WireWriter  {
        readonly MemoryStream s=new MemoryStream();
        public WireWriter U8(int x) {
            s.WriteByte((byte)x);
            return this;
        }
        public WireWriter U16(int x) {
            return U8(x>>8).U8(x);
        }
        public WireWriter U32(uint x) {
            return U16((int)(x>>16)).U16((int)x);
        }
        public WireWriter U64(ulong x) {
            return U32((uint)(x>>32)).U32((uint)x);
        }
        public WireWriter F32(float x) {
            if(float.IsNaN(x)||float.IsInfinity(x))throw new InvalidDataException("nonfinite");
            return U32(BitConverter.ToUInt32(BitConverter.GetBytes(x),0));
        }
        public WireWriter Bytes(byte[] a) {
            s.Write(a,0,a.Length);
            return this;
        }
        public WireWriter Str(string x) {
            byte[] a=Encoding.UTF8.GetBytes(x);
            if(a.Length>65535)throw new InvalidDataException("string");
            return U16(a.Length).Bytes(a);
        }
        public byte[] Done() {
            return s.ToArray();
        }
    }
    public sealed class WireReader  {
        readonly byte[] a;
        int i;
        public int Remaining  {
            get {
                return a.Length-i;
            }
        }
        public WireReader(byte[] x) {
            a=x;
        }
        public int U8() {
            if(i>=a.Length)throw new InvalidDataException("truncated");
            return a[i++];
        }
        public int U16() {
            return (U8()<<8)|U8();
        }
        public uint U32() {
            return ((uint)U16()<<16)|(uint)U16();
        }
        public ulong U64() {
            return ((ulong)U32()<<32)|U32();
        }
        public float F32() {
            float x=BitConverter.ToSingle(BitConverter.GetBytes(U32()),0);
            if(float.IsNaN(x)||float.IsInfinity(x))throw new InvalidDataException("nonfinite");
            return x;
        }
        public byte[] Bytes(int n) {
            if(n<0||n>a.Length-i)throw new InvalidDataException("length");
            byte[] x=new byte[n];
            Array.Copy(a,i,x,0,n);
            i+=n;
            return x;
        }
        public string Str(int max=256) {
            int n=U16();
            if(n>max)throw new InvalidDataException("string length");
            return new UTF8Encoding(false,true).GetString(Bytes(n));
        }
        public bool Bool() {
            int x=U8();
            if(x>1)throw new InvalidDataException("boolean");
            return x==1;
        }
        public void End() {
            if(i!=a.Length)throw new InvalidDataException("trailing bytes");
        }
    }
    public static class Protocol  {
        public static bool Newer(uint a,uint b) {
            return a!=b&&unchecked((int)(a-b))>0;
        }
        public static byte[] Frame(byte op,uint sequence,byte[] p) {
            if(p.Length>16384)throw new InvalidDataException("size");
            return new WireWriter().U16(0x5442).U8(2).U8(op).U32(sequence).U16(p.Length).U16(0).Bytes(p).Done();
        }
        public static Packet Decode(byte[] a,bool udp=false) {
            if(a.Length<12||(udp&&a.Length>1200))throw new InvalidDataException("size");
            var r=new WireReader(a);
            if(r.U16()!=0x5442||r.U8()!=2)throw new InvalidDataException("magic/version");
            var p=new Packet {
                Op=(byte)r.U8(),Sequence=r.U32()
            };
            int n=r.U16();
            if(n>16384||r.U16()!=0)throw new InvalidDataException("flags/length");
            p.Payload=r.Bytes(n);
            r.End();
            return p;
        }
        static byte[] Exact(Stream s,int n) {
            byte[] a=new byte[n];
            int i=0;
            while(i<n) {
                int got=s.Read(a,i,n-i);
                if(got==0)throw new EndOfStreamException();
                i+=got;
            }
            return a;
        }
        public static Packet Read(Stream s) {
            byte[] h=Exact(s,12);
            var r=new WireReader(h);
            if(r.U16()!=0x5442||r.U8()!=2)throw new InvalidDataException("header");
            var p=new Packet {
                Op=(byte)r.U8(),Sequence=r.U32()
            };
            int n=r.U16();
            if(n>16384||r.U16()!=0)throw new InvalidDataException("length/flags");
            p.Payload=Exact(s,n);
            return p;
        }
        static async Task<byte[]> ExactAsync(Stream s,int n) {
            byte[] a=new byte[n];
            int i=0;
            while(i<n) {
                int got=await s.ReadAsync(a,i,n-i).ConfigureAwait(false);
                if(got==0)throw new EndOfStreamException();
                i+=got;
            }
            return a;
        }
        public static async Task<Packet> ReadAsync(Stream s) {
            byte[] h=await ExactAsync(s,12).ConfigureAwait(false);
            var r=new WireReader(h);
            if(r.U16()!=0x5442||r.U8()!=2)throw new InvalidDataException("header");
            var p=new Packet {
                Op=(byte)r.U8(),Sequence=r.U32()
            };
            int n=r.U16();
            if(n>16384||r.U16()!=0)throw new InvalidDataException("length/flags");
            p.Payload=await ExactAsync(s,n).ConfigureAwait(false);
            return p;
        }
    }
}
