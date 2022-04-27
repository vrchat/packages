using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.Core;
using VRC.SDK3.Components;
using System.IO;
using UnityEditor.PackageManager.UI;

namespace VRCSDK.SDK3.Editor
{
    [InitializeOnLoad]
    public class SDK3ImportFix
    {
        private const string packageRuntimePluginsFolder = "Packages/com.vrchat.worlds/Runtime/VRCSDK/Plugins";
        private const string legacyRuntimePluginsFolder = "Assets/VRCSDK/Plugins/";

        private static readonly HashSet<string> _samplesToImport = new HashSet<string>()
        {
            "UdonExampleScene",
        };
        
        static SDK3ImportFix()
        {
            EditorSceneManager.sceneOpened += (scene, mode) => CheckForReload();
            CheckForReload();
            CheckForSampleImport();
        }

        private static void CheckForSampleImport()
        {
            // Get package info for this assembly
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(SDK3ImportFix).Assembly);
            
            var samples = Sample.FindByPackage(packageInfo.name, packageInfo.version);
            foreach (var sample in samples)
            {
                if (!sample.isImported && _samplesToImport.Contains(sample.displayName))
                {
                    if (sample.Import(Sample.ImportOptions.HideImportWindow |
                                      Sample.ImportOptions.OverridePreviousImports))
                    {
                        Debug.Log($"Automatically Imported the required sample {sample.displayName}");
                    }
                    else
                    {
                        Debug.LogWarning($"Could not Import required sample {sample.displayName}");
                    }
                }
            }
        }
        
        private static void CheckForReload()
        {
            var worldGameObject = Object.FindObjectOfType<PipelineManager>();
            if (worldGameObject != null)
            {
                var descriptor = Object.FindObjectOfType<VRCSceneDescriptor>();
                if (descriptor == null)
                {
                    Run();
                }
            }
        }
        
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