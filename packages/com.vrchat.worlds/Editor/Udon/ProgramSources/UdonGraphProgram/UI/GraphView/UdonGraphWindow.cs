#if UNITY_2019_3_OR_NEWER
using UnityEngine.UIElements;
using UnityEditor.UIElements;
#else
using UnityEngine.Experimental.UIElements;
using UnityEditor.Experimental.UIElements;
using UnityEngine.Experimental.UIElements.StyleEnums;
#endif
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.Udon.Graph;

namespace VRC.Udon.Editor.ProgramSources.UdonGraphProgram.UI.GraphView
{
    public class UdonGraphWindow : EditorWindow
    {
        private VisualElement _rootView;

        // Reference to actual Graph View
        [SerializeField] private UdonGraph _graphView;

        private UdonGraphProgramAsset _graphAsset;
        private UdonWelcomeView _welcomeView;
        private VisualElement _curtain;

        // Toolbar and Buttons
        private Toolbar _toolbar;
        private Label _graphAssetName;
        private ToolbarMenu _toolbarOptions;
        private UdonGraphStatus _graphStatus;
        private ToolbarButton _graphReload;
        private ToolbarButton _graphCompile;
        private VisualElement _updateOrderField;
        private IntegerField _updateOrderIntField;

        [MenuItem("VRChat SDK/Udon Graph")]
        private static void ShowWindow()
        {
            // Get or focus the window
            var window = GetWindow<UdonGraphWindow>("Udon Graph", true, typeof(SceneView));
            window.titleContent = new GUIContent("Udon Graph");
        }

        private void LogPlayModeState(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.EnteredEditMode:
                    if (_rootView.Contains(_curtain))
                    {
                        _curtain.RemoveFromHierarchy();
                    }

                    break;
                case PlayModeStateChange.ExitingEditMode:
                    break;
                case PlayModeStateChange.EnteredPlayMode:
                    _rootView.Add(_curtain);
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    break;
                default:
                    break;
            }
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += LogPlayModeState;

            InitializeRootView();

            _curtain = new VisualElement()
            {
                name = "curtain",
            };
            _curtain.Add(new Label("Graph Locked in Play Mode"));

            _welcomeView = new UdonWelcomeView();
            _welcomeView.StretchToParentSize();
            _rootView.Add(_welcomeView);

            SetupToolbar();

            Undo.undoRedoPerformed -=
                OnUndoRedo; //Remove old handler if present to prevent duplicates, doesn't cause errors if not present
            Undo.undoRedoPerformed += OnUndoRedo;

            if (_graphAsset != null)
            {
                UdonBehaviour udonBehaviour = null;
                string gPath = Settings.LastUdonBehaviourPath;
                string sPath = Settings.LastUdonBehaviourScenePath;
                if (!string.IsNullOrEmpty(gPath) && !string.IsNullOrEmpty(sPath))
                {
                    var targetScene = EditorSceneManager.GetSceneByPath(sPath);
                    if (targetScene != null && targetScene.isLoaded && targetScene.IsValid())
                    {
                        var targetObject = GameObject.Find(gPath);
                        if (targetObject != null)
                        {
                            udonBehaviour = targetObject.GetComponent<UdonBehaviour>();
                        }
                    }
                }

                InitializeGraph(_graphAsset, udonBehaviour);
            }
        }

        private void InitializeRootView()
        {
#if UNITY_2019_3_OR_NEWER
            _rootView = rootVisualElement;
            _rootView.styleSheets.Add((StyleSheet) Resources.Load("UdonGraphStyle"));
#else
            _rootView = this.GetRootVisualContainer();
            _rootView.AddStyleSheetPath("UdonGraphStyle2018");
#endif
        }

        public void InitializeGraph(UdonGraphProgramAsset graph, UdonBehaviour udonBehaviour = null)
        {
            this._graphAsset = graph;

            InitializeWindow();

            _graphView = _rootView.Children().FirstOrDefault(e => e is UdonGraph) as UdonGraph;
            if (_graphView == null)
            {
                Debug.LogError("GraphView has not been added to the BaseGraph root view!");
                return;
            }

            _graphView.Initialize(graph, udonBehaviour);

            _graphStatus.LoadAsset(graph);
            // Store GUID for this asset to settings for easy reload later
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(graph, out string guid, out long localId))
            {
                Settings.LastGraphGuid = guid;
            }

            if (udonBehaviour != null)
            {
                Settings.LastUdonBehaviourPath = udonBehaviour.transform.GetHierarchyPath();
                Settings.LastUdonBehaviourScenePath = udonBehaviour.gameObject.scene.path;
            }

