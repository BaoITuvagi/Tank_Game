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
                // A remembered maximized window can override the initial CLI size.
                // Reapply the requested capture size when recreating the swap chain.
                int widthIndex=Array.IndexOf(args,"-screen-width"),heightIndex=Array.IndexOf(args,"-screen-height"),value;
                if(widthIndex>=0&&widthIndex+1<args.Length&&int.TryParse(args[widthIndex+1],out value)&&value>0)width=value;
                if(heightIndex>=0&&heightIndex+1<args.Length&&int.TryParse(args[heightIndex+1],out value)&&value>0)height=value;
                // Shrink first: expanding at desktop width can be clamped and
                // leave the initial hidden swap chain unchanged (black capture).
                Screen.SetResolution(Mathf.Max(1,width-1),height,FullScreenMode.Windowed);
                yield return new WaitForSecondsRealtime(.5f);
                Screen.SetResolution(width,height,FullScreenMode.Windowed);
                yield return new WaitForSecondsRealtime(1);
            }
            yield return new WaitForEndOfFrame();
            Debug.Log("Preview capture: "+Screen.width+"x"+Screen.height);
            ScreenCapture.CaptureScreenshot(args[index+1]);
            yield return new WaitForSecondsRealtime(2);
            Application.Quit();
        }
    }
}
