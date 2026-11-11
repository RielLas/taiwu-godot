using System;
using Godot;
using TaiwuGu.Combat;
using TaiwuGu.Core;
using TaiwuGu.Entities;
using TaiwuGu.Systems;
using Taiwu_Godot.Scripts;
using Color = Godot.Color;
using Mathf = Godot.Mathf;

namespace Taiwu_Godot.Scripts.Combat
{
    /// <summary>
    /// Immediate-mode combat arena presentation at world position (2000, 2000).
    /// Owns camera handover only; every entity/projectile is read from the simulation buffers.
    /// Strict Halal aniconism: medallions, geometric totems and seal calligraphy only.
    /// </summary>
    public partial class CombatArenaView : Node2D
    {
        private const float ArenaHalfW = CombatArenaSimulation.ARENA_HALF_W;
        private const float ArenaHalfH = CombatArenaSimulation.ARENA_HALF_H;
        private const float ArenaZoom = 56f;
        private const float Px = 1f / ArenaZoom; // World-unit equivalent of 1 screen pixel at battle zoom

        private static readonly Color Lacquer = new Color(0.031f, 0.051f, 0.050f, 0.985f);
        private static readonly Color LacquerEdge = new Color(0.72f, 0.56f, 0.24f, 0.85f);
        private static readonly Color Jade = new Color(0.20f, 0.83f, 0.60f, 1f);
        private static readonly Color Cyan = new Color(0.13f, 0.83f, 0.93f, 1f);
        private static readonly Color Crimson = new Color(0.86f, 0.22f, 0.24f, 1f);
        private static readonly Color Bronze = new Color(0.80f, 0.58f, 0.24f, 1f);
        private static readonly Color Amber = new Color(0.98f, 0.75f, 0.14f, 1f);
        private static readonly Color Ink = new Color(0.88f, 0.91f, 0.94f, 1f);
        private static readonly Color HitFlash = new Color(1f, 0.98f, 0.90f, 1f);

        private IWorldStateProvider? _provider;
        private Camera2D? _camera;
        private Font? _font;

        private Vector2 _savedCameraPosition;
        private Vector2 _savedZoom = Vector2.One;
        private bool _hasSavedCamera;

        public void Initialize(IWorldStateProvider provider, Camera2D? camera)
        {
            _provider = provider;
            _camera = camera;
        }

        public override void _Ready()
        {
            Visible = false;
            _font = GD.Load<Font>("res://Resources/Fonts/cjk_fallback_font.tres");
            WorldSignals.OnBattleStageChanged += HandleBattleStageChanged;
        }

        public override void _ExitTree()
        {
            WorldSignals.OnBattleStageChanged -= HandleBattleStageChanged;
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (!Visible || GetTree().Paused) return;

            var world = _provider?.Current;
            if (world == null) return;
            var session = world.Get<CombatSessionComponent>(world.WorldEntity);
            if (session == null || !session.Simulation.IsActive) return;
            var simulation = session.Simulation;

            if (@event is InputEventKey key && key.Pressed && !key.Echo)
            {
                int slot = key.Keycode switch
                {
                    Key.Key1 => 0,
                    Key.Key2 => 1,
                    Key.Key3 => 2,
                    Key.Key4 => 3,
                    Key.Key5 => 4,
                    _ => -1
                };

                if (slot >= 0)
                {
                    simulation.TryCastSkill(world, slot);
                    GetViewport().SetInputAsHandled();
                    return;
                }

                if (key.Keycode == Key.Space)
                {
                    // Space is the canonical dash shortcut (slot 2 / Movement Gu).
                    simulation.TryCastSkill(world, 2);
                    GetViewport().SetInputAsHandled();
                    return;
                }

                if (key.Keycode == Key.Q)
                {
                    simulation.TryCrushStone(world);
                    GetViewport().SetInputAsHandled();
                }
            }
            else if (@event is InputEventMouseButton mouse &&
                     mouse.ButtonIndex == MouseButton.Left &&
                     mouse.Pressed)
            {
                simulation.TryCastSkill(world, 0);
                GetViewport().SetInputAsHandled();
            }
        }

