using System;
using System.Collections.Generic;
using Godot;
using Color = Godot.Color;
using TaiwuGu.Core;
using TaiwuGu.Entities;
using TaiwuGu.Map;
using TaiwuGu.Systems;

namespace Taiwu_Godot.Scripts.Map
{
    public partial class TileLandmarkVisualManager : Node2D
    {
        [Export] public IsometricTileManager? TileManager { get; set; }
        [Export] public TileVisualDatabase? VisualDatabase { get; set; }

        private IWorldStateProvider? _worldProvider;
        private readonly List<Node2D> activeLandmarks = new();
        private readonly Dictionary<Node2D, (Vector2I pos, Sprite2D sprite)> landmarkDataMap = new();
        private readonly List<Node2D> activeCultivatorTokens = new();

        private Texture2D? clanVillageTex;
        private Texture2D? merchantInnTex;
        private Texture2D? primevalSpringTex;
        private Texture2D? bambooMountainTex;
        private Texture2D? jadeMedallionTex;
        private Texture2D? clanBronzeTex;
        private Texture2D? caravanBrassTex;

        public override void _Ready()
        {
            YSortEnabled = true;

            LoadTextures();
            WorldSignals.OnTopicsChanged += HandleTopicsChanged;

            if (TileManager != null)
            {
                SpawnRegionLandmarks();
            }
        }

        public override void _ExitTree()
        {
            WorldSignals.OnTopicsChanged -= HandleTopicsChanged;
            ClearLandmarks();
            ClearCultivatorTokens();
        }

        private void LoadTextures()
        {
            clanVillageTex = ResourceLoader.Load<Texture2D>("res://Resources/Landmarks/poi_clan_village.png");
            merchantInnTex = ResourceLoader.Load<Texture2D>("res://Resources/Landmarks/poi_merchant_inn.png");
            primevalSpringTex = ResourceLoader.Load<Texture2D>("res://Resources/Landmarks/poi_primeval_spring.png");
            bambooMountainTex = ResourceLoader.Load<Texture2D>("res://Resources/Landmarks/tile_bamboo_mountain.png");
            jadeMedallionTex = ResourceLoader.Load<Texture2D>("res://Resources/Tokens/token_player_jade.png");
            clanBronzeTex = ResourceLoader.Load<Texture2D>("res://Resources/Tokens/token_clan_bronze.png");
            caravanBrassTex = ResourceLoader.Load<Texture2D>("res://Resources/Tokens/token_caravan_brass.png");
        }

        public void Initialize(IsometricTileManager manager, IWorldStateProvider? provider = null)
        {
            TileManager = manager;
            _worldProvider = provider;
            if (VisualDatabase == null)
            {
                VisualDatabase = TileVisualDatabase.CreateDefaultDatabase();
            }
            LoadTextures();
            SpawnRegionLandmarks();
        }

        private void HandleTopicsChanged(DirtyTopic topics)
        {
            if ((topics & DirtyTopic.Map) != 0)
            {
                SpawnRegionLandmarks();
                return;
            }
            if ((topics & DirtyTopic.Fog) != 0)
            {
                UpdateLandmarkFogVisibility();
            }
            if ((topics & DirtyTopic.NPCs) != 0)
            {
                RefreshCultivatorTokens();
            }
        }

        private void RefreshCultivatorTokens()
        {
            ClearCultivatorTokens();
            if (TileManager == null || _worldProvider == null) return;

            var occupiedTiles = SocialVendettaSystem.GetOccupiedNPCTiles(_worldProvider.Current);
            for (int i = 0; i < occupiedTiles.Count; i++)
            {
                Vector2I pos = occupiedTiles[i].ToGodot();
                Vector2 worldPos = TileManager.GridToWorld(pos);
                Node2D? token = CreateCultivatorTokenObject(worldPos, pos);
                if (token != null)
                {
                    AddChild(token);
                    activeCultivatorTokens.Add(token);
                }
            }
        }

        public void SpawnRegionLandmarks()
        {
            if (TileManager == null) return;

            ClearLandmarks();
            ClearCultivatorTokens();

            int width = TileManager.GridWidth;
            int height = TileManager.GridHeight;

            int cx = Math.Clamp(width / 2, 0, width - 1);
            int cy = Math.Clamp(height / 2, 0, height - 1);

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    TileNode node = TileManager.GetTile(x, y);
                    if (!node.IsActive || node.POI == TilePOIType.None) continue;

                    Vector2I gridPos = new(x, y);
                    Vector2 tileWorld = TileManager.GridToWorld(gridPos);
                    Node2D? landmark = CreatePOILandmarkObject(node.POI, tileWorld, gridPos);
                    if (landmark != null)
                    {
                        AddChild(landmark);
                        activeLandmarks.Add(landmark);
                    }
                }
            }

