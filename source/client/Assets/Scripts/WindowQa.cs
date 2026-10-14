using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace Tank  {
    // Opt-in QA drives the same fullscreen action used by the F11 key and visible button.
    public sealed class WindowQa:MonoBehaviour  {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot() {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--window-qa")>=0)new GameObject("Window QA").AddComponent<WindowQa>();
        }
        [Serializable]public class Stage {
            public string name;
            public int width,height;
            public bool fullscreen;
        }
        [Serializable]public class Result {
            public bool passed;
            public List<Stage> stages=new List<Stage>();
        }
        IEnumerator Start() {
            string directory=UxTestDriver.Arg("--window-qa","");
            Directory.CreateDirectory(directory);
            var result=new Result();
            yield return new WaitForSecondsRealtime(2);
            var app=FindFirstObjectByType<TankApp>();
            Screen.SetResolution(960,600,FullScreenMode.Windowed);
            yield return new WaitForSecondsRealtime(2);
            yield return Capture(directory,"small",result);
            Screen.SetResolution(1600,900,FullScreenMode.Windowed);
            yield return new WaitForSecondsRealtime(2);
            yield return Capture(directory,"wide",result);
            int restoreWidth=Screen.width,restoreHeight=Screen.height;
            app.ToggleFullscreen();
            yield return new WaitForSecondsRealtime(2);
            yield return Capture(directory,"fullscreen",result);
            app.ToggleFullscreen();
            yield return new WaitForSecondsRealtime(2);
            yield return Capture(directory,"restored",result);
            result.passed=result.stages[0].width==960&&result.stages[0].height==600&&!result.stages[0].fullscreen&&result.stages[1].width==1600&&result.stages[1].height==900&&result.stages[2].fullscreen&&!result.stages[3].fullscreen&&result.stages[3].width==restoreWidth&&result.stages[3].height==restoreHeight;
            File.WriteAllText(Path.Combine(directory,"window-qa.json"),JsonUtility.ToJson(result,true));
            yield return new WaitForSecondsRealtime(1);
            Application.Quit(result.passed?0:1);
        }
        IEnumerator Capture(string directory,string name,Result result) {
            yield return new WaitForEndOfFrame();
            result.stages.Add(new Stage {
                name=name,width=Screen.width,height=Screen.height,fullscreen=Screen.fullScreen
            });
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,name+".png"));
            yield return new WaitForSecondsRealtime(1);
        }
    }
}
