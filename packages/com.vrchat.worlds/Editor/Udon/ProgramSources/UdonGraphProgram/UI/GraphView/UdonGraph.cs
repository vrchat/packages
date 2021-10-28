#if UNITY_2019_3_OR_NEWER
using UnityEditor.Experimental.GraphView;
using UnityGraph = UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using MenuAction = UnityEngine.UIElements.DropdownMenuAction;
#else
using UnityEditor.Experimental.UIElements.GraphView;
using UnityGraph = UnityEditor.Experimental.UIElements.GraphView;
using UnityEngine.Experimental.UIElements;
using MenuAction = UnityEngine.Experimental.UIElements.DropdownMenu.MenuAction;
#endif
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.Udon.Graph;
using VRC.Udon.Serialization;


namespace VRC.Udon.Editor.ProgramSources.UdonGraphProgram.UI.GraphView
{
    public static class UdonGraphCommands
    {
        public const string Reload = "Reload";
        public const string Compile = "Compile";
    }

    public class UdonGraph : UnityGraph.GraphView
    {
        private GridBackground _background;
        private UdonMinimap _map;
        private UdonVariablesBlackboard _blackboard;

        // copied over from Legacy.UdonGraph,
        public UdonGraphProgramAsset graphProgramAsset;
        public UdonBehaviour udonBehaviour;

        public UdonGraphData graphData
        {
            get => graphProgramAsset.graphData;
            set
            {
                graphProgramAsset.graphData = value;
                EditorUtility.SetDirty(graphProgramAsset);
            }
        }

        // Tracking variables
        private List<UdonNodeData> _variableNodes = new List<UdonNodeData>();
        private ImmutableList<string> _variableNames;

        private Vector2 lastMousePosition;
        private VisualElement mouseTipContainer;
        private TextElement mouseTip;
        private Vector2 mouseTipOffset = new Vector2(20, -22);

        private UdonSearchManager _searchManager;

        private bool _reloading = false;
        
        private bool _dragging = false;

        public bool IsReloading => _reloading;

        // Enable stuff from NodeGraphProcessor
        private UdonGraphWindow _window;

        public ImmutableList<string> GetVariableNames
        {
            get => _variableNames;
            private set { }
        }

        public List<UdonNodeData> GetVariableNodes
        {
            get => _variableNodes;
            private set { }
        }

        public bool IsReservedName(string name)
        {
            return name.StartsWith("__");
        }

        public UdonGraph(UdonGraphWindow window)
        {
            _window = window;

            this.StretchToParentSize();
            SetupBackground();
            SetupMap();
            SetupBlackboard();
            SetupZoom(0.2f, 3);
            SetupDragAndDrop();

            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            mouseTipContainer = new VisualElement()
            {
                name = "mouse-tip-container",
            };
            Add(mouseTipContainer);
            mouseTip = new TextElement()
            {
                name = "mouse-tip",
                visible = true,
            };
            SetMouseTip("");
            mouseTipContainer.Add(mouseTip);

            // This event is used to send commands from updated port fields
            RegisterCallback<ExecuteCommandEvent>(OnExecuteCommand);

            // Save last known mouse position for better pasting. Is there a performance hit for this?
            RegisterCallback<MouseMoveEvent>(OnMouseMove);
            RegisterCallback<KeyDownEvent>(OnKeyDown);

            _searchManager = new UdonSearchManager(this, window);

            graphViewChanged = OnViewChanged;
            serializeGraphElements = OnSerializeGraphElements;
            unserializeAndPaste = OnUnserializeAndPaste;
            canPasteSerializedData = CheckCanPaste;
            viewTransformChanged = OnViewTransformChanged;
        }

        private void OnViewTransformChanged(UnityGraph.GraphView graphView)
        {
            graphProgramAsset.viewTransform.position = this.viewTransform.position;
            graphProgramAsset.viewTransform.scale = this.viewTransform.scale.x;
            EditorUtility.SetDirty(graphProgramAsset);
        }

        private bool CheckCanPaste(string pasteData)
        {
            UdonNodeData[] copiedNodeDataArray;
            try
            {
                copiedNodeDataArray = JsonUtility
                    .FromJson<SerializableObjectContainer.ArrayWrapper<UdonNodeData>>(
                        UdonGraphExtensions.UnZipString(pasteData))
                    .value;
            }
            catch
            {
                //oof ouch that's not valid data
                return false;
            }

            return true;
        }

        public void Initialize(UdonGraphProgramAsset asset, UdonBehaviour udonBehaviour)
        {
            if (graphProgramAsset != null)
            {
                SaveGraphToDisk();
            }

            graphProgramAsset = asset;
            if (udonBehaviour != null)
            {
                this.udonBehaviour = udonBehaviour;
            }
            
            graphData = new UdonGraphData(graphProgramAsset.GetGraphData());

            DoDelayedReload();
            EditorApplication.update += DelayedRestoreViewFromData;

            // When pressing ctrl-s, we save the graph
            EditorSceneManager.sceneSaved += _ => SaveGraphToDisk();
        }

