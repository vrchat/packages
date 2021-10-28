#if UNITY_2019_3_OR_NEWER
using UnityEngine.UIElements;
#else
using UnityEngine.Experimental.UIElements;
#endif

namespace VRC.Udon.Editor.ProgramSources.UdonGraphProgram.UI
{
    public class CharField : BaseField<char>
    {
#if UNITY_2019_3_OR_NEWER
        public CharField():base(null,null)
#else
        public CharField():base()
#endif
        {
            // Set up styling
            AddToClassList("UdonValueField");
            
            // Create Char Editor and listen for changes
            TextField field = new TextField();
            field.maxLength = 1;
#if UNITY_2019_3_OR_NEWER
            field.RegisterValueChangedCallback(
#else
            field.OnValueChanged(
#endif
                e =>
                    value = e.newValue.ToCharArray()[0]);

            Add(field);
        }
    }
}