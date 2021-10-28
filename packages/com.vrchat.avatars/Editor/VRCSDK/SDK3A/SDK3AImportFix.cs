using System.IO;
using UnityEditor;
using UnityEngine;

namespace VRCSDK.SDK3.Editor
{
    [InitializeOnLoad]
    public class SDK3AImportFix
    {
        private const string packageRuntimePluginsFolder = "Packages/com.vrchat.avatars/Runtime/VRCSDK/Plugins";
        private const string SDK3_IMPORTS_FIXED = "SDK3AImportsFixed";
        
        static SDK3AImportFix()
        {
            // Only run once per project
            string key = Path.Combine(Application.dataPath, SDK3_IMPORTS_FIXED);
	        
            if (EditorPrefs.HasKey(key))
                return;

            EditorPrefs.SetBool(key, true);
            Run();
        }
        
        public static void Run(){
            AssetDatabase.ImportAsset($"{packageRuntimePluginsFolder}/VRCSDK3A.dll", ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset($"{packageRuntimePluginsFolder}/VRCSDK3A-Editor.dll", ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportPackage($"Packages/com.vrchat.avatars/Samples~/AV3 Demo Assets/SDK3A.unitypackage", false);
        }
    }
}