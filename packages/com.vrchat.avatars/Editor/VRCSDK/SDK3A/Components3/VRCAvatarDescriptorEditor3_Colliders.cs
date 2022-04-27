#if VRC_SDK_VRCSDK3
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

public partial class AvatarDescriptorEditor3 : Editor
{
	private void Init_Colliders()
	{
	}

	static string _CollidersFoldoutPrefsKey = "VRCSDK3_AvatarDescriptorEditor3_CollidersFoldout";
	private void DrawInspector_Colliders()
	{
		bool prevFoldout = EditorPrefs.GetBool(_CollidersFoldoutPrefsKey);
		if (Foldout(_CollidersFoldoutPrefsKey, "Colliders", false))
		{
			UpdateAutoColliders();

			//Colliders
			DrawElement("Head", serializedObject.FindProperty("collider_head"));
			DrawElement("Torso", serializedObject.FindProperty("collider_torso"));

			DrawMirroredElement("Hand", "collider_handL", "collider_handR");
			DrawMirroredElement("Foot", "collider_footL", "collider_footR");
			DrawMirroredElement("Finger Index", "collider_fingerIndexL", "collider_fingerIndexR", true);
			DrawMirroredElement("Finger Middle", "collider_fingerMiddleL", "collider_fingerMiddleR", true);
			DrawMirroredElement("Finger Ring", "collider_fingerRingL", "collider_fingerRingR", true);
			DrawMirroredElement("Finger Little", "collider_fingerLittleL", "collider_fingerLittleR", true);

			void DrawMirroredElement(string displayName, string propNameL, string propNameR, bool isFinger = false)
			{
				var configL = serializedObject.FindProperty(propNameL);
				var configR = serializedObject.FindProperty(propNameR);
				var isMirrored = configL.FindPropertyRelative("isMirrored");

				EditorGUI.BeginChangeCheck();
				if (isMirrored.boolValue)
				{
					DrawElement(displayName, configL, isFinger, true);
				}
				else
				{
					DrawElement($"{displayName} L", configL, isFinger, true);
					DrawElement($"{displayName} R", configR, isFinger);
				}
				if (EditorGUI.EndChangeCheck() && isMirrored.boolValue)
					MirrorCollider(configL, configR);
			}
		}
		if (EditorPrefs.GetBool(_CollidersFoldoutPrefsKey) != prevFoldout)
			EditorUtility.SetDirty(target); //Repaint
	}
	void MirrorCollider(SerializedProperty sourceProp, SerializedProperty destProp)
	{
		destProp.FindPropertyRelative("state").enumValueIndex = sourceProp.FindPropertyRelative("state").enumValueIndex;
		destProp.FindPropertyRelative("radius").floatValue = sourceProp.FindPropertyRelative("radius").floatValue;
		destProp.FindPropertyRelative("height").floatValue = sourceProp.FindPropertyRelative("height").floatValue;

		var sourceTransform = (Transform)sourceProp.FindPropertyRelative("transform").objectReferenceValue;
		var destTransform = (Transform)destProp.FindPropertyRelative("transform").objectReferenceValue;
		if (sourceTransform == null || destTransform == null)
			return;

		//Position
		var position = sourceProp.FindPropertyRelative("position").vector3Value;
		position = sourceTransform.TransformPoint(position); //Move into world space
		position = new Vector3(-position.x, position.y, position.z); //Mirror
		position = destTransform.InverseTransformPoint(position); //Move into dest local space
		destProp.FindPropertyRelative("position").vector3Value = position;

		//Rotation
		var rotation = sourceProp.FindPropertyRelative("rotation").quaternionValue;
		var globalRotation = sourceTransform.rotation * rotation;
		var euler = globalRotation.eulerAngles;
		destProp.FindPropertyRelative("rotation").quaternionValue = Quaternion.Inverse(destTransform.rotation) * Quaternion.Euler(euler.x, -euler.y, -euler.z);
	}

