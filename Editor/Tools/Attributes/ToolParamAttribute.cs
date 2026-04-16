// Copyright (C) Funplay. Licensed under MIT.
using System;

namespace Funplay.Editor.Tools
{
    [AttributeUsage(AttributeTargets.Parameter)]
    internal class ToolParamAttribute : Attribute
    {
        public string Description { get; }
        public bool Required { get; set; } = true;
        public string DefaultValue { get; set; }

        public ToolParamAttribute(string description)
        {
            Description = description;
        }
    }
}