        public override void _Process(double delta)
        {
            if (!Visible || GetTree().Paused) return;

            var world = _provider?.Current;
            if (world == null) return;
            var session = world.Get<CombatSessionComponent>(world.WorldEntity);
            if (session == null || !session.InBattle) return;
            var simulation = session.Simulation;
            if (!simulation.IsActive) return;

            float moveX = 0f;
            float moveY = 0f;
            if (Godot.Input.IsKeyPressed(Key.W) || Godot.Input.IsKeyPressed(Key.Up)) moveY -= 1f;
            if (Godot.Input.IsKeyPressed(Key.S) || Godot.Input.IsKeyPressed(Key.Down)) moveY += 1f;
            if (Godot.Input.IsKeyPressed(Key.A) || Godot.Input.IsKeyPressed(Key.Left)) moveX -= 1f;
            if (Godot.Input.IsKeyPressed(Key.D) || Godot.Input.IsKeyPressed(Key.Right)) moveX += 1f;

            Vector2 mouse = GetGlobalMousePosition() - GlobalPosition;
            simulation.SetPlayerMoveAndAim(moveX, moveY, mouse.X, mouse.Y);
            QueueRedraw();
        }

        private void HandleBattleStageChanged(bool inBattle)
        {
            if (inBattle)
            {
                Visible = true;
                if (_camera != null)
                {
                    _savedCameraPosition = _camera.Position;
                    _savedZoom = _camera.Zoom;
                    _hasSavedCamera = true;
                    _camera.Position = new Vector2(2000f, 2000f);
                    _camera.Zoom = new Vector2(56f, 56f);
                }
            }
            else
            {
                Visible = false;
                if (_camera != null && _hasSavedCamera)
                {
                    var world = _provider?.Current;
                    if (world != null)
                    {
                        _camera.Position = ExplorationSystem.GridToWorld(world.GetPlayerPosition()).ToGodot();
                    }
                    else
                    {
                        _camera.Position = _savedCameraPosition;
                    }
                    _camera.Zoom = _savedZoom;
                    _hasSavedCamera = false;
                }
            }

            QueueRedraw();
        }

        public override void _Draw()
        {
            var world = _provider?.Current;
            if (world == null) return;
            var session = world.Get<CombatSessionComponent>(world.WorldEntity);
            if (session == null || !session.InBattle) return;
            var simulation = session.Simulation;

            DrawArenaFrame();
            DrawEntities(simulation);
            DrawProjectiles(simulation);
        }

        private void DrawArenaFrame()
        {
            var rect = new Rect2(-ArenaHalfW, -ArenaHalfH, ArenaHalfW * 2f, ArenaHalfH * 2f);
            DrawRect(rect, Lacquer, true);
            DrawRect(rect, LacquerEdge, false, 2.5f * Px);

            DrawArc(Vector2.Zero, 4.4f, 0f, Mathf.Tau, 96, new Color(0.72f, 0.56f, 0.24f, 0.22f), 1.5f * Px, true);
            DrawArc(Vector2.Zero, 3.6f, 0f, Mathf.Tau, 96, new Color(0.72f, 0.56f, 0.24f, 0.16f), 1.5f * Px, true);
            DrawArc(Vector2.Zero, 2.4f, 0f, Mathf.Tau, 64, new Color(0.72f, 0.56f, 0.24f, 0.12f), 1.5f * Px, true);

            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.Tau / 8f;
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                DrawLine(dir * 2.4f, dir * 3.6f, new Color(0.72f, 0.56f, 0.24f, 0.28f), 2.0f * Px, true);
                DrawLine(dir * 4.4f, dir * 4.7f, new Color(0.72f, 0.56f, 0.24f, 0.20f), 1.5f * Px, true);
            }

            DrawSeal(new Vector2(0f, -ArenaHalfH + 0.55f), "【斗】", Amber, 22);
        }

