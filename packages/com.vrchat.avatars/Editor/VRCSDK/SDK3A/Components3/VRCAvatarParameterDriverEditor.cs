#if VRC_SDK_VRCSDK3
using UnityEngine;
using UnityEditor;
using VRC.SDK3.Avatars.Components;
using static VRC.SDKBase.VRC_AvatarParameterDriver;
using Boo.Lang;
using System;
using UnityEditorInternal;
using AnimatorControllerParameterType = UnityEngine.AnimatorControllerParameterType;

[CustomEditor(typeof(VRCAvatarParameterDriver))]
public class AvatarParameterDriverEditor : Editor
{
	VRCAvatarParameterDriver driver;
	string[] parameterNames;
	AnimatorControllerParameterType[] parameterTypes;
	private ReorderableList list;

	public ReorderableList List
	{
		get
		{
			if (list == null)
			{
				list = new ReorderableList(serializedObject, serializedObject.FindProperty("parameters"));
				list.drawElementCallback += DrawElementCallback;
				//list.onAddCallback += delegate (ReorderableList reorderableList) { reorderableList.list.Add(new Parameter() { name = parameterNames.Length > 0 ? parameterNames[0] : "" }); };
				list.elementHeightCallback += ElementHeightCallback;
				list.headerHeight = 1;
			}
			return list;
		}
	}

	private float ElementHeightCallback(int index)
	{
		float height = EditorGUIUtility.singleLineHeight * 1.25f; // type

		Rect dummyRect = new Rect(0, 0, 0, 0);
		var parameters = serializedObject.FindProperty("parameters");
		Parameter parameter = driver.parameters[index];
		switch (parameter.type)
		{
			case ChangeType.Set:
				height += EditorGUIUtility.singleLineHeight * 1.25f; // name
				if (DrawParamaterDropdown(parameters.GetArrayElementAtIndex(index).FindPropertyRelative("name"), "", ref dummyRect, false) < 0) {
					HelpBoxHeight(ref height);
				}
				height += EditorGUIUtility.singleLineHeight * 1.25f; // value
				break;
			case ChangeType.Add:
				height += EditorGUIUtility.singleLineHeight * 1.25f; // name
				if (DrawParamaterDropdown(parameters.GetArrayElementAtIndex(index).FindPropertyRelative("name"), "", ref dummyRect, false) < 0) {
					HelpBoxHeight(ref height);
				}
				height += EditorGUIUtility.singleLineHeight * 1.25f; // value
				break;
			case ChangeType.Random:
				height += EditorGUIUtility.singleLineHeight * 1.25f; // name
				if (DrawParamaterDropdown(parameters.GetArrayElementAtIndex(index).FindPropertyRelative("name"), "", ref dummyRect, false) < 0) {
					HelpBoxHeight(ref height);
				}
				height += EditorGUIUtility.singleLineHeight * 1.25f; // value
				if (IndexOf(parameterNames, parameter.name) == -1) {
					height += EditorGUIUtility.singleLineHeight * 1.25f; // value 2
				} else if (parameterTypes[IndexOf(parameterNames, parameter.name)] == AnimatorControllerParameterType.Int || parameterTypes[IndexOf(parameterNames, parameter.name)] == AnimatorControllerParameterType.Float) {
					height += EditorGUIUtility.singleLineHeight * 1.25f; // value 2
				}
				break;
			case ChangeType.Copy:
				height += EditorGUIUtility.singleLineHeight * 1.25f; // source
				if (DrawParamaterDropdown(parameters.GetArrayElementAtIndex(index).FindPropertyRelative("source"), "", ref dummyRect, false) < 0) {
					HelpBoxHeight(ref height);
				}
				height += EditorGUIUtility.singleLineHeight * 1.25f; // destination
				if (DrawParamaterDropdown(parameters.GetArrayElementAtIndex(index).FindPropertyRelative("name"), "", ref dummyRect, false) < 0) {
					HelpBoxHeight(ref height);
				}
				var sourceValueType = IndexOf(parameterNames, parameter.source) >= 0 ? parameterTypes[IndexOf(parameterNames, parameter.source)] : AnimatorControllerParameterType.Float;
				var destValueType = IndexOf(parameterNames, parameter.name) >= 0 ? parameterTypes[IndexOf(parameterNames, parameter.name)] : AnimatorControllerParameterType.Float;
				if (sourceValueType != destValueType || sourceValueType == AnimatorControllerParameterType.Trigger) {
					HelpBoxHeight(ref height);
				}
				height += EditorGUIUtility.singleLineHeight * 1.25f; // convert range checkbox
				if (parameter.convertRange)
				{
					height += EditorGUIUtility.singleLineHeight * 1.25f; // source range
					height += EditorGUIUtility.singleLineHeight * 1.25f; // destination range
				}
				break;
			default:
				break;
		}
		void HelpBoxHeight(ref float height1)
		{
			height1 += EditorGUIUtility.singleLineHeight * 1.25f * 2; // help box when parameter is empty (no parameters are present in animator)
		}

		return height;
	}

