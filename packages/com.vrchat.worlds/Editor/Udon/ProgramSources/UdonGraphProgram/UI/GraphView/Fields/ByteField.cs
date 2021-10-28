using System;
#if UNITY_2019_3_OR_NEWER
using UnityEngine.UIElements;
#else
using UnityEngine.Experimental.UIElements;
#endif

namespace VRC.Udon.Editor.ProgramSources.UdonGraphProgram.UI
{
    public class ByteField : BaseField<byte>
    {
#if UNITY_2019_3_OR_NEWER
        public ByteField():base(null,null)
#else
        public ByteField():base()
#endif
        {
            // Set up styling
            AddToClassList("UdonValueField");
            
            // Create Char Editor and listen for changes
            TextField field = new TextField();
#if UNITY_2019_3_OR_NEWER
            field.RegisterValueChangedCallback(
#else
            field.OnValueChanged(
#endif
                e =>
                    value = Convert.ToByte(e.newValue));

            Add(field);
        }
    }
}