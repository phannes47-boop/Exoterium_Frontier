using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;
public static class FrontierSetup {
 [MenuItem("Exoterium/1 - Create playable scene")]
 public static void CreateScene(){Directory.CreateDirectory("Assets/Scenes");var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);new GameObject("Exoterium Game Controller").AddComponent<FrontierGame>();EditorSceneManager.SaveScene(scene,"Assets/Scenes/Frontier.unity");EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/Frontier.unity",true)};Debug.Log("Exoterium scene created. Press Play.");}
 [InitializeOnLoadMethod] static void OnLoad(){EditorApplication.delayCall+=()=>{if(!File.Exists("Assets/Scenes/Frontier.unity"))CreateScene();};}
}