            UpdateLandmarkFogVisibility();
            RefreshCultivatorTokens();
        }

        private void ClearLandmarks()
        {
            for (int i = 0; i < activeLandmarks.Count; i++)
            {
                if (GodotObject.IsInstanceValid(activeLandmarks[i]))
                {
                    activeLandmarks[i].QueueFree();
                }
            }
            activeLandmarks.Clear();
            landmarkDataMap.Clear();
        }

        private void ClearCultivatorTokens()
        {
            for (int i = 0; i < activeCultivatorTokens.Count; i++)
            {
                if (GodotObject.IsInstanceValid(activeCultivatorTokens[i]))
                {
                    activeCultivatorTokens[i].QueueFree();
                }
            }
            activeCultivatorTokens.Clear();
        }

        private Node2D? CreatePOILandmarkObject(TilePOIType poi, Vector2 worldPos, Vector2I gridPos)
        {
            if (VisualDatabase == null)
            {
                VisualDatabase = TileVisualDatabase.CreateDefaultDatabase();
            }

            var entry = VisualDatabase.GetEntry(poi);
            if (entry == null || entry.DioramaTexture == null) return null;

            Texture2D tex = entry.DioramaTexture;
            Color tint = entry.Tint;
            float targetFootprintWidth = entry.TargetFootprintWidth;
            float contentWidth = entry.ContentWidth;
            Vector2 footprintPivot = entry.FootprintPivot;

            Node2D container = new()
            {
                Name = $"Landmark_{poi}_{gridPos.X}_{gridPos.Y}",
                Position = worldPos,
                YSortEnabled = true
            };

            // Grounding shadow removed per user request

            // 2. High-Fidelity Diorama Sprite, aligned to fill the full diamond footprint (~4.8-5.0 units)
            float texW = tex.GetWidth();
            float texH = tex.GetHeight();
            float scale = targetFootprintWidth / Math.Max(1f, contentWidth);

            Sprite2D sprite = new()
            {
                Name = "DioramaSprite",
                Texture = tex,
                Modulate = tint,
                Scale = new Vector2(scale, scale),
                Centered = false,
                Position = new Vector2(-footprintPivot.X * texW * scale, -footprintPivot.Y * texH * scale)
            };

            container.AddChild(sprite);
            landmarkDataMap[container] = (gridPos, sprite);

            return container;
        }

        /// <summary>
        /// Spawns a Halal-compliant carved bronze clan talisman medallion token with grounding shadow and 【修】 seal calligraphy.
        /// Zero depictions of living human/animal bodies, faces, or eyes.
        /// </summary>
        private Node2D? CreateCultivatorTokenObject(Vector2 worldPos, Vector2I gridPos)
        {
            Node2D container = new()
            {
                Name = $"CultivatorToken_{gridPos.X}_{gridPos.Y}",
                Position = worldPos,
                YSortEnabled = true,
                ZIndex = 3
            };

            // 1. Grounding Ink Shadow (scaled to match 2:1 isometric ratio)
            var shadow = new EllipticalInkGroundShadow(0.44f, 0.22f, 0.45f)
            {
                Name = "TokenShadow"
            };
            container.AddChild(shadow);

            // 2. Halal Carved Bronze Clan Medallion with Radiant Gold Seal Calligraphy 【修】
            var talismanToken = new TalismanMedallionToken(clanBronzeTex ?? jadeMedallionTex, "修", 0.70f)
            {
                Name = "ClanMedallion"
            };
            container.AddChild(talismanToken);

            return container;
        }

        public void UpdateLandmarkFogVisibility()
        {
            if (TileManager == null) return;

            for (int i = 0; i < activeLandmarks.Count; i++)
            {
                Node2D landmark = activeLandmarks[i];
                if (!GodotObject.IsInstanceValid(landmark)) continue;

                if (landmarkDataMap.TryGetValue(landmark, out var data))
                {
                    TileNode node = TileManager.GetTile(data.pos.X, data.pos.Y);
                    bool explored = node.IsExplored;
                    bool visible = node.IsVisible;

                    landmark.Visible = explored;
                    if (explored)
                    {
                        // Clean, bright full-color rendering (no shadow overlay over locations)
                        data.sprite.Modulate = Colors.White;
                    }
                }
            }

            for (int i = 0; i < activeCultivatorTokens.Count; i++)
            {
                Node2D token = activeCultivatorTokens[i];
                if (!GodotObject.IsInstanceValid(token)) continue;
                // Tokens on explored tiles remain visible
                token.Visible = true;
            }
        }

        /// <summary>
        /// Elliptical ink grounding shadow drawn in 2:1 isometric ratio.
        /// </summary>
        public partial class EllipticalInkGroundShadow : Node2D
        {
            private readonly float _rx;
            private readonly float _ry;
            private readonly Color _col;

            public EllipticalInkGroundShadow(float radiusX, float radiusY, float alpha)
            {
                _rx = radiusX;
                _ry = radiusY;
                _col = new Color(0f, 0f, 0f, alpha);
                ZIndex = 0;
            }

            public override void _Draw()
            {
                const int segments = 24;
                Vector2[] pts = new Vector2[segments];
                for (int i = 0; i < segments; i++)
                {
                    float angle = i * (MathF.Tau / segments);
                    pts[i] = new Vector2(MathF.Cos(angle) * _rx, MathF.Sin(angle) * _ry);
                }
                DrawColoredPolygon(pts, _col);
            }
        }

        /// <summary>
        /// Halal-compliant carved Chinese talisman medallion token (Jade/Bronze/Brass)
        /// with grounding elliptical ink-wash shadow and radiant imperial gold seal calligraphy (【道】/【修】).
        /// Zero depictions of living creatures, faces, or animate human forms.
        /// </summary>
        public partial class TalismanMedallionToken : Node2D
        {
            private readonly Texture2D? _tokenTexture;
            private readonly string _sealText;
            private readonly float _targetDiameter;
            private static readonly Color GoldColor = new Color(0.984f, 0.749f, 0.141f, 1f); // Imperial Gold #FBBF24
            private static readonly Color GoldGlowColor = new Color(0.984f, 0.749f, 0.141f, 0.45f);

            public TalismanMedallionToken(Texture2D? tokenTexture, string sealText = "修", float targetDiameter = 0.70f)
            {
                _tokenTexture = tokenTexture;
                _sealText = sealText;
                _targetDiameter = targetDiameter;
                ZIndex = 1;
            }

            public override void _Draw()
            {
                float radius = _targetDiameter * 0.5f;

                if (_tokenTexture != null)
                {
                    // Draw authentic carved talisman medallion sprite centered and slightly elevated for 2.5D perspective
                    var rect = new Rect2(-radius, -radius - 0.04f, _targetDiameter, _targetDiameter);
                    DrawTextureRect(_tokenTexture, rect, false);
                }
                else
                {
                    // Fallback jade body
                    DrawCircle(new Vector2(0f, -0.04f), radius, new Color(0.04f, 0.45f, 0.35f, 1f));
                    DrawArc(new Vector2(0f, -0.04f), radius, 0f, MathF.Tau, 32, GoldColor, 0.04f, antialiased: true);
                }

                // Authentic Radiant Gold Seal Calligraphy with soft drop shadow
                if (!string.IsNullOrEmpty(_sealText))
                {
                    var font = ThemeDB.FallbackFont;
                    int fontSize = 24;
                    var stringSize = font.GetStringSize(_sealText, HorizontalAlignment.Center, -1, fontSize);

                    // World unit compensation: camera zoom is ~68, so text scale is 1.0 / 68
                    float textScale = 1.0f / 68.0f;

                    // Subtle ambient gold glow ring around the seal calligraphy
                    DrawArc(new Vector2(0f, -0.04f), radius * 0.52f, 0f, MathF.Tau, 24, GoldGlowColor, 0.02f, antialiased: true);

                    // Draw calligraphy with drop shadow scaled to world space
                    DrawSetTransform(new Vector2(0f, -0.04f), 0f, new Vector2(textScale, textScale));

                    Vector2 localCenter = new Vector2(-stringSize.X * 0.5f, stringSize.Y * 0.35f);
                    // Drop shadow
                    DrawString(font, localCenter + new Vector2(1.2f, 1.5f), _sealText, HorizontalAlignment.Center, -1, fontSize, new Color(0.02f, 0.04f, 0.03f, 0.95f));
                    // Radiant Gold Seal Calligraphy
                    DrawString(font, localCenter, _sealText, HorizontalAlignment.Center, -1, fontSize, GoldColor);

                    // Reset transform
                    DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
                }
            }
        }

        /// <summary>
        /// Legacy alias for backwards compatibility.
        /// </summary>
        public partial class ProceduralJadeMedallion : TalismanMedallionToken
        {
            public ProceduralJadeMedallion(float radius) : base(null, "道", radius * 2f) { }
        }
    }
}
