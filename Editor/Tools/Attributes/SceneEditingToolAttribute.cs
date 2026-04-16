// Copyright (C) Funplay. Licensed under MIT.
using System;

namespace Funplay.Editor.Tools
{
    /// <summary>
    /// Marks a tool function as modifying the scene.
    /// These functions should use Undo-safe operations.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    internal class SceneEditingToolAttribute : Attribute { }
}
