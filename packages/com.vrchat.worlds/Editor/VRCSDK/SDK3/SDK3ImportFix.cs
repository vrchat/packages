using System.IO;
using UnityEditor;

namespace VRCSDK.SDK3.Editor
{
    [InitializeOnLoad]
    public class SDK3ImportFix
    {
        private const string packageRuntimePluginsFolder = "Packages/com.vrchat.worlds/Runtime/VRCSDK/Plugins";
        private const string legacyRuntimePluginsFolder = "Assets/VRCSDK/Plugins/";
        private const string reloadPluginsKey = "ReloadPlugins";

        static SDK3ImportFix()
        {
            var reloadsUntilRun = SessionState.GetInt(reloadPluginsKey, 0);
            if (reloadsUntilRun > -1)
            {
                reloadsUntilRun--;
                if (reloadsUntilRun == 0)
                {
                    Run();
                }
                SessionState.SetInt(reloadPluginsKey, reloadsUntilRun);
            }
        }
        
        [MenuItem("VRChat SDK/Reload Plugins")]
        public static void Run()
        {
            if (Directory.Exists(packageRuntimePluginsFolder))
            {
                AssetDatabase.ImportAsset($"{packageRuntimePluginsFolder}/VRCSDK3.dll", ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ImportAsset($"{packageRuntimePluginsFolder}/VRCSDK3-Editor.dll", ImportAssetOptions.ForceSynchronousImport);
            }
            else if (Directory.Exists(legacyRuntimePluginsFolder))
            {
                AssetDatabase.ImportAsset($"{legacyRuntimePluginsFolder}/VRCSDK3.dll", ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ImportAsset($"{legacyRuntimePluginsFolder}/VRCSDK3-Editor.dll", ImportAssetOptions.ForceSynchronousImport);
            }
        }
    }
}