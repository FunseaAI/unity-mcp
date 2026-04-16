// Copyright (C) Funplay. Licensed under MIT.

namespace Funplay.Editor.Services
{
    internal interface IEditorStateService
    {
        bool IsPlayingOrWillChangePlaymode { get; }
        bool IsCompiling { get; }
    }
}
