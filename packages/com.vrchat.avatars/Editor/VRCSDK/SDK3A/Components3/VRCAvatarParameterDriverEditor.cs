#if VRC_SDK_VRCSDK3
using UnityEngine;
using UnityEditor;
using VRC.SDK3.Avatars.Components;
using static VRC.SDKBase.VRC_AvatarParameterDriver;
using Boo.Lang;
using System;

[CustomEditor(typeof(VRCAvatarParameterDriver))]
public class AvatarParameterDriverEditor : Editor
{
	string[] parameterNames;
	AnimatorControllerParameterType[] parameterTypes;
	int selectedParam = -1;

	public void OnEnable()
	{
		UpdateParameters();
	}
	void UpdateParameters()
	{
		//Build parameter names
		var controller = GetCurrentController();
		if(controller != null)
		{
			//Standard
			List<string> names = new List<string>();
			List<AnimatorControllerParameterType> types = new List<AnimatorControllerParameterType>();
			foreach(var item in controller.parameters)
			{
				names.Add(item.name);
				types.Add(item.type);
			}
			parameterNames = names.ToArray();
			parameterTypes = types.ToArray();
		}
	}

	static UnityEditor.Animations.AnimatorController GetCurrentController()
	{
		UnityEditor.Animations.AnimatorController controller = null;
		var toolType = Type.GetType("UnityEditor.Graphs.AnimatorControllerTool, UnityEditor.Graphs");
		var tool = EditorWindow.GetWindow(toolType);
		var controllerProperty = toolType.GetProperty("animatorController", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
		if(controllerProperty != null)
		{
			controller = controllerProperty.GetValue(tool, null) as UnityEditor.Animations.AnimatorController;
		}
		else
			Debug.LogError("Unable to find animator window.", tool);
		return controller;
	}

	public override void OnInspectorGUI()
	{
		EditorGUI.BeginChangeCheck();
		serializedObject.Update();
		var driver = target as VRCAvatarParameterDriver;

		//Update parameters
		if(parameterNames == null)
			UpdateParameters();

		//Info
		EditorGUILayout.HelpBox("This behaviour modifies parameters on this and all other animation controllers referenced on the avatar descriptor.\n\nKeep in mind only parameters defined in your VRCExpressionParameter object will be synced across the network.\n\nAdditionally, synced parameters are clamped between Int [0,255] and Float [-1,1]. Operations that modify these parameters will be clipped inside those bounds.", MessageType.Info);

		//Data
		EditorGUILayout.PropertyField(serializedObject.FindProperty("localOnly"));
		EditorGUILayout.PropertyField(serializedObject.FindProperty("debugString"));

		//Local only info
		bool usesAddOrRandom = false;
		foreach(var param in driver.parameters)
		{
			if(param.type == ChangeType.Add || param.type == ChangeType.Random)
				usesAddOrRandom = true;
		}
		if(usesAddOrRandom && !driver.localOnly)
			EditorGUILayout.HelpBox("Using Add & Random may not produce the same result when run on remote instance of the avatar.  When using these modes it's suggested you use a synced parameter and use the local only option.", MessageType.Warning);

		//Parameters
		var editable = new InspectorUtil.EditableArray();
		editable.array = serializedObject.FindProperty("parameters");
		editable.maxElements = int.MaxValue;
		editable.onDrawElement = DrawParameter;
		InspectorUtil.DrawEditableArray(this, editable, ref selectedParam);

		//End
		serializedObject.ApplyModifiedProperties();
		if(EditorGUI.EndChangeCheck())
			EditorUtility.SetDirty(this);
	}

	void DrawParameter(SerializedProperty parameters, int arrayIndex)
	{
		var param = parameters.GetArrayElementAtIndex(arrayIndex);
		var name = param.FindPropertyRelative("name");
		var source = param.FindPropertyRelative("source");
		var changeType = param.FindPropertyRelative("type");
		var value = param.FindPropertyRelative("value");
		var minValue = param.FindPropertyRelative("valueMin");
		var maxValue = param.FindPropertyRelative("valueMax");
		var chance = param.FindPropertyRelative("chance");

		//Change type
		EditorGUILayout.PropertyField(changeType);

		switch((ChangeType)changeType.enumValueIndex)
		{
			case ChangeType.Set:
			{
				DrawSet();
				break;
			}
			case ChangeType.Add:
			{
				DrawAdd();
				break;
			}
			case ChangeType.Random:
			{
				DrawRandom();
				break;
			}
			case ChangeType.Copy:
			{
				DrawCopy();
				break;
			}
		}

		void DrawSet()
		{
			var destIndex = DrawParamaterDropdown(name, "Destination");
			var valueType = destIndex >= 0 ? parameterTypes[destIndex] : AnimatorControllerParameterType.Float;
			switch(valueType)
			{
				case AnimatorControllerParameterType.Bool:
				{
					value.floatValue = EditorGUILayout.Toggle("Value", value.floatValue != 0f) ? 1f : 0f;
					break;
				}
				case AnimatorControllerParameterType.Int:
				{
					value.floatValue = EditorGUILayout.IntField("Value", (int)value.floatValue);
					break;
				}
				case AnimatorControllerParameterType.Float:
				{
					value.floatValue = EditorGUILayout.FloatField("Value", value.floatValue);
					break;
				}
				case AnimatorControllerParameterType.Trigger:
				{
					break;
				}
				default:
				{
					EditorGUILayout.HelpBox($"{valueType} parameters don't support the {changeType.enumNames[changeType.enumValueIndex]} type", MessageType.Warning);
					break;
				}
			}
		}
		void DrawAdd()
		{
			var destIndex = DrawParamaterDropdown(name, "Destination");
			var valueType = destIndex >= 0 ? parameterTypes[destIndex] : AnimatorControllerParameterType.Float;
			switch(valueType)
			{
				case AnimatorControllerParameterType.Int:
				{
					value.floatValue = EditorGUILayout.IntField("Value", (int)value.floatValue);
					break;
				}
				case AnimatorControllerParameterType.Float:
				{
					value.floatValue = EditorGUILayout.FloatField("Value", value.floatValue);
					break;
				}
				default:
				{
					EditorGUILayout.HelpBox($"{valueType} parameters don't support the {changeType.enumNames[changeType.enumValueIndex]} type", MessageType.Warning);
					break;
				}
			}
		}
		void DrawRandom()
		{
			var destIndex = DrawParamaterDropdown(name, "Destination");
			var valueType = destIndex >= 0 ? parameterTypes[destIndex] : AnimatorControllerParameterType.Float;
			switch(valueType)
			{
				case AnimatorControllerParameterType.Bool:
				case AnimatorControllerParameterType.Trigger:
				{
					EditorGUILayout.PropertyField(chance);
					break;
				}
				case AnimatorControllerParameterType.Int:
				{
					minValue.floatValue = EditorGUILayout.IntField("Min Value", (int)minValue.floatValue);
					maxValue.floatValue = EditorGUILayout.IntField("Max Value", (int)maxValue.floatValue);
					break;
				}
				case AnimatorControllerParameterType.Float:
				{
					minValue.floatValue = EditorGUILayout.FloatField("Min Value", minValue.floatValue);
					maxValue.floatValue = EditorGUILayout.FloatField("Max Value", maxValue.floatValue);
					break;
				}
			}
		}
		void DrawCopy()
		{
			var sourceIndex = DrawParamaterDropdown(source, "Source");
			var sourceValueType = sourceIndex >= 0 ? parameterTypes[sourceIndex] : AnimatorControllerParameterType.Float;
			var destIndex = DrawParamaterDropdown(name, "Destination");
			var destValueType = destIndex >= 0 ? parameterTypes[destIndex] : AnimatorControllerParameterType.Float;
			switch(destValueType)
			{
				case AnimatorControllerParameterType.Bool:
				case AnimatorControllerParameterType.Int:
				case AnimatorControllerParameterType.Float:
				{
					if(sourceIndex >= 0)
					{
						if(sourceValueType == AnimatorControllerParameterType.Trigger)
						{
							EditorGUILayout.HelpBox("Source parameter can't be the Trigger type", MessageType.Warning);
						}
						else if(sourceValueType != destValueType)
						{
							EditorGUILayout.HelpBox($"Value will be converted from a {sourceValueType} to a {destValueType}.", MessageType.Info);
						}
					}

					var convertRange = param.FindPropertyRelative("convertRange");
					EditorGUILayout.PropertyField(convertRange);
					if(convertRange.boolValue)
					{
						EditorGUI.indentLevel += 1;
						DrawRange("Source", "sourceMin", "sourceMax");
						DrawRange("Destination", "destMin", "destMax");
						EditorGUI.indentLevel -= 1;

						void DrawRange(string label, string min, string max)
						{
							var minVal = param.FindPropertyRelative(min);
							var maxVal = param.FindPropertyRelative(max);

							EditorGUILayout.BeginHorizontal();
							EditorGUILayout.PrefixLabel(label);
							EditorGUILayout.LabelField("Min", GUILayout.Width(64));
							minVal.floatValue = EditorGUILayout.FloatField(minVal.floatValue);
							EditorGUILayout.LabelField("Max", GUILayout.Width(64));
							maxVal.floatValue = EditorGUILayout.FloatField(maxVal.floatValue);
							EditorGUILayout.EndHorizontal();
						}
					}

					break;
				}
				default:
				{
					EditorGUILayout.HelpBox($"{destValueType} parameters don't support the {changeType.enumNames[changeType.enumValueIndex]} type", MessageType.Warning);
					break;
				}
			}
		}
	}
	int DrawParamaterDropdown(SerializedProperty name, string label)
	{
		//Name
		EditorGUILayout.BeginHorizontal();
		EditorGUILayout.PrefixLabel(label);
		var index = -1;
		if(parameterNames != null)
		{
			//Find index
			EditorGUI.BeginChangeCheck();
			index = Array.IndexOf(parameterNames, name.stringValue);
			index = EditorGUILayout.Popup(index, parameterNames);
			if(EditorGUI.EndChangeCheck() && index >= 0)
				name.stringValue = parameterNames[index];
		}
		name.stringValue = EditorGUILayout.TextField(name.stringValue);
		EditorGUILayout.EndHorizontal();

		if(index < 0)
			EditorGUILayout.HelpBox($"Parameter '{name.stringValue}' not found. Make sure you defined in the Animator window's Parameters tab.", MessageType.Warning);

		return index;
	}
}
#endif
