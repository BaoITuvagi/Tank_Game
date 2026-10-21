using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class BuildClient {
 public static void Build(){BuildTo("../../build/client/TankBattle.exe");}
 public static void BuildGameplay(){BuildTo("../../build/client-gameplay/TankBattle.exe");}
 public static void BuildCurrentScene(){
  string path=Path.GetFullPath("../../build/client/TankBattle.exe");Directory.CreateDirectory(Path.GetDirectoryName(path));
  var result=BuildPipeline.BuildPlayer(new[]{"Assets/Scenes/Main.unity"},path,BuildTarget.StandaloneWindows64,BuildOptions.None);
  if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new System.Exception("Build failed: "+result.summary.result);
 }
 static void BuildTo(string output){
  Directory.CreateDirectory("Assets/Scenes");var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var camera=new GameObject("Camera").AddComponent<Camera>();camera.backgroundColor=new Color(.035f,.065f,.085f);camera.clearFlags=CameraClearFlags.SolidColor;EditorSceneManager.SaveScene(scene,"Assets/Scenes/Main.unity");PlayerSettings.companyName="PTIT";PlayerSettings.productName="Tank Battle";PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=800;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.runInBackground=true;PlayerSettings.resizableWindow=true;
  string path=Path.GetFullPath(output);Directory.CreateDirectory(Path.GetDirectoryName(path));var result=BuildPipeline.BuildPlayer(new[]{"Assets/Scenes/Main.unity"},path,BuildTarget.StandaloneWindows64,BuildOptions.None);if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new System.Exception("Build failed: "+result.summary.result);
 }
}
