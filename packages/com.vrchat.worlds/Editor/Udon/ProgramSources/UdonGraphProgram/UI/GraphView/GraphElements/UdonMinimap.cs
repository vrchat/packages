#if UNITY_2019_3_OR_NEWER
using UnityEditor.Experimental.GraphView;
#else
using UnityEditor.Experimental.UIElements.GraphView;
#endif
using UnityEngine;

namespace VRC.Udon.Editor.ProgramSources.UdonGraphProgram.UI.GraphView
{
    public class UdonMinimap : MiniMap, IUdonGraphElementDataProvider
    {
        private CustomData _customData = new CustomData();
        private UdonGraph _graph;

        public UdonMinimap(UdonGraph graph)
        {
            _graph = graph;

            name = "UdonMap";
            maxWidth = 200;
            maxHeight = 100;
            anchored = false;
            SetPosition(_customData.layout);
        }

        public void SetVisible(bool value)
        {
            visible = value;
            _customData.visible = value;
            _graph.SaveGraphElementData(this);
        }

        public override void UpdatePresenterPosition()
        {
            _customData.layout = GetPosition();
            _graph.SaveGraphElementData(this);
        }

        public UdonGraphElementData GetData()
        {
            return new UdonGraphElementData(UdonGraphElementType.Minimap, this.GetUid(), JsonUtility.ToJson(_customData));
        }

        internal void LoadData(UdonGraphElementData data)
        {
            JsonUtility.FromJsonOverwrite(data.jsonData, _customData);
            SetPosition(_customData.layout);
            visible = _customData.visible;
        }

        public class CustomData
        {
            public bool visible = true;
            public Rect layout = new Rect(new Vector2(10, 20), Vector2.zero);
        }
    }
}