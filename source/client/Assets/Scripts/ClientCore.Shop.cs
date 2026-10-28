using System;
using System.IO;
namespace Tank {
    public static class RoleplayItemSchedule {
        public static int Interval(int duration) {return GameRules.RoleplayItemIntervalTicks;}
        public static int Window(int duration) {return GameRules.RoleplayItemWindowTicks;}
        public static int Wave(int duration,uint tick) {
            int interval=Interval(duration),wave=(int)(tick/(uint)interval);
            return wave>0&&tick<duration&&tick-(uint)(wave*interval)<Window(duration)?wave:0;
        }
    }
    public partial class ClientCore {
        public ulong WalletGold {get;private set;}
        public bool WalletLoaded {get;private set;}
        public string ShopNotice="",WalletError="";
        public bool ShopPending {get;private set;}
        ulong shopStateMatch;
        int shopStateWave,shopMask;
        readonly int[] itemInventory=new int[GameRules.ItemTypeCount];
        uint inventoryRevision;
        int shopCommand;
        uint shopRequest;
        double shopRequestedAt,nextWalletRefresh;
        public int ItemMatchTicks {get{return Latest==null?RoomSeconds*30:(int)(Latest.Tick+Latest.Remaining);}}
        public int ItemWave {get{return RoomMode==RoomMode.ROLEPLAY&&Phase==ClientPhase.PLAYING?RoleplayItemSchedule.Wave(ItemMatchTicks,EstimatedSkillTick):0;}}
        public bool ItemBought(int type) {return shopStateMatch==Match&&shopStateWave==ItemWave&&(shopMask&(1<<type))!=0;}
        public int ItemCount(int type) {return shopStateMatch==Match&&type>=0&&type<itemInventory.Length?itemInventory[type]:0;}
        public bool CanUseItem(int type) {
            TankState tank;
            return Phase==ClientPhase.PLAYING&&RoomMode==RoomMode.ROLEPLAY&&ItemCount(type)>0&&shopRequest==0&&Latest!=null&&Latest.World.Tanks.TryGetValue(Slot,out tank)&&tank.Hp>0;
        }
        public bool CanBuyItem(int type) {
            TankState tank;
            return type>=0&&type<GameRules.ItemTypeCount&&ItemWave>0&&WalletLoaded&&WalletGold>=GameRules.RoleplayItemPrice&&shopRequest==0&&!ItemBought(type)&&Latest!=null&&Latest.World.Tanks.TryGetValue(Slot,out tank)&&tank.Hp>0;
        }
        public void RefreshWallet() {
            if(Account==0||shopRequest!=0)return;
            shopRequest=Request(Op.Shop,new WireWriter().U8(0).U64(Phase==ClientPhase.PLAYING&&RoomMode==RoomMode.ROLEPLAY?Match:0).Done());
            shopCommand=0;
            shopRequestedAt=NowUs;nextWalletRefresh=NowUs+2e6;
        }
        public bool BuyItem(int type) {
            if(!CanBuyItem(type)||shopRequest!=0)return false;
            shopRequest=Request(Op.Shop,new WireWriter().U8(1).U64(Match).U8(ItemWave).U8(type).Done());
            if(shopRequest==0)return false;
            shopRequestedAt=NowUs;shopCommand=1;ShopPending=true;ShopNotice="Đang mua vật phẩm…";return true;
        }
        public bool UseItem(int type) {
            if(!CanUseItem(type))return false;
            shopRequest=Request(Op.Shop,new WireWriter().U8(2).U64(Match).U32(inventoryRevision).U8(type).Done());
            if(shopRequest==0)return false;
            shopRequestedAt=NowUs;shopCommand=2;ShopPending=true;ShopNotice="Đang dùng vật phẩm…";return true;
        }
        void PollShop(double now) {
            if(shopRequest!=0&&now-shopRequestedAt>8e6) {
                requests.Remove(shopRequest);shopRequest=0;ShopPending=false;ShopNotice="Giao dịch phản hồi chậm. Đang tải lại số dư.";
            }
            if(Account!=0&&now>=nextWalletRefresh)RefreshWallet();
        }
        void ReadShop(WireReader r,uint sequence) {
            int command=shopCommand;
            int status=r.U16();
            if(status!=0) {
                string error=r.Str();r.End();
                if(sequence!=shopRequest)return;
                if(command!=0)ShopNotice=error;else WalletError=error;
            }else {
                if(r.U8()!=2)throw new InvalidDataException("wallet version");
                ulong gold=r.U64(),match=r.U64();int wave=r.U8(),mask=r.U8();uint revision=r.U32();
                int[] counts=new int[GameRules.ItemTypeCount];for(int i=0;i<counts.Length;i++){counts[i]=r.U8();if(counts[i]>GameRules.MaxMatchTicks/GameRules.RoleplayItemIntervalTicks)throw new InvalidDataException("inventory count");}r.End();
                if(gold>long.MaxValue||wave>GameRules.MaxMatchTicks/GameRules.RoleplayItemIntervalTicks||(mask&~((1<<GameRules.ItemTypeCount)-1))!=0||wave==0&&mask!=0)throw new InvalidDataException("wallet state");
                if(sequence!=shopRequest)return;
                WalletGold=gold;WalletLoaded=true;WalletError="";
                shopStateMatch=match;shopStateWave=wave;shopMask=mask;
                inventoryRevision=revision;Array.Copy(counts,itemInventory,counts.Length);
                if(command==1)ShopNotice="Đã cất vật phẩm vào túi · −"+GameRules.RoleplayItemPrice+" vàng";
                if(command==2)ShopNotice="Đã sử dụng vật phẩm.";
            }
            shopRequest=0;ShopPending=false;nextWalletRefresh=NowUs+(command!=0?0:2e6);
        }
        void ClearWallet() {
            WalletGold=0;WalletLoaded=ShopPending=false;WalletError=ShopNotice="";
            shopStateMatch=0;shopStateWave=shopMask=0;shopRequest=0;nextWalletRefresh=0;
            inventoryRevision=0;shopCommand=0;Array.Clear(itemInventory,0,itemInventory.Length);
        }
        internal void SetWalletPreview(ulong gold,int[] counts) {
            var args=Environment.GetCommandLineArgs();
            if(Array.IndexOf(args,"--arena-preview")<0&&Array.IndexOf(args,"--menu-preview")<0)return;
            var payload=new WireWriter().U16(0).U8(2).U64(gold).U64(Match).U8(ItemWave).U8(0).U32(1);
            foreach(int count in counts)payload.U8(count);
            ReadShop(new WireReader(payload.Done()),0);
        }
    }
}
