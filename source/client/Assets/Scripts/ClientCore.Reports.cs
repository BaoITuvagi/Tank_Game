using System;
using System.Collections.Generic;
using System.Text;
namespace Tank {
    public partial class ClientCore {
        public static readonly string[] ReportReasons={"AFK / Rời trận","Phá trận / Cố ý bị hạ","Ngôn từ xúc phạm","Gian lận / Dùng phần mềm","Tên không phù hợp","Lý do khác"};
        readonly HashSet<string> reportedSlots=new HashSet<string>();
        uint quickRequest,reportRequest;
        double quickSentAt,reportSentAt;
        int reportingSlot=-1;
        ulong reportingMatch;
        bool quickFiltered;
        uint quickRoomId;
        public uint QuickJoinPasswordRoom;
        public string ReportMessage="";
        public ulong LastReportId {get;private set;}
        public bool QuickJoinPending {get{return quickRequest!=0;}}
        public bool ReportPending {get{return reportRequest!=0;}}
        public ulong ReportContextMatch {get{return Phase==ClientPhase.RESULTS?Match:HistoryDetail==null?0:HistoryDetail.Match;}}
        public bool HasReported(int slot){return reportedSlots.Contains(ReportContextMatch+":"+slot); }
        public bool CanReport(int slot){
            var scores=Phase==ClientPhase.RESULTS?Scores:HistoryDetail==null?null:HistoryDetail.Players;
            int own=Phase==ClientPhase.RESULTS?Slot:HistoryDetail==null?-1:HistoryDetail.OwnSlot;
            return ReportContextMatch!=0&&scores!=null&&slot!=own&&scores.Exists(s=>s.Slot==slot&&!s.BotOrigin)&&!ReportPending&&!HasReported(slot);
        }
        public static byte[] QuickJoinPayload(int mode=255,int mapId=0,uint roomId=0) {
            if(roomId>int.MaxValue)throw new ArgumentOutOfRangeException(nameof(roomId));
            if(roomId!=0)return new WireWriter().U8(255).U16(0).U32(roomId).Done();
            if(mode!=255&&mode!=0&&mode!=1)throw new ArgumentOutOfRangeException(nameof(mode));
            if(mapId!=0&&!MapCatalog.IsSelectable(mapId))throw new ArgumentOutOfRangeException(nameof(mapId));
            var payload=new WireWriter().U8(mode);
            if(mapId!=0)payload.U16(mapId);
            return payload.Done();
        }
        public void QuickJoin(int mode=255,int mapId=0,uint roomId=0) {
            if(Phase!=ClientPhase.LOBBY||QuickJoinPending)return;
            quickRequest=Request(Op.QuickJoin,QuickJoinPayload(mode,mapId,roomId));quickSentAt=NowUs;quickFiltered=mode!=255||mapId!=0;quickRoomId=roomId;QuickJoinPasswordRoom=0;
            Message=quickRequest==0?"Chưa kết nối tới server.":roomId!=0?"Đang tìm phòng ID #"+roomId+"…":quickFiltered?"Đang tìm phòng phù hợp với lựa chọn…":"Đang tìm phòng còn chỗ…";
        }
        public static string ReportValidationError(int reasons,string details) {
            details=details??"";
            if(reasons==0||(reasons&~63)!=0)return "Chọn ít nhất một lý do tố cáo.";
            if(details.Length>500||Encoding.UTF8.GetByteCount(details)>1500)return "Mô tả tối đa 500 ký tự và 1.500 byte UTF-8.";
            foreach(char c in details)if(char.IsControl(c)&&c!='\n'&&c!='\t')return "Mô tả chứa ký tự không hợp lệ.";
            if((reasons&32)!=0&&string.IsNullOrWhiteSpace(details))return "Hãy mô tả cụ thể khi chọn Lý do khác.";
            return "";
        }
        public void ReportPlayer(int slot,int reasons,string details) {
            if(!CanReport(slot))return;
            ReportMessage=ReportValidationError(reasons,details);if(ReportMessage!="")return;
            reportRequest=Request(Op.ReportPlayer,new WireWriter().U64(ReportContextMatch).U8(slot).U8(reasons).Str((details??"").Trim()).Done());
            reportingSlot=slot;reportingMatch=ReportContextMatch;reportSentAt=NowUs;
            if(reportRequest==0)ReportMessage="Chưa kết nối tới server. Hãy thử lại.";
        }
        void ReadReportResult(WireReader r,uint sequence) {
            int code=r.U16();
            if(sequence!=reportRequest)return;
            ulong id=0;bool duplicate=false;string message="";
            if(code==0){id=r.U64();duplicate=r.Bool();}else message=r.Str();r.End();
            reportRequest=0;
            if(reportingMatch!=ReportContextMatch)return;
            if(code==0){reportedSlots.Add(reportingMatch+":"+reportingSlot);LastReportId=id;ReportMessage=(duplicate?"Đơn này đã được tiếp nhận.":"Đã gửi tố cáo tới quản trị viên.")+" Mã đơn #"+id+". Cảm ơn bạn đã phản hồi.";}
            else ReportMessage=code==(int)ErrorCode.RATE_LIMITED?"Bạn gửi quá nhiều yêu cầu. Vui lòng chờ rồi thử lại.":code==(int)ErrorCode.INVALID_REQUEST?"Không thể tố cáo người chơi này trong trận đã chọn. Kiểm tra lý do và mô tả.":code==(int)ErrorCode.DATABASE_UNAVAILABLE?"Server chưa lưu được đơn. Hãy thử gửi lại.":"Không gửi được tố cáo: "+message;
        }
        void ReadQuickJoin(WireReader r,uint sequence) {
            int code=r.U16();string message=code==0?"":r.Str();r.End();
            if(sequence!=quickRequest)return;
            quickRequest=0;
            if(quickRoomId!=0) {
                if(code==(int)ErrorCode.ROOM_PASSWORD_REQUIRED)QuickJoinPasswordRoom=quickRoomId;
                Message=code==0?"Đã tìm thấy phòng ID #"+quickRoomId+".":code==(int)ErrorCode.ROOM_MISSING?"Không tìm thấy phòng ID #"+quickRoomId+" đang chờ và còn chỗ.":code==(int)ErrorCode.ROOM_PASSWORD_REQUIRED?"Phòng có mật khẩu. Nhập mật khẩu để vào đúng phòng này.":"Không vào được phòng ID #"+quickRoomId+": "+message;
                return;
            }
            Message=code==0?"Đã tìm thấy phòng.":code==(int)ErrorCode.ROOM_MISSING?(quickFiltered?"Chưa có phòng đang chờ còn chỗ phù hợp với lựa chọn. Hãy đổi điều kiện hoặc tạo phòng.":"Chưa có phòng đang chờ còn chỗ. Hãy tạo phòng hoặc thử lại."):code==(int)ErrorCode.RATE_LIMITED?"Thao tác quá nhanh. Hãy chờ rồi thử lại.":"Không vào được phòng: "+message;
        }
        void ClearReports(){reportedSlots.Clear();ReportMessage="";LastReportId=0;reportRequest=0;reportingSlot=-1;reportingMatch=0;QuickJoinPasswordRoom=quickRoomId=0;}
        void PollReports(double now) {
            if(quickRequest!=0&&now-quickSentAt>10e6){requests.Remove(quickRequest);quickRequest=0;Message="Tìm phòng quá hạn. Hãy thử lại.";}
            if(reportRequest!=0&&now-reportSentAt>10e6){requests.Remove(reportRequest);reportRequest=0;ReportMessage="Chưa nhận được xác nhận. Bạn có thể gửi lại; server sẽ không tạo đơn trùng.";}
        }
    }
}
