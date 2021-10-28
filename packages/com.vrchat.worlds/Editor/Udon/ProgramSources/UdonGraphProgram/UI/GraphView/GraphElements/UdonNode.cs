#if UNITY_2019_3_OR_NEWER
using UnityEditor.Experimental.GraphView;
using EditorGV = UnityEditor.Experimental.GraphView;
using EngineUI = UnityEngine.UIElements;
using EditorUI = UnityEditor.UIElements;
using UnityEngine.UIElements;
#else
using UnityEditor.Experimental.UIElements.GraphView;
using EditorGV = UnityEditor.Experimental.UIElements.GraphView;
using EngineUI = UnityEngine.Experimental.UIElements;
using EditorUI = UnityEditor.Experimental.UIElements;
using UnityEngine.Experimental.UIElements;
#endif
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.Udon.Editor.ProgramSources.UdonGraphProgram.UI.GraphView.UdonNodes;
using VRC.Udon.Graph;
using VRC.Udon.Graph.Interfaces;
using VRC.Udon.Serialization;
using VRC.Udon.Serialization.OdinSerializer.Utilities;
using Random = UnityEngine.Random;

namespace VRC.Udon.Editor.ProgramSources.UdonGraphProgram.UI.GraphView
{
    public class UdonNode : Node, IEdgeConnectorListener
    {
        // name is inherited from parent VisualElement class
        public Type type;
        public GameObject gameObject;
        protected UdonGraph _graphView;
        private EditorUI.PopupField<string> _popup;
        public UdonNodeDefinition definition;
        public UdonNodeData data;
        public Dictionary<int, UdonPort> portsIn;
        public Dictionary<int, UdonPort> portsOut;
        public List<UdonPort> portsFlowIn;
        public List<UdonPort> portsFlowOut;
        private INodeRegistry _registry;
        public UdonGroup group;

        // Overload handling
        private IList<UdonNodeDefinition> overloadDefinitions;

        private readonly Dictionary<UdonNodeDefinition, string> _optionNameCache =
            new Dictionary<UdonNodeDefinition, string>();

        private readonly Dictionary<UdonNodeDefinition, string> _cleanerOptionNameCache =
            new Dictionary<UdonNodeDefinition, string>();

        
        public bool IsVariableNode => _variableNodeType != VariableNodeType.None;

        public UdonGraph Graph
        {
            get => _graphView;
            private set { }
        }

        public INodeRegistry Registry
        {
            get => _registry;
            private set { }
        }

        private readonly string[] _specialFlows =
        {
            "Block",
            "Branch",
            "For",
            "Foreach",
            "While",
            "Is_Valid",
        };

        protected static readonly Dictionary<string, Type> DefinitionToTypeLookup = new Dictionary<string, Type>()
        {
            {"VRCUdonCommonInterfacesIUdonEventReceiver.__GetProgramVariable__SystemString__SystemObject", typeof(GetOrSetProgramVariableNode)},
            {"VRCUdonCommonInterfacesIUdonEventReceiver.__SetProgramVariable__SystemString_SystemObject__SystemVoid", typeof(GetOrSetProgramVariableNode)},
            {"VRCUdonCommonInterfacesIUdonEventReceiver.__GetProgramVariableType__SystemString__SystemType", typeof(GetOrSetProgramVariableNode)},
            {"VRCUdonCommonInterfacesIUdonEventReceiver.__SendCustomEvent__SystemString__SystemVoid", typeof(SendCustomEventNode)},
            {"VRCUdonCommonInterfacesIUdonEventReceiver.__SendCustomEventDelayedSeconds__SystemString_SystemSingle_VRCUdonCommonEnumsEventTiming__SystemVoid", typeof(SendCustomEventNode)},
            {"VRCUdonCommonInterfacesIUdonEventReceiver.__SendCustomEventDelayedFrames__SystemString_SystemInt32_VRCUdonCommonEnumsEventTiming__SystemVoid", typeof(SendCustomEventNode)},
            {"VRCUdonCommonInterfacesIUdonEventReceiver.__SendCustomNetworkEvent__VRCUdonCommonInterfacesNetworkEventTarget_SystemString__SystemVoid", typeof(SendCustomEventNode)},
            {"Set_ReturnValue", typeof(SetReturnValueNode)},
            {"Set_Variable", typeof(SetVariableNode)},
        };

#if UNITY_2019_3_OR_NEWER
        public string uid
        {
            get => viewDataKey;
            set => viewDataKey = value;
        }
#else
        public string uid { get => persistenceKey; set => persistenceKey = value; }
#endif