        private void DrawEntities(CombatArenaSimulation simulation)
        {
            for (int i = 0; i < CombatArenaSimulation.MAX_ENTITIES; i++)
            {
                CombatEntityData entity = simulation.EntityBuffer[i];
                if (!entity.Active) continue;

                var position = new Vector2(entity.PosX, entity.PosY);
                bool flash = entity.HitFlashTimer > 0f;
                switch (entity.Archetype)
                {
                    case CombatEntityArchetype.PlayerCultivator:
                        DrawPlayer(entity, position, flash);
                        break;
                    case CombatEntityArchetype.WildBoarSwarm:
                        Color boarCol = flash ? HitFlash : Crimson;
                        DrawDiamondTotem(position, entity.Radius, boarCol);
                        DrawSeal(position + new Vector2(0f, entity.Radius + 0.36f), "【兽】", boarCol, 16);
                        break;
                    case CombatEntityArchetype.ClanPatrol:
                        Color patrolCol = flash ? HitFlash : Bronze;
                        DrawOctagonTotem(position, entity.Radius, patrolCol);
                        DrawSeal(position + new Vector2(0f, entity.Radius + 0.38f), "【卫】", patrolCol, 16);
                        break;
                    case CombatEntityArchetype.DemonicCultivator:
                        DrawBaguaMandala(position, entity.Radius, flash ? HitFlash : Crimson);
                        DrawSeal(position + new Vector2(0f, entity.Radius + 0.40f), "【魔】", flash ? HitFlash : Crimson, 16);
                        break;
                }

                DrawHealthBar(entity, position);
            }
        }

        private void DrawPlayer(CombatEntityData player, Vector2 position, bool flash)
        {
            Color body = flash ? HitFlash : new Color(0.13f, 0.77f, 0.52f, 1f);
            DrawCircle(position, player.Radius, body);
            DrawArc(position, player.Radius, 0f, Mathf.Tau, 40, flash ? HitFlash : Cyan, 2.5f * Px, true);
            DrawArc(position, player.Radius * 0.62f, 0f, Mathf.Tau, 28, new Color(0.03f, 0.11f, 0.09f, 1f), 2.0f * Px, true);

            if (player.ShieldHP > 0f)
            {
                DrawArc(position, player.Radius + 0.20f, 0f, Mathf.Tau, 48, Cyan, 2.5f * Px, true);
                DrawArc(position, player.Radius + 0.30f, 0f, Mathf.Tau, 48, new Color(0.13f, 0.83f, 0.93f, 0.35f), 1.5f * Px, true);
            }

            var aim = new Vector2(player.AimX, player.AimY);
            if (aim.LengthSquared() < 0.01f) aim = Vector2.Right;
            var tip = position + aim * (player.Radius + 0.55f);
            var left = position + aim.Rotated(0.55f) * (player.Radius + 0.32f);
            var right = position + aim.Rotated(-0.55f) * (player.Radius + 0.32f);
            DrawLine(left, tip, Amber, 2.5f * Px, true);
            DrawLine(right, tip, Amber, 2.5f * Px, true);

            DrawSeal(position + new Vector2(0f, player.Radius + 0.40f), "【道】", Jade, 16);
        }

        private void DrawDiamondTotem(Vector2 position, float radius, Color color)
        {
            var top = position + new Vector2(0f, -radius);
            var right = position + new Vector2(radius, 0f);
            var bottom = position + new Vector2(0f, radius);
            var left = position + new Vector2(-radius, 0f);

            DrawLine(top, right, color, 2.2f * Px, true);
            DrawLine(right, bottom, color, 2.2f * Px, true);
            DrawLine(bottom, left, color, 2.2f * Px, true);
            DrawLine(left, top, color, 2.2f * Px, true);
            DrawLine(top, bottom, new Color(color.R, color.G, color.B, 0.45f), 1.2f * Px, true);
            DrawCircle(position, radius * 0.28f, color);
        }

