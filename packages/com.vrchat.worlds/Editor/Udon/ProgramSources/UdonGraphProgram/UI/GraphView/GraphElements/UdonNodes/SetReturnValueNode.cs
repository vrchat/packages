#if UNITY_2019_3_OR_NEWER
using UnityEditor.Experimental.GraphView;
using EditorGV = UnityEditor.Experimental.GraphView;
using EngineUI = UnityEngine.UIElements;
using EditorUI = UnityEditor.UIElements;
using UnityEngine.UIElements;
#else
using EditorGV = UnityEditor.Experimental.UIElements.GraphView;
using EngineUI = UnityEngine.Experimental.UIElements;
using EditorUI = UnityEditor.Experimental.UIElements;
#endif
using System.Linq;
using UnityEngine;
using VRC.Udon.Graph;

namespace VRC.Udon.Editor.ProgramSources.UdonGraphProgram.UI.GraphView.UdonNodes
{
    public class SetReturnValueNode : UdonNode
    {
        public SetReturnValueNode(UdonNodeDefinition nodeDefinition, UdonGraph view, UdonNodeData nodeData = null) : base(nodeDefinition, view, nodeData)
        {
        }

        public override void Initialize()
        {
            base.Initialize();
            
            string returnVariable = UdonBehaviour.ReturnVariableName;
            string uuid = null;

            if (!_graphView.GetVariableNames.Contains(returnVariable))
                uuid = _graphView.AddNewVariable("Variable_SystemObject", returnVariable, false);
            else 
                uuid = _graphView.GetVariableNodes.FirstOrDefault(n => (string)n.nodeValues[1].Deserialize() == returnVariable)?.uid;

            if (!string.IsNullOrWhiteSpace(uuid))
                SetNewValue(uuid, 0);
            else
                Debug.LogError("Could not find return value name!");
        }
    }
}