        // Called when creating from Asset, calls the CreateNode method below
        public static UdonNode CreateNode(UdonNodeData nodeData, UdonGraph view)
        {
            UdonNodeDefinition definition = UdonEditorManager.Instance.GetNodeDefinition(nodeData.fullName);
            if (definition == null)
            {
                Debug.LogError($"Cannot create node {nodeData.fullName} because there is no matching Node Definition");
                return null;
            }

            return CreateNode(definition, view, nodeData);
        }

        // Always called when creating UdonNode
        public static UdonNode CreateNode(UdonNodeDefinition definition, UdonGraph view, UdonNodeData nodeData = null)
        {
            Type type = typeof(UdonNode);
            // overwrite type with target type if it exists
            if (DefinitionToTypeLookup.TryGetValue(definition.fullName, out Type childType))
            {
                type = childType;
            }
            UdonNode node = Activator.CreateInstance(type, definition, view, nodeData) as UdonNode;
            node?.Initialize();
            return node;
        }

        private bool skipSubtitle = false;

        private Label subtitle;
        // Constructor is protected to force all paths through Static factory method except for child classes
        public UdonNode(UdonNodeDefinition nodeDefinition, UdonGraph view, UdonNodeData nodeData = null)
        {
            _graphView = view;
            definition = nodeDefinition;
            Undo.RecordObject(view.graphProgramAsset, "Add UdonNode");
            var registry = UdonGraphExtensions.GetRegistryForDefinition(nodeDefinition);
            if(registry != null)
            {
                this._registry = registry;
            }
            else
            {
                Debug.LogWarning($"Couldn't find registry for {nodeDefinition.fullName}");
            }

            VisualElement titleContainer = new VisualElement()
            {
                name = "title-container",
            };
            this.Q("title").Insert(0, titleContainer);

            titleContainer.Add(this.Q("title-label"));
            
            subtitle = new Label("")
            {
                name = "subtitle",
            };
            skipSubtitle = (
                _specialFlows.Contains(definition.fullName)
                || definition.fullName.EndsWith("et_Variable")
                || definition.fullName.StartsWithCached("Const_")
            );
            
            if (!skipSubtitle)
            {
                titleContainer.Insert(0, subtitle);
            }

            name = definition.fullName;
            elementTypeColor = Random.ColorHSV(0.5f, 0.6f, 0.1f, 0.2f, 0.8f, 0.9f);
            
            // Null is a type here, so handle it special
            if (nodeDefinition.type == null)
            {
                AddToClassList("null");
            }
            else
            {
                AddToClassList(definition.type.Namespace);
                AddToClassList(definition.type.Name);
            }
            
            if (nodeDefinition.fullName.Contains('_'))
            {
                AddToClassList(definition.fullName.Substring(0, nodeDefinition.fullName.IndexOf('_')));
            }

            // Create or validate nodeData
            if (nodeData == null)
            {
                data = _graphView.graphData.AddNode(definition.fullName);
                PopulateDefaultValues();
                ValidateNodeData();
            }
            else
            {
                data = nodeData;
                ValidateNodeData();
                SetPosition(new Rect(data.position.x, data.position.y, 0, 0));
            }

            uid = data.uid;

            // Fill in all fields, etc and add to the graph view
            if (UdonGraphExtensions.ShouldShowDocumentationLink(definition))
            {
                DrawHelpButton();
            }

            // Show overloads for nodes EXCEPT type, those have too many entries and break Unity UI
            if (!nodeDefinition.fullName.StartsWith("Type_"))
            {
                RefreshOverloadPopup();
            }
            
            AddToClassList("UdonNode");

            LayoutPorts();
        }

        public virtual void Initialize()
        {
            RefreshTitle();
            _graphView.MarkSceneDirty();
        }

        private string GetTargetVariableUid()
        {
            string result = "";
            if (IsVariableNode && this.data.nodeValues.Length > 0)
            {
                string[] parts = data.nodeValues[0].stringValue.Split('|');
                if (parts.Length > 1)
                {
                    result = parts[1];
                }
            }
            return result;
        }

