#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class HokmnamehSetup
{
    [MenuItem("Hokmnameh/Setup Build Scene")]
    public static void Generate()
    {
        Directory.CreateDirectory("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Hokmnameh.unity");
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Hokmnameh.unity", true) };
        PlayerSettings.companyName = "Hokmnameh";
        PlayerSettings.productName = "Hokmnameh";
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.forceInternetPermission = true;
        Debug.Log("Hokmnameh boot scene generated. Press Play for functional prototype.");
    }
}
#endif
