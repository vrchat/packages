#if UNITY_2019_3_OR_NEWER
using EditorUI = UnityEditor.UIElements;
using UnityEngine.UIElements;
using UnityEditor.Experimental.GraphView;
using MenuAction = UnityEngine.UIElements.DropdownMenuAction;
#else
using EditorUI = UnityEditor.Experimental.UIElements;
using UnityEngine.Experimental.UIElements;
using UnityEditor.Experimental.UIElements.GraphView;
using MenuAction = UnityEngine.Experimental.UIElements.DropdownMenu.MenuAction;
#endif
using UnityEditor;
using UnityEngine;
using VRC.Udon.Graph;
using VRC.Udon.Serialization;

namespace VRC.Udon.Editor.ProgramSources.UdonGraphProgram.UI.GraphView
{
    public class UdonParameterField : BlackboardField
    {
        private UdonGraph udonGraph;
        private UdonNodeData nodeData;
        public UdonNodeData Data => nodeData;

        public UdonParameterField(UdonGraph udonGraph, UdonNodeData nodeData)
        {
            this.udonGraph = udonGraph;
            this.nodeData = nodeData;

            // Get Definition or exit early
            UdonNodeDefinition definition = UdonEditorManager.Instance.GetNodeDefinition(nodeData.fullName);
            if (definition == null)
            {
                Debug.LogWarning($"Couldn't create Parameter Field for {nodeData.fullName}");
                return;
            }

            this.text = (string) nodeData.nodeValues[(int) UdonParameterProperty.ValueIndices.name].Deserialize();
            this.typeText = UdonGraphExtensions.PrettyString(definition.name).FriendlyNameify();

            this.AddManipulator(new ContextualMenuManipulator(BuildContextualMenu));

            this.Q("icon").AddToClassList("parameter-" + definition.type);
            this.Q("icon").visible = true;

            var textField = (TextField) this.Q("textField");
            textField.isDelayed = true;
        }

        void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            evt.menu.AppendAction("Rename", (a) => OpenTextEditor(), MenuAction.AlwaysEnabled);
            evt.menu.AppendAction("Delete", (a) => udonGraph.RemoveNodeAndData(nodeData), MenuAction.AlwaysEnabled);

            evt.StopPropagation();
        }
    }
}