        private void DelayedRestoreViewFromData()
        {
            EditorApplication.update -= DelayedRestoreViewFromData;
            //Todo: restore from saved data instead of FrameAll
#if UNITY_2019_3_OR_NEWER
             FrameAll();
#else
            UpdateViewTransform(graphProgramAsset.viewTransform.position,
                 Vector3.one * graphProgramAsset.viewTransform.scale);
             contentViewContainer.MarkDirtyRepaint();
#endif
        }

        public UdonNode AddNodeFromSearch(UdonNodeDefinition definition, Vector2 position)
        {
            UdonNode node = UdonNode.CreateNode(definition, this);
            AddElement(node);

            node.SetPosition(new Rect(position, Vector2.zero));
            node.Select(this, false);

            return node;
        }

        public void ConnectNodeTo(UdonNode node, UdonPort startingPort, UnityGraph.Direction direction, Type typeToSearch)
        {
            // Find port to connect to
            var collection = direction == Direction.Input ? node.portsIn : node.portsOut;
            UdonPort endPort = collection.FirstOrDefault(p => p.Value.portType == typeToSearch).Value;
            // If found, add edge and serialize the connection in the programAsset
            if(endPort != null)
            {
                // Important not to create and add this edge, we'll restore it below instead
                startingPort.ConnectTo(endPort);
                (startingPort.node as UdonNode).RestoreConnections();
                (endPort.node as UdonNode).RestoreConnections();
                Compile();
            }
        }

        public void SaveGraphToDisk()
        {
            if (graphProgramAsset == null)
                return;
            
            EditorUtility.SetDirty(graphProgramAsset);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.target == this && evt.keyCode == KeyCode.Tab)
            {
                var screenPosition = GUIUtility.GUIToScreenPoint(evt.originalMousePosition);
                nodeCreationRequest(new NodeCreationContext() { screenMousePosition = screenPosition, target = this });
                evt.StopImmediatePropagation();
            }
            else if (evt.keyCode == KeyCode.A && evt.ctrlKey)
            {
                // Select every graph element
                ClearSelection();
                foreach (var element in graphElements.ToList())
                {
                    AddToSelection(element);
                }
            }
            else if (evt.keyCode == KeyCode.G && evt.shiftKey)
            {
                Undo.RecordObject(graphProgramAsset, "Changed Name");
                graphProgramAsset.graphData.name = Guid.NewGuid().ToString();
            }
        }

        public bool GetBlackboardVisible()
        {
            return _blackboard.visible;
        }

        public bool GetMinimapVisible()
        {
            return _map.visible;
        }

        public void ToggleShowVariables(bool value)
        {
            _blackboard.SetVisible(value);
        }

        public void ToggleShowMiniMap(bool value)
        {
            _map.SetVisible(value);
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            if (evt.target is UnityGraph.GraphView || evt.target is UdonNode)
            {
                // Create a Group, enclosing any selected nodes
                evt.menu.AppendAction("Create Group", (m) =>
                {
                    UdonGroup group = UdonGroup.Create("Group", GetRectFromMouse(), this);
                    Undo.RecordObject(graphProgramAsset, "Add Group");
                    AddElement(group);
                    group.UpdateDataId();

                    foreach (ISelectable item in selection)
                    {
                        if (item is UdonNode)
                        {
                            group.AddElement(item as UdonNode);
                        }
                        else if(item is UdonComment)
                        {
                            group.AddElement(item as UdonComment);
                        }
                    }
                    group.Initialize();
                    SaveGraphElementData(group);
                }, MenuAction.AlwaysEnabled);
                var selectedItems = selection.Where(i=>i is UdonNode || i is UdonComment).ToList();
                if (selectedItems.Count > 0)
                {
                    evt.menu.AppendAction("Remove From Group", (m) =>
                    {
                        Undo.RecordObject(graphProgramAsset, "Remove Items from Group");
                        int count = selectedItems.Count;
                        for (int i = count - 1; i >=0; i--)
                        {
                            if(selectedItems.ElementAt(i) is UdonNode)
                            {
                                UdonNode node = selectedItems.ElementAt(i) as UdonNode;
                                if (node.group != null)
                                {
                                    node.group.RemoveElement(node);
                                }
                            }
                            else if (selectedItems.ElementAt(i) is UdonComment)
                            {
                                UdonComment comment = selectedItems.ElementAt(i) as UdonComment;
                                if (comment.group != null)
                                {
                                    comment.group.RemoveElement(comment);
                                }
                            }
                        }
                        
                    }, MenuAction.AlwaysEnabled);
                }

                // Create a Comment
                evt.menu.AppendAction("Create Comment", (m) =>
                {
                    UdonComment comment = UdonComment.Create("Comment", GetRectFromMouse(), this);
                    Undo.RecordObject(graphProgramAsset, "Add Comment");
                    AddElement(comment);
                }, MenuAction.AlwaysEnabled);

                evt.menu.AppendSeparator();
            }

            base.BuildContextualMenu(evt);
        }

        private Rect GetRectFromMouse()
        {
            return new Rect(contentViewContainer.WorldToLocal(lastMousePosition), Vector2.zero);
        }

        private void OnMouseMove(MouseMoveEvent evt)
        {
            lastMousePosition = evt.mousePosition;
            MoveMouseTip(lastMousePosition);
        }

