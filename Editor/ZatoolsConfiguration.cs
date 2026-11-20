using System;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json;

namespace KusakaFactory.Zatools
{
    internal static class ZatoolsConfiguration
    {
        internal sealed class ConfigurationObject
        {
            public bool EnableAsvScanUnmergedArmature = false;
            public bool ClearUndoOnExitingPlayMode = false;
        }

        [MenuItem(MENU_PREFIX_ASV + "Scan suspicious unmerged armature")]
        internal static void ToggleAsvUnmergedArmature()
        {
            Current.EnableAsvScanUnmergedArmature = !Current.EnableAsvScanUnmergedArmature;
            Menu.SetChecked(MENU_PREFIX_ASV + "Scan suspicious unmerged armature", Current.EnableAsvScanUnmergedArmature);
            Save();
        }

        [MenuItem(MENU_PREFIX_ASV + "Scan suspicious unmerged armature", validate = true)]
        private static bool InitializeAsvUnmergedArmature()
        {
            Menu.SetChecked(MENU_PREFIX_ASV + "Scan suspicious unmerged armature", Current.EnableAsvScanUnmergedArmature);
            return true;
        }

        [MenuItem(MENU_PREFIX_ADVANCED + "Clear undo history on exiting play mode")]
        internal static void ToggleClearUndoOnExitingPlayMode()
        {
            if (!Current.ClearUndoOnExitingPlayMode)
            {
                var accepted = EditorUtility.DisplayDialog(
                    "Zatools",
                    "When enabled, the entire undo history is cleared every time you exit play mode. Changes made before entering play mode can no longer be undone.\n\nEnable this option?",
                    "Enable",
                    "Cancel"
                );
                if (!accepted) return;
            }

            Current.ClearUndoOnExitingPlayMode = !Current.ClearUndoOnExitingPlayMode;
            Menu.SetChecked(MENU_PREFIX_ADVANCED + "Clear undo history on exiting play mode", Current.ClearUndoOnExitingPlayMode);
            Save();
        }

        [MenuItem(MENU_PREFIX_ADVANCED + "Clear undo history on exiting play mode", validate = true)]
        private static bool InitializeClearUndoOnExitingPlayMode()
        {
            Menu.SetChecked(MENU_PREFIX_ADVANCED + "Clear undo history on exiting play mode", Current.ClearUndoOnExitingPlayMode);
            return true;
        }

        [InitializeOnLoadMethod]
        private static void RegisterPlayModeUndoCleaner()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode && Current.ClearUndoOnExitingPlayMode) Undo.ClearAll();
        }

#if UNITY_EDITOR_WIN
        [MenuItem(MENU_PREFIX + "Open Windows Terminal in project path")]
        private static void OpenTerminalAtProjectPath()
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo("wt.exe");
            startInfo.ArgumentList.Add("-d");
            startInfo.ArgumentList.Add(System.IO.Path.GetDirectoryName(Application.dataPath));
            startInfo.UseShellExecute = false;
            System.Diagnostics.Process.Start(startInfo);
        }
#endif

        #region Others

        private const string EDITOR_USER_SETTINGS_KEY = "ZatoolsConfigurationJson";
        private const string MENU_PREFIX = "Tools/Zatools: kb10uy's Various Tools/";
        private const string MENU_PREFIX_ASV = MENU_PREFIX + "Avatar Status Validator/";
        private const string MENU_PREFIX_ADVANCED = MENU_PREFIX + "Advanced/";
        private static ConfigurationObject _current = null;
        internal static ConfigurationObject Current
        {
            get
            {
                _current ??= Load();
                return _current;
            }
        }

        private static void Save()
        {
            var configJson = JsonConvert.SerializeObject(_current);
            EditorUserSettings.SetConfigValue(EDITOR_USER_SETTINGS_KEY, configJson);
        }

        private static ConfigurationObject Load()
        {
            try
            {
                var configJson = EditorUserSettings.GetConfigValue(EDITOR_USER_SETTINGS_KEY);
                return configJson != null ? JsonConvert.DeserializeObject<ConfigurationObject>(configJson) ?? new ConfigurationObject() : new ConfigurationObject();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to load zatools configuration: {e.Message}");
                return new ConfigurationObject();
            }
        }

        #endregion
    }
}
