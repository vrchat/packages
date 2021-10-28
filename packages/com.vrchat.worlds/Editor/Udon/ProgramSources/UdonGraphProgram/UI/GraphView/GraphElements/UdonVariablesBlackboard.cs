#if UNITY_2019_3_OR_NEWER
using UnityEngine.UIElements;
using UnityEditor.Experimental.GraphView;
#else
using UnityEngine.Experimental.UIElements;
using UnityEditor.Experimental.UIElements.GraphView;
#endif
using System.Collections.Generic;
using UnityEngine;
using VRC.Udon.Graph;

namespace VRC.Udon.Editor.ProgramSources.UdonGraphProgram.UI.GraphView
{
    public class UdonVariablesBlackboard : Blackboard, IUdonGraphElementDataProvider
    {
        private CustomData _customData = new CustomData();
        private UdonGraph _graph;
        private Dictionary<string, BlackboardRow> _idToRow;

        public UdonVariablesBlackboard(UdonGraph graph)
        {
            _graph = graph;
            title = "Variables";
            name = "Parameters";
            scrollable = true;

            // Remove subtitle
            var subtitle = this.Query<Label>("subTitleLabel").AtIndex(0);
            if (subtitle != null)
            {
                subtitle.RemoveFromHierarchy();
            }

            // Improve resizer UI
            style.borderBottomWidth = 1;

            var resizer = this.Q(null, "resizer");
            if (resizer != null)
            {
                resizer.style.paddingTop = 0;
                resizer.style.paddingLeft = 0;
            }

            SetPosition(_customData.layout);

            _idToRow = new Dictionary<string, BlackboardRow>();
        }

        public new void Clear()
        {
            _idToRow.Clear();
            base.Clear();
        }

        public void AddFromData(UdonNodeData nodeData)
        {
            // don't add internal variables, which start with __
            // Todo: handle all "__" variables instead, need to tell community first and let the word spread
            string newVariableName = (string)nodeData.nodeValues[(int)UdonParameterProperty.ValueIndices.name].Deserialize();
            if (newVariableName.StartsWithCached("__returnValue"))
            {
                return;
            }
            
            UdonNodeDefinition definition = UdonEditorManager.Instance.GetNodeDefinition(nodeData.fullName);
            if (definition != null)
            {
                BlackboardRow row = new BlackboardRow(new UdonParameterField(_graph, nodeData),
                    new UdonParameterProperty(_graph, definition, nodeData));
                contentContainer.Add(row);
                _idToRow.Add(nodeData.uid, row);
            }
            this.Reload();
        }

        public void RemoveByID(string id)
        {
            if (_idToRow.TryGetValue(id, out BlackboardRow row))
            {
                Remove(row);
                _idToRow.Remove(id);
            }
        }
        
        public void SetVisible(bool value)
        {
            visible = value;
            _customData.visible = value;
            SaveData();
        }

        public override void UpdatePresenterPosition()
        {
            _customData.layout = GetPosition();
            SaveData();
        }

        private void SaveData()
        {
            _graph.SaveGraphElementData(this);
        }

        public UdonGraphElementData GetData()
        {
            return new UdonGraphElementData(UdonGraphElementType.VariablesWindow, this.GetUid(), JsonUtility.ToJson(_customData));
        }

        public class CustomData {
            public bool visible = true;
            public Rect layout = new Rect(10, 130, 200, 150);
        }

        internal void LoadData(UdonGraphElementData data)
        {
            _idToRow = new Dictionary<string, BlackboardRow>();
            JsonUtility.FromJsonOverwrite(data.jsonData, _customData);
            SetPosition(_customData.layout);
            this.visible = _customData.visible;
        }
    }

}