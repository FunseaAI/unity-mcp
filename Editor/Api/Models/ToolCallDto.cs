// Copyright (C) Funplay. Licensed under MIT.
using System.Collections.Generic;

namespace Funplay.Editor.Api.Models
{
    internal class ToolCallDto
    {
        public string id;
        public string type = "function";
        public ToolCallFunctionDto function;
    }

    internal class ToolCallFunctionDto
    {
        public string name;
        public string arguments; // JSON string
    }
}
