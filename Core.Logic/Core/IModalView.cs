namespace TaiwuGu.Core
{
    /// <summary>
    /// Pure domain-compatible interface for all presentation modals.
    /// Allows centralized LIFO modal stack management without cross-modal coupling.
    /// </summary>
    public interface IModalView
    {
        bool IsOpen { get; }
        void Dismiss();
    }
}
