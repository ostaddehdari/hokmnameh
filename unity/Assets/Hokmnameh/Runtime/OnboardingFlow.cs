using System;
using System.Collections.Generic;

namespace Hokmnameh.Unity
{
    // Network-independent guard for guest request fan-in and repeated practice taps.
    public sealed class OnboardingFlow
    {
        private readonly List<Action> _guestReady = new List<Action>();
        private bool _guestPending;
        private bool _practicePending;

        public bool RequestGuest(Action onReady)
        {
            if (onReady != null) _guestReady.Add(onReady);
            if (_guestPending) return false;
            _guestPending = true;
            return true;
        }

        public void CompleteGuest(bool success)
        {
            _guestPending = false;
            var callbacks = _guestReady.ToArray();
            _guestReady.Clear();
            if (success) foreach (var callback in callbacks) callback();
        }

        public bool BeginPractice()
        {
            if (_practicePending) return false;
            _practicePending = true;
            return true;
        }
        public void EndPractice() => _practicePending = false;
    }
}
