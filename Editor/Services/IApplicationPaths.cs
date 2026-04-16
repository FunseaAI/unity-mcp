// Copyright (C) Funplay. Licensed under MIT.

namespace Funplay.Editor.Services
{
    internal interface IApplicationPaths
    {
        string ProjectPath { get; }
        string AssetsPath { get; }
        string TempPath { get; }
        string DataPath { get; }
        string PersistentDataPath { get; }
    }
}