        public void RefreshTitle()
        {
            if (IsVariableNode)
            {
                string uid = GetTargetVariableUid();
                if (!string.IsNullOrWhiteSpace(uid))
                {
                    string variableName = _graphView.GetVariableName(uid);
                    if (!string.IsNullOrWhiteSpace(variableName))
                    {
                        switch (_variableNodeType)
                        {
                            case VariableNodeType.Set:
                                title = $"Set {variableName}";
                                break;
                            case VariableNodeType.Change:
                                title = $"{variableName} Change";
                                break;
                            case VariableNodeType.Get:
                            default:
                                title = variableName;
                                break;
                        }
                        return;
                    }
                }
            }
            // Set Title
            var displayTitle = UdonGraphExtensions.PrettyString(definition.name).FriendlyNameify();
            if (displayTitle == "Const_VRCUdonCommonInterfacesIUdonEventReceiver")
            {
                displayTitle = "UdonBehaviour";
            }
            else if(displayTitle == "==" || displayTitle == "!=" || displayTitle == "+")
            {
                displayTitle = $"{definition.type.Name} {displayTitle}";
            }

            if (displayTitle.StartsWith("Op ")) 
                displayTitle = displayTitle.Replace("Op ", "");
            
            title = displayTitle;

            AddToClassList(title.Replace(" ", "").ToLowerFirstChar());

            if (definition == null)
            {
                Debug.LogWarning($"Definition for {this.name} is null");
                return;
            }
            
            string className = definition.name.Split(' ').FirstOrDefault().Split('_').FirstOrDefault();
            AddToClassList(className);

            if (!skipSubtitle)
            {
                if (definition.fullName.StartsWith("Event_"))
                {
                    subtitle.text = "Event";
                }
                // make Constructor nodes readable
                else if (definition.name == "ctor")
                {
                    subtitle.text = definition.type.Name;
                    title = "Constructor";
                }
                else
                {
                    subtitle.text = className;
                    // temp title shenanigans
                    int firstSplit = definition.fullName.IndexOf("__") + 2;
                    if (firstSplit > 1)
                    {
                        int lastSplit = definition.fullName.IndexOf("__", firstSplit);
                        int stringLength = (lastSplit > -1)
                            ? lastSplit - firstSplit
                            : definition.fullName.Length - firstSplit;
                        string line2 = definition.fullName.Substring(firstSplit, stringLength).Replace("_", " ")
                            .UppercaseFirst();
                        if (line2.StartsWith("Op "))
                        {
                            line2 = line2.Replace("Op ", "");
                            subtitle.text = definition.type.Name;
                        }

                        title = line2;
                    }
                    else
                    {
                        //TODO: handle class names not found
                        //Debug.Log($"Couldn't find classname for {nodeDefinition.fullName}");
                    }
                }   
            }
        }

        private void DrawHelpButton()
        {
            Button helpButton = new Button(ShowNodeDocs)
            {
                name = "help-button",
            };
            helpButton.Add(new TextElement()
            {
                name = "icon",
                text = "?"
            });
            titleButtonContainer.Add(helpButton);
        }

        private void ShowNodeDocs()
        {
            string url = UdonGraphExtensions.GetDocumentationLink(definition);
            if (!string.IsNullOrEmpty(url))
            {
                Help.BrowseURL(url);
            }
        }

        public override void SetPosition(Rect newPos)
        {
            newPos.position = GraphElementExtension.GetSnappedPosition(newPos.position);
            base.SetPosition(newPos);
            data.position = newPos.position;
        }
        
        public override void UpdatePresenterPosition()
        {
            base.UpdatePresenterPosition();
            if (group != null)
            {
                group.SaveNewData();
            }
        }

