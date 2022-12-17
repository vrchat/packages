using System.Collections;
using UnityEditor;
using System.IO;

namespace VRC.SDK3A.Editor
{
    [InitializeOnLoad]
    public class SDK3AImportFix
    {
        private const string packageRuntimePluginsFolder = "Packages/com.vrchat.avatars/Runtime/VRCSDK/Plugins";
        private const string legacyRuntimePluginsFolder = "Assets/VRCSDK/Plugins/";

        private const string sdkReloadedKey = "SDK_RELOADED";

        static SDK3AImportFix()
        {
            if (!SessionState.GetBool(sdkReloadedKey, false))
            {
                EditorCoroutine.Start(ReloadSDK());
            }
        }

        public static IEnumerator ReloadSDK()
        {
            //Wait for project to finish compiling
            while (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                yield return null;
            }
            Run();
            
            // Set session key to true, limiting the reload to one run per session
            SessionState.SetBool(sdkReloadedKey, true);
        }
        public static void Run()
        {
            if (Directory.Exists(packageRuntimePluginsFolder))
            {
                AssetDatabase.ImportAsset($"{packageRuntimePluginsFolder}/VRCSDK3A.dll",
                    ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ImportAsset($"{packageRuntimePluginsFolder}/VRCSDK3A-Editor.dll",
                    ImportAssetOptions.ForceSynchronousImport);
                // UnityEditor.SceneManagement.EditorSceneManager.LoadScene(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().name);
            }
            else if (Directory.Exists(legacyRuntimePluginsFolder))
            {
                AssetDatabase.ImportAsset($"{legacyRuntimePluginsFolder}/VRCSDK3A.dll",
                    ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ImportAsset($"{legacyRuntimePluginsFolder}/VRCSDK3A-Editor.dll",
                    ImportAssetOptions.ForceSynchronousImport);
                // UnityEditor.SceneManagement.EditorSceneManager.LoadScene(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().name);
            }
        }
    }
}
