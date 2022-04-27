using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.PhysBone;
using VRC.Dynamics;
using VRC.SDKBase.Validation;

namespace VRC.SDK3.Avatars
{
    public static class AvatarDynamicsSetup
    {
        [RuntimeInitializeOnLoadMethod]
        private static void RuntimeInit()
        {
            //Triggers Manager
            if (ContactManager.Inst == null)
            {
                var obj = new GameObject("TriggerManager");
                UnityEngine.Object.DontDestroyOnLoad(obj);
                ContactManager.Inst = obj.AddComponent<ContactManager>();
            }

            //Triggers
            ContactBase.OnInitialize = Trigger_OnInitialize;

            //PhysBone Manager
            if (PhysBoneManager.Inst == null)
            {
                var obj = new GameObject("PhysBoneManager");
                UnityEngine.Object.DontDestroyOnLoad(obj);

                PhysBoneManager.Inst = obj.AddComponent<PhysBoneManager>();
                PhysBoneManager.Inst.IsSDK = true;
                PhysBoneManager.Inst.Init();
                obj.AddComponent<PhysBoneGrabHelper>();
            }
            VRCPhysBoneBase.OnInitialize = PhysBone_OnInitialize;
        }
        private static bool Trigger_OnInitialize(ContactBase trigger)
        {
            var receiver = trigger as ContactReceiver;
            if (receiver != null && !string.IsNullOrWhiteSpace(receiver.parameter))
            {
                var avatarDesc = receiver.GetComponentInParent<VRCAvatarDescriptor>();
                if (avatarDesc != null)
                {
                    var animator = avatarDesc.GetComponent<Animator>();
                    if (animator != null)
                    {
                        // called from SDK, so create SDK Param access
                        receiver.paramAccess = new AnimParameterAccessAvatarSDK(animator, receiver.parameter);
                    }
                }
            }

            return true;
        }
        private static void PhysBone_OnInitialize(VRCPhysBoneBase physBone)
        {
            if (!string.IsNullOrEmpty(physBone.parameter))
            {
                var avatarDesc = physBone.GetComponentInParent<VRCAvatarDescriptor>();
                if (avatarDesc != null)
                {
                    var animator = avatarDesc.GetComponent<Animator>();
                    if (animator != null)
                    {
                        physBone.param_IsGrabbed = new AnimParameterAccessAvatarSDK(animator, physBone.parameter + VRCPhysBoneBase.PARAM_ISGRABBED);
                        physBone.param_Angle = new AnimParameterAccessAvatarSDK(animator, physBone.parameter + VRCPhysBoneBase.PARAM_ANGLE);
                        physBone.param_Stretch = new AnimParameterAccessAvatarSDK(animator, physBone.parameter + VRCPhysBoneBase.PARAM_STRETCH);
                    }
                }
            }
        }

