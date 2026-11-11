using Godot;

namespace Taiwu_Godot.Scripts.UI
{
    /// <summary>
    /// One repeated caravan lot row. It only renders primitives and reports the
    /// chosen action key; all domain validation stays in CaravanEconomySystem.
    /// </summary>
    public partial class CaravanStockCardView : PanelContainer
    {
        [Signal]
        public delegate void ActionRequestedEventHandler(string key);

        public Label? TitleLabel { get; private set; }
        public Label? DetailLabel { get; private set; }
        public Label? PriceLabel { get; private set; }
        public Button? ActionButton { get; private set; }

        private string _key = string.Empty;

        public override void _Ready()
        {
            TitleLabel = GetNodeOrNull<Label>("Row/Info/Title");
            DetailLabel = GetNodeOrNull<Label>("Row/Info/Detail");
            PriceLabel = GetNodeOrNull<Label>("Row/Price");
            ActionButton = GetNodeOrNull<Button>("Row/ActionButton");

            if (ActionButton != null)
            {
                ActionButton.FocusMode = FocusModeEnum.None;
                ActionButton.Pressed += HandleActionPressed;
            }
        }

        public override void _ExitTree()
        {
            if (ActionButton != null)
            {
                ActionButton.Pressed -= HandleActionPressed;
            }
        }

        public void Bind(string key, string title, string detail, string price, string actionText, bool enabled)
        {
            _key = key;

            if (TitleLabel != null) TitleLabel.Text = title;
            if (DetailLabel != null) DetailLabel.Text = detail;
            if (PriceLabel != null) PriceLabel.Text = price;

            if (ActionButton != null)
            {
                ActionButton.Text = actionText;
                ActionButton.Disabled = !enabled;
            }
        }

        private void HandleActionPressed() => EmitSignal(SignalName.ActionRequested, _key);
    }
}
