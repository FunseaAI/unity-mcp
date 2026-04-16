// Copyright (C) Funplay. Licensed under MIT.

using System;

namespace Funplay.Editor.DI
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
    internal sealed class InjectAttribute : Attribute
    {
    }
}
