// Copyright (C) GameBooom. Licensed under GPLv3.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using GameBooom.Editor.Services;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GameBooom.Editor.MCP.Server
{
    internal class MCPResourceProvider : IDisposable
    {
        private static readonly HashSet<string> UnsafeEditModePropertyNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "material",
            "materials",
            "mesh"
        };

        private readonly IEditorContextBuilder _contextBuilder;
        private readonly IApplicationPaths _applicationPaths;
        private readonly MCPInteractionLog _interactionLog;
        private readonly object _lock = new object();
        private readonly string _projectName;

        private DateTime _lastSnapshotTime = DateTime.MinValue;
        private Dictionary<string, string> _cachedResources = new Dictionary<string, string>();
        private Dictionary<string, string> _sceneObjectSummaries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, string> _componentSummaries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private bool _disposed;

        public MCPResourceProvider(
            IEditorContextBuilder contextBuilder,
            IApplicationPaths applicationPaths,
            MCPInteractionLog interactionLog)
        {
            _contextBuilder = contextBuilder;
            _applicationPaths = applicationPaths;
            _interactionLog = interactionLog;
            _projectName = Application.productName;

            EditorApplication.update += RefreshCacheOnMainThread;
            RefreshCacheOnMainThread();
        }

        public List<Dictionary<string, object>> ListResources()
        {
            RefreshCache();

            return new List<Dictionary<string, object>>
            {
                CreateResource("unity://project/context", $"{_projectName} Project Context", "Live Unity project context summary."),
                CreateResource("unity://project/summary", $"{_projectName} Project Summary", "Project path, folder summary, and asset counts."),
                CreateResource("unity://scene/active", $"{_projectName} Active Scene", "Summary of the active scene."),
                CreateResource("unity://scene/current", $"{_projectName} Current Scene", "Alias of the active scene summary."),
                CreateResource("unity://selection/current", $"{_projectName} Current Selection", "Summary of the current Unity selection."),
                CreateResource("unity://errors/compilation", $"{_projectName} Compilation Errors", "Current compilation errors with source context."),
                CreateResource("unity://errors/console", $"{_projectName} Console Errors", "Recent Unity console errors."),
                CreateResource("unity://mcp/interactions", $"{_projectName} MCP Interactions", "Recent MCP tool interaction summaries.")
            };
        }

        public List<Dictionary<string, object>> ListResourceTemplates()
        {
            return new List<Dictionary<string, object>>
            {
                CreateResourceTemplate("unity://scene/object/{name}", "Scene Object", "Inspect a scene object by name."),
                CreateResourceTemplate("unity://asset/path/{asset_path}", "Asset By Path", "Read a script or text asset by project path."),
                CreateResourceTemplate("unity://component/gameobject/{name}/component/{type}", "Component Details", "Inspect a component on a GameObject.")
            };
        }

        public Dictionary<string, object> ReadResource(string uri)
        {
            RefreshCache();
            var text = ResolveResourceText(uri);

            return new Dictionary<string, object>
            {
                ["contents"] = new List<object>
                {
                    new Dictionary<string, object>
                    {
                        ["uri"] = uri,
                        ["mimeType"] = "text/plain",
                        ["text"] = text ?? "Resource not found."
                    }
                }
            };
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            EditorApplication.update -= RefreshCacheOnMainThread;
        }

        private string ResolveResourceText(string uri)
        {
            if (string.IsNullOrWhiteSpace(uri))
                return "Resource URI is required.";

            lock (_lock)
            {
                if (_cachedResources.TryGetValue(uri, out var cached))
                    return cached;
            }

            if (uri.StartsWith("unity://scene/object/", StringComparison.OrdinalIgnoreCase))
            {
                var name = Uri.UnescapeDataString(uri.Substring("unity://scene/object/".Length));
                lock (_lock)
                {
                    if (_sceneObjectSummaries.TryGetValue(name, out var summary))
                        return summary;
                }

                return "Scene object not found in cached snapshot: " + name;
            }

            if (uri.StartsWith("unity://asset/path/", StringComparison.OrdinalIgnoreCase))
            {
                var assetPath = Uri.UnescapeDataString(uri.Substring("unity://asset/path/".Length));
                return ReadAssetByPath(assetPath);
            }

            if (uri.StartsWith("unity://component/gameobject/", StringComparison.OrdinalIgnoreCase))
            {
                var rest = uri.Substring("unity://component/gameobject/".Length);
                var split = rest.Split(new[] { "/component/" }, StringSplitOptions.None);
                if (split.Length != 2)
                    return "Invalid component resource URI.";

                var gameObjectName = Uri.UnescapeDataString(split[0]);
                var componentType = Uri.UnescapeDataString(split[1]);
                var key = BuildComponentKey(gameObjectName, componentType);

                lock (_lock)
                {
                    if (_componentSummaries.TryGetValue(key, out var summary))
                        return summary;
                }

                return "Component not found in cached snapshot: " + gameObjectName + " / " + componentType;
            }

            return "Resource not found: " + uri;
        }

        private string ReadAssetByPath(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return "Asset path is required.";

            var fullPath = Path.IsPathRooted(assetPath)
                ? assetPath
                : Path.Combine(_applicationPaths.ProjectPath, assetPath).Replace("\\", "/");

            if (!File.Exists(fullPath))
                return "Asset not found: " + assetPath;

            var content = File.ReadAllText(fullPath);
            if (content.Length > 12000)
                content = content.Substring(0, 12000) + "\n... (truncated)";

            return $"[{assetPath}]\n{content}";
        }

        private void RefreshCache()
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                if ((now - _lastSnapshotTime).TotalSeconds < 1.0d && _cachedResources.Count > 0)
                    return;
            }
        }

        private void RefreshCacheOnMainThread()
        {
            if (_disposed)
                return;

            var now = DateTime.UtcNow;
            lock (_lock)
            {
                if ((now - _lastSnapshotTime).TotalSeconds < 1.0d && _cachedResources.Count > 0)
                    return;

                _lastSnapshotTime = now;
            }

            var sceneSummary = _contextBuilder != null ? _contextBuilder.GetActiveSceneSummary() : "Context builder unavailable.";
            var projectSummary = BuildProjectSummary();
            var resources = new Dictionary<string, string>
            {
                ["unity://project/context"] = BuildProjectContext(sceneSummary),
                ["unity://project/summary"] = projectSummary,
                ["unity://scene/active"] = sceneSummary,
                ["unity://scene/current"] = sceneSummary,
                ["unity://selection/current"] = _contextBuilder != null ? _contextBuilder.GetSelectionSummary() : "Context builder unavailable.",
                ["unity://errors/compilation"] = _contextBuilder != null ? _contextBuilder.GetCompileErrorContext(5, 3) : "Context builder unavailable.",
                ["unity://errors/console"] = _contextBuilder != null ? _contextBuilder.GetConsoleErrorSummary(8) : "Context builder unavailable.",
                ["unity://mcp/interactions"] = BuildInteractionSummary()
            };

            var sceneObjectSummaries = BuildSceneObjectSummaries();
            var componentSummaries = BuildComponentSummaries();

            lock (_lock)
            {
                _cachedResources = resources;
                _sceneObjectSummaries = sceneObjectSummaries;
                _componentSummaries = componentSummaries;
            }
        }

        private string BuildProjectContext(string sceneSummary)
        {
            var sb = new StringBuilder();
            sb.AppendLine("GameBooom MCP Project Context");
            sb.AppendLine("- Project: " + _projectName);
            sb.AppendLine("- Unity: " + Application.unityVersion);
            sb.AppendLine("- Package Version: " + PackageVersionUtility.CurrentVersion);
            sb.AppendLine("- Project Path: " + _applicationPaths.ProjectPath);
            sb.AppendLine("- Assets Path: " + _applicationPaths.AssetsPath);
            sb.AppendLine();

            if (_contextBuilder != null)
                sb.AppendLine(_contextBuilder.GetContextBlock());
            else
                sb.AppendLine(sceneSummary);

            return sb.ToString().Trim();
        }

        private string BuildProjectSummary()
        {
            var topLevelDirectories = Directory.Exists(_applicationPaths.AssetsPath)
                ? Directory.GetDirectories(_applicationPaths.AssetsPath)
                : Array.Empty<string>();
            var sb = new StringBuilder();

            sb.AppendLine("Project Summary");
            sb.AppendLine("Project Root: " + _applicationPaths.ProjectPath);
            sb.AppendLine("Assets Path: " + _applicationPaths.AssetsPath);
            sb.AppendLine("Unity Version: " + Application.unityVersion);
            sb.AppendLine("Package Version: " + PackageVersionUtility.CurrentVersion);
            sb.AppendLine();
            sb.AppendLine($"Assets Top-Level Directories ({topLevelDirectories.Length}):");

            Array.Sort(topLevelDirectories, StringComparer.OrdinalIgnoreCase);
            if (topLevelDirectories.Length == 0)
            {
                sb.AppendLine("- (none)");
            }
            else
            {
                for (int i = 0; i < topLevelDirectories.Length; i++)
                    sb.AppendLine("- " + Path.GetFileName(topLevelDirectories[i]));
            }

            sb.AppendLine();
            sb.AppendLine("Asset Counts:");
            sb.AppendLine("- Scenes: " + AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }).Length);
            sb.AppendLine("- Prefabs: " + AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }).Length);
            sb.AppendLine("- Scripts: " + AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets" }).Length);
            sb.AppendLine("- Materials: " + AssetDatabase.FindAssets("t:Material", new[] { "Assets" }).Length);

            return sb.ToString().Trim();
        }

        private string BuildInteractionSummary()
        {
            if (_interactionLog == null)
                return "No MCP interaction log available.";

            var entries = _interactionLog.GetEntries();
            if (entries.Count == 0)
                return "No MCP interactions recorded yet.";

            var sb = new StringBuilder();
            sb.AppendLine("Recent MCP interactions:");
            for (int i = 0; i < entries.Count && i < 10; i++)
            {
                var entry = entries[i];
                sb.AppendLine($"- {entry.Timestamp:HH:mm:ss} [{entry.Status}] {entry.ToolName}: {entry.ResultSummary}");
            }

            return sb.ToString().Trim();
        }

        private static Dictionary<string, string> BuildSceneObjectSummaries()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
                return result;

            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
                CaptureSceneObjectRecursive(roots[i].transform, result);

            return result;
        }

        private static void CaptureSceneObjectRecursive(Transform node, Dictionary<string, string> map)
        {
            if (!map.ContainsKey(node.name))
            {
                var sb = new StringBuilder();
                sb.AppendLine("GameObject: " + node.name);
                sb.AppendLine("Path: " + GetHierarchyPath(node));
                sb.AppendLine("Active: " + node.gameObject.activeInHierarchy);
                sb.AppendLine("Position: " + node.position);
                sb.AppendLine("Rotation: " + node.eulerAngles);
                sb.AppendLine("Scale: " + node.localScale);
                sb.AppendLine();
                sb.AppendLine("Components: " + GetComponentSummary(node.gameObject));
                map[node.name] = sb.ToString().Trim();
            }

            for (int i = 0; i < node.childCount; i++)
                CaptureSceneObjectRecursive(node.GetChild(i), map);
        }

        private static Dictionary<string, string> BuildComponentSummaries()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
                return result;

            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
                CaptureComponentSummariesRecursive(roots[i].transform, result);

            return result;
        }

        private static void CaptureComponentSummariesRecursive(Transform node, Dictionary<string, string> map)
        {
            var components = node.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component == null)
                    continue;

                var key = BuildComponentKey(node.name, component.GetType().Name);
                if (!map.ContainsKey(key))
                    map[key] = BuildComponentSummary(node.gameObject, component);
            }

            for (int i = 0; i < node.childCount; i++)
                CaptureComponentSummariesRecursive(node.GetChild(i), map);
        }

        private static string BuildComponentSummary(GameObject gameObject, Component component)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Component: " + component.GetType().Name);
            sb.AppendLine("GameObject: " + gameObject.name);
            sb.AppendLine("Path: " + GetHierarchyPath(gameObject.transform));
            sb.AppendLine();

            var properties = component.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < properties.Length; i++)
            {
                var property = properties[i];
                if (!IsSafeToReadInEditMode(property))
                    continue;

                try
                {
                    var value = property.GetValue(component, null);
                    sb.AppendLine(property.Name + " = " + FormatValue(value));
                }
                catch
                {
                }
            }

            var fields = component.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < fields.Length; i++)
            {
                var field = fields[i];
                try
                {
                    var value = field.GetValue(component);
                    sb.AppendLine(field.Name + " = " + FormatValue(value));
                }
                catch
                {
                }
            }

            return sb.ToString().Trim();
        }

        private static bool IsSafeToReadInEditMode(PropertyInfo property)
        {
            if (property == null)
                return false;

            if (property.GetMethod == null || property.GetMethod.IsStatic)
                return false;

            if (!property.CanRead || property.GetIndexParameters().Length > 0)
                return false;

            if (property.GetCustomAttribute<ObsoleteAttribute>() != null)
                return false;

            return !UnsafeEditModePropertyNames.Contains(property.Name);
        }

        private static string FormatValue(object value)
        {
            return value == null ? "null" : value.ToString();
        }

        private static string GetHierarchyPath(Transform transform)
        {
            var parts = new List<string>();
            var current = transform;
            while (current != null)
            {
                parts.Insert(0, current.name);
                current = current.parent;
            }

            return string.Join("/", parts);
        }

        private static string GetComponentSummary(GameObject gameObject)
        {
            var components = gameObject.GetComponents<Component>();
            var names = new List<string>();

            for (int i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component == null)
                    continue;

                var typeName = component.GetType().Name;
                if (typeName == "Transform" || typeName == "RectTransform")
                    continue;

                names.Add(typeName);
            }

            return names.Count > 0 ? string.Join(", ", names) : "(none)";
        }

        private static string BuildComponentKey(string gameObjectName, string componentName)
        {
            return gameObjectName + "|" + componentName;
        }

        private static Dictionary<string, object> CreateResource(string uri, string name, string description)
        {
            return new Dictionary<string, object>
            {
                ["uri"] = uri,
                ["name"] = name,
                ["description"] = description,
                ["mimeType"] = "text/plain"
            };
        }

        private static Dictionary<string, object> CreateResourceTemplate(string uriTemplate, string name, string description)
        {
            return new Dictionary<string, object>
            {
                ["uriTemplate"] = uriTemplate,
                ["name"] = name,
                ["description"] = description,
                ["mimeType"] = "text/plain"
            };
        }
    }
}
