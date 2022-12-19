using System.Collections.Generic;
using UnityEngine;
using VRC.SDKBase.Validation;

namespace VRC.SDK3.Validation
{
    public static class AvatarValidation
    {
        private static string[] CombinedComponentTypeWhiteList = null;

        private static HashSet<System.Type> GetComponentWhitelist()
        {
            if(CombinedComponentTypeWhiteList == null)
            {
                List<string> concatenation = new List<string>(VRC.SDKBase.Validation.AvatarValidation.ComponentTypeWhiteListCommon);
                concatenation.AddRange(VRC.SDKBase.Validation.AvatarValidation.ComponentTypeWhiteListSdk3);
                CombinedComponentTypeWhiteList = concatenation.ToArray();
            }
            return ValidationUtils.WhitelistedTypes("avatar-sdk3", CombinedComponentTypeWhiteList);
        }
        public static IEnumerable<Component> FindIllegalComponents(GameObject target)
        {
            return ValidationUtils.FindIllegalComponents(target, GetComponentWhitelist());
        }
        public static IEnumerable<Shader> FindIllegalShaders(GameObject target)
        {
            return ShaderValidation.FindIllegalShaders(target, VRC.SDKBase.Validation.AvatarValidation.ShaderWhiteList);
        }
    }
}