        private void MoveMouseTip(Vector2 position)
        {
            if (mouseTipContainer.visible)
            {
                var newLayout = mouseTipContainer.layout;
                newLayout.position = position + mouseTipOffset;
#if UNITY_2019_3_OR_NEWER
                mouseTipContainer.transform.position = newLayout.position;
#else
                mouseTipContainer.layout = newLayout;
#endif
            }
        }

        public bool IsDuplicateEventNode(string fullName, string uid = "")
        {
            if (fullName.StartsWith("Event_") &&
                (fullName != "Event_Custom")
            )
            {
                if (this.Query(fullName).ToList().Count > 0)
                {
                    Debug.LogWarning(
                            $"Can't create more than one {fullName} node, try managing your flow with a Block node instead!");
                    return true;
                }
            }
            else if(fullName.StartsWith(Common.VariableChangedEvent.EVENT_PREFIX))
            {
                bool isDuplicate = graphData.EventNodes.Any(d =>
                    d.nodeValues.Length > 0 && d.nodeValues[0].Deserialize().ToString() == uid);
                if (isDuplicate)
                {
                    Debug.LogWarning(
                        $"Can't create more than one Change Event for {GetVariableName(uid)}, try managing your flow with a Block node instead!");
                }
                return isDuplicate;
            }

            return false;
        }

        private string OnSerializeGraphElements(IEnumerable<GraphElement> selection)
        {
            Bounds bounds = new Bounds();
            bool startedBounds = false;
            List<UdonNodeData> nodeData = new List<UdonNodeData>();
            List<UdonNodeData> variables = new List<UdonNodeData>();
            foreach (var item in selection)
            {
                // Only serializing UdonNode for now
                if (item is UdonNode)
                {
                    UdonNode node = (UdonNode)item;
                    // Calculate bounding box to enclose all items
                    if (!startedBounds)
                    {
                        bounds = new Bounds(node.data.position, Vector3.zero);
                        startedBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(node.data.position);
                    }

                    // Handle Get/Set Variables
                    if (node.data.fullName == "Get_Variable" || node.data.fullName == "Set_Variable" || node.data.fullName == "Set_ReturnValue")
                    {
                        // make old-school get-variable node data from existing variable
                        var targetUid = node.data.nodeValues[0].Deserialize();
                        var matchingNode = GetVariableNodes.First(v => v.uid == (string)targetUid);
                        if (matchingNode != null && !variables.Contains(matchingNode))
                        {
                            variables.Add(matchingNode);
                        }
                    }

                    nodeData.Add(new UdonNodeData(node.data));
                }
            }

            // Add variables at beginning of list so they get created first
            nodeData.InsertRange(0, variables);

            // Go through each item and offset its position by the center of the group (normalizes the coordinates around 0,0)
            var offset = new Vector2(bounds.center.x, bounds.center.y);
            foreach (UdonNodeData data in nodeData)
            {
                var ogPosition = data.position;
                data.position -= offset;
            }

            string result = UdonGraphExtensions.ZipString(JsonUtility.ToJson(
                new SerializableObjectContainer.ArrayWrapper<UdonNodeData>(nodeData.ToArray())));

            return result;
        }

