using System.IO;
using UnityEditor;
using UnityEngine;

namespace VRCSDK.SDK3.Editor
{
    [InitializeOnLoad]
    public class SDK3ImportFix
    {
        private const string packageRuntimePluginsFolder = "Packages/com.vrchat.worlds/Runtime/VRCSDK/Plugins";
        private const string SDK3_IMPORTS_FIXED = "SDK3ImportsFixed";

        static SDK3ImportFix()
        {
            // Only run once per project
            string key = Path.Combine(Application.dataPath, SDK3_IMPORTS_FIXED);
	        
            if (EditorPrefs.HasKey(key))
                return;

            EditorPrefs.SetBool(key, true);
            Run();
        }
        
        public static void Run(){
            AssetDatabase.ImportAsset($"{packageRuntimePluginsFolder}/VRCSDK3.dll", ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset($"{packageRuntimePluginsFolder}/VRCSDK3-Editor.dll", ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportPackage($"Packages/com.vrchat.worlds/Samples~/UdonExampleScene/SDK3.unitypackage", false);
        }
    }
}