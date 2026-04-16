// Copyright (C) Funplay. Licensed under MIT.

using System;

namespace Funplay.Editor.State
{
    internal interface IStateController
    {
        FunplayState CurrentState { get; }
        event Action<FunplayState> OnStateChanged;
        event Action OnCancelRequested;

        void SetState(FunplayState state);
        void ReturnToPreviousState();
        void ClearState();
        void RequestCancel();
        bool IsInitialized { get; }
    }
}
