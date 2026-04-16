// Copyright (C) Funplay. Licensed under MIT.

using Funplay.Editor.MCP.Server;
using Funplay.Editor.Services;
using Funplay.Editor.Services.UnityLogs;
using Funplay.Editor.Settings;
using Funplay.Editor.State;
using Funplay.Editor.Threading;
using Funplay.Editor.Tools;

namespace Funplay.Editor.DI
{
    internal static class ServiceRegistration
    {
        public static ServiceCollection RegisterServices(this ServiceCollection services)
        {
            // Core Infrastructure (Singletons)
            services.AddSingleton<IApplicationPaths, ApplicationPaths>();
            services.AddSingleton<IEditorStateService, EditorStateService>();
            services.AddSingleton<IEditorContextBuilder, EditorContextBuilder>();
            services.AddSingleton<ISettingsController, SettingsController>();
            services.AddSingleton<IEditorThreadHelper, EditorThreadHelper>();

            // Services (Singletons)
            services.AddSingleton<ICompilationService, CompilationService>();
            services.AddSingleton<UnityLogsRepository, UnityLogsRepository>();
            services.AddSingleton<FunctionInvokerController, FunctionInvokerController>();

            // MCP Server (Singleton)
            services.AddSingleton<MCPServerService, MCPServerService>();

            // State (Scoped)
            services.AddScoped<IStateController, StateController>();

            return services;
        }
    }
}