	void DrawElement(string title, SerializedProperty property, bool isFinger = false, bool mirror = false)
	{
		EditorGUILayout.BeginVertical(GUI.skin.box);
		{
			var state = property.FindPropertyRelative("state");
			EditorGUILayout.BeginHorizontal();
			{
				if (mirror)
				{
					var isMirrored = property.FindPropertyRelative("isMirrored");
					if (GUILayout.Button(new GUIContent(_linkIcon), GUI.skin.label, GUILayout.MaxWidth(16)))
					{
						isMirrored.boolValue = !isMirrored.boolValue;
					}
				}
				else
				{
					EditorGUILayout.Space(16f);
				}

				EditorGUILayout.PrefixLabel(title);
				EditorGUILayout.PropertyField(state, new GUIContent(""));

				bool isActiveProperty = IsActiveProperty(property);
				if (isActiveProperty && state.enumValueIndex != (int)VRCAvatarDescriptor.ColliderConfig.State.Custom)
				{
					SetActiveProperty(null);
					isActiveProperty = false;
				}

				GUI.backgroundColor = isActiveProperty ? _activeButtonColor : Color.white;
				if (GUILayout.Button(isActiveProperty ? "Return" : "Edit", EditorStyles.miniButton, GUILayout.MaxWidth(PreviewButtonWidth), GUILayout.Height(PreviewButtonHeight)))
				{
					if (isActiveProperty)
						SetActiveProperty(null);
					else
					{
						state.enumValueIndex = (int)VRCAvatarDescriptor.ColliderConfig.State.Custom;
						SetActiveProperty(property);
					}
				}
				GUI.backgroundColor = Color.white;
			}
			EditorGUILayout.EndHorizontal();

			if (state.enumValueIndex == (int)VRCAvatarDescriptor.ColliderConfig.State.Custom)
			{
				EditorGUILayout.PropertyField(property.FindPropertyRelative("radius"));

				var height = property.FindPropertyRelative("height");
				EditorGUILayout.PropertyField(height);
				if (isFinger)
				{
					var transform = (Transform)property.FindPropertyRelative("transform").objectReferenceValue;
					if (transform != null)
					{
						var minLength = transform.localPosition.magnitude;
						if (height.floatValue < minLength)
							height.floatValue = minLength;
					}
				}

				if (!isFinger)
				{
					EditorGUILayout.PropertyField(property.FindPropertyRelative("position"));
					InspectorUtil.QuaternionAsEulerField(property.FindPropertyRelative("rotation"));
				}
			}
		}
		EditorGUILayout.EndVertical();
	}
	void UpdateAutoColliders()
	{
		var animator = avatarDescriptor.GetComponent<Animator>();
		if (animator == null || !animator.isHuman)
			return;

		//Head
		UpdateConfig(ref avatarDescriptor.collider_head, VRCAvatarDescriptor.CalcHeadCollider(animator, avatarDescriptor.ViewPosition));

		//Torso
		UpdateConfig(ref avatarDescriptor.collider_torso, VRCAvatarDescriptor.CalcTorsoCollider(animator));

		//Palm
		UpdateConfig(ref avatarDescriptor.collider_handL, VRCAvatarDescriptor.CalcPalmCollider(animator, true));
		UpdateConfig(ref avatarDescriptor.collider_handR, VRCAvatarDescriptor.CalcPalmCollider(animator, false));

		//Foot
		UpdateConfig(ref avatarDescriptor.collider_footL, VRCAvatarDescriptor.CalcFootCollider(animator, true));
		UpdateConfig(ref avatarDescriptor.collider_footR, VRCAvatarDescriptor.CalcFootCollider(animator, false));

		//Fingers L
		UpdateConfig(ref avatarDescriptor.collider_fingerIndexL, VRCAvatarDescriptor.CalcFingerCollider(animator, 0, true));
		UpdateConfig(ref avatarDescriptor.collider_fingerMiddleL, VRCAvatarDescriptor.CalcFingerCollider(animator, 1, true));
		UpdateConfig(ref avatarDescriptor.collider_fingerRingL, VRCAvatarDescriptor.CalcFingerCollider(animator, 2, true));
		UpdateConfig(ref avatarDescriptor.collider_fingerLittleL, VRCAvatarDescriptor.CalcFingerCollider(animator, 3, true));

		//Fingers R
		UpdateConfig(ref avatarDescriptor.collider_fingerIndexR, VRCAvatarDescriptor.CalcFingerCollider(animator, 0, false));
		UpdateConfig(ref avatarDescriptor.collider_fingerMiddleR, VRCAvatarDescriptor.CalcFingerCollider(animator, 1, false));
		UpdateConfig(ref avatarDescriptor.collider_fingerRingR, VRCAvatarDescriptor.CalcFingerCollider(animator, 2, false));
		UpdateConfig(ref avatarDescriptor.collider_fingerLittleR, VRCAvatarDescriptor.CalcFingerCollider(animator, 3, false));

		void UpdateConfig(ref VRCAvatarDescriptor.ColliderConfig dest, VRCAvatarDescriptor.ColliderConfig config)
		{
			config.isMirrored = dest.isMirrored;
			if (dest.state == VRCAvatarDescriptor.ColliderConfig.State.Automatic)
				dest = config;
			dest.transform = config.transform;
		}
	}
	void DrawScene_Colliders()
	{
		if (!EditorPrefs.GetBool(_CollidersFoldoutPrefsKey))
			return;

		DrawHandle(serializedObject.FindProperty("collider_head"));
		DrawHandle(serializedObject.FindProperty("collider_torso"));
		DrawMirroredHandle(serializedObject.FindProperty("collider_handL"), serializedObject.FindProperty("collider_handR"));
		DrawMirroredHandle(serializedObject.FindProperty("collider_footL"), serializedObject.FindProperty("collider_footR"));
		DrawMirroredHandle(serializedObject.FindProperty("collider_fingerIndexL"), serializedObject.FindProperty("collider_fingerIndexR"), true);
		DrawMirroredHandle(serializedObject.FindProperty("collider_fingerRingL"), serializedObject.FindProperty("collider_fingerRingR"), true);
		DrawMirroredHandle(serializedObject.FindProperty("collider_fingerMiddleL"), serializedObject.FindProperty("collider_fingerMiddleR"), true);
		DrawMirroredHandle(serializedObject.FindProperty("collider_fingerLittleL"), serializedObject.FindProperty("collider_fingerLittleR"), true);

		void DrawMirroredHandle(SerializedProperty configL, SerializedProperty configR, bool isFinger = false)
		{
			bool isMirrored = configL.FindPropertyRelative("isMirrored").boolValue;

			EditorGUI.BeginChangeCheck();
			if (isFinger)
			{
				DrawFingerHandle(configL);
				DrawFingerHandle(configR);
			}
			else
			{
				DrawHandle(configL);
				DrawHandle(configR);
			}
			if (EditorGUI.EndChangeCheck() && isMirrored)
			{
				MirrorCollider(configL, configR);
			}
		}

		void DrawHandle(SerializedProperty config)
		{
			var transform = (Transform)config.FindPropertyRelative("transform").objectReferenceValue;
			var state = (VRCAvatarDescriptor.ColliderConfig.State)config.FindPropertyRelative("state").enumValueIndex;
			if (transform == null || state == VRCAvatarDescriptor.ColliderConfig.State.Disabled)
				return;

			var position = config.FindPropertyRelative("position");
			var rotation = config.FindPropertyRelative("rotation");
			var radius = config.FindPropertyRelative("radius");
			var height = config.FindPropertyRelative("height");

			var maxScale = VRCAvatarDescriptor.MaxScale(transform.lossyScale);

			var globalPos = transform.TransformPoint(position.vector3Value);
			var globalRot = transform.rotation * rotation.quaternionValue;

			var clampedRadius = Mathf.Min(radius.floatValue * maxScale, VRCAvatarDescriptor.COLLIDER_MAX_SIZE * 0.5f) / maxScale;
			var clampedHeight = Mathf.Min(height.floatValue * maxScale, VRCAvatarDescriptor.COLLIDER_MAX_SIZE) / maxScale;

			//Check if active
			bool isActive = IsActiveProperty(config);
			if (isActive)
			{
				Handles.color = Color.green;
				Handles.matrix = Matrix4x4.identity;

				//Capsule
				var outRadius = clampedRadius * maxScale;
				var outHeight = clampedHeight * maxScale;
				DrawCapsuleHandle(globalPos, globalRot, ref outRadius, ref outHeight);
				radius.floatValue = outRadius / maxScale;
				height.floatValue = outHeight / maxScale;

				//Transform
				var outGlobalPos = globalPos;
				var outGlobalRot = globalRot;
				Handles.TransformHandle(ref outGlobalPos, ref outGlobalRot);

				if (!Approximately(outGlobalPos, globalPos))
					position.vector3Value = transform.InverseTransformPoint(outGlobalPos);
				if (!Approximately(outGlobalRot, globalRot))
					rotation.quaternionValue = Quaternion.Normalize(Quaternion.Inverse(transform.rotation) * outGlobalRot);
			}
			else
			{
				Handles.color = Color.white;
				Handles.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one * maxScale);
				HandlesUtil.DrawWireCapsule(position.vector3Value, rotation.quaternionValue, clampedHeight, clampedRadius);
			}
		}
		void DrawFingerHandle(SerializedProperty config)
		{
			var transform = (Transform)config.FindPropertyRelative("transform").objectReferenceValue;
			var state = (VRCAvatarDescriptor.ColliderConfig.State)config.FindPropertyRelative("state").enumValueIndex;
			if (transform == null || state == VRCAvatarDescriptor.ColliderConfig.State.Disabled)
				return;

			var radius = config.FindPropertyRelative("radius");
			var height = config.FindPropertyRelative("height");

			var scale = VRCAvatarDescriptor.MaxScale(transform.lossyScale);

			var begin = transform.parent.position;
			var end = transform.position;
			var normal = (end - begin).normalized;

			var minLength = transform.localPosition.magnitude;
			var globalHeight = height.floatValue * scale;
			end = begin + normal * globalHeight;
			var center = (end + begin) * 0.5f;

			var globalRadius = Mathf.Min(radius.floatValue * scale, VRCAvatarDescriptor.COLLIDER_MAX_SIZE * 0.5f);

			bool isActive = IsActiveProperty(config);
			if (isActive)
			{
				Handles.color = Color.green;
				Handles.matrix = Matrix4x4.identity;

				var outRadius = globalRadius;
				var outHeight = globalHeight;
				DrawCapsuleFingerHandle(begin, center, Quaternion.FromToRotation(Vector3.up, normal), ref outRadius, ref outHeight);

				//Radius
				if (!Approximately(globalRadius, outRadius))
					radius.floatValue = outRadius / scale;

				//Height
				if (!Approximately(outHeight, globalHeight))
					height.floatValue = Mathf.Max(outHeight / scale, minLength);
			}
			else
			{
				Handles.color = Color.white;
				Handles.matrix = Matrix4x4.identity;
				HandlesUtil.DrawWireCapsule(center, Quaternion.FromToRotation(Vector3.up, normal), globalHeight, globalRadius);
			}
		}
		/*void DrawFingerHandle(SerializedProperty config)
        {
			var transform = (Transform)config.FindPropertyRelative("transform").objectReferenceValue;
			var state = (VRCAvatarDescriptor.ColliderConfig.State)config.FindPropertyRelative("state").enumValueIndex;
			if (transform == null || state == VRCAvatarDescriptor.ColliderConfig.State.Disabled)
				return;

			var position = config.FindPropertyRelative("position");
			var rotation = config.FindPropertyRelative("rotation");
			var radius = config.FindPropertyRelative("radius");
			var height = config.FindPropertyRelative("height");

			var maxScale = VRCAvatarDescriptor.MaxScale(transform.lossyScale);

			var begin = transform.parent.position;
			var end = transform.TransformPoint(position.vector3Value);
			var normal = (end - begin).normalized;

			var length = Mathf.Min((end - begin).magnitude, VRCAvatarDescriptor.COLLIDER_MAX_SIZE);
			begin = end - normal * length;
			var center = (end + begin) * 0.5f;

			var globalRadius = Mathf.Min(radius.floatValue * maxScale, VRCAvatarDescriptor.COLLIDER_MAX_SIZE * 0.5f) / maxScale;

			bool isActive = IsActiveProperty(config);
			if(isActive)
            {
				Handles.color = Color.green;
				Handles.matrix = Matrix4x4.identity;

				var outRadius = globalRadius;
				var outHeight = length;
				DrawCapsuleHandle(center, Quaternion.FromToRotation(Vector3.up, normal), ref outRadius, ref outHeight);

				//Radius
				if(!Approximately(globalRadius, outRadius))
					radius.floatValue = outRadius / maxScale;

				//Height
				var finalHeight = Mathf.Max(outHeight, Vector3.Distance(begin, transform.position));
				if (!Approximately(outHeight, finalHeight))
					outHeight = finalHeight;

				//Position
				var finalEnd = center + normal * outHeight * 0.5f;
				if (!Approximately(end, finalEnd))
					position.vector3Value = transform.InverseTransformPoint(finalEnd);
			}
			else
            {
				Handles.color = Color.white;
				Handles.matrix = Matrix4x4.identity;
				HandlesUtil.DrawWireCapsule(center, Quaternion.FromToRotation(Vector3.up, normal), length, globalRadius);
			}
		}*/

