using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEngine;

namespace VRC.SDK3A.Editor
{
    [InitializeOnLoad]
    public class SDK3AImportFix
    {
        private const string packageRuntimePluginsFolder = "Packages/com.vrchat.avatars/Runtime/VRCSDK/Plugins";
        private const string legacyRuntimePluginsFolder = "Assets/VRCSDK/Plugins/";
        private const string reloadPluginsKey = "ReloadPlugins";
        
        private static readonly HashSet<string> _samplesToImport = new HashSet<string>()
        {
            "AV3 Demo Assets",
            "Robot Avatar"
        };
        
        static SDK3AImportFix()
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
            CheckForSampleImport();
        }
        
        private static void CheckForSampleImport()
        {
#if VRCUPM
            // Get package info for this assembly
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(SDK3AImportFix).Assembly);

            // Exit early if package cannot be found
            if (packageInfo == null)
            {
                return;
            }
            
            // Check if samples have ever been imported, exit if they have
            var settings = VRCPackageSettings.Create();
            if (settings.samplesImported)
            {
                return;
            }
            
            var samples = Sample.FindByPackage(packageInfo.name, packageInfo.version);
            foreach (var sample in samples)
            {
                if (!sample.isImported && _samplesToImport.Contains(sample.displayName))
                {
                    if (sample.Import(Sample.ImportOptions.HideImportWindow |
                                      Sample.ImportOptions.OverridePreviousImports))
                    {
                        Debug.Log($"Automatically Imported the required sample {sample.displayName}");
                        settings.samplesImported = true;
                        settings.Save();
                    }
                    else
                    {
                        Debug.LogWarning($"Could not Import required sample {sample.displayName}");
                    }
                }
            }
#endif    
        }
        
        public static void Run(){
            if (Directory.Exists(packageRuntimePluginsFolder))
            {
                AssetDatabase.ImportAsset($"{packageRuntimePluginsFolder}/VRCSDK3A.dll",
                    ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ImportAsset($"{packageRuntimePluginsFolder}/VRCSDK3A-Editor.dll",
                    ImportAssetOptions.ForceSynchronousImport);
            }
            else if (Directory.Exists(legacyRuntimePluginsFolder))
            {
                AssetDatabase.ImportAsset($"{legacyRuntimePluginsFolder}/VRCSDK3A.dll",
                    ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ImportAsset($"{legacyRuntimePluginsFolder}/VRCSDK3A-Editor.dll",
                    ImportAssetOptions.ForceSynchronousImport);
            }
        }
    }
}