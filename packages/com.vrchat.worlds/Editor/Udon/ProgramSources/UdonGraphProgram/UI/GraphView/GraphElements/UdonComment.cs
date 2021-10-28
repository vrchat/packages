#if UNITY_2019_3_OR_NEWER
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using UnityEngine.UIElements.StyleSheets;
#else
using UnityEditor.Experimental.UIElements.GraphView;
using UnityEngine.Experimental.UIElements;
using UnityEngine.Experimental.UIElements.StyleSheets;
#endif
using System;
using UnityEditor;
using UnityEngine;

namespace VRC.Udon.Editor.ProgramSources.UdonGraphProgram.UI.GraphView
{
    public class UdonComment : UdonGraphElement, IUdonGraphElementDataProvider
    {
        private VisualElement _mainContainer;
        private Label _label;
        private TextField _textField;
        private CustomData _customData = new CustomData();
        private UdonGraph _graph;
        public UdonGroup group;

        // Called from Context menu and Reload
        public static UdonComment Create(string value, Rect position, UdonGraph graph)
        {
            var comment = new UdonComment("", graph);

            // make sure rect size is not 0
            position.width = position.width > 0 ? position.width : 128;
            position.height = position.height > 0 ? position.height : 40;

            comment._customData.layout = position;
            comment._customData.title = value;
            
            comment.UpdateFromData();
            graph.MarkSceneDirty();

            return comment;
        }

        public static UdonComment Create(UdonGraphElementData elementData, UdonGraph graph)
        {
            var comment = new UdonComment(elementData.jsonData, graph);
            
            comment.UpdateFromData();
            graph.MarkSceneDirty();
            
            return comment;
        }

        private UdonComment(string jsonData, UdonGraph graph)
        {
            title = "Comment";
            name = "comment";
            _graph = graph;

            capabilities |= Capabilities.Selectable | Capabilities.Movable | Capabilities.Deletable |
                            Capabilities.Resizable;
            pickingMode = PickingMode.Ignore;

            type = UdonGraphElementType.UdonComment;

            if(!string.IsNullOrEmpty(jsonData))
            {
                EditorJsonUtility.FromJsonOverwrite(jsonData, _customData);
            }

            _mainContainer = new VisualElement();
            _mainContainer.StretchToParentSize();
            _mainContainer.AddToClassList("mainContainer");
            Add(_mainContainer);

            _label = new Label();
            _label.RegisterCallback<MouseDownEvent>(OnLabelClick);
            _mainContainer.Add(_label);

            _textField = new TextField(1000, true, false, '*');
            _textField.isDelayed = true;
            
            // Support IME
            _textField.RegisterCallback<FocusInEvent>(evt =>{ Input.imeCompositionMode = IMECompositionMode.On;});
            _textField.RegisterCallback<FocusOutEvent>(evt =>
            {
                SetText(_textField.text);
                Input.imeCompositionMode = IMECompositionMode.Auto;
                SwitchToEditMode(false);
            });

#if UNITY_2019_3_OR_NEWER
            _textField.RegisterValueChangedCallback((evt) =>
#else
            _textField.OnValueChanged((evt) =>
#endif
            {
                SetText(evt.newValue);
                SwitchToEditMode(false);
            });
        }
        
        private void SaveNewData()
        {
            _graph.SaveGraphElementData(this);
        }

        private void UpdateFromData()
        {
            if(_customData != null)
            {
                layer = _customData.layer;
                if(string.IsNullOrEmpty(_customData.uid))
                {
                    _customData.uid = Guid.NewGuid().ToString();
                }

                uid = _customData.uid;

                SetPosition(_customData.layout);
                SetText(_customData.title);
            }
        }
#if UNITY_2019_3_OR_NEWER
        protected override void OnCustomStyleResolved(ICustomStyle style)
        {
            base.OnCustomStyleResolved(style);
#else
        protected override void OnStyleResolved(ICustomStyle style)
        {
            base.OnStyleResolved(style);
#endif
            // Something is forcing style! Resetting a few things here, grrr.

            this.style.borderBottomWidth = 1;
            
            var resizer = this.Q(null, "resizer");
            if(resizer != null)
            {
                resizer.style.paddingTop = 0;
                resizer.style.paddingLeft = 0;
            }
        }

        public override void SetPosition(Rect newPos)
        {
            newPos = GraphElementExtension.GetSnappedRect(newPos);
            base.SetPosition(newPos);
        }

        public override void UpdatePresenterPosition()
        {
            base.UpdatePresenterPosition();
            _customData.layout = GraphElementExtension.GetSnappedRect(GetPosition());
            SaveNewData();
            if (group != null)
            {
                group.SaveNewData();
            }
        }

        private double lastClickTime;
        private const double doubleClickSpeed = 0.5;

        private void OnLabelClick(MouseDownEvent evt)
        {
            var newTime = EditorApplication.timeSinceStartup;
            if(newTime - lastClickTime < doubleClickSpeed)
            {
                SwitchToEditMode(true);
            }

            lastClickTime = newTime;
        }

        private void SwitchToEditMode(bool switchingToEdit)
        {
            if (switchingToEdit)
            {
                _mainContainer.Remove(_label);
                _textField.value = _label.text;
                _mainContainer.Add(_textField);
                _textField.delegatesFocus = true;
                _textField.Focus();
            }
            else
            {
                _mainContainer.Remove(_textField);
                _mainContainer.Add(_label);
            }

            MarkDirtyRepaint();
        }

        public void SetText(string value)
        {
            Undo.RecordObject(_graph.graphProgramAsset, "Rename Comment");
            value = value.TrimEnd();
            _customData.title = value;
            _label.text = value;
            SaveNewData();
            MarkDirtyRepaint();
        }
        
        public UdonGraphElementData GetData()
        {
            return new UdonGraphElementData(UdonGraphElementType.UdonComment, uid,
                EditorJsonUtility.ToJson(_customData));
        }

        public class CustomData
        {
            public string uid;
            public Rect layout;
            public string title = "Comment";
            public int layer;
            public Color elementTypeColor;
        }
    }
}
