using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.Core;
using VRC.SDK3.Components;
using System.IO;

namespace VRCSDK.SDK3.Editor
{
    [InitializeOnLoad]
    public class SDK3ImportFix
    {
        private const string packageRuntimePluginsFolder = "Packages/com.vrchat.worlds/Runtime/VRCSDK/Plugins";
        private const string legacyRuntimePluginsFolder = "Assets/VRCSDK/Plugins/";

        static SDK3ImportFix()
        {
            EditorSceneManager.sceneOpened += (scene, mode) => Check();
            Check();
        }
        
        private static void Check()
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