            _graphAssetName.text = graph.name;
            ShowGraphTools(true);
        }

        private void InitializeWindow()
        {
            if (_graphView == null)
            {
                _graphView = new UdonGraph(this);
                // we could add the toolbar in here
            }

            RemoveIfContaining(_welcomeView);
            RemoveIfContaining(_graphView);
            _rootView.Insert(0, _graphView);
        }

        private void ReloadWelcome()
        {
            RemoveIfContaining(_welcomeView);
            _rootView.Add(_welcomeView);
            ShowGraphTools(false);
        }

        // TODO: maybe move this to GraphView since it's so connected?
        private void SetupToolbar()
        {
            _toolbar = new Toolbar()
            {
                name = "UdonToolbar",
            };
            _rootView.Add(_toolbar);

            _toolbar.Add(new ToolbarButton(() => { ReloadWelcome(); })
                {text = "Welcome"});

            _graphAssetName = new Label()
            {
                name = "assetName",
            };
            _toolbar.Add(_graphAssetName);

#if UNITY_2019_3_OR_NEWER
            _toolbar.Add(new ToolbarSpacer()
            {
                flex = true,
            });
#else
            _toolbar.Add(new ToolbarFlexSpacer());
#endif
            _updateOrderField = new VisualElement()
            {
                visible = false,
            };
            _updateOrderField.Add(new Label("UpdateOrder"));
            _updateOrderIntField = new IntegerField()
            {
                name = "UpdateOrderIntegerField",
                value = (_graphAsset == null) ? 0 : _graphAsset.graphData.updateOrder,
            };
#if UNITY_2019_3_OR_NEWER
            _updateOrderIntField.RegisterValueChangedCallback((e) =>
#else
            _updateOrderIntField.OnValueChanged(e =>
#endif
            {
                _graphView.graphProgramAsset.graphData.updateOrder = e.newValue;
                _updateOrderField.visible = false;
            });
            _updateOrderIntField.isDelayed = true;
            _updateOrderField.Add(_updateOrderIntField);
            _toolbar.Add(_updateOrderField);

            _toolbarOptions = new ToolbarMenu
            {
                text = "Settings"
            };
            // Show Variables Window
            _toolbarOptions.menu.AppendAction("Show Variables",
                (m) => { _graphView.ToggleShowVariables(!_graphView.GetBlackboardVisible()); },
                (s) => { return BoolToStatus(_graphView.GetBlackboardVisible()); });
            // Show Minimap
            _toolbarOptions.menu.AppendAction("Show MiniMap",
                (m) => { _graphView.ToggleShowMiniMap(!_graphView.GetMinimapVisible()); },
                (s) => { return BoolToStatus(_graphView.GetMinimapVisible()); });
            _toolbarOptions.menu.AppendSeparator();
            // Show Update Order
            _toolbarOptions.menu.AppendAction("Show UpdateOrder", (m) =>
            {
#if UNITY_2019_3_OR_NEWER
                _updateOrderField.visible = !(m.status == DropdownMenuAction.Status.Checked);
#else
                _updateOrderField.visible = !(m.status == DropdownMenu.MenuAction.StatusFlags.Checked);
#endif
                if (_updateOrderField.visible)
                {
                    _updateOrderIntField.value = _graphAsset.graphData.updateOrder;
                }

                _updateOrderIntField.Focus();
                _updateOrderIntField.SelectAll();
            }, (s) => { return BoolToStatus(_updateOrderField.visible); });
            // Search On Noodle Drop
            _toolbarOptions.menu.AppendAction("Search on Noodle Drop",
                (m) => { Settings.SearchOnNoodleDrop = !Settings.SearchOnNoodleDrop; },
                (s) => { return BoolToStatus(Settings.SearchOnNoodleDrop); });
            // Search On Selected Node
            _toolbarOptions.menu.AppendAction("Search on Selected Node",
                (m) => { Settings.SearchOnSelectedNodeRegistry = !Settings.SearchOnSelectedNodeRegistry; },
                (s) => { return BoolToStatus(Settings.SearchOnSelectedNodeRegistry); });
            _toolbar.Add(_toolbarOptions);

            _graphCompile = new ToolbarButton(() =>
                {
                    if (_graphAsset != null && _graphAsset is AbstractUdonProgramSource udonProgramSource)
                    {
                        UdonEditorManager.Instance.QueueAndRefreshProgram(udonProgramSource);
                    }
                })
                {text = "Compile"};
            _toolbar.Add(_graphCompile);

            _graphReload = new ToolbarButton(() => { _graphView.Reload(); })
                {text = "Reload"};
            _toolbar.Add(_graphReload);

            _graphStatus = new UdonGraphStatus(_rootView);
            _toolbar.Add(_graphStatus);

            ShowGraphTools(false);
        }

#if UNITY_2019_3_OR_NEWER
        private DropdownMenuAction.Status BoolToStatus(bool value)
        {
            return value ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal;
        }
#else
        private DropdownMenu.MenuAction.StatusFlags BoolToStatus(bool value)
        {
            return value ? DropdownMenu.MenuAction.StatusFlags.Checked : DropdownMenu.MenuAction.StatusFlags.Normal;
        }
#endif

        public void ShowGraphTools(bool value)
        {
            _graphAssetName.style.visibility = value ? Visibility.Visible : Visibility.Hidden;
            _toolbarOptions.style.visibility = value ? Visibility.Visible : Visibility.Hidden;
            _graphCompile.style.visibility = value ? Visibility.Visible : Visibility.Hidden;
            _graphReload.style.visibility = value ? Visibility.Visible : Visibility.Hidden;
            _graphStatus.style.visibility = value ? Visibility.Visible : Visibility.Hidden;
        }

        private void RemoveIfContaining(VisualElement element)
        {
            if (_rootView.Contains(element))
            {
                _rootView.Remove(element);
            }
        }

        private void OnUndoRedo()
        {
            Repaint();
        }

        public UdonGraphData GetGraphDataFromAsset(UdonGraphProgramAsset asset)
        {
            InitializeGraph(asset);
            return _graphView.graphData;
        }
    }
}
