using Godot;

namespace Taiwu_Godot.Scripts.UI
{
    /// <summary>Bronze-framed fluid gauge used by the ledger's sea, wall and satiety rows.</summary>
    public partial class StyledGaugeControl : Control
    {
        public Texture2D? FrameTexture { get; set; }
        public Texture2D? FillTexture { get; set; }
        public float Value { get; set; } = 1.0f; // [0..1]
        public string CenterText { get; set; } = "";

        public override void _Draw()
        {
            var size = Size;
            var bgRect = new Rect2(Vector2.Zero, size);

            float padX = (FrameTexture != null) ? size.X * 0.1435f : 0f;
            float padY = (FrameTexture != null) ? size.Y * 0.2209f : 0f;
            float innerW = size.X - (padX * 2f);
            float innerH = size.Y - (padY * 2f);
            float clampedFill = Mathf.Clamp(Value, 0f, 1f);

            // 1. Dark background in cavity
            DrawRect(new Rect2(padX, padY, innerW, innerH), new Color(0.047f, 0.063f, 0.055f, 1f));

            // 2. Fluid fill strictly inside cavity (never leaks through ornamental bronze ends)
            if (clampedFill > 0.001f)
            {
                float fillW = innerW * clampedFill;
                var fillRect = new Rect2(padX, padY, fillW, innerH);

                if (FillTexture != null)
                {
                    var srcRect = new Rect2(0f, 0f, FillTexture.GetWidth() * clampedFill, FillTexture.GetHeight());
                    DrawTextureRectRegion(FillTexture, fillRect, srcRect);
                }
                else
                {
                    DrawRect(fillRect, new Color(0.133f, 0.773f, 0.369f, 0.85f));
                }
            }

            // 3. Bronze Frame Over entire bounds
            if (FrameTexture != null)
            {
                DrawTextureRect(FrameTexture, bgRect, false);
            }
            else
            {
                DrawRect(bgRect, new Color(0.851f, 0.467f, 0.024f, 0.6f), false, 1f);
            }

            // 4. Center Label with Drop Shadow
            if (!string.IsNullOrEmpty(CenterText))
            {
                var font = ThemeDB.FallbackFont;
                int fontSize = 11;
                var stringSize = font.GetStringSize(CenterText, HorizontalAlignment.Center, -1, fontSize);
                var textPos = new Vector2((size.X - stringSize.X) * 0.5f, (size.Y + stringSize.Y * 0.65f) * 0.5f);

                DrawString(font, textPos + new Vector2(1f, 1f), CenterText, HorizontalAlignment.Center, -1, fontSize, new Color(0f, 0f, 0f, 0.9f));
                DrawString(font, textPos, CenterText, HorizontalAlignment.Center, -1, fontSize, new Color(1f, 1f, 1f, 0.95f));
            }
        }
    }

    /// <summary>Directional reputation gauge with a centre zero line, spanning [-300..+300].</summary>
    public partial class FactionReputationGaugeControl : Control
    {
        public int Score { get; set; } = 0; // [-300 .. +300]

        public override void _Draw()
        {
            var size = Size;
            var bgRect = new Rect2(Vector2.Zero, size);

            // 1. Dark background
            DrawRect(bgRect, new Color(0.047f, 0.063f, 0.055f, 1f));

            float midX = size.X * 0.5f;

            // 2. Directional Fill
            if (Score > 0)
            {
                float ratio = Mathf.Clamp(Score / 300f, 0f, 1f);
                float fillW = (size.X * 0.5f) * ratio;
                DrawRect(new Rect2(midX, 1f, fillW, size.Y - 2f), new Color(0.204f, 0.827f, 0.6f, 0.85f));
            }
            else if (Score < 0)
            {
                float ratio = Mathf.Clamp(Mathf.Abs(Score) / 300f, 0f, 1f);
                float fillW = (size.X * 0.5f) * ratio;
                DrawRect(new Rect2(midX - fillW, 1f, fillW, size.Y - 2f), new Color(0.937f, 0.267f, 0.267f, 0.85f));
            }

            // 3. Center zero line
            DrawLine(new Vector2(midX, 0f), new Vector2(midX, size.Y), new Color(0.58f, 0.64f, 0.72f, 1f), 1.5f);

            // 4. Border outline
            DrawRect(bgRect, new Color(0.133f, 0.200f, 0.188f, 1f), false, 1f);
        }
    }
}
