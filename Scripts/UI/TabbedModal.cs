using System;
using System.Collections.Generic;
using Godot;

namespace Taiwu_Godot.Scripts.UI
{
    /// <summary>
    /// Behaviour half of the tabbed modal frame. TabbedModal.tscn composes
    /// ModalScaffold.tscn with a row above the tab strip, the strip itself, and a body
    /// that hosts one page per tab; this controller owns tab registration, selection,
    /// page visibility and tab styling.
    /// Views keep their page contents and their own refresh logic; they only hand over
    /// each page root once, so the strip and the "exactly one body visible" rule stop
    /// being copy-pasted per view. A tab may be registered without a strip button, for
    /// views whose sub-views are reached through their own buttons.
    /// </summary>
    public partial class TabbedModal : Control
    {
        private const string ContentPath = "Scaffold/CenterContainer/Panel/Margin/VBox/Content";

        private readonly List<Control> _pages = new();
        private readonly List<Button?> _tabButtons = new();

        public ModalScaffold? Scaffold { get; private set; }

        /// <summary>Rows above the tab strip: subtitle, status line, and similar.</summary>
        public VBoxContainer? HeaderRows { get; private set; }

        public HBoxContainer? TabBar { get; private set; }

        /// <summary>Host for the tab pages; also usable for panel styling overrides.</summary>
        public PanelContainer? Body { get; private set; }

        public int ActiveIndex { get; private set; } = -1;

        /// <summary>Raised when a strip button changes the active tab, so the view can refresh that page.</summary>
        public event Action<int>? TabSelected;

        public override void _Ready()
        {
            Scaffold = GetNodeOrNull<ModalScaffold>("Scaffold");
            HeaderRows = GetNodeOrNull<VBoxContainer>($"{ContentPath}/HeaderRows");
            TabBar = GetNodeOrNull<HBoxContainer>($"{ContentPath}/Tabs");
            Body = GetNodeOrNull<PanelContainer>($"{ContentPath}/Body");

            if (Scaffold == null || HeaderRows == null || TabBar == null || Body == null)
            {
                GD.PushError("[TabbedModal] TabbedModal.tscn is missing part of its layout (Scaffold, HeaderRows, Tabs, Body).");
            }
        }

        /// <summary>
        /// Registers <paramref name="page"/> as the next tab and, unless
        /// <paramref name="showButton"/> is false, gives it a strip button that selects it.
        /// The page starts hidden; call <see cref="SelectTab"/> to show one.
        /// </summary>
        public void AddTab(string label, Control? page, float minWidth = 0f, bool showButton = true)
        {
            if (page == null) return;

            _pages.Add(page);
            Body?.AddChild(page);
            page.Visible = false;

            Button? button = null;
            if (showButton && TabBar != null)
            {
                button = new Button
                {
                    Text = label,
                    CustomMinimumSize = new Vector2(minWidth, 32f),
                    MouseFilter = MouseFilterEnum.Stop,
                    FocusMode = FocusModeEnum.None
                };
                int index = _pages.Count - 1;
                button.Pressed += () => SelectTab(index);
                TabBar.AddChild(button);
            }
            _tabButtons.Add(button);

            // A strip with no buttons collapses, so button-driven views keep their spacing.
            if (TabBar != null)
            {
                TabBar.Visible = TabBar.GetChildCount() > 0;
            }
        }

        public void SetTabLabel(int index, string label)
        {
            if (index < 0 || index >= _tabButtons.Count) return;
            if (_tabButtons[index] is { } button) button.Text = label;
        }

        public void SelectTab(int index)
        {
            if (_pages.Count == 0) return;

            index = Mathf.Clamp(index, 0, _pages.Count - 1);
            bool changed = index != ActiveIndex;
            ActiveIndex = index;

            for (int i = 0; i < _pages.Count; i++)
            {
                _pages[i].Visible = i == index;
            }

            for (int i = 0; i < _tabButtons.Count; i++)
            {
                StyleTabButton(_tabButtons[i], i == index);
            }

            if (changed) TabSelected?.Invoke(index);
        }

        private static void StyleTabButton(Button? button, bool isActive)
        {
            if (button == null) return;

            var style = new StyleBoxFlat
            {
                BgColor = isActive ? new Color(0.133f, 0.200f, 0.188f, 1f) : new Color(0.082f, 0.118f, 0.114f, 1f),
                BorderColor = isActive ? new Color(0.961f, 0.620f, 0.043f, 1f) : new Color(0.133f, 0.200f, 0.188f, 0.8f),
                BorderWidthBottom = isActive ? 2 : 1,
                BorderWidthTop = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                CornerRadiusTopLeft = 3,
                CornerRadiusTopRight = 3
            };
            button.AddThemeStyleboxOverride("normal", style);
            button.AddThemeStyleboxOverride("hover", style);
            button.AddThemeStyleboxOverride("pressed", style);
            button.Modulate = isActive ? new Color(1f, 1f, 1f, 1f) : new Color(0.7f, 0.75f, 0.8f, 0.9f);
        }
    }
}