        private void OnUnserializeAndPaste(string operationName, string pasteData)
        {
            ClearSelection();

            UdonNodeData[] copiedNodeDataArray;
            // Note: CheckCanPaste already does this check but it doesn't cost much to do it twice
            try
            {
                copiedNodeDataArray = JsonUtility
                    .FromJson<SerializableObjectContainer.ArrayWrapper<UdonNodeData>>(
                        UdonGraphExtensions.UnZipString(pasteData))
                    .value;
            }
            catch
            {
                //oof ouch that's not valid data
                return;
            }

            var copiedNodeDataList = new List<UdonNodeData>();
            // Add new variables if needed
            for (int i = 0; i < copiedNodeDataArray.Length; i++)
            {
                if (copiedNodeDataArray[i].fullName.StartsWith("Variable_"))
                {
                    if (!graphData.nodes.Exists(n => n.uid == copiedNodeDataArray[i].uid))
                    {
                        // set graph to this one in case it was pasted from somewhere else
                        copiedNodeDataArray[i].SetGraph(graphData);
                        
                        // check for conflicting variable names
                        int nameIndex = (int)UdonParameterProperty.ValueIndices.name;
                        string varName = (string)copiedNodeDataArray[i].nodeValues[nameIndex].Deserialize();
                        if (GetVariableNames.Contains(varName))
                        {
                            // if we already have a variable with that name, find a new name and serialize it into the data
                            varName = GetUnusedVariableNameLike(varName);
                            copiedNodeDataArray[i].nodeValues[nameIndex] =
                                SerializableObjectContainer.Serialize(varName);
                        }

                        _blackboard.AddFromData(copiedNodeDataArray[i]);
                        graphData.nodes.Add(copiedNodeDataArray[i]);
                    }
                }
                else if(IsDuplicateEventNode(copiedNodeDataArray[i].fullName))
                {
                    // don't add duplicate event nodes
                }
                else
                {
                    copiedNodeDataList.Add(copiedNodeDataArray[i]);
                }
            }

            // Remove duplicate events
            RefreshVariables(false);

            // copy modified list back to array
            copiedNodeDataArray = copiedNodeDataList.ToArray();

            _reloading = true;
            var graphMousePosition = GetRectFromMouse().position;
            List<UdonNode> pastedNodes = new List<UdonNode>();
            Dictionary<string, string> uidMap = new Dictionary<string, string>();
            UdonNodeData[] newNodeDataArray = new UdonNodeData[copiedNodeDataArray.Length];

            for (int i = 0; i < copiedNodeDataArray.Length; i++)
            {
                UdonNodeData nodeData = copiedNodeDataArray[i];
                newNodeDataArray[i] = new UdonNodeData(graphData, nodeData.fullName)
                {
                    position = nodeData.position + graphMousePosition,
                    uid = Guid.NewGuid().ToString(),
                    nodeUIDs = new string[nodeData.nodeUIDs.Length],
                    nodeValues = nodeData.nodeValues,
                    flowUIDs = new string[nodeData.flowUIDs.Length]
                };

                uidMap.Add(nodeData.uid, newNodeDataArray[i].uid);
            }

            for (int i = 0; i < copiedNodeDataArray.Length; i++)
            {
                UdonNodeData nodeData = copiedNodeDataArray[i];
                UdonNodeData newNodeData = newNodeDataArray[i];
                
                // Set the new node to point at this graph if it came from a different one
                newNodeData.SetGraph(graphData);

                for (int j = 0; j < newNodeData.nodeUIDs.Length; j++)
                {
                    if (uidMap.ContainsKey(nodeData.nodeUIDs[j].Split('|')[0]))
                    {
                        newNodeData.nodeUIDs[j] = uidMap[nodeData.nodeUIDs[j].Split('|')[0]];
                    }
                }

                for (int j = 0; j < newNodeData.flowUIDs.Length; j++)
                {
                    if (uidMap.ContainsKey(nodeData.flowUIDs[j].Split('|')[0]))
                    {
                        newNodeData.flowUIDs[j] = uidMap[nodeData.flowUIDs[j].Split('|')[0]];
                    }
                }

                UdonNode udonNode = UdonNode.CreateNode(newNodeData, this);
                if (udonNode != null)
                {
                    graphData.nodes.Add(newNodeData);
                    AddElement(udonNode);
                    pastedNodes.Add(udonNode);
                }
            }

            // Select all newly-pasted nodes after reload
            foreach (var item in pastedNodes)
            {
                item.RestoreConnections();
                item.BringToFront();
                AddToSelection(item as GraphElement);
            }
            
            _reloading = false;
            Compile();
        }

        // This is needed to properly clear the selection in some cases (like deleting a stack node) even though it doesn't appear to do anything
        public override void ClearSelection()
        {
            base.ClearSelection();
        }

        public void MarkSceneDirty()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }
        }

        private GraphViewChange OnViewChanged(GraphViewChange changes)
        {
            bool dirty = false;
            bool needsVariableRefresh = false;
            // Remove node from Data when removed from Graph
            if (!_reloading && changes.elementsToRemove != null && changes.elementsToRemove.Count > 0)
            {

                foreach (var element in changes.elementsToRemove)
                {
                    if (element is UdonNode)
                    {
                        var nodeData = ((UdonNode)element).data;
                        RemoveNodeAndData(nodeData);
                        continue;
                    }

                    if (element is Edge)
                    {
                        Undo.RecordObject(graphProgramAsset, $"delete-{element.name}");
                        continue;
                    }

                    if (element is UdonParameterField)
                    {
                        needsVariableRefresh = true;
                        
                        var pField = element as UdonParameterField;
                        if (graphData.nodes.Contains(pField.Data))
                        {
                            RemoveNodeAndData(pField.Data);
                        }
                    }

                    if (element is IUdonGraphElementDataProvider)
                    {
                        Undo.RecordObject(graphProgramAsset, $"delete-{element.name}");
                        var provider = (IUdonGraphElementDataProvider) element;
                        DeleteGraphElementData(provider, false);
                        RemoveElement(element);
                    }
                }

                ClearSelection();
                dirty = true;
            }

            if (dirty)
            {
                MarkSceneDirty();
                SaveGraphToDisk();
            }

            if (needsVariableRefresh)
            {
                RefreshVariables(true);
            }

            return changes;
        }

        public void DoDelayedCompile()
        {
            EditorApplication.update += DelayedCompile;
        }

        private void DelayedCompile()
        {
            EditorApplication.update -= DelayedCompile;
            graphProgramAsset.RefreshProgram();
        }
        
        private bool _waitingToReload;
        public void DoDelayedReload()
        {
            if (!_waitingToReload && !_reloading)
            {
                _waitingToReload = true;
                EditorApplication.update += DelayedReload;
            }
        }
        
        void DelayedReload()
        {
            _waitingToReload = false;
            EditorApplication.update -= DelayedReload;
            Reload();
        }

        private void SetupBackground()
        {
            _background = new GridBackground
            {
                name = "bg"
            };
            Insert(0, _background);
            _background.StretchToParentSize();
        }

        private void SetupBlackboard()
        {
            _blackboard = new UdonVariablesBlackboard(this);

            _blackboard.addItemRequested = BlackboardAddVariable;
            _blackboard.editTextRequested = BlackboardEditVariableName;
            _blackboard.SetPosition(new Rect(10, 130, 200, 150));
            Add(_blackboard);
        }

        private void BlackboardEditVariableName(Blackboard b, VisualElement v, string newValue)
        {
            UdonParameterField field = (UdonParameterField) v;
            Undo.RecordObject(graphProgramAsset, "Rename Variable");
            
            // Sanitize value for variable name
            string newVariableName = newValue.SanitizeVariableName();
            newVariableName = GetUnusedVariableNameLike(newVariableName);
            field.Data.nodeValues[(int)UdonParameterProperty.ValueIndices.name] = SerializableObjectContainer.Serialize(newVariableName);
            field.text = newVariableName;
            
            // Find all nodes that are getters/setters for this variable
            // Change their title text by hand
            nodes.ForEach((node =>
            {
                UdonNode udonNode = (UdonNode) node;
                if (udonNode != null && udonNode.IsVariableNode)
                {
                    udonNode.RefreshTitle();
                }
            }));
            
            RefreshVariables(true);
        }

        private void BlackboardAddVariable(Blackboard obj)
        {
            var screenPosition = GUIUtility.GUIToScreenPoint(lastMousePosition);
            _searchManager.OpenVariableSearch(screenPosition);
        }

        public void OpenPortSearch(Type type, Vector2 position, UdonPort output, Direction direction)
        {
            _searchManager.OpenPortSearch(type, position, output, direction);
        }

        private void SetupMap()
        {
            _map = new UdonMinimap(this);
            Add(_map);
        }

        private void OnExecuteCommand(ExecuteCommandEvent evt)
        {
            switch (evt.commandName)
            {
                case UdonGraphCommands.Reload:
                    DoDelayedReload();
                    break;
                case UdonGraphCommands.Compile:
                    Compile();
                    break;
                default:
                    break;
            }
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            var result = ports.ToList().Where(
                    port => port.direction != startPort.direction
                    && port.node != startPort.node
                    && port.portType.IsReallyAssignableFrom(startPort.portType)
                    && (port.capacity == Port.Capacity.Multi || port.connections.Count() == 0)
                ).ToList();
            return result;
        }

