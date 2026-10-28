using System;
using System.Collections.Generic;

namespace Tank {
    // Network notification data only; no dependency on a scene or game state.
    public sealed class AdminNotice {
        public readonly int Code;
        public readonly string Text;
        public AdminNotice(int code,string text) {Code=code;Text=text;}
    }

    public static class AdminNoticeChannel {
        public static event Action<AdminNotice> Received;
        public static void Publish(int code,string text) {
            if(code<200||code>202||string.IsNullOrWhiteSpace(text))return;
            var handler=Received;
            if(handler!=null)handler(new AdminNotice(code,text));
        }
    }

    // Retain the visible notice until dismissed; bound pending notifications.
    public sealed class AdminNoticeQueue {
        public const int Capacity=32;
        readonly Queue<AdminNotice> pending=new Queue<AdminNotice>();
        public AdminNotice Current {get;private set;}
        public int PendingCount {get{return pending.Count;}}
        public int DroppedCount {get;private set;}
        public void Enqueue(AdminNotice notice) {
            if(notice==null)return;
            if(Current==null) {Current=notice;return;}
            if(pending.Count==Capacity-1) {pending.Dequeue();DroppedCount++;}
            pending.Enqueue(notice);
        }
        public void Dismiss() {
            Current=pending.Count==0?null:pending.Dequeue();
            if(Current==null)DroppedCount=0;
        }
    }
}
