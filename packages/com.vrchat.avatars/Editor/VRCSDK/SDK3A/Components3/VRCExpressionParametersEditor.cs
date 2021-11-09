using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using AnimatorController = UnityEditor.Animations.AnimatorController;
using AnimatorControllerParameter = UnityEngine.AnimatorControllerParameter;
using AnimatorControllerParameterType = UnityEngine.AnimatorControllerParameterType;
using ExpressionParameters = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters;
using ExpressionParameter = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.Parameter;

[CustomEditor(typeof(ExpressionParameters))]
public class VRCExpressionParametersEditor : UnityEditor.Editor
{
	private const int TypeWidth = 60;
	private const int DefaultWidth = 50;
	private const int SavedWidth = 40;

	private AnimatorController controllerToTransfer;
	
	private ReorderableList list;
	
	public void OnEnable()
	{
		//Init parameters
		var customExpressionParams = target as ExpressionParameters;
		if (customExpressionParams.parameters == null)
			InitExpressionParameters(true);
		 
		// initialize ReorderableList
		list = new ReorderableList(serializedObject, serializedObject.FindProperty("parameters"), 
		                           true, true, true, true);
		list.drawElementCallback = OnDrawElement;
		list.drawHeaderCallback = OnDrawHeader;
	}
		
	private void OnDrawHeader(Rect rect) {
		//rect.y += 2;
		
		Rect _rect = new Rect(rect.x-5, rect.y, 25, EditorGUIUtility.singleLineHeight);
		EditorGUI.LabelField(_rect, $"{list.count}");
		
		rect.x += 15;
		rect.width -= 15;
		
		_rect = new Rect(rect.x + 5, rect.y, TypeWidth, EditorGUIUtility.singleLineHeight);
		EditorGUI.LabelField(_rect, "Type");

		rect.x += TypeWidth + 10;
		rect.width -= TypeWidth + 10;
		
		_rect = new Rect(rect.x, rect.y, rect.width - (5 + DefaultWidth + 5 + SavedWidth), EditorGUIUtility.singleLineHeight);
		EditorGUI.LabelField(_rect, "Name");

		rect.x += rect.width - (DefaultWidth + 5 + SavedWidth);
		rect.width = 5 + DefaultWidth + 5 + SavedWidth;
		
		_rect = new Rect(rect.x, rect.y, DefaultWidth, EditorGUIUtility.singleLineHeight);
		EditorGUI.LabelField(_rect, "Default");

		rect.x += DefaultWidth + 5;
		rect.width -= DefaultWidth + 5;
		
		_rect = new Rect(rect.x, rect.y, SavedWidth, EditorGUIUtility.singleLineHeight);
		EditorGUI.LabelField(_rect, "Saved");
		
	}
		
	private void OnDrawElement(Rect rect, int index, bool isActive, bool isFocused) {
		var element = list.serializedProperty.GetArrayElementAtIndex(index);
		rect.y += 2;
			
		Rect _rect = new Rect(rect.x + 5, rect.y, TypeWidth, EditorGUIUtility.singleLineHeight);
		EditorGUI.PropertyField(_rect, element.FindPropertyRelative("valueType"), GUIContent.none);

		rect.x += TypeWidth + 10;
		rect.width -= TypeWidth + 10;
		
		_rect = new Rect(rect.x, rect.y, rect.width - (5 + DefaultWidth + 5 + SavedWidth), EditorGUIUtility.singleLineHeight);
		EditorGUI.PropertyField(_rect, element.FindPropertyRelative("name"), GUIContent.none );

		rect.x += rect.width - (DefaultWidth + 5 + SavedWidth);
		rect.width = 5 + DefaultWidth + 5 + SavedWidth;

		_rect = new Rect(rect.x, rect.y, DefaultWidth, EditorGUIUtility.singleLineHeight);
		SerializedProperty defaultValue = element.FindPropertyRelative("defaultValue");
		var type = (ExpressionParameters.ValueType)element.FindPropertyRelative("valueType").intValue;
		switch(type)
		{
			case ExpressionParameters.ValueType.Int:
				defaultValue.floatValue = Mathf.Clamp(EditorGUI.IntField(_rect, (int)defaultValue.floatValue), 0, 255);
				break;
			case ExpressionParameters.ValueType.Float:
				defaultValue.floatValue = Mathf.Clamp(EditorGUI.FloatField(_rect, defaultValue.floatValue), -1f, 1f);
				break;
			case ExpressionParameters.ValueType.Bool:
				_rect.x += 20;
				_rect.width -= 20;
				defaultValue.floatValue = EditorGUI.Toggle(_rect, defaultValue.floatValue != 0 ? true : false) ? 1f : 0f;
				break;
		}

		rect.x += DefaultWidth + 5 + 13;
		rect.width -= DefaultWidth + 5 + 13;
		
		_rect = new Rect(rect.x, rect.y, SavedWidth, EditorGUIUtility.singleLineHeight);
		EditorGUI.PropertyField(_rect,
		                        element.FindPropertyRelative("saved"), GUIContent.none );
	}

