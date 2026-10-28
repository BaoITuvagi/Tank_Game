using System;
using System.Collections;
using UnityEngine;
namespace Tank  {
    public class CapturePreview:MonoBehaviour  {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Boot() {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--capture")>=0)new GameObject("Preview Capture").AddComponent<CapturePreview>();
        }
        IEnumerator Start() {
            var args=Environment.GetCommandLineArgs();
            int index=Array.IndexOf(args,"--capture");
            int delayIndex=Array.IndexOf(args,"--capture-delay");float delay=3,requested;
            if(delayIndex>=0&&delayIndex+1<args.Length&&float.TryParse(args[delayIndex+1],System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out requested)&&requested>=0&&requested<=60)delay=requested;
            yield return new WaitForSecondsRealtime(delay);
            // A helper launched with a hidden window can have a black initial swap chain.
            // Recreate it through Unity's resize path, then capture at the requested size.
            if(!Screen.fullScreen) {
                int width=Screen.width,height=Screen.height;
                Screen.SetResolution(width+1,height,FullScreenMode.Windowed);
                yield return new WaitForSecondsRealtime(.5f);
                Screen.SetResolution(width,height,FullScreenMode.Windowed);
                yield return new WaitForSecondsRealtime(1);
            }
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(args[index+1]);
            yield return new WaitForSecondsRealtime(2);
            Application.Quit();
        }
    }
}
