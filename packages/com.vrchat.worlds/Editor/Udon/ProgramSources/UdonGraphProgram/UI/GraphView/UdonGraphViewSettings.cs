using UnityEngine;

namespace VRC.Udon.Editor.ProgramSources.UdonGraphProgram.UI.GraphView
{
    public static class Settings
    {
        private static string UseNeonStyleString = "UdonGraphViewSettings.UseNeonStyle";
        private static string LastGraphGuidString = "UdonGraphViewSettings.LastGraphGuid";
        private static string LastUdonBehaviourPathString = "UdonGraphViewSettings.LastUdonBehaviourPath";
        private static string LastUdonBehaviourScenePathString = "UdonGraphViewSettings.LastUdonBehaviourScenePath";
        private static string SearchOnSelectedNodeRegistryString = "UdonGraphViewSettings.SearchOnSelectedNodeRegistry";
        private static string GridSnapSizeString = "UdonGraphViewSettings.GridSnapSize";
        private static string SearchOnNoodleDropString = "UdonGraphViewSettings.SearchOnNoodleDrop";

        public static bool UseNeonStyle
        {
            get { return PlayerPrefs.GetInt(UseNeonStyleString, 0) == 1; }
            set { PlayerPrefs.SetInt(UseNeonStyleString, value ? 1 : 0); }
        }

        public static string LastGraphGuid
        {
            get { return PlayerPrefs.GetString(LastGraphGuidString, ""); }
            set { PlayerPrefs.SetString(LastGraphGuidString, value); }
        }

        public static string LastUdonBehaviourPath
        {
            get { return PlayerPrefs.GetString(LastUdonBehaviourPathString, ""); }
            set { PlayerPrefs.SetString(LastUdonBehaviourPathString, value); }
        }

        public static string LastUdonBehaviourScenePath
        {
            get { return PlayerPrefs.GetString(LastUdonBehaviourScenePathString, ""); }
            set { PlayerPrefs.SetString(LastUdonBehaviourScenePathString, value); }
        }

        public static bool SearchOnSelectedNodeRegistry
        {
            get { return PlayerPrefs.GetInt(SearchOnSelectedNodeRegistryString, 1) == 1; }
            set { PlayerPrefs.SetInt(SearchOnSelectedNodeRegistryString, value ? 1 : 0); }
        }

        public static int GridSnapSize
        {
            get { return PlayerPrefs.GetInt(GridSnapSizeString, 0); }
            set { PlayerPrefs.SetInt(GridSnapSizeString, value); }
        }

        public static bool SearchOnNoodleDrop
        {
            get { return PlayerPrefs.GetInt(SearchOnNoodleDropString, 1) == 1; }
            set { PlayerPrefs.SetInt(SearchOnNoodleDropString, value ? 1 : 0); }
        }

    }
}