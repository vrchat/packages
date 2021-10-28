#if UNITY_2019_3_OR_NEWER
using UnityEngine.UIElements;
using UIElements = UnityEditor.UIElements;
#else
using UnityEngine.Experimental.UIElements;
using UIElements = UnityEditor.Experimental.UIElements;
#endif
using UnityEngine;

namespace VRC.Udon.Editor.ProgramSources.UdonGraphProgram.UI
{
    public class LayerMaskField : BaseField<LayerMask>
    {
        #if UNITY_2019_3_OR_NEWER
        public LayerMaskField() : base(null,null)
        #else
        public LayerMaskField()
        #endif
        {
            // Set up styling
            AddToClassList("UdonValueField");

            // Create LayerMask Editor and listen for changes
            UIElements.LayerMaskField field = new UIElements.LayerMaskField();
#if UNITY_2019_3_OR_NEWER
            field.RegisterValueChangedCallback(e =>
#else
            field.OnValueChanged(e =>
#endif
            {
                this.value = e.newValue;
            });

            Add(field);
        }
    }
}