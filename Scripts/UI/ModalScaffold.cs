using Godot;
using TaiwuGu.Core;

namespace Taiwu_Godot.Scripts.UI
{
    /// <summary>
    /// Behaviour half of the shared modal chrome. The layout lives in
    /// Scenes/UI/ModalScaffold.tscn; this controller only owns open/close, LIFO
    /// modal-stack registration, Escape and backdrop dismissal, and event cleanup.
    /// Views add their own rows to <see cref="ContentRoot"/> and extra header
    /// badges to <see cref="HeaderAccessory"/> instead of rebuilding the frame.
    /// The scaffold never tears a view down on its own: dismissal requests are
    /// forwarded to the owning view so domain state is reset by the view itself.
    /// </summary>
    public partial class ModalScaffold : Control
    {
        private const string HeaderPath = "CenterContainer/Panel/Margin/VBox/Header";
        private const string ContentPath = "CenterContainer/Panel/Margin/VBox/Content";
        private const string BackdropPath = "Backdrop";

        private IModalView? _owner;

        public Label? TitleLabel { get; private set; }
        public PanelContainer? Panel { get; private set; }
        public HBoxContainer? HeaderAccessory { get; private set; }
        public Button? CloseButton { get; private set; }
        public VBoxContainer? ContentRoot { get; private set; }
        public ColorRect? Backdrop { get; private set; }

        /// <summary>True while this chrome is displayed and registered as a modal.</summary>
        public bool IsOpen { get; private set; }

        public TaiwuModalStack? ModalStack { get; private set; }

        public override void _Ready()
        {
            Visible = false;

            Panel = GetNodeOrNull<PanelContainer>("CenterContainer/Panel");
            TitleLabel = GetNodeOrNull<Label>($"{HeaderPath}/Title");
            HeaderAccessory = GetNodeOrNull<HBoxContainer>($"{HeaderPath}/HeaderAccessory");
            CloseButton = GetNodeOrNull<Button>($"{HeaderPath}/CloseButton");
            ContentRoot = GetNodeOrNull<VBoxContainer>(ContentPath);
            Backdrop = GetNodeOrNull<ColorRect>(BackdropPath);

            if (CloseButton != null)
            {
                CloseButton.Pressed += RequestDismiss;
            }

            if (Backdrop != null)
            {
                Backdrop.GuiInput += HandleBackdropInput;
            }

            GameEventManager.OnEscapeRequested += HandleEscapeRequested;
        }

        public override void _ExitTree()
        {
            GameEventManager.OnEscapeRequested -= HandleEscapeRequested;

            if (CloseButton != null)
            {
                CloseButton.Pressed -= RequestDismiss;
            }

            if (Backdrop != null)
            {
                Backdrop.GuiInput -= HandleBackdropInput;
            }
        }

        public void BindModalStack(TaiwuModalStack? modalStack) => ModalStack = modalStack;

        /// <summary>
        /// Shows the chrome and registers <paramref name="owner"/> with the modal
        /// stack. The stack's own bookkeeping raises the UI-modal-active broadcast.
        /// </summary>
        public void Open(IModalView owner, string title)
        {
            _owner = owner;
            SetTitle(title);
            Visible = true;

            if (IsOpen) return;
            IsOpen = true;
            GameEventManager.RaiseModalOpened(owner);
        }

        /// <summary>
        /// Called by the owning view once it has finished its own teardown and
        /// state reset, so registration always mirrors the view's real state.
        /// </summary>
        public void Close()
        {
            Visible = false;
            if (!IsOpen) return;

            IsOpen = false;
            if (_owner != null)
            {
                GameEventManager.RaiseModalClosed(_owner);
            }
        }

        public void SetTitle(string title)
        {
            if (TitleLabel != null)
            {
                TitleLabel.Text = title;
            }
        }

        private void RequestDismiss() => _owner?.Dismiss();

        private void HandleBackdropInput(InputEvent @event)
        {
            if (@event is not InputEventMouseButton mouse || !mouse.Pressed || mouse.ButtonIndex != MouseButton.Left)
            {
                return;
            }

            _owner?.Dismiss();
            GetViewport()?.SetInputAsHandled();
        }

        private void HandleEscapeRequested()
        {
            if (!IsOpen || _owner == null || GameEventManager.EscapeHandled) return;

            // Strict LIFO: only the modal on top of the stack answers Escape, which
            // replaces the per-view priority checks the views used to duplicate.
            if (ModalStack != null && !ReferenceEquals(ModalStack.TopModal, _owner)) return;

            _owner.Dismiss();
            GameEventManager.EscapeHandled = true;
        }
    }
}
