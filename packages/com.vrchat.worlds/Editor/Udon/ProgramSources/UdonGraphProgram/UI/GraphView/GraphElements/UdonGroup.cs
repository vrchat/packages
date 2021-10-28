#if UNITY_2019_3_OR_NEWER
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
#else
using UnityEditor.Experimental.UIElements.GraphView;
using UnityEngine.Experimental.UIElements;
#endif
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VRC.Udon.Editor.ProgramSources.UdonGraphProgram.UI.GraphView
{
    public class UdonGroup : Group, IUdonGraphElementDataProvider
    {
        public string uid { get => _uid; set => _uid = value; }
        private CustomData _customData = new CustomData();
        private UdonGraph _graph;
        private string _uid;
        private const int GROUP_LAYER = -1;

        public static UdonGroup Create(string value, Rect position, UdonGraph graph)
        {
            var group = new UdonGroup("", graph);

            group.uid = Guid.NewGuid().ToString();

            // make sure rect size is not 0
            position.width = position.width > 0 ? position.width : 128;
            position.height = position.height > 0 ? position.height : 128;

            group._customData.uid = group.uid;
            group._customData.layout = position;
            group._customData.title = value;

            return group;
        }

        // Called in Reload > RestoreElementFromData
        public static UdonGroup Create(UdonGraphElementData elementData, UdonGraph graph)
        {
            return new UdonGroup(elementData.jsonData, graph);
        }

        // current order of operations issue when creating a group from the context menu means this isn't set until first save. This allows us to force it.
        public void UpdateDataId()
        {
            _customData.uid = uid;
        }

        // Build a Group from jsonData, save to userData
        private UdonGroup(string jsonData, UdonGraph graph)
        {
            title = "Group";
            _graph = graph;
            layer = GROUP_LAYER;

            if (!string.IsNullOrEmpty(jsonData))
            {
                EditorJsonUtility.FromJsonOverwrite(jsonData, _customData);
            }
            
            // listen for changes on child elements
            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            _customData.layout = GraphElementExtension.GetSnappedRect(GetPosition());
        }

        public void Initialize()
        {
            if (_customData != null)
            {
                // Propagate data to useful places
                title = _customData.title;
                layer = _customData.layer;
                if (string.IsNullOrEmpty(_customData.uid))
                {
                    _customData.uid = Guid.NewGuid().ToString();
                }

                uid = _customData.uid;

                // Add all elements from graph to self
                var childUIDs = _customData.containedElements;
                if (childUIDs.Count > 0)
                {
                    foreach (var item in childUIDs)
                    {
                        GraphElement element = _graph.GetElementByGuid(item);
                        if (element != null)
                        {
                            if (ContainsElement(element)) continue;
                            AddElement(element);
                            if (element is UdonComment c)
                            {
                                c.group = this;
                            }
                            else if (element is UdonNode n)
                            {
                                n.group = this;
                            }
                        }
                    }
                }
                else
                {
                    // No children, so restore the saved position
                    SetPosition(_customData.layout);
                }
            }
        }

        public override void SetPosition(Rect newPos)
        {
            newPos = GraphElementExtension.GetSnappedRect(newPos);
            base.SetPosition(newPos);
        }

        public void SaveNewData()
        {
            _graph.SaveGraphElementData(this);
        }

        // Save data to asset after new position set
        public override void UpdatePresenterPosition()
        {
            base.UpdatePresenterPosition();
            Rect layout = GraphElementExtension.GetSnappedRect(GetPosition());
            base.SetPosition(layout);
            SaveNewData();
        }

        // Save data to asset after rename
        protected override void OnGroupRenamed(string oldName, string newName)
        {
            // limit name to 100 characters
            title = newName.Substring(0, Mathf.Min(newName.Length, 100));
            _customData.title = title;
            SaveNewData();
        }

        protected override void OnElementsAdded(IEnumerable<GraphElement> elements)
        {
            base.OnElementsAdded(elements);
            foreach (var element in elements)
            {
                if (!_customData.containedElements.Contains(element.GetUid()))
                {
                    _customData.containedElements.Add(element.GetUid());
                }
            
                // Set group variable on UdonNodes
                if (element is UdonNode)
                {
                    (element as UdonNode).group = this;
                    element.BringToFront();
                }
                
                if (element is UdonComment)
                {
                    (element as UdonComment).group = this;
                }
            }
            SaveNewData();
        }

        protected override void OnElementsRemoved(IEnumerable<GraphElement> elements)
        {
            base.OnElementsRemoved(elements);
            foreach (var element in elements)
            {
                if (_customData.containedElements.Contains(element.GetUid()))
                {
                    _customData.containedElements.Remove(element.GetUid());
                    if (element is UdonNode)
                    {
                        (element as UdonNode).group = null;
                    }
                    else if (element is UdonComment)
                    {
                        (element as UdonComment).group = null;
                    }
                }
            }

            SaveNewData();
        }

        public override bool AcceptsElement(GraphElement element, ref string reasonWhyNotAccepted)
        {
            return base.AcceptsElement(element, ref reasonWhyNotAccepted);
        }

        public UdonGraphElementData GetData()
        {
            return new UdonGraphElementData(UdonGraphElementType.UdonGroup, uid, EditorJsonUtility.ToJson(_customData));
        }

        public class CustomData
        {
            public string uid;
            public Rect layout;
            public List<string> containedElements = new List<string>();
            public string title;
            public int layer;
            public Color elementTypeColor;
        }
    }
}