        public void RefreshOverloadPopup()
        {
            // Get overloads, draw them if we have more than one signature for this method
            overloadDefinitions = CacheOverloads();
            if (overloadDefinitions != null && overloadDefinitions.Count > 1)
            {
                // Get index of currently selected (could cache this on node instead)
                // TODO: switch to just reading this from Popup, which probably stores it
                int currentIndex = 0;
                for (int i = 0; i < overloadDefinitions.Count; i++)
                {
                    if (overloadDefinitions.ElementAt(i).fullName != name)
                    {
                        continue;
                    }

                    currentIndex = i;
                    break;
                }

                // Build dropdown list
                List<string> options = new List<string>();
                for (int i = 0; i < overloadDefinitions.Count; i++)
                {
                    UdonNodeDefinition nodeDefinition = overloadDefinitions.ElementAt(i);
                    if (!_optionNameCache.TryGetValue(nodeDefinition, out string optionName))
                    {
                        optionName = nodeDefinition.fullName;
                        // don't add overload types that take pointers, not supported
                        string[] splitOptionName = optionName.Split(new[] { "__" }, StringSplitOptions.None);
                        if (splitOptionName.Length >= 3)
                        {
                            optionName = $"({splitOptionName[2].Replace("_", ", ")})";
                        }

                        optionName = optionName.FriendlyNameify();
                        _optionNameCache.Add(nodeDefinition, optionName);
                    }

                    if (!_cleanerOptionNameCache.TryGetValue(nodeDefinition, out string cleanerOptionName))
                    {
                        cleanerOptionName =
                            optionName.Replace("UnityEngine", "").Replace("System", "").Replace("Variable_", "");
                        _cleanerOptionNameCache.Add(nodeDefinition, cleanerOptionName);
                    }

                    options.Add(cleanerOptionName);
                    // optionName is what was used as the tooltip. Do we need the tooltip?
                }

                // Clear out old one
                if (inputContainer.Contains(_popup))
                {
                    inputContainer.Remove(_popup);
                }

                _popup = new EditorUI.PopupField<string>(options, currentIndex);
#if UNITY_2019_3_OR_NEWER
                _popup.RegisterValueChangedCallback(
#else
                _popup.OnValueChanged(
#endif
                    (e) =>
                {
                    // TODO - store data in the dropdown and use formatListItemCallback?
                        SetNewFullName(overloadDefinitions.ElementAt(_popup.index).fullName);
                });
                inputContainer.Add(_popup);
            }
        }

        private void SetNewFullName(string newFullName)
        {
            data.fullName = newFullName;
            definition = UdonEditorManager.Instance.GetNodeDefinition(data.fullName);
            data.Resize(definition.Inputs.Count);
            // Todo: see if we can get rid of this reload. Tried ValidateNodeData,LayoutPorts,RestoreConnections but noodles were left hanging
            this.Reload();
        }

        private List<UdonNodeDefinition> CacheOverloads()
        {
            string baseIdentifier = name;
            string[] splitBaseIdentifier = baseIdentifier.Split(new[] { "__" }, StringSplitOptions.None);
            if (splitBaseIdentifier.Length >= 2)
            {
                baseIdentifier = $"{splitBaseIdentifier[0]}__{splitBaseIdentifier[1]}__";
            }

            if (baseIdentifier.StartsWithCached("Const_"))
            {
                return null;
            }

            if (baseIdentifier.StartsWithCached("Type_"))
            {
                baseIdentifier = "Type_";
            }

            if (baseIdentifier.StartsWithCached("Variable_"))
            {
                baseIdentifier = "Variable_";
            }

            // This used to be cached on graph instead of calculated per-node
            // TODO: cache this somewhere, maybe UdonEditorManager? Is that worth it for performance?
            IEnumerable<UdonNodeDefinition> matchingNodeDefinitions =
                UdonEditorManager.Instance.GetNodeDefinitions(baseIdentifier);

            var result = new List<UdonNodeDefinition>();
            foreach (var definition in matchingNodeDefinitions)
            {
                // don't add definitions with pointer parameters, not supported in Udon
                if (!definition.fullName.Contains('*'))
                {
                    result.Add(definition);
                }
            }

            return result;
        }

        internal void RestoreConnections()
        {
            RestoreInputs();
            RestoreFlows();
        }

        private void RestoreFlows()
        {
            for (int i = 0; i < data.flowUIDs.Length; i++)
            {
                // skip if flow uid is empty
                string nodeUID = data.flowUIDs[i];
                if (string.IsNullOrEmpty(nodeUID))
                {
                    continue;
                }

                // Find connected node via Graph
                UdonNode connectedNode = _graphView.GetNodeByGuid(nodeUID) as UdonNode;
                if (connectedNode == null)
                {
                    Debug.Log($"Couldn't find node with GUID {nodeUID}, clearing data");
                    data.flowUIDs[i] = "";
                    continue;
                }
                
                // Trying to move a Block's flow that was left at the end to the beginning
                if (portsFlowOut != null && i >= portsFlowOut.Count)
                {
                    Debug.LogWarning(
                        $"Trying to restore flow to {connectedNode.name} from a non-existent port, skipping");
                    
                    for (int j = 0; j < data.flowUIDs.Length; j++)
                    {
                        bool didRestoreFlow = false;
                        if (string.IsNullOrEmpty(data.flowUIDs[j]))
                        {
                            data.flowUIDs[j] = data.flowUIDs[i];
                            data.flowUIDs[i] = "";
                            didRestoreFlow = true;
                        }

                        if (didRestoreFlow)
                        {
                            RestoreFlows();
                        }
                    }
                    
                    continue;
                }

                UdonPort sourcePort = null;
                // Edge case, but its possible that this is null in broken graphs
                // Skip if we can't find the source port
                if (portsFlowOut != null)
                {
                    sourcePort = portsFlowOut.Count > 1 ? portsFlowOut[i] : portsFlowOut.FirstOrDefault();
                    if (sourcePort == null)
                    {
                        Debug.LogError($"Failed to find output flow port for node {uid}");
                        // clear the flow uid, user will have to reconnect by hand
                        data.flowUIDs[i] = "";
                        continue;
                    }
                }
                else
                {
                    Debug.LogError($"Failed to find output flow port for node {uid}");
                    // clear the flow uid, user will have to reconnect by hand
                    data.flowUIDs[i] = "";
                    continue;
                }


                UdonPort destPort = null;
                // Edge case, but its possible that this is null in broken graphs
                if(connectedNode.portsFlowIn != null)
                {
                    destPort = connectedNode.portsFlowIn.FirstOrDefault();
                    if (destPort == null)
                    {
                        Debug.LogError($"Failed to find input flow port node node {nodeUID}");
                        // clear the flow uid, user will have to reconnect by hand
                        data.flowUIDs[i] = "";
                        continue;
                    }
                }
                else
                {
                    Debug.LogError($"Failed to find input flow port node node {nodeUID}");
                    // clear the flow uid, user will have to reconnect by hand
                    data.flowUIDs[i] = "";
                    continue;
                }

                // Passed the tests! ready to connect
                var edge = sourcePort.ConnectTo(destPort);
                edge.AddToClassList("flow");
                _graphView.AddElement(edge);
            }

        }

        private void RestoreInputs()
        {
            for (int i = 0; i < definition.Inputs.Count; i++)
            {
                // Skip to next input if we don't have a node to check at this index
                if (data.nodeUIDs.Length <= i)
                {
                    continue;
                }

                // Skip to next input if we have a bad node reference
                if (string.IsNullOrEmpty(data.nodeUIDs[i]))
                {
                    continue;
                }

                // get otherIndex. not 100% sure what this refers to yet, maybe a port index?
                string[] splitUID = data.nodeUIDs[i].Split('|');
                string nodeUID = splitUID[0];
                int otherIndex = 0;
                if (splitUID.Length > 1)
                {
                    otherIndex = int.Parse(splitUID[1]);
                }

                // Skip if we don't have a good uid for the other node
                if (string.IsNullOrEmpty(nodeUID))
                {
                    continue;
                }

                // Find connected node via Graph
                UdonNode connectedNode = _graphView.GetNodeByGuid(nodeUID) as UdonNode;
                if (connectedNode == null)
                {
                    Debug.Log($"Couldn't find node with GUID {nodeUID}");
                    data.nodeUIDs[i] = "";
                    continue;
                }

                // No matching port for this data, skip
                if (portsIn == null) continue;
                if (!portsIn.TryGetValue(i, out UdonPort destPort))
                {
                    Debug.LogError($"Failed to find input data slot (index {i}) for node {uid} {data.fullName}");
                    continue;
                }

                // Copied from Legacy, not sure what conditions would cause this
                if (otherIndex < 0 || connectedNode?.portsOut.Keys.Count <= otherIndex)
                {
                    otherIndex = 0;
                }

                // skip if we can't find the sourcePort - comment better once you understand what this is exactly
                if (connectedNode == null || !connectedNode.portsOut.TryGetValue(otherIndex, out UdonPort sourcePort))
                {
                    Debug.LogError($"Failed to find output data slot for node {nodeUID}");
                    continue;
                }

                // Passed the tests! ready to connect
                var edge = sourcePort.ConnectTo(destPort);
                _graphView.AddElement(edge);
            }
        }

        // Legacy, haven't gone through yet
        void ValidateNodeData()
        {
            // set data to this graph
            data.SetGraph(_graphView.graphData);
            
            for (int i = 0; i < data.nodeValues.Length; i++)
            {
                if (definition.Inputs.Count <= i)
                {
                    continue;
                }

                Type expectedType = definition.Inputs[i].type;

                // Skip over if the value is null and that's ok
                if (data.nodeValues[i] == null)
                {
                    if (expectedType == null || Nullable.GetUnderlyingType(expectedType) != null)
                    {
                    continue;
                }
                    else
                    {
                        data.nodeValues[i] = SerializableObjectContainer.Serialize(default, expectedType);
                        continue;
                    }
                }

                object value = data.nodeValues[i].Deserialize();
                if (value == null)
                {
                    if (expectedType == null || Nullable.GetUnderlyingType(expectedType) != null)
                    {
                        // type is nullable, leave it alone
                    continue;
                }
                    else
                {
                        // not a nullable type - set a default
                        data.nodeValues[i] = SerializableObjectContainer.Serialize(default, expectedType);
                }
            }

                if (!expectedType.IsInstanceOfType(value))
            {
                    data.nodeValues[i] = SerializableObjectContainer.Serialize(null, expectedType);
                }
            }
        }

        void PopulateDefaultValues()
        {
            // No default values so I'm just...making them?
            int count = definition.Inputs.Count;

            data.nodeValues = new SerializableObjectContainer[count];
            data.nodeUIDs = new string[count];
            for (int i = 0; i < count; i++)
            {
                object value = definition.defaultValues.Count > i ? definition.defaultValues[i] : default;
                data.nodeValues[i] = SerializableObjectContainer.Serialize(value, definition.Inputs[i].type);
            }
        }

        private enum VariableNodeType
        {
            Get,
            Set,
            None,
            Change,
        };

        private VariableNodeType _variableNodeType = VariableNodeType.None;

        
        public virtual void LayoutPorts()
        {
            ClearPorts();
            SetupFlowPorts();

            // Don't setup in ports for Get_Variable node types, instead add variable popup
            if (name.CompareTo("Get_Variable") == 0)
            {
                _variableNodeType = VariableNodeType.Get;
                RefreshVariablePopup();
            }
            else if (name.CompareTo("Event_OnVariableChange") == 0)
            {
                _variableNodeType = VariableNodeType.Change;
            }
            else
            {
                // Add Variable popup and in-ports for Set_Variable
                if (name.CompareTo("Set_Variable") == 0)
                {
                    _variableNodeType = VariableNodeType.Set;
                    RefreshVariablePopup();
                }

                SetupInPorts();
            }
            
            SetupOutPorts();

            RefreshExpandedState();
            RefreshPorts();
        }

        public void ClearPorts()
        {
            portsFlowIn?.ForEach(port => port.RemoveFromHierarchy());
            portsFlowOut?.ForEach(port => port.RemoveFromHierarchy());
            portsIn?.Values.ForEach(port => port.RemoveFromHierarchy());
            portsOut?.Values.ForEach(port => port.RemoveFromHierarchy());
        }

        private EditorUI.PopupField<string> _variablePopupField;
        // TODO: Test this again after we have the new graph serializing the addition of nodes
        public void RefreshVariablePopup()
        {
            if (_variableNodeType == VariableNodeType.None)
            {
                Debug.LogError($"Not Creating Variable Pop-Up for Non Variable Node {data.fullName}");
            }
            
            // Legacy method of determining currently selected index
            // TODO: upgrade this logic path from the legacy method of determining Variable indices
            // Get Variable nodes only have one value, get it and deserialize

            var value = data.nodeValues[0].Deserialize();

            var options = _graphView.GetVariableNames.Where(t => !t.StartsWith("__")).ToList();

            // Get value of selected node in rather roundabout way
            int originalIndex = _graphView.GetVariableNodes
                .IndexOf(_graphView.GetVariableNodes.FirstOrDefault(v => v.uid == (string)value));

            // Allow OnVariableChange events to start with just any existing index
            if (_variableNodeType == VariableNodeType.Change && originalIndex == -1) originalIndex = 0;
            
            int currentIndex = _graphView.GetVariableNames.FindIndex(s => s == _graphView.GetVariableNames[originalIndex]);

            if (currentIndex < 0)
            {
                Debug.LogWarning($"Node {name} didn't have a variable assigned, removing");
                _graphView.RemoveNodeAndData(data);
                return;
            }

            // Create popup, set current value and set function to update data when it's changed.
            if (_variablePopupField == null)
            {
                // First time creating, just add it
                _variablePopupField = new EditorUI.PopupField<string>(options, currentIndex);
                inputContainer.Add(_variablePopupField);
            }
            else
            {
                // Remaking it - remove the old one, at the new one at its previous location
                int index = inputContainer.IndexOf(_variablePopupField);
                _variablePopupField.RemoveFromHierarchy();
                _variablePopupField = new EditorUI.PopupField<string>(options, currentIndex);
                inputContainer.Insert(index, _variablePopupField);
            }
#if UNITY_2019_3_OR_NEWER
            _variablePopupField.RegisterValueChangedCallback(
#else
            _variablePopupField.OnValueChanged(
#endif
                (e) =>
                {
                    // Ensure we've selected an existing variable
                    if (_variablePopupField.index < options.Count)
                    {
                        int trueIndex = _graphView.GetVariableNames.FindIndex(s => s == _variablePopupField.text);

                        // not currently using event value, which is variable name. Instead using legacy method of comparing index to graph variable nodes array index
                        string newUid = _graphView.GetVariableNodes[trueIndex].uid;
                        // Get Variable nodes only have one entry, so index is 0 below
                        SetNewValue(newUid, 0);
                            
                        this.Reload(); // Didn't want to do this, but can't get flows to restore otherwise Ideally, we call RefreshTitle(), LayoutPorts(), RestoreConnections(), RestoreFlows()
                    }
            });

            string startingUid = _graphView.GetVariableNodes[originalIndex].uid;
            SetNewValue(startingUid, 0);
        }

        public void SetNewValue(object newValue, int index, Type inType = null)
        {
            data.nodeValues[index] = SerializableObjectContainer.Serialize(newValue, inType);
        }

        private void SetupOutPorts()
        {
            portsOut = new Dictionary<int, UdonPort>();
            for (int i = 0; i < definition.Outputs.Count; i++)
            {
                var item = definition.Outputs[i];

                // Convert object type to variable type for Get_Variable nodes, or run them through the SlotTypeConverter for all other nodes
                Type type = (_variableNodeType == VariableNodeType.Get || _variableNodeType == VariableNodeType.Change)
                    ? GetTypeForDefinition(definition)
                    : UdonGraphExtensions.SlotTypeConverter(item.type, definition.fullName);

                string label = UdonGraphExtensions.FriendlyTypeName(type).FriendlyNameify();
                if (label == "IUdonEventReceiver")
                {
                    label = "UdonBehaviour";
                }

                if (item.name != null) label = $"{label} {item.name}";
                UdonPort port = (UdonPort) UdonPort.Create(label, Direction.Output, this, type, data, i);
                outputContainer.Add(port);
                portsOut[i] = port;
            }
        }

        private void SetupInPorts()
        {
            portsIn = new Dictionary<int, UdonPort>();

            // Expand node data to hold values for all inputs
            data.Resize(definition.Inputs.Count);

            int startIndex = 0;
            // Skip first input for Set_Variable since that's the eventName which is set via dropdown
            if (name.CompareTo("Set_Variable") == 0)
            {
                startIndex = 1;
            }
            // Skip first input for Set_ReturnValue since that's the special variable
            if (name.CompareTo("Set_ReturnValue") == 0)
            {
                startIndex = 1;
            }

            // Skip inputs for Null and This nodes
            if (name.Contains("Const_Null") || name.Contains("Const_This"))
            {
                return;
            }

            for (int index = startIndex; index < definition.Inputs.Count; index++)
            {
                UdonNodeParameter input = definition.Inputs[index];
                string label = "";
                // TODO: Ask Cubed what this does? Or figure it out.
                if (definition.Inputs.Count > index && index >= 0)
                {
                    label = definition.Inputs[index].name;
                }

                if (label == "IUdonEventReceiver")
                {
                    label = "UdonBehaviour";
                }

                label = label.FriendlyNameify();
                string typeName = UdonGraphExtensions.FriendlyTypeName(input.type);

                // skip over types with pointers. Should remove these from included overloads in the first place!
                if (typeName.Contains('*'))
                {
                    continue;
                }

                // Convert object type to variable type for Set_Variable nodes, or run them through the SlotTypeConverter for all other nodes
                Type type = (_variableNodeType == VariableNodeType.Set && index == 1)
                    ? type = GetTypeForDefinition(definition)
                    : UdonGraphExtensions.SlotTypeConverter(input.type, definition.fullName);
                
                if (_variableNodeType == VariableNodeType.Set && index == 2)
                {
                    AddToClassList("send-change");
                }
                
                // not 100% sure if I should use label or typeName here
                UdonPort p = UdonPort.Create(label, Direction.Input, this, type, data, index) as UdonPort;
                inputContainer.Add(p);
                portsIn.Add(index, p);
            }
        }

        private Type GetTypeForDefinition(UdonNodeDefinition udonNodeDefinition)
        {
            string targetUid = data.nodeValues[0].Deserialize().ToString();
            UdonNodeData varData = _graphView.GetVariableNodes.Where(n => n.uid == targetUid).FirstOrDefault();
            if (varData != null)
            {
                var targetDefinition = UdonEditorManager.Instance.GetNodeDefinition(varData.fullName);
                if (targetDefinition != null)
                {
                    return UdonGraphExtensions.SlotTypeConverter(targetDefinition.type, udonNodeDefinition.fullName);
                }
            }

            // if we fail, return generic object type
            return typeof(object);
        }

        private void SetupFlowPorts()
        {
            if (definition.flow)
            {
                portsFlowIn = new List<UdonPort>();
                portsFlowOut = new List<UdonPort>();

                string label = "";

                int inFlowIndex = -1;
                int outFlowIndex = -1;
                // don't add input flow for events, they're called from above
                if (!definition.fullName.StartsWith("Event_"))
                {
                    label = definition.inputFlowNames.Count > 0 ? definition.inputFlowNames[0] : "";
                    AddFlowPort(Direction.Input, label, ++inFlowIndex);
                }

                // add output flow
                label = definition.outputFlowNames.Count > 0 ? definition.outputFlowNames[0] : "";
                AddFlowPort(Direction.Output, label, ++outFlowIndex);
                if (_specialFlows.Contains(definition.fullName))
                {
                    label = definition.outputFlowNames.Count > 1 ? definition.outputFlowNames[1] : "";
                    AddFlowPort(Direction.Output, label, ++outFlowIndex);
                }

                // Add the number of output flows we need for a Block
                if (definition.fullName == "Block")
                {
                    data.flowUIDs = data.flowUIDs.Where(f => !string.IsNullOrEmpty(f)).ToArray();
                    int connectedFlows = data.flowUIDs.Length;
                    if (connectedFlows > 1)
                    {
                        for (int i = 0; i < connectedFlows - 1; i++)
                        {
                            AddFlowPort(Direction.Output, "", ++outFlowIndex);
                        }
                    }
                }
            }
        }

        private void AddFlowPort(Direction d, string label, int index)
        {
            UdonPort p = (UdonPort) UdonPort.Create(label, d, this, null, data, index);
            p.AddToClassList("flow");
            if(d == Direction.Input)
            {
                inputContainer.Add(p);
                portsFlowIn.Add(p);
            }
            else
            {
                outputContainer.Add(p);
                portsFlowOut.Add(p);
            }
        }

        private bool HasRecursiveFlow(Port fromSlot, Port toSlot)
        {
            // No need to check connections to value slots
            if (toSlot.portType != null) return false;

            // Check out ports of node being connected TO
            foreach (var port in (toSlot.node as UdonNode).portsFlowOut)
            {
                // if any of its ports connect to fromSlot, it's recursive. using foreach for convenience, should be just one edge
                foreach (var edge in port.connections)
                {
                    // if this connection goes to the node that started this all, then it's recursion
                    if(edge.input.node == fromSlot.node)
                    {
                        return true;
                    }

                    // Need to run this recursively to check all ports
                    if(HasRecursiveFlow(fromSlot, edge.input))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        #region IEdgeConnectorListener

        public void OnDrop(EditorGV.GraphView graphView, Edge edge)
        {
            if (edge.output != null && edge.input != null && !HasRecursiveFlow(edge.output, edge.input))
            {
                edge.output.Connect(edge);
                edge.input.Connect(edge);
                graphView.AddElement(edge);

                // Reload block nodes after new connections
                if(definition.fullName == "Block")
                {
                    RestoreFlows();
                }
            }
        }

        public void OnDropOutsidePort(Edge edge, Vector2 position)
        {
            if (!Settings.SearchOnNoodleDrop) return;

            if (edge.output != null && edge.output.portType != null)
            {
                _graphView.OpenPortSearch(edge.output.portType, position, edge.output as UdonPort, Direction.Input);
            }
            else if (edge.input != null && edge.input.portType != null)
            {
                _graphView.OpenPortSearch(edge.input.portType, position, edge.input as UdonPort, Direction.Output);
            }
        }

        #endregion
    }
}
