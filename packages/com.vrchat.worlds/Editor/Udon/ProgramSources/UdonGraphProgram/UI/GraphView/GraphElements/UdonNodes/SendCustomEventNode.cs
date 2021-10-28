#if UNITY_2019_3_OR_NEWER
using EditorGV = UnityEditor.Experimental.GraphView;
using EngineUI = UnityEngine.UIElements;
using EditorUI = UnityEditor.UIElements;
#else
using EditorGV = UnityEditor.Experimental.UIElements.GraphView;
using EngineUI = UnityEngine.Experimental.UIElements;
using EditorUI = UnityEditor.Experimental.UIElements;
#endif
using VRC.Udon.Graph;

namespace VRC.Udon.Editor.ProgramSources.UdonGraphProgram.UI.GraphView.UdonNodes
{
    public class SendCustomEventNode : UdonNode
    {
        private EditorUI.PopupField<string> _eventNamePopup;

        public SendCustomEventNode(UdonNodeDefinition nodeDefinition, UdonGraph view, UdonNodeData nodeData = null) : base(nodeDefinition, view, nodeData)
        {
        }
        
        public override void Initialize()
        {
            base.Initialize();
            _eventNamePopup = this.GetProgramPopup(UdonNodeExtensions.ProgramPopupType.Events, _eventNamePopup);
        }
    }
}