	public void OnEnable()
	{
		UpdateParameters();
	}
	void UpdateParameters()
	{
		driver = target as VRCAvatarParameterDriver;

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

	private void DrawElementCallback(Rect rect, int i, bool isactive, bool isfocused)
	{
		var param = driver.parameters[i];
		var index = IndexOf(parameterNames, param.name);
		rect.height = EditorGUIUtility.singleLineHeight;

		DrawParameter(serializedObject.FindProperty("parameters"), i, rect);

		
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
		driver = target as VRCAvatarParameterDriver;

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
		List.DoLayoutList();

		//End
		serializedObject.ApplyModifiedProperties();
		if(EditorGUI.EndChangeCheck())
			EditorUtility.SetDirty(this);
	}

	void DrawParameter(SerializedProperty parameters, int arrayIndex, Rect rect)
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
		EditorGUI.PropertyField(rect, changeType);
		rect.y += EditorGUIUtility.singleLineHeight * 1.25f;
		Rect _rect = new Rect(rect);

		switch ((ChangeType)changeType.enumValueIndex)
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
			var destIndex = DrawParamaterDropdown(name, "Destination", ref _rect);
			rect = _rect;
			var valueType = destIndex >= 0 ? parameterTypes[destIndex] : AnimatorControllerParameterType.Float;
			rect.y += EditorGUIUtility.singleLineHeight * 1.25f;
			_rect = new Rect(rect);
			switch (valueType)
			{
				case AnimatorControllerParameterType.Bool:
				{
					value.floatValue = EditorGUI.Toggle(_rect, "Value", value.floatValue != 0f) ? 1f : 0f;
					break;
				}
				case AnimatorControllerParameterType.Int:
				{
					value.floatValue = EditorGUI.IntField(_rect, "Value", (int)value.floatValue);
					break;
				}
				case AnimatorControllerParameterType.Float:
				{
					value.floatValue = EditorGUI.FloatField(_rect, "Value", value.floatValue);
					break;
				}
				case AnimatorControllerParameterType.Trigger:
				{
					break;
				}
				default:
				{
					rect.y += EditorGUIUtility.singleLineHeight * 1.25f;
					_rect = new Rect(rect.x - 30, rect.y, rect.width + 30, rect.height * 2);
					EditorGUI.HelpBox(_rect, $"{valueType} parameters don't support the {changeType.enumNames[changeType.enumValueIndex]} type", MessageType.Warning);
					break;
				}
			}
		}
		void DrawAdd()
		{
			var destIndex = DrawParamaterDropdown(name, "Destination", ref _rect);
			rect = _rect;
			var valueType = destIndex >= 0 ? parameterTypes[destIndex] : AnimatorControllerParameterType.Float;
			rect.y += EditorGUIUtility.singleLineHeight * 1.25f;
			_rect = new Rect(rect);
			switch (valueType)
			{
				case AnimatorControllerParameterType.Int:
				{
					value.floatValue = EditorGUI.IntField(_rect, "Value", (int)value.floatValue);
					break;
				}
				case AnimatorControllerParameterType.Float:
				{
					value.floatValue = EditorGUI.FloatField(_rect, "Value", value.floatValue);
					break;
				}
				default:
				{
					rect.y += EditorGUIUtility.singleLineHeight * 1.25f;
					_rect = new Rect(rect.x, rect.y, rect.width, rect.height * 2);
					EditorGUI.HelpBox(_rect, $"{valueType} parameters don't support the {changeType.enumNames[changeType.enumValueIndex]} type", MessageType.Warning);
					break;
				}
			}
		}
		void DrawRandom()
		{
			var destIndex = DrawParamaterDropdown(name, "Destination", ref _rect);
			rect = _rect;
			var valueType = destIndex >= 0 ? parameterTypes[destIndex] : AnimatorControllerParameterType.Float;
			rect.y += EditorGUIUtility.singleLineHeight * 1.25f;
			_rect = new Rect(rect);
			switch (valueType)
			{
				case AnimatorControllerParameterType.Bool:
				case AnimatorControllerParameterType.Trigger:
				{
					EditorGUI.PropertyField(_rect, chance);
					break;
				}
				case AnimatorControllerParameterType.Int:
				{
					minValue.floatValue = EditorGUI.IntField(_rect, "Min Value", (int)minValue.floatValue);
					rect.y += EditorGUIUtility.singleLineHeight * 1.25f;
					_rect = new Rect(rect);
					maxValue.floatValue = EditorGUI.IntField(_rect, "Max Value", (int)maxValue.floatValue);
					break;
				}
				case AnimatorControllerParameterType.Float:
				{
					minValue.floatValue = EditorGUI.FloatField(_rect, "Min Value", minValue.floatValue);
					rect.y += EditorGUIUtility.singleLineHeight * 1.25f;
					_rect = new Rect(rect);
					maxValue.floatValue = EditorGUI.FloatField(_rect, "Max Value", maxValue.floatValue);
					break;
				}
			}
		}
		void DrawCopy()
		{
			var sourceIndex = DrawParamaterDropdown(source, "Source", ref _rect);
			rect = _rect;
			var sourceValueType = sourceIndex >= 0 ? parameterTypes[sourceIndex] : AnimatorControllerParameterType.Float;
			rect.y += EditorGUIUtility.singleLineHeight * 1.25f;
			_rect = new Rect(rect);
			var destIndex = DrawParamaterDropdown(name, "Destination", ref _rect);
			rect = _rect;
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
							rect.y += EditorGUIUtility.singleLineHeight * 1.25f;
							_rect = new Rect(rect.x, rect.y, rect.width, rect.height * 2);
							rect.y += EditorGUIUtility.singleLineHeight * 1.25f;
							EditorGUI.HelpBox(_rect, "Source parameter can't be the Trigger type", MessageType.Warning);
						}
						else if(sourceValueType != destValueType)
						{
							rect.y += EditorGUIUtility.singleLineHeight * 1.25f;
							_rect = new Rect(rect.x, rect.y, rect.width, rect.height * 2);
							rect.y += EditorGUIUtility.singleLineHeight * 1.25f;
							EditorGUI.HelpBox(_rect, $"Value will be converted from a {sourceValueType} to a {destValueType}.", MessageType.Info);
						}
					}

					rect.y += EditorGUIUtility.singleLineHeight * 1.25f;
					_rect = new Rect(rect);
					var convertRange = param.FindPropertyRelative("convertRange");
					EditorGUI.PropertyField(_rect, convertRange);
					if(convertRange.boolValue)
					{
						rect.x += 10;
						rect.width -= 10;
						rect.y += EditorGUIUtility.singleLineHeight * 1.25f;
						_rect = new Rect(rect);
						DrawRange("Source", "sourceMin", "sourceMax", _rect);
						rect.y += EditorGUIUtility.singleLineHeight * 1.25f;
						_rect = new Rect(rect);
						DrawRange("Destination", "destMin", "destMax", _rect);

						void DrawRange(string label, string min, string max, Rect _rect1)
						{
							var minVal = param.FindPropertyRelative(min);
							var maxVal = param.FindPropertyRelative(max);

							EditorGUI.PrefixLabel(_rect1, new GUIContent(label));
							_rect1.x += (_rect1.width / 2) -10;
							_rect1.width = _rect1.width / 8;
							_rect = new Rect(_rect1);
							EditorGUI.LabelField(_rect1, "Min");
							_rect1.x += _rect1.width;
							_rect = new Rect(_rect1);
							minVal.floatValue = EditorGUI.FloatField(_rect1, minVal.floatValue);
							_rect1.x += _rect1.width + 10;
							_rect = new Rect(_rect1);
							EditorGUI.LabelField(_rect1, "Max");
							_rect1.x += _rect1.width;
							_rect = new Rect(_rect1);
							maxVal.floatValue = EditorGUI.FloatField(_rect1, maxVal.floatValue);
						}
					}

					break;
				}
				default:
				{
					rect.y += EditorGUIUtility.singleLineHeight * 1.25f;
					_rect = new Rect(rect.x, rect.y, rect.width, rect.height * 2);
					rect.y += EditorGUIUtility.singleLineHeight * 1.25f;
					EditorGUI.HelpBox(_rect, $"{destValueType} parameters don't support the {changeType.enumNames[changeType.enumValueIndex]} type", MessageType.Warning);
					break;
				}
			}
		}
	}
	int DrawParamaterDropdown(SerializedProperty name, string label, ref Rect rect, bool drawUI = true)
	{
		//Name
		Rect _rect = new Rect(rect.x, rect.y, rect.width - 100, rect.height);
		EditorGUI.PrefixLabel(_rect, new GUIContent(label));
		var index = -1;
		if (parameterNames != null)
		{
			//Find index
			EditorGUI.BeginChangeCheck();
			index = Array.IndexOf(parameterNames, name.stringValue);
			if (drawUI) {
				_rect = new Rect(200, rect.y, rect.width - 300, rect.height);
				index = EditorGUI.Popup(_rect, index, parameterNames);
			}
			if (EditorGUI.EndChangeCheck() && index >= 0)
				name.stringValue = parameterNames[index];
		}
		if (drawUI) {
			_rect = new Rect(rect.width - 90, rect.y, 130, rect.height);
			name.stringValue = EditorGUI.TextField(_rect, name.stringValue);
		}

		if (index < 0) {
			rect.y += EditorGUIUtility.singleLineHeight * 1.25f;
			_rect = new Rect(rect.x, rect.y, rect.width, rect.height * 2);
			rect.y += EditorGUIUtility.singleLineHeight * 1.25f;
			EditorGUI.HelpBox(_rect, $"Parameter '{name.stringValue}' not found. Make sure you defined in the Animator window's Parameters tab.", MessageType.Warning);
		}

		return index;
	}

	private int IndexOf(string[] array, string value)
	{
		if (array == null)
			return -1;
		for (int i = 0; i < array.Length; i++)
		{
			if (array[i] == value)
				return i;
		}
		return -1;
	}
}
#endif

