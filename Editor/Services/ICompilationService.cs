// Copyright (C) Funplay. Licensed under MIT.

using System;
using System.Threading.Tasks;

namespace Funplay.Editor.Services
{
    internal interface ICompilationService
    {
        bool IsCompiling { get; }
        event Action OnCompilationFinished;
        Task<bool> WaitForCompilationAsync(bool forceRefresh, int timeoutSeconds);
        string GetCompilationErrors(int maxEntries = 50, bool includeWarnings = false);
    }
}
