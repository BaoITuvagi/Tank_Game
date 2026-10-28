using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Tank;
using UnityEngine;

public class CombatTests {
    [Test] public void RectangularArenaFitsWithoutOverlappingTheRightAnchoredSidebarAndAimStaysCorrect() {
        foreach(var size in new[]{new[]{1280f,800f},new[]{1920f,1080f},new[]{960f,640f}}) {
            var layout=ArenaLayout.Fit(size[0],size[1],120,80);
            Assert.AreEqual(size[0],layout.SidebarX+ArenaLayout.SidebarWidth);
            Assert.GreaterOrEqual(layout.X,0);Assert.LessOrEqual(layout.X+120*layout.Scale,layout.SidebarX);
            Assert.GreaterOrEqual(layout.Y,ArenaLayout.Top);Assert.LessOrEqual(layout.Y+80*layout.Scale,size[1]);
            var tank=new TankState{X=60,Y=40};
            Assert.AreEqual(0,GameplayAim.Angle(layout.X+70*layout.Scale,layout.Y+40*layout.Scale,1,layout.X,layout.Y,layout.Scale,80,tank));
            Assert.AreEqual(9000,GameplayAim.Angle(layout.X+60*layout.Scale,layout.Y+30*layout.Scale,1,layout.X,layout.Y,layout.Scale,80,tank));
        }
    }
    [Test] public void ReducedTankHasMatchingRotatedCollisionAndMuzzleOffsets() {
        Assert.AreEqual(2.04f,CombatGeometry.TankSize);Assert.AreEqual(1.26f,GameRules.TankMuzzleOffset);Assert.AreEqual(.72f,GameRules.BulletSpawnOffset);
        var map=new GameMap{width=120,height=80,walls=new[]{new MapRect{x=0,y=0,w=1.5f,h=80}},bricks=new MapRect[0],water=new MapRect[0]};
        Assert.IsTrue(map.Blocked(2.9f,10,4500,new byte[64]));Assert.IsFalse(map.Blocked(3,10,4500,new byte[64]));
    }
    [Test] public void SkillExplosionsFitTheirZonesAndRocketsHaveNoSmoke() {
        var p=new CombatPresentation();var w=World();p.Accept(9,Snap(w,1),0);
        foreach(int kind in new[]{SkillEffect.Bomb,SkillEffect.Rocket,SkillEffect.Kamikaze})w.Effects.Add(new SkillEffect{Id=(uint)kind,Kind=kind,Start=10,End=10,X=10,Y=10,Radius=kind==SkillEffect.Bomb?GameRules.BombRadius:kind==SkillEffect.Rocket?GameRules.RocketRadius:GameRules.KamikazeRadius});
        p.Accept(9,Snap(w,10),0);
        var explosions=p.Transients.Where(f=>f.Active&&f.Clip=="combat/explosion_top").ToArray();
        CollectionAssert.AreEquivalent(new[]{GameRules.BombRadius*2,GameRules.RocketRadius*2,GameRules.KamikazeRadius*2},explosions.Select(f=>ArenaRenderer.EffectWidth(f.Width,f.OriginalSize)));
        Assert.AreEqual(2,Clips(p,"nature/smoke_top"));
        Assert.AreEqual(2,p.Transients.Count(f=>f.Active&&f.Clip=="nature/smoke_top"&&f.OriginalSize));
        Assert.IsTrue(p.Transients.Where(f=>f.Active&&f.Clip=="nature/smoke_top").All(f=>ArenaRenderer.EffectWidth(f.Width,f.OriginalSize)<=GameRules.KamikazeRadius*2));
        Assert.AreEqual(6,ArenaRenderer.EffectWidth(1.2f));Assert.AreEqual(1,Clips(p,"combat/ground_slam_top"));
    }
    WorldState World() {
        var w=new WorldState();
        w.Tanks[0]=new TankState{Slot=0,Life=1,X=10,Y=10,Hp=100,Nation=6};
        w.Tanks[1]=new TankState{Slot=1,Life=2,X=12,Y=10,Hp=100};return w;
    }
    Snapshot Snap(WorldState w,uint tick) {return new Snapshot{Match=9,Id=tick+1,Tick=tick,World=w};}
    CombatEvent Event(uint id,uint tick,int kind=CombatEvent.Shot,int target=255,int life=0,int cause=0,uint causeId=0) {
        return new CombatEvent(id,tick,kind,0,target,1,life,1,65535,0,10,10,0,kind==CombatEvent.Death?100:kind==CombatEvent.Damage?20:0,cause,causeId);
    }
    static byte[] Bytes(string hex) {var data=new byte[hex.Length/2];for(int i=0;i<data.Length;i++)data[i]=Convert.ToByte(hex.Substring(i*2,2),16);return data;}
    static int Clips(CombatPresentation p,string clip) {return p.Transients.Count(f=>f.Active&&f.Clip==clip);}
    [Test] public void RecordAndWorldRoundTripAreImmutableAndExactly43Bytes() {
        var w=World();var e=Event(1,10,CombatEvent.Death,1,2,3,123);w.Combat.Add(1,e);
        var writer=new WireWriter();e.Write(writer);Assert.AreEqual(43,writer.Done().Length);
        var r=new WireReader(w.Encode());var copy=WorldState.Full(r);r.End();CollectionAssert.AreEqual(w.Encode(),copy.Encode());
        Assert.AreSame(e,w.Copy().Combat[1]);Assert.AreEqual(123,copy.Combat[1].CauseId);Assert.IsTrue(copy.Combat[1].CauseOwnsExplosion);
    }
    [Test] public void InvalidCauseSlotLifeAndNonfiniteRecordsAreRejected() {
        Assert.Throws<InvalidDataException>(()=>Event(1,10,CombatEvent.Death,1,2,3,0));
        Assert.Throws<InvalidDataException>(()=>Event(1,10,CombatEvent.Shot,255,0,3,1));
        Assert.Throws<InvalidDataException>(()=>new CombatEvent(1,1,1,0,255,0,0,1,65535,0,0,0,0,0,0,0));
        Assert.Throws<InvalidDataException>(()=>new CombatEvent(1,1,1,0,255,1,0,1,65535,0,float.NaN,0,0,0,0,0));
    }
    [Test] public void JavaCombatDeltaRepairsDroppedSnapshotAndExpiresWithoutChangingBase() {
        WorldState world=null;
        foreach(string line in File.ReadAllLines(Path.GetFullPath("../../source/server/target/interop/combat.tsv"))) {
            var f=line.Split('\t');if(f[0]=="DROPPED")continue;
            var r=new WireReader(Bytes(f[1]));byte[] before=world==null?null:world.Encode();var previous=world;
            world=f[0]=="FULL"?WorldState.Full(r):DeltaCodec.Apply(world,r);r.End();
            if(f.Length>2)CollectionAssert.AreEqual(Bytes(f[2]),world.Encode());
            if(previous!=null)CollectionAssert.AreEqual(before,previous.Encode());
        }
        Assert.AreEqual(0,world.Combat.Count);
    }
    [Test] public void JavaEveryCombatKindAndCauseIsIdenticalInCSharp() {
        WorldState world=null;
        foreach(string line in File.ReadAllLines(Path.GetFullPath("../../source/server/target/interop/combat-all.tsv"))) {
            var f=line.Split('\t');var r=new WireReader(Bytes(f[1]));world=f[0]=="FULL"?WorldState.Full(r):DeltaCodec.Apply(world,r);r.End();
            if(f.Length>2)CollectionAssert.AreEqual(Bytes(f[2]),world.Encode());
        }
        CollectionAssert.AreEquivalent(new[]{1,2,3,4,5,6},world.Combat.Values.Select(e=>e.Kind).Distinct());
        CollectionAssert.AreEquivalent(new[]{0,1,2,3,4,10},world.Combat.Values.Select(e=>e.CauseKind).Distinct());
    }
    [Test] public void MaximumCombatSkillAndItemFragmentsRecoverAfterReorderingAndDuplicates() {
        var lines=File.ReadAllLines(Path.GetFullPath("../../source/server/target/interop/combat-fragments.tsv"));var assembler=new SnapshotAssembler();WorldState world=null;
        foreach(string kind in new[]{"FULL","DELTA"}) {
            var packets=lines.Where(line=>line.StartsWith(kind+"\t")).Select(line=>Protocol.Decode(Bytes(line.Split('\t')[1]),true)).ToArray();Packet completed=null;
            for(int i=packets.Length-1;i>=0;i--) {
                var result=assembler.Add(packets[i],7,kind=="FULL"?0u:1u,1000);
                if(i>0) {Assert.IsNull(result);Assert.IsNull(assembler.Add(packets[i],7,kind=="FULL"?0u:1u,1000));}else completed=result;
            }
            Assert.IsNotNull(completed);var r=new WireReader(completed.Payload);r.U64();r.U32();r.U32();r.U64();r.U32();r.U16();
            if(kind=="FULL")world=WorldState.Full(r);else {Assert.AreEqual(1,r.U32());world=DeltaCodec.Apply(world,r);}r.End();
            Assert.AreEqual(256,world.Combat.Count);Assert.AreEqual(192,world.Effects.Count);Assert.AreEqual(256,world.ItemEffects.Count);
        }
        CollectionAssert.AreEqual(Bytes(lines.Last().Split('\t')[1]),world.Encode());
    }
    [Test] public void MalformedCombatDeltaDoesNotMutateBase() {
        var w=World();w.Combat.Add(1,Event(1,1));byte[] before=w.Encode();
        var payload=new WireWriter().U8(0).U8(0).U8(0).U8(0).U16(0);ItemCodec.Write(w,payload);
        payload.U16(1).U32(1).U16(1);Event(1,2).Write(payload); // Cannot remove and re-add an immutable ID.
        Assert.Throws<InvalidDataException>(()=>DeltaCodec.Apply(w,new WireReader(payload.Done())));
        CollectionAssert.AreEqual(before,w.Encode());
    }
    [Test] public void SnapshotAcceptanceDispatchesEachIntermediateStateBeforeEviction() {
        using(var client=new ClientCore(null,new byte[32])) {
            client.Match=9;client.Slot=0;client.Phase=ClientPhase.PLAYING;
            var p=new CombatPresentation();int accepted=0;client.SnapshotAccepted+=s=> {p.Accept(9,s,0);accepted++;};
            var handle=typeof(ClientCore).GetMethod("Handle",BindingFlags.Instance|BindingFlags.NonPublic);
            foreach(uint tick in new uint[]{1,2,3}) {
                var w=World();if(tick==2)w.Combat.Add(1,Event(1,2));
                byte[] payload=new WireWriter().U64(9).U32(tick).U32(tick).U64(tick*33333).U32(tick).U16(100).Bytes(w.Encode()).Done();
                handle.Invoke(client,new object[]{new Packet{Op=Op.Full,Sequence=tick,Payload=payload}});
            }
            Assert.AreEqual(3,accepted);Assert.AreEqual(1,Clips(p,"combat/muzzle"));Assert.AreEqual(0,client.Latest.World.Combat.Count);
        }
    }
    [Test] public void InvalidCombatFullCannotChangeLatestOrAcknowledgedSnapshot() {
        using(var client=new ClientCore(null,new byte[32])) {
            client.Match=9;client.Slot=0;client.Phase=ClientPhase.PLAYING;int accepted=0;client.SnapshotAccepted+=s=>accepted++;
            var handle=typeof(ClientCore).GetMethod("Handle",BindingFlags.Instance|BindingFlags.NonPublic);var w=World();w.Combat.Add(1,Event(1,2));
            byte[] payload=new WireWriter().U64(9).U32(1).U32(2).U64(66666).U32(2).U16(100).Bytes(w.Encode()).Done();
            handle.Invoke(client,new object[]{new Packet{Op=Op.Full,Sequence=1,Payload=payload}});var before=client.Latest;
            w.Combat[1]=Event(1,3);payload=new WireWriter().U64(9).U32(2).U32(3).U64(99999).U32(3).U16(100).Bytes(w.Encode()).Done();
            Assert.Throws<TargetInvocationException>(()=>handle.Invoke(client,new object[]{new Packet{Op=Op.Full,Sequence=2,Payload=payload}}));
            Assert.AreSame(before,client.Latest);Assert.AreEqual(1,client.LastSnapshot);Assert.AreEqual(1,accepted);
        }
    }
    [Test] public void DuplicateFullDoesNotRestartClipAndLateClipKeepsItsServerAge() {
        var p=new CombatPresentation();var w=World();p.Accept(9,Snap(w,1),0);
        w.Combat.Add(1,Event(1,9));p.Accept(9,Snap(w,10),0);Assert.AreEqual(1,Clips(p,"combat/muzzle"));
        float start=p.Transients.First(f=>f.Active&&f.Clip=="combat/muzzle").Start;
        p.Update(.04f,w,null,0);p.Accept(9,Snap(w,11),0);
        Assert.AreEqual(1,Clips(p,"combat/muzzle"));Assert.AreEqual(start,p.Transients.First(f=>f.Active&&f.Clip=="combat/muzzle").Start);
        p.Update(.3f,w,null,0);Assert.AreEqual(0,Clips(p,"combat/muzzle"));
    }
    [Test] public void FreshEventUsesConfirmedAgeEvenAfterClockCorrectionAndQueuedOldAudioIsDropped() {
        var p=new CombatPresentation();var w=World();p.Accept(9,Snap(w,1),0);p.Update(10,w,null,0);float clock=p.Clock;
        w.Combat.Add(1,Event(1,2));p.Accept(9,Snap(w,2),0);Assert.GreaterOrEqual(p.Clock,clock);Assert.AreEqual(1,Clips(p,"combat/muzzle"));
        var effect=p.Transients.First(f=>f.Active&&f.Clip=="combat/muzzle");Assert.AreEqual(0,p.Clock-effect.Start,.0001);
        w.Combat.Clear();p.Accept(9,Snap(w,100),0);CombatPresentation.Sound sound;Assert.IsFalse(p.NextSound(out sound));
    }
    [Test] public void KamikazeAndDeathsHaveOneExplosionAndOneWreckPerLife() {
        var p=new CombatPresentation();var w=World();p.Accept(9,Snap(w,1),0);
        w.Effects.Add(new SkillEffect{Id=1,Kind=3,Owner=0,Start=10,End=10,X=10,Y=10,Radius=9.5625f});
        w.Tanks[0].Hp=w.Tanks[1].Hp=0;
        w.Combat.Add(1,Event(1,10,CombatEvent.Death,0,1,3,1));w.Combat.Add(2,Event(2,10,CombatEvent.Death,1,2,3,1));
        p.Accept(9,Snap(w,10),0);Assert.AreEqual(1,Clips(p,"combat/explosion_top"));Assert.AreEqual(2,p.Wrecks.Count(f=>f.Active));
        int explosions=0;CombatPresentation.Sound sound;while(p.NextSound(out sound))if(sound.Clip==13||sound.Clip==25)explosions++;
        Assert.AreEqual(1,explosions);
    }
    [Test] public void MissingOrLaterCauseDoesNotMakeDeathRecreateExplosion() {
        var p=new CombatPresentation();var w=World();p.Accept(9,Snap(w,1),0);w.Tanks[1].Hp=0;
        w.Combat.Add(1,Event(1,10,CombatEvent.Death,1,2,10,100));p.Accept(9,Snap(w,10),0);
        Assert.AreEqual(0,Clips(p,"combat/explosion_top"));Assert.AreEqual(1,p.Wrecks.Count(f=>f.Active));
        w.ItemEffects.Add(new SkillEffect{Id=100,Kind=10,Start=10,End=10,X=10,Y=10,Radius=1.7f});p.Accept(9,Snap(w,11),0);
        Assert.AreEqual(1,Clips(p,"combat/explosion_top"));
    }
    [Test] public void LethalHeSplashMergesTheDirectHitAndSplashFeedbackByCause() {
        var p=new CombatPresentation();var w=World();p.Accept(9,Snap(w,1),0);w.Tanks[1].Hp=0;
        w.Combat.Add(1,Event(1,10,CombatEvent.Damage,1,2,10,100));
        w.Combat.Add(2,new CombatEvent(2,10,CombatEvent.Death,0,1,1,2,0,65535,0,10,10,0,100,10,100));
        w.ItemEffects.Add(new SkillEffect{Id=100,Kind=10,Start=10,End=10,X=10,Y=10,Radius=1.7f});p.Accept(9,Snap(w,10),0);
        Assert.AreEqual(1,Clips(p,"combat/explosion_top"));Assert.AreEqual(1,Clips(p,"combat/hit_top"));
        CombatPresentation.Sound sound;while(p.NextSound(out sound))Assert.AreNotEqual(28,sound.Clip);
    }
    [Test] public void CombinedMirrorBulletsKeepTrailsAndPlayExplosionThenSplit() {
        var p=new CombatPresentation();var w=World();p.Accept(9,Snap(w,1),0);
        w.Bullets[1]=new BulletState{Id=1,Kind=ItemCodec.HeMirrorBullet,Damage=15};w.Bullets[2]=new BulletState{Id=2,Kind=ItemCodec.HeMirrorChild,Damage=25};
        w.ItemEffects.Add(new SkillEffect{Id=1,Kind=10,Start=10,End=10,X=10,Y=10,Radius=GameRules.HighExplosiveRadius});
        w.ItemEffects.Add(new SkillEffect{Id=2,Kind=11,Start=10,End=10,X=10,Y=10,Radius=1,Angle=18000});
        p.Accept(9,Snap(w,10),0);Assert.AreEqual(2,p.TrailBulletCount);Assert.AreEqual(1,Clips(p,"combat/explosion_top"));Assert.AreEqual(1,Clips(p,"movement/dash"));
        p.Accept(9,Snap(w,11),0);Assert.AreEqual(1,Clips(p,"combat/explosion_top"));Assert.AreEqual(1,Clips(p,"movement/dash"));
    }
    [Test] public void BeamAndIndependentNearbyCausesKeepTheirDistinctVisuals() {
        var p=new CombatPresentation();var w=World();p.Accept(9,Snap(w,1),0);
        for(uint id=1;id<=2;id++)w.ItemEffects.Add(new SkillEffect{Id=id,Kind=10,Start=10,End=10,X=10,Y=10,Radius=1.7f});
        p.Accept(9,Snap(w,10),0);Assert.AreEqual(2,Clips(p,"combat/explosion_top"));
        w.Combat.Add(3,Event(3,11,CombatEvent.Death,1,2,4,500));w.Tanks[1].Hp=0;p.Accept(9,Snap(w,11),0);
        Assert.AreEqual(3,Clips(p,"combat/explosion_top"));
    }
    [Test] public void BeamOneShotRespectsJoinWatermarkAndOnlyConfirmedImpactCreatesLine() {
        var p=new CombatPresentation();var w=World();
        w.Effects.Add(new SkillEffect{Id=1,Kind=4,Start=10,End=10,X=10,Y=10,Radius=.85f});p.Accept(9,Snap(w,10),0);
        Assert.AreEqual(0,Clips(p,"@beam"));
        w.Effects.Clear();w.Effects.Add(new SkillEffect{Id=2,Kind=4,Start=15,End=15,X=10,Y=10,Radius=.85f});p.Accept(9,Snap(w,14),0);
        Assert.AreEqual(0,Clips(p,"@beam"));p.Accept(9,Snap(w,15),0);Assert.AreEqual(1,Clips(p,"@beam"));
    }
    [Test] public void RejoinWatermarkAndHiddenEffectsNeverReplay() {
        var p=new CombatPresentation();var w=World();w.Combat.Add(1,Event(1,10));p.Accept(9,Snap(w,10),0);Assert.AreEqual(0,p.ActiveCount);
        w.Tanks[1].X=w.Tanks[1].Y=50;w.Tanks[1].FogEndTick=100;
        w.Combat.Add(2,Event(2,11));p.Accept(9,Snap(w,11),1);w.Tanks[1].FogEndTick=0;p.Accept(9,Snap(w,12),1);
        Assert.AreEqual(0,p.ActiveCount);p.Clear();p.Accept(9,Snap(w,12),0);Assert.AreEqual(0,p.ActiveCount);
    }
    [Test] public void RespawnAndMatchChangeClearAttachedEffectsAndOldWrecks() {
        var p=new CombatPresentation();var w=World();p.Accept(9,Snap(w,1),0);w.Combat.Add(1,Event(1,2));p.Accept(9,Snap(w,2),0);
        w.Tanks[0].Life=3;p.Update(.01f,w,null,0);Assert.AreEqual(0,Clips(p,"combat/muzzle"));
        Assert.AreEqual(0,p.Recoil(w.Tanks[0]));p.Accept(10,Snap(w,3),0);Assert.AreEqual(0,p.ActiveCount);
    }
    [Test] public void SmokeStopsAfterHealingAndShiftDoesNotEmitWhileStationary() {
        var p=new CombatPresentation();var w=World();w.Tanks[0].Hp=30;p.Accept(9,Snap(w,1),0);p.Update(.01f,w,null,0);
        Assert.AreEqual(1,p.Smoke.Count(f=>f.Active));Assert.AreEqual(0,p.Dust.Count(f=>f.Active));
        Assert.GreaterOrEqual(p.Smoke.First(f=>f.Active).Tint.a,.8f);
        w.Tanks[0].Hp=100;p.Update(1,w,null,0);Assert.AreEqual(0,p.Smoke.Count(f=>f.Active));
    }
    [Test] public void PoolAndLedgerRemainBoundedAndExpiredKeysArePruned() {
        var p=new CombatPresentation();var w=World();p.Accept(9,Snap(w,1),0);
        for(uint tick=2;tick<350;tick++) {w.Combat.Clear();w.Combat.Add(tick,Event(tick,tick));p.Accept(9,Snap(w,tick),0);p.Update(1f/30,w,null,0);}
        Assert.LessOrEqual(p.LedgerCount,91);Assert.LessOrEqual(p.Transients.Count(f=>f.Active),256);
        p.Clear();Assert.AreEqual(0,p.ActiveCount);Assert.AreEqual(0,p.LedgerCount);
    }
    [Test] public void CriticalEffectsReplaceDecorationsBeforeOtherCriticalEffects() {
        var p=new CombatPresentation();var w=World();p.Accept(9,Snap(w,1),0);
        for(uint id=1;id<=160;id++)w.Combat.Add(id,Event(id,10));p.Accept(9,Snap(w,10),0);
        Assert.AreEqual(160,p.Transients.Count(f=>f.Active));
        w.Combat.Clear();w.Tanks[1].Hp=0;
        for(uint id=161;id<=400;id++)w.Combat.Add(id,Event(id,11,CombatEvent.Death,1,2));p.Accept(9,Snap(w,11),0);
        Assert.AreEqual(256,p.Transients.Count(f=>f.Active));Assert.AreEqual(256,p.Transients.Count(f=>f.Active&&f.Critical));
    }
    [Test] public void PresentationUpdateAllocatesNoManagedMemoryAfterWarmup() {
        var p=new CombatPresentation();var w=World();w.Tanks[0].Hp=25;p.Accept(9,Snap(w,1),0);
        for(int i=0;i<100;i++)p.Update(1f/60,w,null,0);
        long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<600;i++)p.Update(1f/60,w,null,0);
        long allocated=GC.GetAllocatedBytesForCurrentThread()-before;Assert.AreEqual(0,allocated);
    }
