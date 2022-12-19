using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using VRC.SDKBase.Editor.Source.Helpers;

namespace VRC.SDK3A.Editor
{
    [InitializeOnLoad]
    public class SDK3AImportFix
    {
        private const string avatarsReimportedKey = "AVATARS_REIMPORTED";
        
        private const string exampleScenePath =
            "Packages/com.vrchat.avatars/Samples/Dynamics/Robot Avatar/Avatar Dynamics Robot Avatar.unity";

        static SDK3AImportFix()
        {
            // Skip if we've already checked for the canary file during this Editor Session
            if (!SessionState.GetBool(avatarsReimportedKey, false))
            {
                // Check for canary file in Library - package probably needs a reimport after a Library wipe
                string canaryFilePath = Path.Combine("Library", avatarsReimportedKey);
                if (File.Exists(canaryFilePath))
                {
                    SessionState.SetBool(avatarsReimportedKey, true);
                }
                else
                {
#pragma warning disable 4014
                    ReloadSDK();
#pragma warning restore 4014
                    File.WriteAllText(canaryFilePath, avatarsReimportedKey);
                }
            }
        }

        [MenuItem("VRChat SDK/Samples/Avatar Dynamics Robot Avatar")]
        private static void OpenAvatarsExampleScene()
        {
            if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(exampleScenePath);
            }
        }

        public static async Task ReloadSDK()
        {
            // Set session key to true, limiting the reload to one run per session
            SessionState.SetBool(avatarsReimportedKey, true);
            
            //Wait for project to finish compiling
            while (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                await Task.Delay(250);
            }
            
            ReloadUtil.ReloadSDK();
        }
    }
}