using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VRC.Udon.Editor;
using VRC.Udon.Editor.ProgramSources.UdonGraphProgram;
using VRC.Udon.Graph;
using UINew = VRC.Udon.Editor.ProgramSources.UdonGraphProgram.UI.GraphView;

namespace Tests
{
    public class UICompilerTests
    {
        [Test]
        public void CompareAssemblies()
        {
            // Cache Udon Graph View window for reuse
            var graphViewWindow = EditorWindow.GetWindow<UINew.UdonGraphWindow>();

            // Loop through every asset in project
            var assets = AssetDatabase.FindAssets("t:UdonGraphProgramAsset");
            foreach (string guid in assets)
            {
                // Compile assembly from copy of existing asset
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var legacyData = GetDataFromAssetAtPath(path);
                var legacyAssembly = UdonEditorManager.Instance.CompileGraph(legacyData, null, out Dictionary<string, (string uid, string fullName, int index)> _, out Dictionary<string, (object value, Type type)> heapDefaultValues);

                // Compile assembly from copy of asset loaded into new graph
                var newAsset = ScriptableObject.CreateInstance<UdonGraphProgramAsset>();
                newAsset.graphData = new UdonGraphData(legacyData);
                // This function loads the asset and reserializes it
                var newData = graphViewWindow.GetGraphDataFromAsset(newAsset);
                var newAssembly = UdonEditorManager.Instance.CompileGraph(newData, null, out Dictionary<string, (string uid, string fullName, int index)> _, out Dictionary<string, (object value, Type type)> heapDefaultValues1);

                Assert.AreEqual(newAssembly, legacyAssembly);
            }
            graphViewWindow.Close();
        }

        public UdonGraphData GetDataFromAssetAtPath(string path)
        {
            var targetAsset = AssetDatabase.LoadAssetAtPath<UdonGraphProgramAsset>(path);
            return new UdonGraphData(targetAsset.graphData);

        }
    }
}
