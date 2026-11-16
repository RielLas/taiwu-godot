using System;
using Godot;
using TaiwuGu.Combat;
using TaiwuGu.Core;
using TaiwuGu.Entities;
using TaiwuGu.Systems;
using Taiwu_Godot.Scripts;
using Taiwu_Godot.Scripts.Map;
using Color = Godot.Color;
using Mathf = Godot.Mathf;

namespace Taiwu_Godot.Scripts.Combat
{
    /// <summary>
    /// Immediate-mode combat arena presentation at world position (2000, 2000).
    /// Uses 2D textures for stone floor, parallax mountains/mist, and entity medallion tokens.
    /// Boundary formations use geometric Daoist array brackets and talisman calligraphy.
    /// Strict Halal aniconism: medallions, geometric totems, and talisman calligraphy only.
    /// Employs a zero-GC ring buffer of 32 floating damage number structs, local hit-stop,
    /// trauma-decoupled mouse aiming with a 0.2 deadzone, and a minimal ink-and-gold reticle.
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

        // Pre-cached damage string lookup (0 B runtime GC allocation)
        private static readonly string[] PrecachedDamageStrings = new string[1000];
        static CombatArenaView()
        {
            for (int i = 0; i < 1000; i++) PrecachedDamageStrings[i] = $"-{i}";
        }

        private static string GetDamageString(float amount)
        {
            int val = (int)MathF.Round(amount);
            if (val >= 0 && val < 1000) return PrecachedDamageStrings[val];
            return $"-{val}";
        }

        // Struct for zero-GC floating combat damage numbers
        private struct FloatingDamageEntry
        {
            public Vector2 Position;
            public float Amount;
            public float Lifetime;
            public float MaxLifetime;
            public Color Color;
            public bool Active;
        }

        private readonly FloatingDamageEntry[] _damageRing = new FloatingDamageEntry[32];
        private int _damageRingHead = 0;

        // Health tracking to detect hits and trigger local hit-stop
        private readonly float[] _prevHealth = new float[CombatArenaSimulation.MAX_ENTITIES];
        private float _hitStopTimer = 0f;

        // Decoupled un-shaken mouse aiming position
        private Vector2 _arenaCursorPos;

        private IWorldStateProvider? _provider;
        private Camera2D? _camera;
        private Font? _font;

        private Vector2 _savedCameraPosition;
        private Vector2 _savedZoom = Vector2.One;
        private bool _hasSavedCamera;

        // Textures loaded in _Ready
        private Texture2D? _texArenaFloor;
        private Texture2D? _texBaguaAureole;
        private Texture2D? _texParallaxMountain;
        private Texture2D? _texParallaxMist;
        private Texture2D? _texTokenPlayer;
        private Texture2D? _texTokenBoar;
        private Texture2D? _texTokenCultivator;
        private Texture2D? _texTokenBoss;
        private Texture2D? _texIconMoonblade;
        private Texture2D? _texIconShockwave;
        private Texture2D? _texIconQiArcs;

        public void Initialize(IWorldStateProvider provider, Camera2D? camera)
        {
            _provider = provider;
            _camera = camera;
        }

        public override void _Ready()
        {
            Visible = false;
            _font = GD.Load<Font>("res://Resources/Fonts/cjk_fallback_font.tres");

            // Load combat texture assets safely
            _texArenaFloor = LoadTexture("res://Resources/Combat/arena_floor_stone.png");
            _texBaguaAureole = LoadTexture("res://Resources/Combat/BaguaAureole.png");
            _texParallaxMountain = LoadTexture("res://Resources/Combat/parallax_mountain_layer.png");
            _texParallaxMist = LoadTexture("res://Resources/Combat/parallax_mist_layer.png");
            _texTokenPlayer = LoadTexture("res://Resources/Combat/token_player_seal.png");
            _texTokenBoar = LoadTexture("res://Resources/Combat/token_beast_boar.png");
            _texTokenCultivator = LoadTexture("res://Resources/Combat/token_cultivator_crimson.png");
            _texTokenBoss = LoadTexture("res://Resources/Combat/token_boss_mandala.png");
            _texIconMoonblade = LoadTexture("res://Resources/Combat/icon_moonblade.png");
            _texIconShockwave = LoadTexture("res://Resources/Combat/icon_mountain_shockwave.png");
            _texIconQiArcs = LoadTexture("res://Resources/Combat/icon_qiarcs.png");

            WorldSignals.OnBattleStageChanged += HandleBattleStageChanged;
        }

