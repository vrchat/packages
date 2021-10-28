#if UNITY_2019_3_OR_NEWER
using UnityEngine.UIElements;
using UnityEditor.UIElements;
#else
using UnityEngine.Experimental.UIElements;
using UnityEditor.Experimental.UIElements;
#endif
using UnityEngine;

namespace VRC.Udon.Editor.ProgramSources.UdonGraphProgram.UI
{
    public class QuaternionField : BaseField<Quaternion>
    {
        #if UNITY_2019_3_OR_NEWER
        public QuaternionField() :base(null, null)
        #else
        public QuaternionField()
        #endif
        {
            // Set up styling
            AddToClassList("UdonValueField");
            
            // Create Vector4 Editor and listen for changes
            Vector4Field field = new Vector4Field();
            #if UNITY_2019_3_OR_NEWER
            field.RegisterValueChangedCallback(
            #else
            field.OnValueChanged(
            #endif
                e => 
                    value = new Quaternion(e.newValue.x, e.newValue.y, e.newValue.z, e.newValue.w)
                );
            Add(field);
        }

    }
}