#if UNITY_2019_3_OR_NEWER
        private StyleSheet neonStyle = (StyleSheet) Resources.Load("UdonGraphNeonStyle");
#endif

        public void Reload()
        {
            if (_reloading) return;
            
            _reloading = true;

#if UNITY_2019_3_OR_NEWER

            if (Settings.UseNeonStyle && !styleSheets.Contains(neonStyle))
            {
                styleSheets.Add(neonStyle);
            }
            else if (!Settings.UseNeonStyle && !styleSheets.Contains(neonStyle))
            {
                styleSheets.Remove(neonStyle);
            }
#else
            string customStyle = "UdonGraphNeonStyle";
            if (Settings.UseNeonStyle && !HasStyleSheetPath(customStyle))
            {
                AddStyleSheetPath(customStyle);
            }
            else if (!Settings.UseNeonStyle && HasStyleSheetPath(customStyle))
            {
                RemoveStyleSheetPath(customStyle);
            }
#endif
            Undo.undoRedoPerformed -=
                OnUndoRedo; //Remove old handler if present to prevent duplicates, doesn't cause errors if not present
            Undo.undoRedoPerformed += OnUndoRedo;

            // Clear out Blackboard here
            _blackboard.Clear();
            
            // clear existing elements, probably need to update to only clear nodes and edges
            DeleteElements(graphElements.ToList());

            RefreshVariables(false);

            List<UdonNodeData> nodesToDelete = new List<UdonNodeData>();
            // add all nodes to graph
            for (int i = 0; i < graphData.nodes.Count; i++)
            {
                UdonNodeData nodeData = graphData.nodes[i];

                // Check for Node type - create nodes, separate out Variables
                if (nodeData.fullName.StartsWithCached("Variable_"))
                {
                    _blackboard.AddFromData(nodeData);
                }
                else if (nodeData.fullName.StartsWithCached("Comment"))
                {
                    // one way conversion from Comment Node > Comment Group
                    var commentString = nodeData.nodeValues[0].Deserialize();
                    if (commentString != null)
                    {
                        var comment = UdonComment.Create((string) commentString,
                            new Rect(nodeData.position, Vector2.zero), this);
                        AddElement(comment);
                        SaveGraphElementData(comment);
                    }

                    // Remove from data, no longer a node
                    nodesToDelete.Add(nodeData);
                }
                else
                {
                    try
                    {
                        UdonNode udonNode = UdonNode.CreateNode(nodeData, this);
                        AddElement(udonNode);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Error Loading Node {nodeData.fullName} : {e.Message}");
                        nodesToDelete.Add(nodeData);
                        continue;
                    }
                }
            }

            // Delete old comments and data that could not be turned into an UdonNode
            foreach (UdonNodeData nodeData in nodesToDelete)
            {
                if (graphData.nodes.Remove(nodeData))
                {
                    Debug.Log($"removed node {nodeData.fullName}");
                }
            }

            // reconnect nodes
            nodes.ForEach((genericNode) =>
            {
                UdonNode udonNode = (UdonNode)genericNode;
                udonNode.RestoreConnections();
            });
            
            // Add all Graph Elements
            if (graphProgramAsset.graphElementData != null)
            {
                var orderedElements = graphProgramAsset.graphElementData.ToList().OrderByDescending(e => e.type);
                foreach (var elementData in orderedElements)
                {
                    GraphElement element = RestoreElementFromData(elementData);
                    if (element != null)
                    {
                        AddElement(element);
                        if (element is UdonGroup group)
                        {
                            group.Initialize();
                        }
                    }
                }
            }
            
            _reloading = false;
            Compile();
        }

        // TODO: create generic to restore any supported element from UdonGraphElementData?
        private GraphElement RestoreElementFromData(UdonGraphElementData data)
        {
            switch (data.type)
            {
                case UdonGraphElementType.GraphElement:
                    {
                        return null;
                    }

                case UdonGraphElementType.UdonGroup:
                    {
                        return UdonGroup.Create(data, this);
                    }

                case UdonGraphElementType.UdonComment:
                    {
                        return UdonComment.Create(data, this);
                    }
                case UdonGraphElementType.Minimap:
                    {
                        _map.LoadData(data);
                        return null;
                    }
                case UdonGraphElementType.VariablesWindow:
                    {
                        _blackboard.LoadData(data);
                        return null;
                    }
                default:
                    return null;
            }
        }

        private void OnUndoRedo()
        {
            Reload();
        }

        public void RefreshVariables(bool recompile = true)
        {
            // we want internal variables at the end of the list so they can be trivially filtered out
            _variableNodes = graphData.nodes
                .Where(n => n.fullName.StartsWithCached("Variable_"))
                .Where(n => n.nodeValues.Length > 1 && n.nodeValues[1] != null)
                .OrderBy(n => ((string)n.nodeValues[1].Deserialize()).StartsWith("__"))
                .ToList();
            _variableNames = ImmutableList.Create(
                _variableNodes.Select(s => (string) s.nodeValues[1].Deserialize()).ToArray()
            );

            // Refresh variable options in popup
            nodes.ForEach(node =>
            {
                if (node is UdonNode udonNode && udonNode.IsVariableNode)
                {
                    udonNode.RefreshVariablePopup();
                }
            });
            
            // We usually want to compile after a Refresh
            if(recompile)
                Compile();
            DoDelayedReload();
        }

        // Returns UID of newly created variable
        public string AddNewVariable(string typeName = "Variable_SystemString", string variableName = "",
            bool isPublic = false)
        {
            // Figure out unique variable name to use
            string newVariableName = string.IsNullOrEmpty(variableName) ? "newVariable" : variableName;
            newVariableName = GetUnusedVariableNameLike(newVariableName);

            string newVarUid = Guid.NewGuid().ToString();
            UdonNodeData newNodeData = new UdonNodeData(graphData, typeName)
            {
                uid = newVarUid,
                nodeUIDs = new string[5],
                nodeValues = new[]
                                {
                    SerializableObjectContainer.Serialize(default),
                    SerializableObjectContainer.Serialize(newVariableName, typeof(string)),
                    SerializableObjectContainer.Serialize(isPublic, typeof(bool)),
                    SerializableObjectContainer.Serialize(false, typeof(bool)),
                    SerializableObjectContainer.Serialize("none", typeof(string))
                },
                position = Vector2.zero
            };

            graphData.nodes.Add(newNodeData);
            _blackboard.AddFromData(newNodeData);
            RefreshVariables(true);
            return newVarUid;
        }

        public void RemoveNodeAndData(UdonNodeData nodeData)
            {
            Undo.RecordObject(graphProgramAsset, $"Removing {nodeData.fullName}");

            if (nodeData.fullName.StartsWithCached("Variable_"))
                {
                var allVariableNodes = new HashSet<Node>();
                // Find all get/set variable nodes that reference this node
                nodes.ForEach((graphNode =>
                {
                    UdonNode udonNode = graphNode as UdonNode;
                    if (udonNode != null && udonNode.IsVariableNode)
                    {
                        // Get variable uid and recursively remove all nodes that refer to it
                        var values = udonNode.data.nodeValues[0].stringValue.Split('|');
                        if (values.Length > 1)
                {
                            string targetVariable = values[1];
                            if (targetVariable.CompareTo(nodeData.uid) == 0)
                {
                                // We have a match! Delete this node
                                allVariableNodes.Add(graphNode);
                                RemoveNodeAndData(udonNode.data);
                    }
                }
                }
                }));

                // remove each edge connected to a Get/Set Variable node which will be deleted
                edges.ForEach(edge =>
                {
                    if (allVariableNodes.Contains(edge.input.node) || allVariableNodes.Contains(edge.output.node))
                    {
                        (edge.output as UdonPort)?.Disconnect(edge);
                        (edge.input as UdonPort)?.Disconnect(edge);
                        RemoveElement(edge);
                    }
                });
                
                // remove from existing blackboard
                _blackboard.RemoveByID(nodeData.uid);
                RefreshVariables(true);
            }
            
            UdonNode node = (UdonNode)GetNodeByGuid(nodeData.uid);
            if (node != null)
                            {
                node.RemoveFromHierarchy();
                    }

            if (graphData.nodes.Contains(nodeData))
                {
                graphData.nodes.Remove(nodeData);
            }
            }

        public void Compile()
            {
            UdonEditorManager.Instance.QueueAndRefreshProgram(graphProgramAsset);
        }

        private bool ShouldUpdateAsset => !IsReloading && graphProgramAsset != null;

        private readonly HashSet<UdonGraphElementType> singleElementTypes = new HashSet<UdonGraphElementType>()
        {
            UdonGraphElementType.Minimap, UdonGraphElementType.VariablesWindow
        };
        
        public void SaveGraphElementData(IUdonGraphElementDataProvider provider)
        {
            if (ShouldUpdateAsset)
            {
                UdonGraphElementData newData = provider.GetData();
                if (graphProgramAsset.graphElementData == null)
                {
                    graphProgramAsset.graphElementData = new UdonGraphElementData[0];
                }
                
                int index = -1;
                // Some elements like minimap and variables window should only ever have one entry, so find by type
                if (singleElementTypes.Contains(newData.type))
                {
                    index = Array.FindIndex(graphProgramAsset.graphElementData, e => e.type == newData.type);
                }
                // other elements can have multiples, so find by uid
                else
                {
                    index = Array.FindIndex(graphProgramAsset.graphElementData, e => e.uid == newData.uid);
                }
                if (index > -1)
                {
                    // Update
                    graphProgramAsset.graphElementData[index] = newData;
                }
                else
                {
                    // Add
                    int arrayLength = graphProgramAsset.graphElementData.Length;
                    Array.Resize(ref graphProgramAsset.graphElementData, arrayLength+1);
                    graphProgramAsset.graphElementData[arrayLength] = newData;
                }
                SaveGraphToDisk();
            }
        }

        public void DeleteGraphElementData(IUdonGraphElementDataProvider provider, bool save = true)
        {
            int index = Array.FindIndex(graphProgramAsset.graphElementData, e => e.uid == provider.GetData().uid);
            // remove if found
            if (index > -1)
            {
                graphProgramAsset.graphElementData = graphProgramAsset.graphElementData.Where((source, i) => i != index).ToArray();
            }

            if (save)
            {
                SaveGraphToDisk();
            }
        }

        #region Drag and Drop Support

        private void SetupDragAndDrop()
        {
            RegisterCallback<DragEnterEvent>(OnDragEnter);
            RegisterCallback<DragPerformEvent>(OnDragPerform, TrickleDown.TrickleDown);
            RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            RegisterCallback<DragExitedEvent>(OnDragExited);
            RegisterCallback<DragLeaveEvent>((e)=>OnDragExited(null));
        }

        private void OnDragEnter(DragEnterEvent e)
        {
            OnDragEnter(e.mousePosition, e.ctrlKey, e.altKey);
        }

        private void OnDragEnter(Vector2 mousePosition, bool ctrlKey, bool altKey)
        {
            MoveMouseTip(mousePosition);

            var dragData = DragAndDrop.GetGenericData("DragSelection") as List<ISelectable>;
            _dragging = false;

            if (dragData != null)
            {
                // Handle drag from exposed parameter view
                if (dragData.OfType<UdonParameterField>().Any())
                {
                    _dragging = true;
                    string tip = "Get Variable\n+Ctrl: Set Variable\n+Alt: On Var Change";
                    if (ctrlKey)
                    {
                        tip = "Set Variable";
                    } else if (altKey)
                    {
                        tip = "On Variable Changed";
                    }
                    SetMouseTip(tip);
                }
            }

            if (DragAndDrop.objectReferences.Length == 1 && DragAndDrop.objectReferences[0] != null)
            {
                var target = DragAndDrop.objectReferences[0];
                switch (target)
                {
                    case GameObject g:
                    case Component c:
                    {
                        string type = GetDefinitionNameForType(target.GetType());
                        if (UdonEditorManager.Instance.GetNodeDefinition(type) != null)
                        {
                            _dragging = true;
                        }
                        break;
                    }
                }
            }

            if (_dragging)
            {
                DragAndDrop.visualMode = ctrlKey ? DragAndDropVisualMode.Link : DragAndDropVisualMode.Copy;
            }
        }

        private void OnDragUpdated(DragUpdatedEvent e)
        {
            if (_dragging)
            {
                MoveMouseTip(e.mousePosition);
                DragAndDrop.visualMode = e.ctrlKey ? DragAndDropVisualMode.Link : DragAndDropVisualMode.Copy;
            }
            else
            {
                OnDragEnter(e.mousePosition, e.ctrlKey, e.altKey);
            }
        }

        private void OnDragPerform(DragPerformEvent e)
        {
            if (!_dragging) return;
            var graphMousePosition = this.contentViewContainer.WorldToLocal(e.mousePosition);
            var draggedVariables = DragAndDrop.GetGenericData("DragSelection") as List<ISelectable>;

            if (draggedVariables != null)
            {
                // Handle Drop of Variables
                var parameters = draggedVariables.OfType<UdonParameterField>();
                if (parameters.Any())
                {
                    RefreshVariables(false);
                    VariableNodeType nodeType = VariableNodeType.Getter;
                    if (e.ctrlKey) nodeType = VariableNodeType.Setter;
                    else if (e.altKey) nodeType = VariableNodeType.Change;
                    foreach (var parameter in parameters)
                    {
                        UdonNode udonNode = MakeVariableNode(parameter.Data.uid, graphMousePosition, nodeType);
                        if (udonNode != null)
                        {
                            AddElement(udonNode);
                        }
                    }
                    RefreshVariables(true);
                }
            }

            // Handle Drop of single GameObjects and Assets
            if (DragAndDrop.objectReferences.Length == 1 && DragAndDrop.objectReferences[0] != null)
            {
                var target = DragAndDrop.objectReferences[0];
                switch (target)
                {
                    case Component c:
                        SetupDraggedObject(c, graphMousePosition);
                    break;

                    case GameObject g:
                        SetupDraggedObject(g, graphMousePosition);
                    break;
                }
            }

            _dragging = false;
        }

        private void OnDragExited(DragExitedEvent e)
        {
            SetMouseTip("");
            _dragging = false;
        }

        #endregion
        public enum VariableNodeType
        {
            Getter,
            Setter,
            Return,
            Change,
        }

        public string GetVariableName(string uid)
        {
            var targetNode = GetVariableNodes.Where(n => n.uid == uid).First();
            try
            {
                return targetNode.nodeValues[1].Deserialize() as string;
            }
            catch (Exception e)
            {
                Debug.LogError($"Couldn't find variable name for {uid}: {e.Message}");
                return "";
            }
        }

        public UdonNode MakeVariableNode(string selectedUid, Vector2 graphMousePosition, VariableNodeType nodeType)
        {
            string definitionName = "";
            switch (nodeType)
            {
                case VariableNodeType.Getter:
                    definitionName = "Get_Variable";
                    break;
                case VariableNodeType.Setter:
                    definitionName = "Set_Variable";
                    break;
                case VariableNodeType.Return:
                    definitionName = "Set_ReturnValue";
                    break;
                case VariableNodeType.Change:
                    definitionName = "Event_OnVariableChange";
                    break;
            }

            if (nodeType == VariableNodeType.Change)
            {
                string variableName = GetVariableName(selectedUid);
                if (!string.IsNullOrWhiteSpace(variableName))
                {
                    string eventName = UdonGraphExtensions.GetVariableChangeEventName(variableName);
                    if (IsDuplicateEventNode(eventName, selectedUid))
                    {
                        return null;
                    }
                }
            }

            var definition = UdonEditorManager.Instance.GetNodeDefinition(definitionName);
            var nodeData = this.graphData.AddNode(definition.fullName);
            nodeData.nodeValues = new SerializableObjectContainer[2];
            nodeData.nodeUIDs = new string[1];
            nodeData.nodeValues[0] = SerializableObjectContainer.Serialize(selectedUid);
            nodeData.position = graphMousePosition;

            Undo.RecordObject(graphProgramAsset, "Add Variable");
            var udonNode = UdonNode.CreateNode(nodeData, this);
            return udonNode;
        }

        public string GetUnusedVariableNameLike(string newVariableName)
        {
            RefreshVariables(false);

            while (GetVariableNames.Contains(newVariableName))
            {
                char lastChar = newVariableName[newVariableName.Length - 1];
                if(char.IsDigit(lastChar))
                {
                    string newLastChar = (int.Parse(lastChar.ToString()) + 1).ToString();
                    newVariableName = newVariableName.Substring(0, newVariableName.Length - 1) + newLastChar;
                } 
                else
                {
                    newVariableName = $"{newVariableName}_1";   
                }
            }

            return newVariableName;
        }

        private void SetMouseTip(string message)
        {
            if (mouseTipContainer.visible)
            {
                mouseTip.text = message;
            }
        }

        private void LinkAfterCompile(string variableName, object target)
        {
            UdonAssemblyProgramAsset.AssembleDelegate listener = null;
            listener = (success, assembly) =>
            {
                if (!success) return;

                //TODO: get actual variable name in case it was auto-changed on add
                var result = udonBehaviour.publicVariables.TrySetVariableValue(variableName, target);
                if (result)
                {
                    graphProgramAsset.OnAssemble -= listener;
                }
            };

            graphProgramAsset.OnAssemble += listener;
            EditorUtility.SetDirty(graphProgramAsset);
            AssetDatabase.SaveAssets();
            graphProgramAsset.RefreshProgram();
        }

        private string GetDefinitionNameForType(Type t)
        {
            string variableType = $"Variable_{t}".SanitizeVariableName();
            variableType = variableType.Replace("UdonBehaviour", "CommonInterfacesIUdonEventReceiver");
            return variableType;
        }

        private void SetupDraggedObject(UnityEngine.Object o, Vector2 graphMousePosition)
        {
            // Ensure variable type is allowed
            
            // create new Component variable and add to graph
            string variableType = GetDefinitionNameForType(o.GetType());
            string variableName = GetUnusedVariableNameLike(o.name.SanitizeVariableName());

            SetMouseTip($"Made {variableName}");

            string uid = AddNewVariable(variableType, variableName, true);
            RefreshVariables(false);

            object target = o;
            // Cast component to expected type
            if (o is Component) target = Convert.ChangeType(o, o.GetType());
            var variableNode = MakeVariableNode(uid, graphMousePosition, UdonGraph.VariableNodeType.Getter);
            AddElement(variableNode);

            LinkAfterCompile(variableName, target);
        }

        [Serializable]
        public class ViewTransformData
        {
            public Vector2 position = Vector2.zero;
            public float scale = 1f;
        }
    }
}
