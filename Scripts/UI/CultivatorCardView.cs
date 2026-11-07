using Godot;

namespace Taiwu_Godot.Scripts.UI
{
    /// <summary>
    /// Behaviour for one repeated cultivator row, paired with CultivatorCard.tscn.
    /// Takes primitives rather than domain entities so the card stays a pure view,
    /// and reports interaction intent through a signal instead of calling services.
    /// </summary>
    public partial class CultivatorCardView : PanelContainer
    {
        [Signal]
        public delegate void InteractRequestedEventHandler(string entityId);

        public Label? BadgeLabel { get; private set; }
        public Label? InfoLabel { get; private set; }
        public Button? InteractButton { get; private set; }

        private string _entityId = string.Empty;

        public override void _Ready()
        {
            BadgeLabel = GetNodeOrNull<Label>("Row/Badge");
            InfoLabel = GetNodeOrNull<Label>("Row/Info");
            InteractButton = GetNodeOrNull<Button>("Row/InteractButton");

            if (InteractButton != null)
            {
                InteractButton.Pressed += HandleInteractPressed;
            }
        }

        public override void _ExitTree()
        {
            if (InteractButton != null)
            {
                InteractButton.Pressed -= HandleInteractPressed;
            }
        }

        public void Bind(string entityId, int rank, int substage, string name, string clan, int favorability, int primevalStones)
        {
            _entityId = entityId;

            if (BadgeLabel != null)
            {
                BadgeLabel.Text = $"[R{rank} S{substage}]";
            }

            if (InfoLabel != null)
            {
                InfoLabel.Text = $"【 {name} 】 — {clan} | Favor: {favorability:+0;-0;0} | Stones: {primevalStones}";
            }
        }

        private void HandleInteractPressed() => EmitSignal(SignalName.InteractRequested, _entityId);
    }
}
