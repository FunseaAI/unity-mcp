// Copyright (C) Funplay. Licensed under MIT.

using UnityEditor;

namespace Funplay.Editor.Services
{
    internal class EditorStateService : IEditorStateService
    {
        public bool IsPlayingOrWillChangePlaymode =>
            EditorApplication.isPlayingOrWillChangePlaymode;

        public bool IsCompiling => EditorApplication.isCompiling;
    }
}
