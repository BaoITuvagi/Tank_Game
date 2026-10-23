using System;
using System.Collections.Generic;
using System.IO;
namespace Tank {
    public sealed class ItemState {
        public const int Medical=0,Armor=1,HighExplosive=2,Purification=3,Invincibility=4,Mirror=5;
        public uint Id;
        public int Type,Expires;
        public float X,Y;
        public bool IsExpired(uint tick) {return Expires>0&&tick>=Expires;}
        public ItemState Copy() {return (ItemState)MemberwiseClone();}
        public void Validate() {
            if(Id==0||Id>int.MaxValue||Type<0||Type>=GameRules.ItemTypeCount||Expires<0||Expires>65535||float.IsNaN(X)||float.IsInfinity(X)||float.IsNaN(Y)||float.IsInfinity(Y))throw new InvalidDataException("item");
        }
        public static ItemState Read(WireReader r) {
            var i=new ItemState{Id=r.U32(),Type=r.U8(),X=r.F32(),Y=r.F32(),Expires=r.U16()};i.Validate();return i;
        }
        public void Write(WireWriter w) {Validate();w.U32(Id).U8(Type).F32(X).F32(Y).U16(Expires);}
    }
    public static class ItemCodec {
        public const int NormalBullet=0,HeBullet=1,MirrorBullet=2,MirrorChild=3,HeMirrorBullet=4,HeMirrorChild=5;
        public const int MirrorDamage=(GameRules.BulletDamage*GameRules.MirrorDamagePercent+50)/100,GermanMirrorDamage=GameRules.GermanBulletDamage+MirrorDamage;
        public static bool HasHe(int kind) {return kind==HeBullet||kind==HeMirrorBullet||kind==HeMirrorChild;}
        public static bool IsMirrorParent(int kind) {return kind==MirrorBullet||kind==HeMirrorBullet;}
        public static bool IsMirrorChild(int kind) {return kind==MirrorChild||kind==HeMirrorChild;}
        public static bool HasMirror(int kind) {return IsMirrorParent(kind)||IsMirrorChild(kind);}
        public static bool IsGerman(BulletState bullet) {return bullet.Damage==GameRules.GermanBulletDamage||IsMirrorChild(bullet.Kind)&&bullet.Damage==GermanMirrorDamage;}
        public static bool ValidBulletDamage(int kind,int damage) {
            if(kind<NormalBullet||kind>HeMirrorChild)return false;
            return IsMirrorChild(kind)?damage==MirrorDamage||damage==GermanMirrorDamage:damage==GameRules.BulletDamage||damage==GameRules.GermanBulletDamage;
        }
        public static float BulletSize(int kind) {return (HasHe(kind)?GameRules.HighExplosiveSizePercent/100f:1)*(IsMirrorChild(kind)?GameRules.MirrorSizePercent/100f:1);}
        public static void Read(WorldState w,WireReader r) {
            int n=r.U8();if(n!=w.Tanks.Count)throw new InvalidDataException("item tank count");
            var slots=new HashSet<int>();
            for(int i=0;i<n;i++) {
                int slot=r.U8();TankState t;
                if(!w.Tanks.TryGetValue(slot,out t)||!slots.Add(slot))throw new InvalidDataException("item tank");
                t.ArmorEndTick=r.U16();t.HeEndTick=r.U16();t.InvincibilityEndTick=r.U16();t.MirrorShots=r.U8();t.QBaseReadyTick=r.U16();Validate(t);
            }
            n=r.U8();if(n>GameRules.MaxItems)throw new InvalidDataException("items");w.Items.Clear();
            for(int i=0;i<n;i++) {var item=ItemState.Read(r);if(w.Items.ContainsKey(item.Id))throw new InvalidDataException("duplicate item");w.Items.Add(item.Id,item);}
            n=r.U16();if(n>GameRules.MaxItemEffects)throw new InvalidDataException("item effects");w.ItemEffects.Clear();var ids=new HashSet<uint>();
            for(int i=0;i<n;i++) {
                var e=SkillEffect.Read(r);
                if(e.Kind<9||e.Kind>11||e.Kind==9&&e.Angle>=GameRules.ItemTypeCount||!ids.Add(e.Id))throw new InvalidDataException("item effect");
                w.ItemEffects.Add(e);
            }
        }
        public static void Validate(TankState t) {
            foreach(int end in new[]{t.ArmorEndTick,t.HeEndTick,t.InvincibilityEndTick,t.QBaseReadyTick})if(end<0||end>65535)throw new InvalidDataException("item deadline");
            if(t.MirrorShots<0||t.MirrorShots>GameRules.MirrorShots)throw new InvalidDataException("mirror shots");
        }
        public static void Write(WorldState w,WireWriter writer) {
            if(w.Items.Count>GameRules.MaxItems||w.ItemEffects.Count>GameRules.MaxItemEffects)throw new InvalidDataException("item count");
            writer.U8(w.Tanks.Count);
            foreach(var t in w.Tanks.Values) {Validate(t);writer.U8(t.Slot).U16(t.ArmorEndTick).U16(t.HeEndTick).U16(t.InvincibilityEndTick).U8(t.MirrorShots).U16(t.QBaseReadyTick);}
            writer.U8(w.Items.Count);foreach(var item in w.Items.Values)item.Write(writer);
            writer.U16(w.ItemEffects.Count);foreach(var e in w.ItemEffects)e.Write(writer);
        }
    }
}