        public override void _ExitTree()
        {
            WorldSignals.OnBattleStageChanged -= HandleBattleStageChanged;
        }

        private static Texture2D? LoadTexture(string resPath)
        {
            try
            {
                string globalPath = ProjectSettings.GlobalizePath(resPath);
                if (System.IO.File.Exists(globalPath))
                {
                    var img = Image.LoadFromFile(globalPath);
                    if (img != null && !img.IsEmpty())
                    {
                        return ImageTexture.CreateFromImage(img);
                    }
                }
            }
            catch { }

            if (ResourceLoader.Exists(resPath))
            {
                try
                {
                    return GD.Load<Texture2D>(resPath);
                }
                catch { }
            }
            return null;
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
                    // Space = Dash (slot 2 / Movement Gu) or Shield (slot 1 / Defense)
                    if (!simulation.TryCastSkill(world, 2))
                    {
                        simulation.TryCastSkill(world, 1);
                    }
                    GetViewport().SetInputAsHandled();
                    return;
                }

                if (key.Keycode == Key.Q)
                {
                    simulation.TryCrushStone(world);
                    GetViewport().SetInputAsHandled();
                }
            }
            else if (@event is InputEventMouseButton mouse && mouse.Pressed)
            {
                if (mouse.ButtonIndex == MouseButton.Left)
                {
                    simulation.TryCastSkill(world, 0);
                    GetViewport().SetInputAsHandled();
                }
                else if (mouse.ButtonIndex == MouseButton.Right)
                {
                    // Right-Click = Dash (slot 2 / Movement Gu) or Shield (slot 1 / Defense)
                    if (!simulation.TryCastSkill(world, 2))
                    {
                        simulation.TryCastSkill(world, 1);
                    }
                    GetViewport().SetInputAsHandled();
                }
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

            float dt = (float)delta;

            // 1. Local hit-stop timer update
            if (_hitStopTimer > 0f)
            {
                _hitStopTimer = MathF.Max(0f, _hitStopTimer - dt);
            }

            // 2. Advance active floating damage numbers
            for (int i = 0; i < 32; i++)
            {
                if (!_damageRing[i].Active) continue;
                _damageRing[i].Lifetime -= dt;
                if (_damageRing[i].Lifetime <= 0f)
                {
                    _damageRing[i].Active = false;
                }
                else
                {
                    _damageRing[i].Position += new Vector2(0f, -0.6f * dt);
                }
            }

            // 3. Detect entity health reductions to trigger hit-stop, camera trauma, and damage numbers
            for (int i = 0; i < CombatArenaSimulation.MAX_ENTITIES; i++)
            {
                CombatEntityData entity = simulation.EntityBuffer[i];
                if (entity.Active)
                {
                    float prev = _prevHealth[i];
                    if (prev > 0.001f && entity.Health < prev)
                    {
                        float deltaDmg = prev - entity.Health;
                        SpawnDamageNumber(new Vector2(entity.PosX, entity.PosY), deltaDmg, entity.Team == CombatEntityTeam.Player);

                        // Trigger local hit-stop and camera trauma
                        _hitStopTimer = 0.045f;
                        simulation.HitStopTimer = 0.045f;

                        if (_camera is CameraIsometricController isoCam)
                        {
                            isoCam.AddTrauma(entity.Team == CombatEntityTeam.Player ? 0.40f : 0.25f);
                        }
                    }
                    _prevHealth[i] = entity.Health;
                }
                else
                {
                    _prevHealth[i] = 0f;
                }
            }

            // 4. Calculate un-shaken mouse aim decoupled from camera trauma
            Vector2 viewportCenter = GetViewportRect().Size * 0.5f;
            Vector2 screenOffset = GetViewport().GetMousePosition() - viewportCenter;
            float currentZoom = _camera?.Zoom.X > 0.01f ? _camera.Zoom.X : ArenaZoom;
            Vector2 unShakenCursor = screenOffset / currentZoom;
            float cursorX = Math.Clamp(unShakenCursor.X, -ArenaHalfW, ArenaHalfW);
            float cursorY = Math.Clamp(unShakenCursor.Y, -ArenaHalfH, ArenaHalfH);
            _arenaCursorPos = new Vector2(cursorX, cursorY);

            // Left-click hold-to-fire on cooldown
            if (Godot.Input.IsMouseButtonPressed(MouseButton.Left))
            {
                simulation.TryCastSkill(world, 0);
            }

            // 5. Player movement and aiming inputs with deadzone
            float moveX = 0f;
            float moveY = 0f;
            if (Godot.Input.IsKeyPressed(Key.W) || Godot.Input.IsKeyPressed(Key.Up)) moveY -= 1f;
            if (Godot.Input.IsKeyPressed(Key.S) || Godot.Input.IsKeyPressed(Key.Down)) moveY += 1f;
            if (Godot.Input.IsKeyPressed(Key.A) || Godot.Input.IsKeyPressed(Key.Left)) moveX -= 1f;
            if (Godot.Input.IsKeyPressed(Key.D) || Godot.Input.IsKeyPressed(Key.Right)) moveX += 1f;

            ref CombatEntityData player = ref simulation.EntityBuffer[0];
            float aimDx = _arenaCursorPos.X - player.PosX;
            float aimDy = _arenaCursorPos.Y - player.PosY;
            float aimDistSq = aimDx * aimDx + aimDy * aimDy;

            float targetAimX = player.AimX;
            float targetAimY = player.AimY;
            if (aimDistSq >= 0.04f) // 0.2-unit deadzone prevents 180° flipping on cursor overlap
            {
                targetAimX = aimDx;
                targetAimY = aimDy;
            }

            simulation.SetPlayerMoveAndAim(moveX, moveY, targetAimX, targetAimY);
            QueueRedraw();
        }

        private void SpawnDamageNumber(Vector2 pos, float amount, bool isPlayerDamage)
        {
            _damageRing[_damageRingHead] = new FloatingDamageEntry
            {
                Position = pos + new Vector2((float)(Random.Shared.NextDouble() * 0.4 - 0.2), -0.2f),
                Amount = amount,
                Lifetime = 0.85f,
                MaxLifetime = 0.85f,
                Color = isPlayerDamage ? Crimson : Amber,
                Active = true
            };
            _damageRingHead = (_damageRingHead + 1) % 32;
        }

        private void HandleBattleStageChanged(bool inBattle)
        {
            if (inBattle)
            {
                Visible = true;
                _hitStopTimer = 0f;

                for (int i = 0; i < 32; i++) _damageRing[i] = default;
                for (int i = 0; i < CombatArenaSimulation.MAX_ENTITIES; i++) _prevHealth[i] = 0f;

                var world = _provider?.Current;
                if (world != null)
                {
                    var session = world.Get<CombatSessionComponent>(world.WorldEntity);
                    if (session?.Simulation != null)
                    {
                        for (int i = 0; i < CombatArenaSimulation.MAX_ENTITIES; i++)
                        {
                            CombatEntityData entity = session.Simulation.EntityBuffer[i];
                            if (entity.Active) _prevHealth[i] = entity.Health;
                        }
                    }
                }

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

            DrawArenaFrame(simulation);
            DrawEntities(simulation);
            DrawProjectiles(simulation);
            DrawFloatingDamage();
            DrawAimReticle(simulation);
        }

        private void DrawArenaFrame(CombatArenaSimulation simulation)
        {
            // 1. Distant Parallax Mountain Layer
            if (_texParallaxMountain != null)
            {
                var mountainRect = new Rect2(-ArenaHalfW * 1.4f, -ArenaHalfH * 1.55f, ArenaHalfW * 2.8f, ArenaHalfH * 3.1f);
                DrawTextureRect(_texParallaxMountain, mountainRect, false, new Color(1f, 1f, 1f, 0.45f));
            }

            // 2. Mid Mist Layer with subtle drift
            if (_texParallaxMist != null)
            {
                float mistShift = Mathf.Sin(simulation.ElapsedTime * 0.2f) * 0.4f;
                var mistRect = new Rect2(-ArenaHalfW * 1.3f + mistShift, -ArenaHalfH * 1.4f, ArenaHalfW * 2.6f, ArenaHalfH * 2.8f);
                DrawTextureRect(_texParallaxMist, mistRect, false, new Color(1f, 1f, 1f, 0.25f));
            }

            // 3. Stone Arena Floor
            var floorRect = new Rect2(-ArenaHalfW, -ArenaHalfH, ArenaHalfW * 2f, ArenaHalfH * 2f);
            if (_texArenaFloor != null)
            {
                DrawTextureRect(_texArenaFloor, floorRect, false, new Color(0.85f, 0.88f, 0.86f, 0.98f));
            }
            else
            {
                DrawRect(floorRect, Lacquer, true);
            }

            // Outer Boundary Gold Trim
            DrawRect(floorRect, LacquerEdge, false, 2.5f * Px);

            // 4. Central Rotating Bagua Aureole Array
            if (_texBaguaAureole != null)
            {
                float rot = simulation.ElapsedTime * 0.08f;
                DrawSetTransform(Vector2.Zero, rot, Vector2.One);
                DrawTextureRect(_texBaguaAureole, new Rect2(-4.2f, -4.2f, 8.4f, 8.4f), false, new Color(0.984f, 0.749f, 0.141f, 0.38f));
                DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
            }
            else
            {
                DrawArc(Vector2.Zero, 4.2f, 0f, Mathf.Tau, 96, new Color(0.72f, 0.56f, 0.24f, 0.22f), 1.5f * Px, true);
            }

            // 5. Four Corner Formation Boundary Brackets & Seals (Pure Halal Geometric Aniconism)
            DrawCornerFormationBracket(new Vector2(-ArenaHalfW + 0.35f, -ArenaHalfH + 0.35f), 1f, 1f);
            DrawCornerFormationBracket(new Vector2(ArenaHalfW - 0.35f, -ArenaHalfH + 0.35f), -1f, 1f);
            DrawCornerFormationBracket(new Vector2(-ArenaHalfW + 0.35f, ArenaHalfH - 0.35f), 1f, -1f);
            DrawCornerFormationBracket(new Vector2(ArenaHalfW - 0.35f, ArenaHalfH - 0.35f), -1f, -1f);

            DrawSeal(new Vector2(-ArenaHalfW + 0.9f, -ArenaHalfH + 0.9f), "【界】", Amber, 14);
            DrawSeal(new Vector2(ArenaHalfW - 0.9f, -ArenaHalfH + 0.9f), "【界】", Amber, 14);
            DrawSeal(new Vector2(-ArenaHalfW + 0.9f, ArenaHalfH - 0.9f), "【阵】", Amber, 14);
            DrawSeal(new Vector2(ArenaHalfW - 0.9f, ArenaHalfH - 0.9f), "【阵】", Amber, 14);

            DrawSeal(new Vector2(0f, -ArenaHalfH + 0.55f), "【斗】", Amber, 22);
        }

        private void DrawCornerFormationBracket(Vector2 corner, float dirX, float dirY)
        {
            float len = 1.6f;
            Vector2 pX = corner + new Vector2(dirX * len, 0f);
            Vector2 pY = corner + new Vector2(0f, dirY * len);

            DrawLine(corner, pX, LacquerEdge, 2.5f * Px, true);
            DrawLine(corner, pY, LacquerEdge, 2.5f * Px, true);

            DrawArc(corner + new Vector2(dirX * 0.45f, dirY * 0.45f), 0.35f, 0f, Mathf.Tau, 24, new Color(0.984f, 0.749f, 0.141f, 0.45f), 1.5f * Px, true);
            DrawCircle(corner + new Vector2(dirX * 0.45f, dirY * 0.45f), 0.10f, Amber);
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
                        DrawWildBoar(entity, position, flash);
                        break;
                    case CombatEntityArchetype.ClanPatrol:
                        DrawClanPatrol(entity, position, flash);
                        break;
                    case CombatEntityArchetype.DemonicCultivator:
                        DrawDemonicCultivator(entity, position, flash);
                        break;
                }

                DrawHealthBar(entity, position);
            }
        }

        private void DrawPlayer(CombatEntityData player, Vector2 position, bool flash)
        {
            float diameter = player.Radius * 2.2f;
            var tokenRect = new Rect2(position - new Vector2(diameter * 0.5f, diameter * 0.5f), new Vector2(diameter, diameter));

            if (_texTokenPlayer != null)
            {
                Color mod = flash ? HitFlash : Colors.White;
                DrawTextureRect(_texTokenPlayer, tokenRect, false, mod);
            }
            else
            {
                Color body = flash ? HitFlash : new Color(0.13f, 0.77f, 0.52f, 1f);
                DrawCircle(position, player.Radius, body);
            }

            DrawArc(position, player.Radius, 0f, Mathf.Tau, 40, flash ? HitFlash : Cyan, 2.5f * Px, true);

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

        private void DrawWildBoar(CombatEntityData entity, Vector2 position, bool flash)
        {
            float diameter = entity.Radius * 2.2f;
            var tokenRect = new Rect2(position - new Vector2(diameter * 0.5f, diameter * 0.5f), new Vector2(diameter, diameter));

            if (_texTokenBoar != null)
            {
                Color mod = flash ? HitFlash : Colors.White;
                DrawTextureRect(_texTokenBoar, tokenRect, false, mod);
            }
            else
            {
                Color boarCol = flash ? HitFlash : Crimson;
                DrawDiamondTotem(position, entity.Radius, boarCol);
            }

            DrawArc(position, entity.Radius, 0f, Mathf.Tau, 32, flash ? HitFlash : Crimson, 2.0f * Px, true);
            DrawSeal(position + new Vector2(0f, entity.Radius + 0.36f), "【兽】", Crimson, 16);
        }

        private void DrawClanPatrol(CombatEntityData entity, Vector2 position, bool flash)
        {
            float diameter = entity.Radius * 2.2f;
            var tokenRect = new Rect2(position - new Vector2(diameter * 0.5f, diameter * 0.5f), new Vector2(diameter, diameter));

            if (_texTokenCultivator != null)
            {
                Color mod = flash ? HitFlash : new Color(0.80f, 0.58f, 0.24f, 1f); // Bronze tint
                DrawTextureRect(_texTokenCultivator, tokenRect, false, mod);
            }
            else
            {
                Color patrolCol = flash ? HitFlash : Bronze;
                DrawOctagonTotem(position, entity.Radius, patrolCol);
            }

            DrawArc(position, entity.Radius, 0f, Mathf.Tau, 32, flash ? HitFlash : Bronze, 2.0f * Px, true);
            DrawSeal(position + new Vector2(0f, entity.Radius + 0.38f), "【卫】", Bronze, 16);
        }

        private void DrawDemonicCultivator(CombatEntityData entity, Vector2 position, bool flash)
        {
            float diameter = entity.Radius * 2.4f;
            var tokenRect = new Rect2(position - new Vector2(diameter * 0.5f, diameter * 0.5f), new Vector2(diameter, diameter));

            if (_texTokenBoss != null)
            {
                Color mod = flash ? HitFlash : Crimson;
                DrawTextureRect(_texTokenBoss, tokenRect, false, mod);
            }
            else if (_texTokenCultivator != null)
            {
                Color mod = flash ? HitFlash : Crimson;
                DrawTextureRect(_texTokenCultivator, tokenRect, false, mod);
            }
            else
            {
                DrawBaguaMandala(position, entity.Radius, flash ? HitFlash : Crimson);
            }

            DrawArc(position, entity.Radius, 0f, Mathf.Tau, 36, flash ? HitFlash : Crimson, 2.2f * Px, true);
            DrawSeal(position + new Vector2(0f, entity.Radius + 0.40f), "【魔】", flash ? HitFlash : Crimson, 16);
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
                        if (_texIconMoonblade != null)
                        {
                            DrawSetTransform(position, direction.Angle(), Vector2.One);
                            DrawTextureRect(_texIconMoonblade, new Rect2(-0.35f, -0.35f, 0.7f, 0.7f), false, Cyan);
                            DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
                        }
                        else
                        {
                            float spin = simulation.ElapsedTime * 9f;
                            DrawArc(position, 0.28f, spin, spin + 2.1f, 12, Cyan, 2.8f * Px, true);
                        }
                        break;
                    }
                    case 1:
                    {
                        if (_texIconShockwave != null)
                        {
                            DrawSetTransform(position, direction.Angle(), Vector2.One);
                            DrawTextureRect(_texIconShockwave, new Rect2(-0.30f, -0.30f, 0.6f, 0.6f), false, Crimson);
                            DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
                        }
                        else
                        {
                            DrawDiamondTotem(position, 0.22f, Crimson);
                        }
                        break;
                    }
                    case 2:
                    {
                        if (_texIconQiArcs != null)
                        {
                            DrawSetTransform(position, direction.Angle(), Vector2.One);
                            DrawTextureRect(_texIconQiArcs, new Rect2(-0.25f, -0.25f, 0.5f, 0.5f), false, Amber);
                            DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
                        }
                        else
                        {
                            DrawArc(position, 0.26f, 0f, Mathf.Tau * 0.62f, 10, Crimson, 2.8f * Px, true);
                        }
                        break;
                    }
                    default:
                        DrawLine(position - direction * 0.30f, position + direction * 0.34f, Amber, 3.2f * Px, true);
                        break;
                }
            }
        }

        private void DrawAimReticle(CombatArenaSimulation simulation)
        {
            ref CombatEntityData player = ref simulation.EntityBuffer[0];
            if (!player.Active) return;

            Vector2 playerPos = new Vector2(player.PosX, player.PosY);
            Vector2 aimDir = new Vector2(player.AimX, player.AimY);
            if (aimDir.LengthSquared() < 0.001f) aimDir = Vector2.Right;

            // 1. Subtle dashed / semi-transparent aim guideline from player edge to reticle
            Vector2 playerEdge = playerPos + aimDir * (player.Radius + 0.18f);
            DrawLine(playerEdge, _arenaCursorPos, new Color(Amber.R, Amber.G, Amber.B, 0.28f), 1.2f * Px, true);

            // 2. Ink & Gold concentric reticle
            DrawArc(_arenaCursorPos, 0.24f, 0f, Mathf.Tau, 32, Amber, 1.8f * Px, true);
            DrawArc(_arenaCursorPos, 0.10f, 0f, Mathf.Tau, 20, Cyan, 1.2f * Px, true);
            DrawCircle(_arenaCursorPos, 0.035f, Amber);

            // 3. Four crosshair tick marks
            float tickInner = 0.14f;
            float tickOuter = 0.32f;
            DrawLine(_arenaCursorPos + new Vector2(tickInner, 0f), _arenaCursorPos + new Vector2(tickOuter, 0f), Amber, 1.5f * Px, true);
            DrawLine(_arenaCursorPos - new Vector2(tickInner, 0f), _arenaCursorPos - new Vector2(tickOuter, 0f), Amber, 1.5f * Px, true);
            DrawLine(_arenaCursorPos + new Vector2(0f, tickInner), _arenaCursorPos + new Vector2(0f, tickOuter), Amber, 1.5f * Px, true);
            DrawLine(_arenaCursorPos - new Vector2(0f, tickInner), _arenaCursorPos - new Vector2(0f, tickOuter), Amber, 1.5f * Px, true);
        }

        private void DrawFloatingDamage()
        {
            for (int i = 0; i < 32; i++)
            {
                ref FloatingDamageEntry entry = ref _damageRing[i];
                if (!entry.Active) continue;

                float alpha = Mathf.Clamp(entry.Lifetime / entry.MaxLifetime, 0f, 1f);
                Color col = new Color(entry.Color.R, entry.Color.G, entry.Color.B, alpha);
                string text = GetDamageString(entry.Amount);

                DrawSeal(entry.Position, text, col, 18);
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
            DrawString(font, localOrigin + new Vector2(1.2f, 1.5f), text, HorizontalAlignment.Left, -1, fontSize, new Color(0.01f, 0.02f, 0.02f, color.A * 0.95f));
            DrawString(font, localOrigin, text, HorizontalAlignment.Left, -1, fontSize, color);
            DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
        }
    }
}
