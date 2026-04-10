// Copyright (C) GameBooom. Licensed under MIT.

using System;
using UnityEditor;

namespace GameBooom.Editor.Settings
{
    internal class SettingsController : ISettingsController
    {
        private const string Prefix = "GameBooom_";

        public event Action OnSettingsChanged;

        public bool MCPServerEnabled
        {
            get => EditorPrefs.GetBool(Prefix + "MCPServerEnabled", false);
            set
            {
                if (EditorPrefs.GetBool(Prefix + "MCPServerEnabled", false) == value)
                    return;

                EditorPrefs.SetBool(Prefix + "MCPServerEnabled", value);
                OnSettingsChanged?.Invoke();
            }
        }

        public int MCPServerPort
        {
            get => EditorPrefs.GetInt(Prefix + "MCPServerPort", 8765);
            set
            {
                if (EditorPrefs.GetInt(Prefix + "MCPServerPort", 8765) == value)
                    return;

                EditorPrefs.SetInt(Prefix + "MCPServerPort", value);
                OnSettingsChanged?.Invoke();
            }
        }

        public string MCPToolExportProfile
        {
            get => EditorPrefs.GetString(Prefix + "MCPToolExportProfile", "core");
            set
            {
                var normalized = string.IsNullOrWhiteSpace(value) ? "core" : value;
                if (string.Equals(EditorPrefs.GetString(Prefix + "MCPToolExportProfile", "core"), normalized, StringComparison.Ordinal))
                    return;

                EditorPrefs.SetString(Prefix + "MCPToolExportProfile", normalized);
                OnSettingsChanged?.Invoke();
            }
        }
    }
}