#if UNITY_EDITOR_WIN
    [Test] public void WindowsVoicesReleasePinnedBuffersAcrossTenMatchStopsAndCloseOnDispose() {
        var type=typeof(ClientCore).Assembly.GetType("Tank.SkillSounds",true);
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var audio=(IDisposable)Activator.CreateInstance(type,true);
        var voices=(Array)type.GetField("voices",flags).GetValue(audio);
        var play=type.GetMethod("PlayWorld");var stop=type.GetMethod("Stop");
        int opened=0,prepared=0,closed=0;
        try {
            // Exercise the real Windows device/buffer lifecycle without audible output.
            var clips=(System.Collections.IDictionary)type.GetField("clips",flags).GetValue(audio);
            foreach(byte[] pcm in clips.Values)Array.Clear(pcm,0,pcm.Length);
            foreach(object voice in voices)if(voice!=null)opened++;
            Assert.LessOrEqual(opened,6);
            for(int match=0;match<10;match++) {
                for(int cue=0;cue<6;cue++)play.Invoke(audio,new object[]{24,1f,cue<2});
                foreach(object voice in voices)if(voice!=null&&(bool)voice.GetType().GetField("Prepared").GetValue(voice))prepared++;
                stop.Invoke(audio,null);
                foreach(object voice in voices)if(voice!=null) {
                    Assert.IsFalse((bool)voice.GetType().GetField("Prepared").GetValue(voice));
                    var buffer=(System.Runtime.InteropServices.GCHandle)voice.GetType().GetField("Buffer").GetValue(voice);
                    Assert.IsFalse(buffer.IsAllocated,"Pinned PCM retained after match stop");
                }
            }
            audio.Dispose();audio.Dispose();
            var close=type.GetMethod("waveOutClose",BindingFlags.Static|BindingFlags.NonPublic);
            foreach(object voice in voices)if(voice!=null) {
                var handle=(IntPtr)voice.GetType().GetField("Handle").GetValue(voice);
                // A second native close must fail: Dispose already released this handle.
                Assert.AreNotEqual(0u,(uint)close.Invoke(null,new object[]{handle}));closed++;
            }
            Debug.Log("COMBAT_AUDIO_LIFECYCLE opened="+opened+" prepared="+prepared+" closed="+closed+" matchStops=10");
        }
        finally {audio.Dispose();}
    }
#endif
    [Test] public void SpriteFramesClampLoopAndUsePlaybackSpeed() {
        var description=new CombatFxCatalog.Description{frames=8,fps=24,cell=128};var clip=new CombatFxCatalog.Clip(description,null);
        Assert.AreEqual(0,clip.Frame(0));Assert.AreEqual(7,clip.Frame(100));Assert.AreEqual(4,clip.Frame(1f/12,2));
        description.loop=true;Assert.AreEqual(0,clip.Frame(1f/3));Assert.AreEqual(1f/8,clip.Uv(0).width);
    }
}