        [MenuItem("VRChat SDK/Utilities/Convert DynamicBones To PhysBones")]
        public static void ConvertSelectedToPhysBones()
        {
            List<GameObject> avatarObjs = new List<GameObject>();
            foreach (var obj in Selection.objects)
            {
                var gameObj = obj as GameObject;
                if (gameObj == null)
                    continue;

                var descriptor = gameObj.GetComponent<VRCAvatarDescriptor>();
                if (descriptor != null)
                {
                    avatarObjs.Add(gameObj);
                }
            }
            if (avatarObjs.Count == 0)
            {
                EditorUtility.DisplayDialog("Warning", "No avatars found.  Please select an avatar in the hierarchy window before using this feature.", "Okay");
            }
            else
            {
                ConvertDynamicBonesToPhysBones(avatarObjs);
            }
        }
        public static void ConvertDynamicBonesToPhysBones(IEnumerable<GameObject> avatarObjs)
        {
            if (!EditorUtility.DisplayDialog("Warning", "This operation will remove all DynamicBone components and replace them with PhysBone components on your avatar. This process attempts to match settings but the result may not appear to be the same. This is not reversible so please make a backup before continuing!", "Proceed", "Cancel"))
                return;

            foreach(var obj in avatarObjs)
                ConvertToPhysBones(obj);
        }
        static void ConvertToPhysBones(GameObject avatarObj)
        {
            try
            {
                //Find types
                var TypeDynamicBone = ValidationUtils.GetTypeFromName("DynamicBone");
                var TypeDynamicBoneCollider = ValidationUtils.GetTypeFromName("DynamicBoneCollider");
                if (TypeDynamicBone == null || TypeDynamicBoneCollider == null)
                {
                    EditorUtility.DisplayDialog("Error", "DynamicBone not found in the project.", "Okay");
                    return;
                }

                //Get Data
                var animator = avatarObj.GetComponent<Animator>();
                var dbcList = avatarObj.GetComponentsInChildren(TypeDynamicBoneCollider, true);
                var dbList = avatarObj.GetComponentsInChildren(TypeDynamicBone, true);

                //Convert Colliders
                var dbcDataList = new List<PhysBoneMigration.DynamicBoneColliderData>();
                foreach (var dbc in dbcList)
                {
                    var data = new PhysBoneMigration.DynamicBoneColliderData();
                    data.gameObject = dbc.gameObject;
                    data.bound = (PhysBoneMigration.DynamicBoneColliderData.Bound)(int)TypeDynamicBoneCollider.GetField("m_Bound").GetValue(dbc);
                    data.direction = (PhysBoneMigration.DynamicBoneColliderData.Direction)(int)TypeDynamicBoneCollider.GetField("m_Direction").GetValue(dbc);
                    data.radius = (float)TypeDynamicBoneCollider.GetField("m_Radius").GetValue(dbc);
                    data.height = (float)TypeDynamicBoneCollider.GetField("m_Height").GetValue(dbc);
                    data.center = (Vector3)TypeDynamicBoneCollider.GetField("m_Center").GetValue(dbc);

                    dbcDataList.Add(data);
                }

                //Convert to PhysBones
                var dbDataList = new List<PhysBoneMigration.DynamicBoneData>();
                foreach (var db in dbList)
                {
                    var data = new PhysBoneMigration.DynamicBoneData();
                    data.gameObject = db.gameObject;
                    data.root = (Transform)TypeDynamicBone.GetField("m_Root").GetValue(db);
                    data.exclusions = (List<Transform>)TypeDynamicBone.GetField("m_Exclusions").GetValue(db);
                    data.endLength = (float)TypeDynamicBone.GetField("m_EndLength").GetValue(db);
                    data.endOffset = (Vector3)TypeDynamicBone.GetField("m_EndOffset").GetValue(db);
                    data.elasticity = (float)TypeDynamicBone.GetField("m_Elasticity").GetValue(db);
                    data.elasticityDistrib = (AnimationCurve)TypeDynamicBone.GetField("m_ElasticityDistrib").GetValue(db);
                    data.damping = (float)TypeDynamicBone.GetField("m_Damping").GetValue(db);
                    data.dampingDistrib = (AnimationCurve)TypeDynamicBone.GetField("m_DampingDistrib").GetValue(db);
                    data.inert = (float)TypeDynamicBone.GetField("m_Inert").GetValue(db);
                    data.inertDistrib = (AnimationCurve)TypeDynamicBone.GetField("m_InertDistrib").GetValue(db);
                    data.stiffness = (float)TypeDynamicBone.GetField("m_Stiffness").GetValue(db);
                    data.stiffnessDistrib = (AnimationCurve)TypeDynamicBone.GetField("m_StiffnessDistrib").GetValue(db);
                    data.radius = (float)TypeDynamicBone.GetField("m_Radius").GetValue(db);
                    data.radiusDistrib = (AnimationCurve)TypeDynamicBone.GetField("m_RadiusDistrib").GetValue(db);
                    data.freezeAxis = (PhysBoneMigration.DynamicBoneData.FreezeAxis)(int)TypeDynamicBone.GetField("m_FreezeAxis").GetValue(db);
                    data.gravity = (Vector3)TypeDynamicBone.GetField("m_Gravity").GetValue(db);
                    data.force = (Vector3)TypeDynamicBone.GetField("m_Force").GetValue(db);

                    //Colliders
                    var dbColliders = (IList)TypeDynamicBone.GetField("m_Colliders").GetValue(db);
                    if (dbColliders != null && dbColliders.Count > 0)
                    {
                        var colliders = new List<PhysBoneMigration.DynamicBoneColliderData>(dbColliders.Count);
                        foreach (var dbc in dbColliders)
                        {
                            var index = System.Array.IndexOf(dbcList, (Component)dbc);
                            if (index >= 0)
                                colliders.Add(dbcDataList[index]);
                        }
                        data.colliders = colliders;
                    }

                    dbDataList.Add(data);
                }

                //Convert to PhysBones
                PhysBoneMigration.Convert(animator, dbDataList, dbcDataList);

                //Cleanup
                foreach (var dbc in dbcList)
                    Component.DestroyImmediate(dbc);
                foreach (var db in dbList)
                    Component.DestroyImmediate(db);
            }
            catch (System.Exception e)
            {
                Debug.LogError(e);
                EditorUtility.DisplayDialog("Error", "Encountered critical error while attempting to this operation.", "Okay");
            }
        }
    }
}


