using System;
using System.Collections.Generic;
using Godot;
using TaiwuGu.Core;

namespace Taiwu_Godot.Scripts.UI
{
    /// <summary>
    /// Centralized LIFO Modal Stack Manager under Constitutional Laws 3, 4 & 6.
    /// Dynamically tracks active presentation modals with zero cross-modal coupling.
    /// Manages top-level Esc pop operations and UI modal active broadcasts.
    /// </summary>
    public class TaiwuModalStack : IDisposable
    {
        private readonly List<IModalView> _activeModals = new();

        public int Count => _activeModals.Count;
        public bool HasActiveModal => _activeModals.Count > 0;
        public IModalView? TopModal => _activeModals.Count > 0 ? _activeModals[^1] : null;

        public bool IsModalOpen<TModal>() where TModal : class, IModalView
        {
            foreach (var modal in _activeModals)
            {
                if (modal is TModal && modal.IsOpen) return true;
            }

            return false;
        }

        public TaiwuModalStack()
        {
            GameEventManager.OnModalOpened += HandleModalOpened;
            GameEventManager.OnModalClosed += HandleModalClosed;
        }

        private void HandleModalOpened(IModalView modal)
        {
            if (modal == null) return;
            if (!_activeModals.Contains(modal))
            {
                _activeModals.Add(modal);
            }
            GameEventManager.TriggerUIModalActive(true);
        }

        private void HandleModalClosed(IModalView modal)
        {
            if (modal == null) return;
            _activeModals.Remove(modal);
            if (_activeModals.Count == 0)
            {
                GameEventManager.TriggerUIModalActive(false);
            }
        }

        /// <summary>
        /// Attempts to pop and dismiss the uppermost active modal.
        /// Returns true if a modal was popped and dismissed; false if no modals were open.
        /// </summary>
        public bool TryPop()
        {
            while (_activeModals.Count > 0)
            {
                var top = _activeModals[^1];
                _activeModals.RemoveAt(_activeModals.Count - 1);

                if (top.IsOpen)
                {
                    top.Dismiss();
                    if (_activeModals.Count == 0)
                    {
                        GameEventManager.TriggerUIModalActive(false);
                    }
                    return true;
                }
            }

            return false;
        }

        public void Clear()
        {
            _activeModals.Clear();
            GameEventManager.TriggerUIModalActive(false);
        }

        public void Dispose()
        {
            GameEventManager.OnModalOpened -= HandleModalOpened;
            GameEventManager.OnModalClosed -= HandleModalClosed;
            _activeModals.Clear();
        }
    }
}
