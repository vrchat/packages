namespace VRC.SDK3A.Editor
{
    public class VRCPackageSettings : SDKBase.Editor.VRCPackageSettings
    {
        // Need to implement this Factory method to properly load this extension class
        public new static VRCPackageSettings Create()
        {
            var result = new VRCPackageSettings();
            result.Load();
            return result;
        }
    }
}