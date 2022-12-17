using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.Core;
using VRC.SDK3.Components;
using System.IO;
using VRC.SDK3.Video.Components.Base;
using Object = UnityEngine.Object;

namespace VRC.SDK3.Editor
{
    [InitializeOnLoad]
    public class SDK3ImportFix
    {
        // variables for finding missing objects
        static int missing_count;
        
        private const string packageRuntimePluginsFolder = "Packages/com.vrchat.worlds/Runtime/VRCSDK/Plugins";
        private const string baseRuntimePluginsFolder = "Packages/com.vrchat.base/Runtime/VRCSDK/Dependencies/Managed";
        private const string legacyRuntimePluginsFolder = "Assets/VRCSDK/Plugins/";
        private const string needsReloadCheck = "NeedsReloadCheck";

        static SDK3ImportFix()
        {
            EditorSceneManager.sceneOpened += (scene, mode) => CheckForReload();
            if (SessionState.GetBool(needsReloadCheck, true))
            {
                // Only do this once per session
                SessionState.SetBool(needsReloadCheck, false);
                
                CheckForReload();
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
            
            var videoPlayers = FindObjectsOfTypeAll<BaseVRCVideoPlayer>();
            if (videoPlayers.Count > 0)
            {
                foreach (var v in videoPlayers)
                {
                    FindInGO(v.gameObject);
                }
            }
            
            if (missing_count > 0)
            {
                Run();
            }
        }
        
        public static void Run()
        {
            if (Directory.Exists(packageRuntimePluginsFolder))
            {
                AssetDatabase.ImportAsset($"{packageRuntimePluginsFolder}/VRCSDK3.dll", ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ImportAsset($"{packageRuntimePluginsFolder}/VRCSDK3-Editor.dll", ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ImportAsset($"{baseRuntimePluginsFolder}/VRC.Collections.dll", ImportAssetOptions.ForceSynchronousImport);
            }
            else if (Directory.Exists(legacyRuntimePluginsFolder))
            {
                AssetDatabase.ImportAsset($"{legacyRuntimePluginsFolder}/VRCSDK3.dll", ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ImportAsset($"{legacyRuntimePluginsFolder}/VRCSDK3-Editor.dll", ImportAssetOptions.ForceSynchronousImport);
            }
        }
        
        private static void FindInGO(GameObject g)
        {
            Component[] components = g.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null)
                {
                    missing_count++;
                    string s = g.name;
                    Transform t = g.transform;
                    while (t.parent != null) 
                    {
                        var parent = t.parent;
                        s = parent.name +"/"+s;
                        t = parent;
                    }
                }
            }
            // Now recurse through each child GO (if there are any):
            foreach (Transform childT in g.transform)
            {
                FindInGO(childT.gameObject);
            }
        }
    
        /// Use this method to get all loaded objects of some type, including inactive objects. 
        /// This is an alternative to Resources.FindObjectsOfTypeAll (returns project assets, including prefabs), and GameObject.FindObjectsOfTypeAll (deprecated).
        public static List<T> FindObjectsOfTypeAll<T>()
        {
            List<T> results = new List<T>();
            for(int i = 0; i< EditorSceneManager.sceneCount; i++)
            {
                var s = EditorSceneManager.GetSceneAt(i);
                if (s.isLoaded)
                {
                    var allGameObjects = s.GetRootGameObjects();
                    for (int j = 0; j < allGameObjects.Length; j++)
                    {
                        var go = allGameObjects[j];
                        results.AddRange(go.GetComponentsInChildren<T>(true));
                    }
                }
            }
            return results;
        }
    }
}