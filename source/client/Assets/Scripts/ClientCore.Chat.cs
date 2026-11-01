using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
namespace Tank {
    public sealed class ChatMessage {
        public uint Id;
        public int Slot,Palette;
        public string Name,Text;
    }
    public sealed class MatchChatState {
        public const int Limit=100,MaxChars=200,MaxBytes=800;
        readonly List<ChatMessage> messages=new List<ChatMessage>();
        public IReadOnlyList<ChatMessage> Messages {get {return messages;}}
        public ulong Match {get;private set;}
        public uint LastId {get;private set;}
        public void Reset(ulong match=0) {Match=match;LastId=0;messages.Clear();}
        public void Receive(WireReader r,ulong currentMatch,bool active) {
            ulong match=r.U64();int count=r.U8();
            if(count<1||count>10)throw new InvalidDataException("chat count");
            var batch=new List<ChatMessage>();uint previous=0;
            for(int i=0;i<count;i++) {
                var message=new ChatMessage{Id=r.U32(),Slot=r.U8(),Palette=r.U8(),Name=r.Str(96),Text=r.Str(MaxBytes)};
                string text;
                if(message.Id==0||message.Id<=previous||message.Slot>7||message.Palette>7||!TryNormalize(message.Text,out text)||text!=message.Text)throw new InvalidDataException("chat message");
                previous=message.Id;batch.Add(message);
            }
            r.End();
            if(!active||match!=currentMatch||match!=Match)return;
            foreach(var message in batch)if(message.Id>LastId) {
                messages.Add(message);LastId=message.Id;
                if(messages.Count>Limit)messages.RemoveAt(0);
            }
        }
        public static bool TryNormalize(string raw,out string text) {
            text="";
            if(raw==null)return false;
            try {if(new UTF8Encoding(false,true).GetByteCount(raw)>MaxBytes)return false;}
            catch(EncoderFallbackException){return false;}
            // Match Java String.strip(), including Unicode whitespace other than NBSP.
            text=raw.Replace('\r',' ').Replace('\n',' ').Replace('\t',' ');
            int start=0,end=text.Length;
            while(start<end&&StripSpace(text[start]))start++;
            while(end>start&&StripSpace(text[end-1]))end--;
            text=text.Substring(start,end-start);
            int count=0;
            for(int i=0;i<text.Length;i++) {
                if(char.IsControl(text[i]))return false;
                if(char.IsHighSurrogate(text[i]))i++;
                count++;
            }
            return count>0&&count<=MaxChars;
        }
        static bool StripSpace(char c) {return c>='\u001c'&&c<='\u001f'||char.IsWhiteSpace(c)&&c!='\u0085'&&c!='\u00a0'&&c!='\u2007'&&c!='\u202f';}
    }
    public partial class ClientCore {
        public readonly MatchChatState Chat=new MatchChatState();
        public string ChatError="";
        uint chatRequest;
        double chatSentAt;
        public bool ChatPending {get {return chatRequest!=0;}}
        public bool SendChat(string raw) {
            string text;
            if(!MatchChatState.TryNormalize(raw,out text)){ChatError="Nhập 1–200 ký tự, tối đa 800 byte UTF-8.";return false;}
            if(Phase!=ClientPhase.PLAYING||ChatPending){ChatError="Chưa thể gửi tin lúc này.";return false;}
            chatRequest=Request(Op.ChatSend,new WireWriter().U64(Match).Str(text).Done());
            chatSentAt=NowUs;
            ChatError=chatRequest==0?"Mất kết nối, tin chưa được gửi.":"";
            return chatRequest!=0;
        }
        void ReadChatResult(WireReader r,uint sequence) {
            int code=r.U16();if(code!=0)r.Str();r.End();
            if(sequence!=chatRequest)return;
            chatRequest=0;
            ChatError=code==0?"":code==(int)ErrorCode.RATE_LIMITED?"Bạn gửi quá nhanh. Mỗi giây chỉ gửi một tin.":code==(int)ErrorCode.INVALID_REQUEST?"Tin nhắn không hợp lệ hoặc quá dài.":"Không thể gửi tin trong trạng thái hiện tại.";
        }
        void ClearChat(ulong match=0) {Chat.Reset(match);ChatError="";chatRequest=0;}
        void PollChat(double now) {
            if(chatRequest!=0&&now-chatSentAt>8e6){requests.Remove(chatRequest);chatRequest=0;ChatError="Phản hồi chậm; tin có thể đã được gửi. Kiểm tra chat trước khi gửi lại.";}
        }
    }
}
