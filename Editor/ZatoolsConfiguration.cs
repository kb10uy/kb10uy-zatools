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
        private static ConfigurationObject _current = null;
        private static ConfigurationObject Current
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
            Debug.Log(configJson);
            EditorUserSettings.SetConfigValue(EDITOR_USER_SETTINGS_KEY, configJson);
        }

        internal static ConfigurationObject Load()
        {
            try
            {
                var configJson = EditorUserSettings.GetConfigValue(EDITOR_USER_SETTINGS_KEY);
                Debug.Log(configJson);
                return configJson != null ? JsonConvert.DeserializeObject<ConfigurationObject>(configJson) : new ConfigurationObject();
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
