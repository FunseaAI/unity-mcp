// Copyright (C) GameBooom. Licensed under GPLv3.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GameBooom.Editor.MCP.Server
{
    /// <summary>
    /// Handles MCP protocol requests (initialize, tools/list, tools/call, etc.)
    /// </summary>
    internal class MCPRequestHandler
    {
        private readonly MCPToolExporter _toolExporter;
        private readonly MCPExecutionBridge _executionBridge;

        public MCPRequestHandler(MCPToolExporter toolExporter, MCPExecutionBridge executionBridge)
        {
            _toolExporter = toolExporter ?? throw new ArgumentNullException(nameof(toolExporter));
            _executionBridge = executionBridge ?? throw new ArgumentNullException(nameof(executionBridge));
        }

        public async Task<MCPResponse> HandleRequestAsync(MCPRequest request, CancellationToken ct)
        {
            try
            {
                if (request == null)
                    return CreateErrorResponse(null, -32600, "Invalid Request");

                if (request.JsonRpc != "2.0")
                    return CreateErrorResponse(request.Id, -32600, "Invalid Request: jsonrpc must be '2.0'");

                Debug.Log($"[GameBooom MCP Server] Handling request: {request.Method}");

                return request.Method switch
                {
                    "initialize" => HandleInitialize(request),
                    "notifications/initialized" => null,
                    "notifications/cancelled" => null,
                    "tools/list" => HandleToolsList(request),
                    "tools/call" => await HandleToolsCallAsync(request, ct),
                    "prompts/list" => HandlePromptsList(request),
                    "resources/list" => HandleResourcesList(request),
                    "resources/read" => HandleResourcesRead(request),
                    "resources/templates/list" => HandleResourceTemplatesList(request),
                    _ when request.Method != null && request.Method.StartsWith("notifications/") => null,
                    _ => CreateErrorResponse(request.Id, -32601, $"Method not found: {request.Method}")
                };
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameBooom MCP Server] Error handling request: {ex.Message}\n{ex.StackTrace}");
                return CreateErrorResponse(request?.Id, -32603, $"Internal error: {ex.Message}");
            }
        }

        private MCPResponse HandleInitialize(MCPRequest request)
        {
            var result = new Dictionary<string, object>
            {
                ["protocolVersion"] = "2024-11-05",
                ["serverInfo"] = new Dictionary<string, object>
                {
                    ["name"] = "GameBooom MCP Server",
                    ["version"] = "1.0.0"
                },
                ["capabilities"] = new Dictionary<string, object>
                {
                    ["tools"] = new Dictionary<string, object>(),
                    ["resources"] = new Dictionary<string, object>()
                }
            };

            Debug.Log("[GameBooom MCP Server] Initialized successfully");
            return new MCPResponse { Id = request.Id, Result = result };
        }

        private MCPResponse HandleToolsList(MCPRequest request)
        {
            var tools = _toolExporter.ExportTools();
            Debug.Log($"[GameBooom MCP Server] Returning {tools.Count} tools");

            return new MCPResponse
            {
                Id = request.Id,
                Result = new Dictionary<string, object> { ["tools"] = tools }
            };
        }

        private async Task<MCPResponse> HandleToolsCallAsync(MCPRequest request, CancellationToken ct)
        {
            try
            {
                if (!request.Params.TryGetValue("name", out var nameObj) || !(nameObj is string toolName))
                    return CreateErrorResponse(request.Id, -32602, "Invalid params: 'name' is required");

                var arguments = request.Params.ContainsKey("arguments") && request.Params["arguments"] is Dictionary<string, object> args
                    ? args
                    : new Dictionary<string, object>();

                Debug.Log($"[GameBooom MCP Server] Calling tool: {toolName}");
                var result = await _executionBridge.ExecuteToolAsync(toolName, arguments, ct);

                return new MCPResponse
                {
                    Id = request.Id,
                    Result = new Dictionary<string, object>
                    {
                        ["content"] = BuildContentFromResult(result)
                    }
                };
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameBooom MCP Server] Error executing tool: {ex.Message}");
                return CreateErrorResponse(request.Id, -32603, $"Tool execution failed: {ex.Message}");
            }
        }

        private MCPResponse HandlePromptsList(MCPRequest request)
        {
            return new MCPResponse
            {
                Id = request.Id,
                Result = new Dictionary<string, object> { ["prompts"] = new List<object>() }
            };
        }

        private MCPResponse HandleResourcesList(MCPRequest request)
        {
            var resources = new List<object>
            {
                new Dictionary<string, object>
                {
                    ["uri"] = "unity://scene/current",
                    ["name"] = "Current Scene",
                    ["description"] = "Current scene details and a hierarchy snapshot.",
                    ["mimeType"] = "text/plain"
                },
                new Dictionary<string, object>
                {
                    ["uri"] = "unity://project/summary",
                    ["name"] = "Project Summary",
                    ["description"] = "Project root, top-level Assets folders, and asset counts.",
                    ["mimeType"] = "text/plain"
                }
            };

            return new MCPResponse
            {
                Id = request.Id,
                Result = new Dictionary<string, object> { ["resources"] = resources }
            };
        }

        private MCPResponse HandleResourcesRead(MCPRequest request)
        {
            if (request.Params == null ||
                !request.Params.TryGetValue("uri", out var uriObj) ||
                !(uriObj is string uri) ||
                string.IsNullOrWhiteSpace(uri))
            {
                return CreateErrorResponse(request.Id, -32602, "Invalid params: 'uri' is required");
            }

            string text;
            string name;

            switch (uri)
            {
                case "unity://scene/current":
                    name = "Current Scene";
                    text = BuildCurrentSceneResourceText();
                    break;
                case "unity://project/summary":
                    name = "Project Summary";
                    text = BuildProjectSummaryResourceText();
                    break;
                default:
                    return CreateErrorResponse(request.Id, -32602, $"Unknown resource URI: {uri}");
            }

            return new MCPResponse
            {
                Id = request.Id,
                Result = new Dictionary<string, object>
                {
                    ["contents"] = new List<object>
                    {
                        new Dictionary<string, object>
                        {
                            ["uri"] = uri,
                            ["name"] = name,
                            ["mimeType"] = "text/plain",
                            ["text"] = text
                        }
                    }
                }
            };
        }

        private MCPResponse HandleResourceTemplatesList(MCPRequest request)
        {
            return new MCPResponse
            {
                Id = request.Id,
                Result = new Dictionary<string, object>
                {
                    ["resourceTemplates"] = new List<object>()
                }
            };
        }

        private const string ImageDataUriPrefix = "data:image/png;base64,";

        private string BuildCurrentSceneResourceText()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var rootObjects = scene.GetRootGameObjects();
            var sb = new StringBuilder();

            sb.AppendLine("Current Scene");
            sb.AppendLine($"Name: {scene.name}");
            sb.AppendLine($"Path: {(string.IsNullOrEmpty(scene.path) ? "(unsaved scene)" : scene.path)}");
            sb.AppendLine($"Is Loaded: {scene.isLoaded}");
            sb.AppendLine($"Is Dirty: {scene.isDirty}");
            sb.AppendLine($"Root Objects: {rootObjects.Length}");
            sb.AppendLine($"Total GameObjects: {CountSceneGameObjects(rootObjects)}");
            sb.AppendLine();
            sb.AppendLine("Hierarchy Snapshot:");

            if (rootObjects.Length == 0)
            {
                sb.AppendLine("- (no root objects)");
            }
            else
            {
                foreach (var root in rootObjects)
                {
                    AppendHierarchySnapshot(sb, root.transform, 0, 3);
                }
            }

            return sb.ToString().TrimEnd();
        }

        private string BuildProjectSummaryResourceText()
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath) ?? Application.dataPath;
            var assetsPath = Application.dataPath;
            var topLevelDirectories = Directory.Exists(assetsPath)
                ? Directory.GetDirectories(assetsPath)
                : Array.Empty<string>();
            var sb = new StringBuilder();

            sb.AppendLine("Project Summary");
            sb.AppendLine($"Project Root: {projectRoot}");
            sb.AppendLine($"Assets Path: {assetsPath}");
            sb.AppendLine();
            sb.AppendLine($"Assets Top-Level Directories ({topLevelDirectories.Length}):");

            if (topLevelDirectories.Length == 0)
            {
                sb.AppendLine("- (none)");
            }
            else
            {
                Array.Sort(topLevelDirectories, StringComparer.OrdinalIgnoreCase);
                foreach (var directory in topLevelDirectories)
                {
                    sb.AppendLine($"- {Path.GetFileName(directory)}");
                }
            }

            sb.AppendLine();
            sb.AppendLine("Asset Counts:");
            sb.AppendLine($"- Scenes: {CountAssets("t:Scene")}");
            sb.AppendLine($"- Prefabs: {CountAssets("t:Prefab")}");
            sb.AppendLine($"- Scripts: {CountAssets("t:MonoScript")}");
            sb.AppendLine($"- Materials: {CountAssets("t:Material")}");

            return sb.ToString().TrimEnd();
        }

        private List<Dictionary<string, object>> BuildContentFromResult(string result)
        {
            var content = new List<Dictionary<string, object>>();

            if (result != null && result.StartsWith(ImageDataUriPrefix))
            {
                var base64Data = result.Substring(ImageDataUriPrefix.Length);
                content.Add(new Dictionary<string, object>
                {
                    ["type"] = "image", ["data"] = base64Data, ["mimeType"] = "image/png"
                });
                content.Add(new Dictionary<string, object>
                {
                    ["type"] = "text", ["text"] = "Screenshot captured successfully."
                });
            }
            else
            {
                content.Add(new Dictionary<string, object>
                {
                    ["type"] = "text", ["text"] = result
                });
            }

            return content;
        }

        private int CountSceneGameObjects(GameObject[] rootObjects)
        {
            int count = 0;

            foreach (var rootObject in rootObjects)
            {
                if (rootObject == null)
                    continue;

                count += CountGameObjectTree(rootObject.transform);
            }

            return count;
        }

        private int CountGameObjectTree(Transform current)
        {
            int count = 1;

            for (int i = 0; i < current.childCount; i++)
            {
                count += CountGameObjectTree(current.GetChild(i));
            }

            return count;
        }

        private void AppendHierarchySnapshot(StringBuilder sb, Transform current, int depth, int maxDepth)
        {
            var indent = new string(' ', depth * 2);
            var stateSuffix = current.gameObject.activeSelf ? string.Empty : " [inactive]";
            var tagSuffix = current.tag == "Untagged" ? string.Empty : $" tag={current.tag}";
            var componentSummary = GetComponentSummary(current.gameObject);

            sb.Append(indent);
            sb.Append("- ");
            sb.Append(current.name);
            sb.Append(stateSuffix);
            sb.Append(tagSuffix);

            if (!string.IsNullOrEmpty(componentSummary))
            {
                sb.Append(" [");
                sb.Append(componentSummary);
                sb.Append(']');
            }

            sb.AppendLine();

            if (depth >= maxDepth)
            {
                if (current.childCount > 0)
                {
                    sb.Append(new string(' ', (depth + 1) * 2));
                    sb.AppendLine($"- ... ({current.childCount} children)");
                }

                return;
            }

            for (int i = 0; i < current.childCount; i++)
            {
                AppendHierarchySnapshot(sb, current.GetChild(i), depth + 1, maxDepth);
            }
        }

        private string GetComponentSummary(GameObject gameObject)
        {
            var components = gameObject.GetComponents<Component>();
            var names = new List<string>();

            foreach (var component in components)
            {
                if (component == null)
                    continue;

                var typeName = component.GetType().Name;
                if (typeName == "Transform" || typeName == "RectTransform")
                    continue;

                names.Add(typeName);
            }

            return names.Count > 0 ? string.Join(", ", names) : string.Empty;
        }

        private int CountAssets(string filter)
        {
            return AssetDatabase.FindAssets(filter, new[] { "Assets" }).Length;
        }

        private MCPResponse CreateErrorResponse(object requestId, int code, string message)
        {
            return new MCPResponse
            {
                Id = requestId,
                Error = new MCPError { Code = code, Message = message }
            };
        }
    }
}
