#if UNITY_2019_3_OR_NEWER
using UnityEngine.UIElements;
using EditorUI = UnityEditor.UIElements;
using EngineUI = UnityEngine.UIElements;
#else
using UnityEngine.Experimental.UIElements;
using EditorUI = UnityEditor.Experimental.UIElements;
using EngineUI = UnityEngine.Experimental.UIElements;
#endif
using System.Collections.Generic;
using UnityEngine;
using VRC.Udon.Graph;
using VRC.Udon.Serialization;

namespace VRC.Udon.Editor.ProgramSources.UdonGraphProgram.UI.GraphView
{
    public class UdonParameterProperty : VisualElement
	{
		protected UdonGraph graph;
		protected UdonNodeData nodeData;
		protected UdonNodeDefinition definition;

        //public ExposedParameter parameter { get; private set; }

		public Toggle isPublic { get; private set; }
		public Toggle isSynced { get; private set; }
		public VisualElement defaultValueContainer { get; private set; }
		public EditorUI.PopupField<string> syncField { get; private set; }
		private VisualElement _inputField;

        public enum ValueIndices
        {
            value = 0,
            name = 1,
            isPublic = 2,
            isSynced = 3,
            syncType = 4
        }

        private static SerializableObjectContainer[] GetDefaultNodeValues()
        {
            return new[]
            {
                SerializableObjectContainer.Serialize("", typeof(string)),
                SerializableObjectContainer.Serialize("newVariableName", typeof(string)),
                SerializableObjectContainer.Serialize(false, typeof(bool)),
                SerializableObjectContainer.Serialize(false, typeof(bool)),
                SerializableObjectContainer.Serialize("none", typeof(string))
            };
        }

        // 0 = Value, 1 = name, 2 = public, 3 = synced, 4 = syncType
        public UdonParameterProperty(UdonGraph graphView, UdonNodeDefinition definition, UdonNodeData nodeData)
        {
            this.graph = graphView;
            this.definition = definition;
            this.nodeData = nodeData;
            
            string friendlyName = UdonGraphExtensions.FriendlyTypeName(definition.type).FriendlyNameify();
            
            // Public Toggle
            isPublic = new Toggle
            {
                text = "public",
                value = (bool) GetValue(ValueIndices.isPublic)
            };
#if UNITY_2019_3_OR_NEWER
            isPublic.RegisterValueChangedCallback(
#else
            isPublic.OnValueChanged(
#endif
                e => { SetNewValue(e.newValue, ValueIndices.isPublic); });
            Add(isPublic);

            if(UdonNetworkTypes.CanSync(definition.type))
            {
                // Is Synced Field
                isSynced = new Toggle
                {
                    text = "synced",
                    value = (bool)GetValue(ValueIndices.isSynced),
                    name = "syncToggle",
                };
                
#if UNITY_2019_3_OR_NEWER
                isSynced.RegisterValueChangedCallback(
#else
                isSynced.OnValueChanged(
#endif
                    e =>
                    {
                        SetNewValue(e.newValue, ValueIndices.isSynced);
                        syncField.visible = e.newValue;
                    });
                
                // Sync Field, add to isSynced
                List<string> choices = new List<string>()
                {
                    "none"
                };

                if(UdonNetworkTypes.CanSyncLinear(definition.type))
                {
                    choices.Add("linear");
                }

                if(UdonNetworkTypes.CanSyncSmooth(definition.type))
                {
                    choices.Add("smooth");
                }

                syncField = new EditorUI.PopupField<string>(choices, 0)
                {
                    visible = isSynced.value,
                };
                syncField.Insert(0, new Label("smooth:"));

#if UNITY_2019_3_OR_NEWER
                syncField.RegisterValueChangedCallback(
#else
                syncField.OnValueChanged(
#endif
                    e =>
                    {
                        SetNewValue(e.newValue, ValueIndices.syncType);
                    });

                // Only show sync smoothing dropdown if there are choices to be made
                if (choices.Count > 1)
                {
                    isSynced.Add(syncField);
                }
                
                Add(isSynced);
            }
            else
            {
                // Cannot Sync
                SetNewValue(false, ValueIndices.isSynced);
                Add(new Label($"{friendlyName} cannot be synced."));
            }

            // Container to show/edit Default Value
            defaultValueContainer = new VisualElement();
            defaultValueContainer.Add(
                new Label("default value") {name = "default-value-label"});

            // Generate Default Value Field
            var value = TryGetValueObject(out object result);
            _inputField = UdonFieldFactory.CreateField(
                definition.type,
                result,
                newValue => SetNewValue(newValue, ValueIndices.value)
            );
            if (_inputField != null)
            {
                defaultValueContainer.Add(_inputField);
                Add(defaultValueContainer);
            }
        }

        private object GetValue(ValueIndices index)
        {
            if ((int) index >= nodeData.nodeValues.Length)
            {
                Debug.LogWarning($"Can't get {index} from {definition.name} variable");
                return null;
            }

            return nodeData.nodeValues[(int) index].Deserialize();
        }

        private bool TryGetValueObject(out object result)
        {
            result = null;

            var container = nodeData.nodeValues[0];
            if (container == null)
            {
                return false;
            }

            result = container.Deserialize();
            if (result == null)
            {
                return false;
            }

            return true;
        }

        private void SetNewValue(object newValue, ValueIndices index)
        {
            nodeData.nodeValues[(int) index] = SerializableObjectContainer.Serialize(newValue);
        }

        // Convenience wrapper for field types that don't need special initialization
        private VisualElement SetupField<TField, TType>()
            where TField : VisualElement, INotifyValueChanged<TType>, new()
        {
            var field = new TField();
            return SetupField<TField, TType>(field);
        }

        // Works for any TextValueField types, needs to know fieldType and object type
        private VisualElement SetupField<TField, TType>(TField field)
            where TField : VisualElement, INotifyValueChanged<TType>
        {
            field.AddToClassList("portField");
            if (TryGetValueObject(out object result))
            {
                field.value = (TType) result;
            }
#if UNITY_2019_3_OR_NEWER
            field.RegisterValueChangedCallback(
#else
			field.OnValueChanged(
#endif
                (e) => SetNewValue(e.newValue, ValueIndices.value));
            _inputField = field;
            return _inputField;
        }
    }
}
