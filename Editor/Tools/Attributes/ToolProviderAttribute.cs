// Copyright (C) Funplay. Licensed under MIT.
using System;

namespace Funplay.Editor.Tools
{
    [AttributeUsage(AttributeTargets.Class)]
    internal class ToolProviderAttribute : Attribute
    {
        public string Category { get; }

        public ToolProviderAttribute(string category = null)
        {
            Category = category;
        }
    }
}
