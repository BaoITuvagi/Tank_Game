using System;
using System.IO;
using NUnit.Framework;
using Tank;
using UnityEngine;
public class ClientPreferencesTests {
    string folder,path;
    [SetUp] public void Setup(){folder=Path.Combine(Path.GetTempPath(),"tank-settings-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);path=Path.Combine(folder,"settings.json");}
    [TearDown] public void Cleanup(){Directory.Delete(folder,true);}
    [Test] public void DefaultsPreserveBothMovementSetsMouseFireAndCampaignControls(){
        var p=ClientPreferences.Defaults();
        Assert.IsTrue(p.SoundEnabled);
        Assert.IsTrue(p.IsPressed(ControlAction.Up,k=>k==KeyCode.W));Assert.IsTrue(p.IsPressed(ControlAction.Up,k=>k==KeyCode.UpArrow));
        Assert.IsTrue(p.IsPressed(ControlAction.Fire,k=>k==KeyCode.Mouse0));Assert.IsTrue(p.IsPressed(ControlAction.Fire,k=>k==KeyCode.Space));
        Assert.IsTrue(p.IsPressed(ControlAction.Sprint,k=>k==KeyCode.RightShift));Assert.IsTrue(p.IsPressed(ControlAction.CampaignFire,k=>k==KeyCode.I));
    }
    [Test] public void RebindingChangesInputAndMouseButtonsStayOutOfArenaClicks(){
        var p=ClientPreferences.Defaults();string error;
        Assert.IsTrue(p.TryBind(ControlAction.Up,false,KeyCode.F6,out error));
        Assert.IsFalse(p.IsPressed(ControlAction.Up,k=>k==KeyCode.W));Assert.IsTrue(p.IsPressed(ControlAction.Up,k=>k==KeyCode.F6));
        Assert.IsFalse(p.IsPressed(ControlAction.Fire,k=>k==KeyCode.Mouse0,false));Assert.IsTrue(p.IsPressed(ControlAction.Fire,k=>k==KeyCode.Space,false));
        Assert.IsTrue(p.TryBind(ControlAction.AutoFire,false,KeyCode.F7,out error));Assert.IsTrue(p.IsPressed(ControlAction.AutoFire,k=>k==KeyCode.F7,false));
        Assert.IsTrue(p.TryBind(ControlAction.Skill,false,KeyCode.Mouse3,out error));Assert.IsFalse(p.IsPressed(ControlAction.Skill,k=>k==KeyCode.Mouse3,false));
    }
    [Test] public void ConflictsAreCheckedInEachGameModeAndSharedMovementCannotOverlap(){
        var p=ClientPreferences.Defaults();string error;
        Assert.IsFalse(p.TryBind(ControlAction.Up,false,KeyCode.S,out error));StringAssert.Contains("Di chuyển xuống",error);
        Assert.IsFalse(p.TryBind(ControlAction.CampaignFire,false,KeyCode.W,out error));
        Assert.IsTrue(p.TryBind(ControlAction.Fire,true,KeyCode.I,out error),"Different modes may share keys.");
        Assert.IsFalse(p.TryBind(ControlAction.CampaignSkill,true,KeyCode.I,out error));
        Assert.IsFalse(p.TryBind(ControlAction.Skill,true,KeyCode.Q,out error),"Two slots of an action must not duplicate.");
        Assert.IsFalse(p.TryBind(ControlAction.Up,false,KeyCode.Escape,out error));Assert.IsFalse(p.TryBind(ControlAction.Up,false,KeyCode.F11,out error));
        Assert.IsTrue(p.TryBind(ControlAction.Left,false,KeyCode.None,out error));Assert.IsTrue(p.TryBind(ControlAction.Left,false,KeyCode.Mouse4,out error));
    }
    [Test] public void SaveReloadAndReplacePersistSoundAndKeysWithoutChangingAnUnsavedCopy(){
        var p=ClientPreferences.Defaults();var draft=p.Copy();string error;
        draft.SoundEnabled=false;Assert.IsTrue(draft.TryBind(ControlAction.CampaignSkill,false,KeyCode.F8,out error));
        Assert.IsTrue(p.SoundEnabled);Assert.AreEqual(KeyCode.O,p.Bindings[(int)ControlAction.CampaignSkill].Primary);
        Assert.IsTrue(draft.Save(path,out error),error);var loaded=ClientPreferences.Load(path,out error);Assert.IsEmpty(error);
        Assert.IsFalse(loaded.SoundEnabled);Assert.AreEqual(KeyCode.F8,loaded.Bindings[(int)ControlAction.CampaignSkill].Primary);
        loaded.SoundEnabled=true;Assert.IsTrue(loaded.Save(path,out error));Assert.IsTrue(ClientPreferences.Load(path,out error).SoundEnabled);Assert.IsFalse(File.Exists(path+".tmp"));
    }
    [Test] public void CorruptUnknownVersionOrConflictingSavedSettingsFallBackToDefaults(){
        string warning;
        Assert.IsTrue(ClientPreferences.Load(path,out warning).SoundEnabled);Assert.IsEmpty(warning);
        foreach(string text in new[]{"invalid", "{}", "{\"Version\":9}"}){File.WriteAllText(path,text);var p=ClientPreferences.Load(path,out warning);Assert.IsNotEmpty(warning);Assert.AreEqual(KeyCode.W,p.Bindings[0].Primary);}
        var bad=ClientPreferences.Defaults();bad.Bindings[1].Primary=KeyCode.W;File.WriteAllText(path,JsonUtility.ToJson(bad));
        Assert.AreEqual(KeyCode.S,ClientPreferences.Load(path,out warning).Bindings[1].Primary);Assert.IsNotEmpty(warning);
    }
    [Test] public void FailedSaveDoesNotReplaceAnExistingFileAndMasterAudioCanBeRestored(){
        string error;var p=ClientPreferences.Defaults();Assert.IsTrue(p.Save(path,out error));string original=File.ReadAllText(path);
        p.Bindings[0].Primary=KeyCode.Escape;Assert.IsFalse(p.Save(path,out error));Assert.AreEqual(original,File.ReadAllText(path));
        var previous=ClientPreferences.Active;float volume=AudioListener.volume;
        try{var muted=ClientPreferences.Defaults();muted.SoundEnabled=false;ClientPreferences.Activate(muted);Assert.AreEqual(0,AudioListener.volume);ClientPreferences.Activate(ClientPreferences.Defaults());Assert.AreEqual(1,AudioListener.volume);}
        finally{ClientPreferences.Activate(previous);AudioListener.volume=volume;}
    }
}
