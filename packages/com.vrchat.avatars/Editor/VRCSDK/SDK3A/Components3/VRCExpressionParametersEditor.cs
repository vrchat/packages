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
    private const int ParamCountLabelWidth = 20;
    private const int TypeWidth = 60;
	private const int DefaultWidth = 50;
	private const int SavedWidth = 50;
    private const int SyncedWidth = 45;

    private AnimatorController controllerToTransfer;
	
	private ReorderableList list;

    public enum Column {
        OverallParamCount,
        Type,
        ParameterName,
        Default,
        Saved,
        Synced
    }

	private Rect GetColumnSection(Column column, Rect rect, bool isHeader = false, bool isToggle = false) {
		int _paramCountLabelWidth = isHeader ? ParamCountLabelWidth : 0;

		Rect _rect = new Rect(rect);
		_rect.height = EditorGUIUtility.singleLineHeight;

        switch (column) {
            case Column.OverallParamCount:
                _rect.width = _paramCountLabelWidth;
                _rect.x = rect.x;
                break;
            case Column.Type:
                _rect.width = TypeWidth;
                _rect.x = rect.x + _paramCountLabelWidth;
                break;
            case Column.ParameterName:
                _rect.width = rect.width - (DefaultWidth + SavedWidth + SyncedWidth + TypeWidth);
                _rect.x = rect.x + _paramCountLabelWidth + TypeWidth;
                break;
            case Column.Default:
                _rect.width = DefaultWidth;
                _rect.x = rect.x + rect.width - (DefaultWidth + SyncedWidth + SavedWidth) + (isToggle ? _rect.width / 2 - 4 : 0);
                if (isToggle) _rect.width = EditorGUIUtility.singleLineHeight;
                break;
            case Column.Saved:
                _rect.width = SavedWidth;
                _rect.x = rect.x + rect.width - (SyncedWidth + SavedWidth) + (isToggle ? _rect.width / 2 - 4 : 0);
                if (isToggle) _rect.width = EditorGUIUtility.singleLineHeight;
                break;
            case Column.Synced:
                _rect.width = SyncedWidth;
                _rect.x = rect.x + rect.width - SyncedWidth + (isToggle ? _rect.width / 2 - 4 : 0);
				if (isToggle) _rect.width = EditorGUIUtility.singleLineHeight;
                break;
        }
		return _rect;
    }

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
        // the default size of the rect is bs, need to shift it to the left and make it wider to fit the entire space
        rect.x -= 5;
        rect.width += 5;

        var centeredStyle = new GUIStyle(GUI.skin.GetStyle("Label")) {
            alignment = TextAnchor.UpperCenter
        };

        EditorGUI.LabelField(GetColumnSection(Column.OverallParamCount, rect, true), $"{list.count}");
		EditorGUI.LabelField(GetColumnSection(Column.Type, rect, true), "Type", centeredStyle);
		EditorGUI.LabelField(GetColumnSection(Column.ParameterName, rect, true), "Name", centeredStyle);
		EditorGUI.LabelField(GetColumnSection(Column.Default, rect, true), "Default", centeredStyle);
		EditorGUI.LabelField(GetColumnSection(Column.Saved, rect, true), "Saved", centeredStyle);
        EditorGUI.LabelField(GetColumnSection(Column.Synced, rect, true), "Synced", centeredStyle);

    }
		
	private void OnDrawElement(Rect rect, int index, bool isActive, bool isFocused) {
		var element = list.serializedProperty.GetArrayElementAtIndex(index);

        EditorGUI.PropertyField(GetColumnSection(Column.Type, rect), element.FindPropertyRelative("valueType"), GUIContent.none);

        EditorGUI.PropertyField(GetColumnSection(Column.ParameterName, rect), element.FindPropertyRelative("name"), GUIContent.none );

		SerializedProperty defaultValue = element.FindPropertyRelative("defaultValue");
		var type = (ExpressionParameters.ValueType)element.FindPropertyRelative("valueType").intValue;
		switch(type)
		{
			case ExpressionParameters.ValueType.Int:
				defaultValue.floatValue = Mathf.Clamp(EditorGUI.IntField(GetColumnSection(Column.Default, rect), (int)defaultValue.floatValue), 0, 255);
				break;
			case ExpressionParameters.ValueType.Float:
				defaultValue.floatValue = Mathf.Clamp(EditorGUI.FloatField(GetColumnSection(Column.Default, rect), defaultValue.floatValue), -1f, 1f);
				break;
			case ExpressionParameters.ValueType.Bool:
				defaultValue.floatValue = EditorGUI.Toggle(GetColumnSection(Column.Default, rect, false, true), defaultValue.floatValue != 0 ? true : false) ? 1f : 0f;
				break;
		}
		EditorGUI.PropertyField(GetColumnSection(Column.Saved, rect, false, true), element.FindPropertyRelative("saved"), GUIContent.none);

        EditorGUI.PropertyField(GetColumnSection(Column.Synced, rect, false, true), element.FindPropertyRelative("networkSynced"), GUIContent.none);
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

    private string FindAnimatorParameterMatch() {
        var expressionParameters = target as ExpressionParameters;
        List<AnimatorControllerParameter> controllerParamsList = new List<AnimatorControllerParameter>(controllerToTransfer.parameters);
        List<string> matchingParameters = new List<string>();

        foreach (var parameter in expressionParameters.parameters) {
            bool parameterExists = false;
            //AnimatorControllerParameter foundControllerParameter = null;
            foreach (var controllerParameter in controllerToTransfer.parameters) {
                if (controllerParameter.name == parameter.name) {
                    parameterExists = true;
                    //foundControllerParameter = controllerParameter;
                    break;
                }
            }

            if (!parameterExists) {
                matchingParameters.Add(parameter.name);
            }
        }

        return string.Join(", ", matchingParameters);
    }

    private void TransferToAnimatorController() {
        var expressionParameters = target as ExpressionParameters;
        List<AnimatorControllerParameter> controllerParamsList = new List<AnimatorControllerParameter>(controllerToTransfer.parameters);

        foreach (var parameter in expressionParameters.parameters) {
            bool parameterExists = false;
            //AnimatorControllerParameter foundControllerParameter = null;
            foreach (var controllerParameter in controllerToTransfer.parameters) {
                if (controllerParameter.name == parameter.name) {
                    parameterExists = true;
                    //foundControllerParameter = controllerParameter;
                    break;
                }
            }

            if (!parameterExists) {
                controllerParamsList.Add(new AnimatorControllerParameter() {
                    name = parameter.name,
                    defaultBool = parameter.defaultValue > 0.5,
                    defaultFloat = parameter.defaultValue,
                    defaultInt = (int)Math.Floor(parameter.defaultValue),
                    type = VRCType2UnityType(parameter.valueType)
                });
            }
        }

        controllerToTransfer.parameters = controllerParamsList.ToArray();
    }

    private AnimatorControllerParameterType VRCType2UnityType(ExpressionParameters.ValueType type) {
        switch (type) {
            case ExpressionParameters.ValueType.Int:
                return AnimatorControllerParameterType.Int;
            case ExpressionParameters.ValueType.Float:
                return AnimatorControllerParameterType.Float;
            case ExpressionParameters.ValueType.Bool:
                return AnimatorControllerParameterType.Bool;
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
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