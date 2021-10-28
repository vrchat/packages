#if UNITY_2019_3_OR_NEWER
using UnityEngine.UIElements;
#else
using UnityEngine.Experimental.UIElements;
#endif
using System;
using VRC.SDKBase;

namespace VRC.Udon.Editor.ProgramSources.UdonGraphProgram.UI
{
    public class VRCUrlField : BaseField<VRCUrl>
    {
#if UNITY_2019_3_OR_NEWER
        public VRCUrlField():base(null,null)
#else
        public VRCUrlField():base()
#endif
        {
            // Set up styling
            AddToClassList("UdonValueField");
            
            // Create Text Editor and listen for changes
            TextField field = new TextField(50, false, false, Char.MinValue);
#if UNITY_2019_3_OR_NEWER
            field.RegisterValueChangedCallback(
#else
            field.OnValueChanged(
#endif
                e => 
                    value = new VRCUrl(e.newValue)
            );
            Add(field);
        }
    }
}