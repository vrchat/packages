using System.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using VRC.SDKBase.Editor.Source.Helpers;

namespace VRC.SDK3A.Editor
{
    [InitializeOnLoad]
    public class SDK3AImportFix
    {
        private const string sdkReloadedKey = "SDK_RELOADED";
        
        private const string exampleScenePath =
            "Packages/com.vrchat.avatars/Samples/Dynamics/Robot Avatar/Avatar Dynamics Robot Avatar.unity";

        static SDK3AImportFix()
        {
            EditorSceneManager.activeSceneChangedInEditMode += OnEditorSceneChanged;
        }

        private static void OnEditorSceneChanged(Scene arg0, Scene arg1)
        {
            if (!SessionState.GetBool(sdkReloadedKey, false))
            {
                EditorCoroutine.Start(ReloadSDK());
            }
        }
        
        [MenuItem("VRChat SDK/Samples/Avatar Dynamics Robot Avatar")]
        private static void OpenSampleUdonExampleScene()
        {
            EditorSceneManager.OpenScene(exampleScenePath);
        }

        public static IEnumerator ReloadSDK()
        {
            // Set session key to true, limiting the reload to one run per session
            SessionState.SetBool(sdkReloadedKey, true);
            
            //Wait for project to finish compiling
            while (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                yield return null;
            }
            
            ReloadUtil.ReloadSDK();
        }
    }
}