	public override void OnInspectorGUI()
	{
		serializedObject.Update();
		{
			serializedObject.Update();
			list.DoLayoutList();
			serializedObject.ApplyModifiedProperties();
			
			//Draw parameters
			var parameters = serializedObject.FindProperty("parameters");
			
			// old school draw parameters without ReorderableList
			/*for (int i = 0; i < ExpressionParameters.MAX_PARAMETERS; i++)
				DrawExpressionParameter(parameters, i);
			*/

			//Cost
			int cost = (target as ExpressionParameters).CalcTotalCost();
			if(cost <= ExpressionParameters.MAX_PARAMETER_COST)
				EditorGUILayout.HelpBox($"Total Memory: {cost}/{ExpressionParameters.MAX_PARAMETER_COST}", MessageType.Info);
			else
				EditorGUILayout.HelpBox($"Total Memory: {cost}/{ExpressionParameters.MAX_PARAMETER_COST}\nParameters use too much memory.  Remove parameters or use bools which use less memory.", MessageType.Error);

			//Info
			EditorGUILayout.HelpBox("Only parameters defined here can be used by expression menus, sync between all playable layers and sync across the network to remote clients.", MessageType.Info);
			EditorGUILayout.HelpBox("The parameter name and type should match a parameter defined on one or more of your animation controllers.", MessageType.Info);
			EditorGUILayout.HelpBox("Parameters used by the default animation controllers (Optional)\nVRCEmote, Int\nVRCFaceBlendH, Float\nVRCFaceBlendV, Float", MessageType.Info);

			//Clear
			if (GUILayout.Button("Clear Parameters"))
			{
				if (EditorUtility.DisplayDialogComplex("Warning", "Are you sure you want to clear all expression parameters?", "Clear", "Cancel", "") == 0)
				{
					InitExpressionParameters(false);
				}
			}
			if (GUILayout.Button("Default Parameters"))
			{
				if (EditorUtility.DisplayDialogComplex("Warning", "Are you sure you want to reset all expression parameters to default?", "Reset", "Cancel", "") == 0)
				{
					InitExpressionParameters(true);
				}
			}
		}
		serializedObject.ApplyModifiedProperties();
	}

	void InitExpressionParameters(bool populateWithDefault)
	{
		var expressionParameters = target as ExpressionParameters;
		serializedObject.Update();
		{
			if (populateWithDefault)
			{
				expressionParameters.parameters = new ExpressionParameter[3];

				expressionParameters.parameters[0] = new ExpressionParameter();
				expressionParameters.parameters[0].name = "VRCEmote";
				expressionParameters.parameters[0].valueType = ExpressionParameters.ValueType.Int;

				expressionParameters.parameters[1] = new ExpressionParameter();
				expressionParameters.parameters[1].name = "VRCFaceBlendH";
				expressionParameters.parameters[1].valueType = ExpressionParameters.ValueType.Float;

				expressionParameters.parameters[2] = new ExpressionParameter();
				expressionParameters.parameters[2].name = "VRCFaceBlendV";
				expressionParameters.parameters[2].valueType = ExpressionParameters.ValueType.Float;
			}
			else
			{
				//Empty
				expressionParameters.parameters = new ExpressionParameter[0];
			}
		}
		serializedObject.ApplyModifiedProperties();
	}
}