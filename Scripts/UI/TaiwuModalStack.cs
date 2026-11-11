using System;
using System.Collections.Generic;
using TaiwuGu.Core;
using TaiwuGu.Entities;

namespace Taiwu_Godot.Scripts.UI
{
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
            WorldSignals.OnModalOpened += HandleModalOpened;
            WorldSignals.OnModalClosed += HandleModalClosed;
        }

        private void HandleModalOpened(IModalView modal)
        {
            if (modal == null) return;
            if (!_activeModals.Contains(modal)) _activeModals.Add(modal);
            WorldSignals.EmitUIModalActive(true);
        }

        private void HandleModalClosed(IModalView modal)
        {
            if (modal == null) return;
            _activeModals.Remove(modal);
            if (_activeModals.Count == 0) WorldSignals.EmitUIModalActive(false);
        }

        public bool TryPop()
        {
            while (_activeModals.Count > 0)
            {
                var top = _activeModals[^1];
                _activeModals.RemoveAt(_activeModals.Count - 1);

                if (top.IsOpen)
                {
                    top.Dismiss();
                    if (_activeModals.Count == 0) WorldSignals.EmitUIModalActive(false);
                    return true;
                }
            }
            return false;
        }

        public void Clear()
        {
            _activeModals.Clear();
            WorldSignals.EmitUIModalActive(false);
        }

        public void Dispose()
        {
            WorldSignals.OnModalOpened -= HandleModalOpened;
            WorldSignals.OnModalClosed -= HandleModalClosed;
            _activeModals.Clear();
        }
    }
}