        private void DrawOctagonTotem(Vector2 position, float radius, Color color)
        {
            Vector2 previous = Vector2.Zero;
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.Tau / 8f + Mathf.Pi / 8f;
                var point = position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                if (i > 0) DrawLine(previous, point, color, 2.2f * Px, true);
                previous = point;
            }
            DrawLine(previous, position + new Vector2(Mathf.Cos(Mathf.Pi / 8f), Mathf.Sin(Mathf.Pi / 8f)) * radius, color, 2.2f * Px, true);
            DrawCircle(position, radius * 0.25f, color);
        }

        private void DrawBaguaMandala(Vector2 position, float radius, Color color)
        {
            DrawArc(position, radius, 0f, Mathf.Tau, 48, color, 2.5f * Px, true);
            DrawArc(position, radius * 0.62f, 0f, Mathf.Tau, 32, new Color(0.55f, 0.10f, 0.14f, 1f), 2.0f * Px, true);
            DrawArc(position, radius * 0.30f, 0f, Mathf.Tau, 24, color, 1.8f * Px, true);

            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.Tau / 8f;
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                DrawLine(position + dir * radius * 0.34f, position + dir * radius, new Color(0.55f, 0.10f, 0.14f, 0.9f), 1.8f * Px, true);
            }
        }

        private void DrawHealthBar(CombatEntityData entity, Vector2 position)
        {
            float width = Mathf.Max(0.9f, entity.Radius * 2.6f);
            float height = 0.10f;
            var origin = position + new Vector2(-width * 0.5f, -entity.Radius - 0.32f);

            DrawRect(new Rect2(origin, new Vector2(width, height)), new Color(0.02f, 0.03f, 0.03f, 0.9f), true);
            float ratio = entity.MaxHealth > 0f ? Mathf.Clamp(entity.Health / entity.MaxHealth, 0f, 1f) : 0f;
            Color fill = entity.Team == CombatEntityTeam.Player ? Jade : Crimson;
            DrawRect(new Rect2(origin, new Vector2(width * ratio, height)), fill, true);
        }

        private void DrawProjectiles(CombatArenaSimulation simulation)
        {
            for (int i = 0; i < CombatArenaSimulation.MAX_PROJECTILES; i++)
            {
                CombatProjectileData projectile = simulation.ProjectileBuffer[i];
                if (!projectile.Active) continue;

                var position = new Vector2(projectile.PosX, projectile.PosY);
                var direction = new Vector2(projectile.VelX, projectile.VelY);
                if (direction.LengthSquared() > 0.0001f) direction = direction.Normalized();

                switch (projectile.VisualStyle)
                {
                    case 0:
                    {
                        float spin = simulation.ElapsedTime * 9f;
                        DrawArc(position, 0.28f, spin, spin + 2.1f, 12, Cyan, 2.8f * Px, true);
                        DrawArc(position, 0.16f, spin + Mathf.Pi, spin + Mathf.Pi + 1.6f, 8, new Color(0.13f, 0.83f, 0.93f, 0.55f), 1.8f * Px, true);
                        break;
                    }
                    case 1:
                        DrawDiamondTotem(position, 0.22f, Crimson);
                        break;
                    case 2:
                        DrawArc(position, 0.26f, 0f, Mathf.Tau * 0.62f, 10, Crimson, 2.8f * Px, true);
                        break;
                    default:
                        DrawLine(position - direction * 0.30f, position + direction * 0.34f, Amber, 3.2f * Px, true);
                        break;
                }
            }
        }

        private void DrawSeal(Vector2 position, string text, Color color, int fontSize)
        {
            Font font = _font ?? ThemeDB.FallbackFont;
            if (font == null) return;

            // Compensate for Camera2D zoom (~56x) so calligraphy stays razor-sharp in world space
            Vector2 strSize = font.GetStringSize(text, HorizontalAlignment.Center, -1, fontSize);
            DrawSetTransform(position, 0f, new Vector2(Px, Px));
            Vector2 localOrigin = new Vector2(-strSize.X * 0.5f, strSize.Y * 0.30f);
            DrawString(font, localOrigin + new Vector2(1.2f, 1.5f), text, HorizontalAlignment.Left, -1, fontSize, new Color(0.01f, 0.02f, 0.02f, 0.95f));
            DrawString(font, localOrigin, text, HorizontalAlignment.Left, -1, fontSize, color);
            DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
        }
    }
}
