#if VRC_SDK_VRCSDK3

using UnityEditor;

[CustomEditor (typeof(VRC.SDK3.Components.VRCSceneDescriptor))]
public class VRCSceneDescriptorEditor3 : Editor
{
    VRC.SDK3.Components.VRCSceneDescriptor sceneDescriptor;
    VRC.Core.PipelineManager pipelineManager;

    public override void OnInspectorGUI()
    {
        if(sceneDescriptor == null)
            sceneDescriptor = (VRC.SDK3.Components.VRCSceneDescriptor)target;

        if(pipelineManager == null)
        {
            pipelineManager = sceneDescriptor.GetComponent<VRC.Core.PipelineManager>();
            if(pipelineManager == null)
                sceneDescriptor.gameObject.AddComponent<VRC.Core.PipelineManager>();
        }

        DrawDefaultInspector();
    }
}

#endif