		void DrawCapsuleHandle(Vector3 position, Quaternion rotation, ref float radius, ref float height)
		{
			//Height Handles
			float halfHeight = height * 0.5f;
			Handles.DrawLine(position + rotation * Vector3.up * halfHeight, position + rotation * Vector3.down * halfHeight);
			halfHeight = DrawRadiusSlider(position, rotation * Vector3.up, halfHeight);
			halfHeight = DrawRadiusSlider(position, rotation * Vector3.down, halfHeight);
			height = halfHeight * 2.0f;

			//Radius
			Handles.DrawWireDisc(position, rotation * Vector3.up, radius);
			radius = DrawRadiusSlider(position, rotation * Vector3.right, radius);
			radius = DrawRadiusSlider(position, rotation * Vector3.left, radius);
			radius = DrawRadiusSlider(position, rotation * Vector3.forward, radius);
			radius = DrawRadiusSlider(position, rotation * Vector3.back, radius);

			//Wireframe
			HandlesUtil.DrawWireCapsule(position, rotation, height, radius);
		}
		void DrawCapsuleFingerHandle(Vector3 begin, Vector3 center, Quaternion rotation, ref float radius, ref float height)
		{
			//Height Handles
			height = DrawRadiusSlider(begin, rotation * Vector3.up, height);

			//Radius
			Handles.DrawWireDisc(center, rotation * Vector3.up, radius);
			radius = DrawRadiusSlider(center, rotation * Vector3.right, radius);
			radius = DrawRadiusSlider(center, rotation * Vector3.left, radius);
			radius = DrawRadiusSlider(center, rotation * Vector3.forward, radius);
			radius = DrawRadiusSlider(center, rotation * Vector3.back, radius);

			//Wireframe
			HandlesUtil.DrawWireCapsule(center, rotation, height, radius);
		}
		float DrawRadiusSlider(Vector3 position, Vector3 direction, float radius)
		{
			var endpoint = position + direction * radius;
			var handleSize = HandleUtility.GetHandleSize(endpoint) * 0.05f;
			endpoint = Handles.Slider(endpoint, direction, handleSize, Handles.DotHandleCap, 0f);

			var finalRadius = Vector3.Distance(position, endpoint);
			if (!Mathf.Approximately(finalRadius, radius))
				radius = finalRadius;
			if (Vector3.Dot(direction, endpoint - position) < 0)
				radius = 0;
			return radius;
		}
	}
	bool Approximately(float a, float b)
	{
		return Mathf.Abs(a - b) < 0.0000001f;
	}
	bool Approximately(Vector3 a, Vector3 b)
	{
		return Approximately(a.x, b.x) && Approximately(a.y, b.y) && Approximately(a.z, b.z);
	}
	bool Approximately(Quaternion a, Quaternion b)
	{
		return Approximately(a.x, b.x) && Approximately(a.y, b.y) && Approximately(a.z, b.z) && Approximately(a.w, b.w);
	}
}

#endif