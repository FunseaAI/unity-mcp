// Copyright (C) Funplay. Licensed under MIT.

using System;
using System.Collections.Generic;

namespace Funplay.Editor.State
{
    internal class StateController : IStateController
    {
        private readonly Stack<FunplayState> _stateHistory = new Stack<FunplayState>();
        private FunplayState _currentState = FunplayState.Initialized;

        public FunplayState CurrentState => _currentState;
        public bool IsInitialized => _currentState == FunplayState.Initialized;

        public event Action<FunplayState> OnStateChanged;
        public event Action OnCancelRequested;

        public void SetState(FunplayState state)
        {
            if (_currentState == state) return;

            _stateHistory.Push(_currentState);
            _currentState = state;
            OnStateChanged?.Invoke(state);
        }

        public void ReturnToPreviousState()
        {
            _currentState = _stateHistory.Count > 0
                ? _stateHistory.Pop()
                : FunplayState.Initialized;

            OnStateChanged?.Invoke(_currentState);
        }

        public void ClearState()
        {
            _stateHistory.Clear();
            _currentState = FunplayState.Initialized;
            OnStateChanged?.Invoke(_currentState);
        }

        public void RequestCancel()
        {
            OnCancelRequested?.Invoke();
            ReturnToPreviousState();
        }
    }
}
