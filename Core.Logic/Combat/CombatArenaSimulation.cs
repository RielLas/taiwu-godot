using System;
using TaiwuGu.Core;
using TaiwuGu.Entities;
using TaiwuGu.Systems;

namespace TaiwuGu.Combat
{
    /// <summary>
    /// Deterministic real-time arena simulation. All state lives in pre-allocated flat struct
    /// buffers; Step() performs no allocations so the presentation layer can render straight
    /// from the buffers every frame.
    /// </summary>
    public sealed class CombatArenaSimulation
    {
        public const int MAX_ENTITIES = 64;
        public const int MAX_PROJECTILES = 128;
        public const int MAX_SKILLS = 6;

        public const float ARENA_HALF_W = 9.5f;
        public const float ARENA_HALF_H = 5.2f;
        private const float PlayerIFrameDuration = 0.35f;

        public readonly CombatEntityData[] EntityBuffer = new CombatEntityData[MAX_ENTITIES];
        public readonly CombatProjectileData[] ProjectileBuffer = new CombatProjectileData[MAX_PROJECTILES];
        public readonly CombatSkillSlot[] PlayerSkills = new CombatSkillSlot[MAX_SKILLS];

        private readonly EntityId[] _skillGuEntityIds = new EntityId[MAX_SKILLS];
        private EntityId _playerEntity;
        private DaoMarksLedger? _playerDaoMarks;
        private WorldStore? _lastWorld;
        private float _playerIFrameTimer;

        public bool IsActive;
        public CombatOutcome Outcome;
        public ArenaEncounterType EncounterType;
        public GridCoord SourceTilePos;
        public EntityId? TargetNpcId { get; private set; }
        public bool IsLethal;
        public string EnemyDisplayName = string.Empty;
        public float StoneCrushCooldown;
        public float ElapsedTime;
        public float HitStopTimer;
        public float MoveDirX;
        public float MoveDirY;
        public float TargetVelX;
        public float TargetVelY;

        public void InitializeArena(
            WorldStore world,
            ArenaEncounterType encounter,
            GridCoord pos,
            EntityId? targetNpcId,
            bool isLethal,
            string? enemyName,
            IRandomSource rng)
        {
            for (int i = 0; i < MAX_ENTITIES; i++) EntityBuffer[i] = default;
            for (int i = 0; i < MAX_PROJECTILES; i++) ProjectileBuffer[i] = default;
            for (int i = 0; i < MAX_SKILLS; i++)
            {
                PlayerSkills[i] = default;
                _skillGuEntityIds[i] = EntityId.None;
            }

            IsActive = true;
            Outcome = CombatOutcome.Ongoing;
            EncounterType = encounter;
            SourceTilePos = pos;
            TargetNpcId = targetNpcId;
            IsLethal = isLethal;
            StoneCrushCooldown = 0f;
            ElapsedTime = 0f;
            HitStopTimer = 0f;
            _playerIFrameTimer = 0f;
            MoveDirX = 0f;
            MoveDirY = 0f;
            TargetVelX = 0f;
            TargetVelY = 0f;
            EnemyDisplayName = enemyName ?? GetDefaultEnemyName(encounter);

            _playerEntity = world.PlayerEntity;
            _lastWorld = world;
            _playerDaoMarks = world.Get<DaoMarksComponent>(_playerEntity)?.Ledger;
            ApertureComponent? aperture = world.Get<ApertureComponent>(_playerEntity);

            ref CombatEntityData player = ref EntityBuffer[0];
            player.Active = true;
            player.SlotIndex = 0;
            player.Team = CombatEntityTeam.Player;
            player.Archetype = CombatEntityArchetype.PlayerCultivator;
            player.PosX = -4.5f;
            player.PosY = 0f;
            player.AimX = 1f;
            player.AimY = 0f;
            player.Radius = 0.55f;
            player.MaxHealth = aperture?.MaxHealth ?? 100f;
            player.Health = aperture?.CurrentHealth ?? player.MaxHealth;
            if (player.Health > player.MaxHealth) player.Health = player.MaxHealth;
            player.MoveSpeed = 4.8f;
            player.Rank = aperture?.Rank ?? 1;
            player.ChannelTimer = 0f;
            player.StunTimer = 0f;

            PopulateSkills(world);
            SeedEnemies(world, rng);
        }

        public void SetPlayerMoveAndAim(float moveX, float moveY, float aimDirX, float aimDirY)
        {
            ref CombatEntityData player = ref EntityBuffer[0];
            if (!player.Active) return;

            float moveMag = MathF.Sqrt(moveX * moveX + moveY * moveY);
            if (moveMag > 0.001f)
            {
                MoveDirX = moveX / moveMag;
                MoveDirY = moveY / moveMag;
                TargetVelX = MoveDirX * player.MoveSpeed;
                TargetVelY = MoveDirY * player.MoveSpeed;
            }
            else
            {
                MoveDirX = 0f;
                MoveDirY = 0f;
                TargetVelX = 0f;
                TargetVelY = 0f;
            }

            float aimMag = MathF.Sqrt(aimDirX * aimDirX + aimDirY * aimDirY);
            if (aimMag > 0.001f)
            {
                player.AimX = aimDirX / aimMag;
                player.AimY = aimDirY / aimMag;
            }
        }

        public bool TryCastSkill(WorldStore world, int slotIndex)
        {
            if (!IsActive || slotIndex < 0 || slotIndex >= MAX_SKILLS) return false;

            ref CombatSkillSlot skill = ref PlayerSkills[slotIndex];
            if (!skill.IsPopulated || skill.CooldownRemaining > 0f) return false;

            ref CombatEntityData player = ref EntityBuffer[0];
            if (!player.Active || player.StunTimer > 0f || player.ChannelTimer > 0f) return false;

            if (!world.TryGet<ApertureComponent>(_playerEntity, out var aperture) || aperture == null) return false;

            // Slot 5 is dedicated Killer Move [R]
            if (slotIndex == 5)
            {
                // Dead or hibernating Gu cannot power the Resonance Killer Move
                bool hasActiveGu = false;
                for (int s = 0; s < 5; s++)
                {
                    EntityId guEid = _skillGuEntityIds[s];
                    if (guEid.IsValid && world.TryGet<GuWormComponent>(guEid, out var eqGu) && eqGu != null)
                    {
                        GuStarvationStage eqStage = eqGu.GetStarvationStage();
                        if (eqStage != GuStarvationStage.Hibernating && eqStage != GuStarvationStage.Dead)
                        {
                            hasActiveGu = true;
                            break;
                        }
                    }
                }

                if (!hasActiveGu)
                {
                    WorldSignals.EmitSound(HalalSoundType.WoodenKnock);
                    return false;
                }

                double density = aperture.GetDensityCoefficient();
                float realCostPct = (float)(skill.BaseBeuCost / density);
                if (aperture.CurrentEssenceSea < realCostPct)
                {
                    WorldSignals.EmitSound(HalalSoundType.WoodenKnock);
                    return false;
                }

                aperture.CurrentEssenceSea -= realCostPct;
                WorldSignals.EmitTopics(DirtyTopic.Aperture);

                skill.CooldownRemaining = skill.CooldownDuration;
                player.ChannelTimer = 1.0f;
                player.VelX = 0f;
                player.VelY = 0f;
                WorldSignals.EmitSound(HalalSoundType.CauldronFire);
                return true;
            }

            if (!world.TryGet<GuWormComponent>(_skillGuEntityIds[slotIndex], out var gu) || gu == null) return false;

            GuStarvationStage stage = gu.GetStarvationStage();
            if (stage == GuStarvationStage.Hibernating || stage == GuStarvationStage.Dead)
            {
                WorldSignals.EmitSound(HalalSoundType.WoodenKnock);
                return false;
            }

            float realCost = gu.GetRealEssenceCost(aperture.Rank, aperture.Substage);
            if (aperture.CurrentEssenceSea < realCost)
            {
                WorldSignals.EmitSound(HalalSoundType.WoodenKnock);
                return false;
            }

            aperture.CurrentEssenceSea -= realCost;
            WorldSignals.EmitTopics(DirtyTopic.Aperture);

            float mult = gu.GetPowerMultiplier();
            float daoAmp = _playerDaoMarks != null ? _playerDaoMarks.GetPathAmplificationMultiplier(skill.PathType) : 1.0f;
            skill.CooldownRemaining = skill.CooldownDuration;

            switch (skill.EffectKind)
            {
                case GuSkillEffectKind.Projectile:
                {
                    float speed = skill.ProjectileSpeed > 0f ? skill.ProjectileSpeed : 12f;
                    int style = skill.GuId.IndexOf("moon", StringComparison.OrdinalIgnoreCase) >= 0 ? 0 : 3;
                    SpawnProjectile(
                        CombatEntityTeam.Player,
                        player.PosX + player.AimX * (player.Radius + 0.15f),
                        player.PosY + player.AimY * (player.Radius + 0.15f),
                        player.AimX * speed,
                        player.AimY * speed,
                        skill.BaseDamage * mult * daoAmp,
                        skill.Range > 0f ? skill.Range : 8f,
                        style);
                    WorldSignals.EmitSound(HalalSoundType.BladeSlash);
                    break;
                }
                case GuSkillEffectKind.Shield:
                    player.ShieldHP = (25f + skill.Rank * 15f) * mult * daoAmp;
                    player.ShieldTimer = 4.0f;
                    WorldSignals.EmitSound(HalalSoundType.WoodenKnock);
                    break;
                case GuSkillEffectKind.Dash:
                {
                    float dashDirX = MoveDirX;
                    float dashDirY = MoveDirY;
                    if (dashDirX * dashDirX + dashDirY * dashDirY < 0.01f)
                    {
                        dashDirX = player.AimX;
                        dashDirY = player.AimY;
                    }
                    player.VelX = dashDirX * 12.5f;
                    player.VelY = dashDirY * 12.5f;
                    player.DashTimer = 0.25f;
                    WorldSignals.EmitSound(HalalSoundType.DashWhoosh);
                    break;
                }
                case GuSkillEffectKind.Heal:
                    player.Health = MathF.Min(player.MaxHealth, player.Health + 20f * skill.Rank * mult * daoAmp);
                    aperture.CurrentHealth = player.Health;
                    WorldSignals.EmitSound(HalalSoundType.CauldronSteam);
                    break;
                case GuSkillEffectKind.AoEPulse:
                {
                    const float pulseRadius = 3.2f;
                    for (int i = 1; i < MAX_ENTITIES; i++)
                    {
                        ref CombatEntityData enemy = ref EntityBuffer[i];
                        if (!enemy.Active || enemy.Team != CombatEntityTeam.Hostile) continue;

                        float dx = enemy.PosX - player.PosX;
                        float dy = enemy.PosY - player.PosY;
                        float distSq = dx * dx + dy * dy;
                        if (distSq > pulseRadius * pulseRadius) continue;

                        enemy.Health -= 18f * skill.Rank * mult * daoAmp;
                        enemy.HitFlashTimer = 0.15f;
                        if (enemy.Health <= 0f) enemy.Active = false;

                        float dist = MathF.Sqrt(distSq);
                        if (dist > 0.0001f)
                        {
                            enemy.PosX += dx / dist * 0.6f;
                            enemy.PosY += dy / dist * 0.6f;
                            ClampToArena(ref enemy);
                        }
                    }
                    WorldSignals.EmitSound(HalalSoundType.CauldronFire);
                    break;
                }
            }

            return true;
        }

        public bool TryCrushStone(WorldStore world)
        {
            if (!IsActive || StoneCrushCooldown > 0f) return false;
            if (!CultivationSystem.ConsumeStone(world)) return false;
            StoneCrushCooldown = 1.5f;
            return true;
        }

        public void RequestRetreat()
        {
            if (!IsActive) return;
            IsActive = false;
            Outcome = CombatOutcome.Retreated;
        }

        public CombatOutcome Step(WorldStore world, float deltaTime, IRandomSource? rngOverride = null)
        {
            float dt = MathF.Min(deltaTime, 0.1f);
            if (!IsActive || dt <= 0f) return Outcome;

            if (HitStopTimer > 0f)
            {
                HitStopTimer = MathF.Max(0f, HitStopTimer - dt);
                return Outcome;
            }

            ElapsedTime += dt;
            if (StoneCrushCooldown > 0f) StoneCrushCooldown = MathF.Max(0f, StoneCrushCooldown - dt);
            if (_playerIFrameTimer > 0f) _playerIFrameTimer = MathF.Max(0f, _playerIFrameTimer - dt);

            for (int s = 0; s < MAX_SKILLS; s++)
            {
                if (PlayerSkills[s].CooldownRemaining > 0f)
                {
                    PlayerSkills[s].CooldownRemaining = MathF.Max(0f, PlayerSkills[s].CooldownRemaining - dt);
                }
            }

            _lastWorld = world;
            ref CombatEntityData player = ref EntityBuffer[0];
            if (!player.Active)
            {
                IsActive = false;
                Outcome = CombatOutcome.Defeat;
                return Outcome;
            }

            TickTimers(ref player, dt);

            // ChannelTimer processing: Killer Move charging and execution
            if (player.ChannelTimer > 0f)
            {
                player.ChannelTimer = MathF.Max(0f, player.ChannelTimer - dt);
                if (player.ChannelTimer <= 0f)
                {
                    // Channel succeeded! Unleash Killer Move resonance blast!
                    ref CombatSkillSlot km = ref PlayerSkills[5];
                    float kmDaoAmp = _playerDaoMarks != null ? _playerDaoMarks.GetPathAmplificationMultiplier(km.PathType) : 1.0f;
                    float kmDamage = km.BaseDamage * kmDaoAmp;
                    float burstRange = km.Range > 0f ? km.Range : 6.0f;

                    for (int j = 1; j < MAX_ENTITIES; j++)
                    {
                        ref CombatEntityData enemy = ref EntityBuffer[j];
                        if (!enemy.Active || enemy.Team != CombatEntityTeam.Hostile) continue;

                        float edx = enemy.PosX - player.PosX;
                        float edy = enemy.PosY - player.PosY;
                        float distSq = edx * edx + edy * edy;
                        if (distSq <= burstRange * burstRange)
                        {
                            enemy.Health -= kmDamage;
                            enemy.HitFlashTimer = 0.25f;
                            enemy.StunTimer = 1.2f;
                            if (enemy.Health <= 0f) enemy.Active = false;
                        }
                    }

                    HitStopTimer = 0.12f;
                    WorldSignals.EmitSound(HalalSoundType.CauldronFire);
                }
            }

            // Stun or active channel restricts voluntary movement
            if (player.StunTimer > 0f || player.ChannelTimer > 0f)
            {
                player.VelX = 0f;
                player.VelY = 0f;
            }
            else if (player.DashTimer <= 0f)
            {
                bool isDecel = (TargetVelX == 0f && TargetVelY == 0f);
                float rate = isDecel ? 22f : 18f;
                float blend = MathF.Min(1.0f, rate * dt);
                player.VelX += (TargetVelX - player.VelX) * blend;
                player.VelY += (TargetVelY - player.VelY) * blend;

                if (isDecel && MathF.Abs(player.VelX) < 0.05f) player.VelX = 0f;
                if (isDecel && MathF.Abs(player.VelY) < 0.05f) player.VelY = 0f;
            }

            player.PosX += player.VelX * dt;
            player.PosY += player.VelY * dt;
            ClampToArena(ref player);

            for (int i = 1; i < MAX_ENTITIES; i++)
            {
                ref CombatEntityData enemy = ref EntityBuffer[i];
                if (!enemy.Active) continue;

                TickTimers(ref enemy, dt);
                if (enemy.Health <= 0f)
                {
                    enemy.Active = false;
                    continue;
                }

                // Stunned enemy cannot move or attack
                if (enemy.StunTimer > 0f)
                {
                    enemy.VelX = 0f;
                    enemy.VelY = 0f;
                    continue;
                }

                float dx = player.PosX - enemy.PosX;
                float dy = player.PosY - enemy.PosY;
                float distSq = dx * dx + dy * dy;
                float dist = MathF.Sqrt(distSq);

                if (enemy.Archetype == CombatEntityArchetype.WildBoarSwarm)
                {
                    float contact = player.Radius + enemy.Radius + 0.12f;
                    if (dist > contact)
                    {
                        if (dist > 0.0001f)
                        {
                            enemy.PosX += dx / dist * enemy.MoveSpeed * dt;
                            enemy.PosY += dy / dist * enemy.MoveSpeed * dt;
                        }
                    }
                    else if (enemy.AttackCooldown <= 0f)
                    {
                        DamagePlayer(world, ref player, 10f);
                        enemy.AttackCooldown = 1.8f;
                    }
                    ClampToArena(ref enemy);
                }
                else if (enemy.Archetype == CombatEntityArchetype.SovereignElder)
                {
                    // Peak Rank 5 Sovereign Boss AI: strafes, keeps mid range, escalates below 50% HP
                    bool phase2 = enemy.MaxHealth > 0f && enemy.Health <= enemy.MaxHealth * 0.50f;
                    float bossMoveSpeed = enemy.MoveSpeed * (phase2 ? 1.22f : 1.0f);
                    float strafeX = dist > 0.0001f ? -dy / dist : 0f;
                    float strafeY = dist > 0.0001f ? dx / dist : 1f;

                    if (dist < 3.5f && dist > 0.0001f)
                    {
                        enemy.PosX -= (dx / dist * 0.7f + strafeX * 0.7f) * bossMoveSpeed * dt;
                        enemy.PosY -= (dy / dist * 0.7f + strafeY * 0.7f) * bossMoveSpeed * dt;
                    }
                    else if (dist > 6.0f && dist > 0.0001f)
                    {
                        enemy.PosX += (dx / dist * 0.7f + strafeX * 0.7f) * bossMoveSpeed * dt;
                        enemy.PosY += (dy / dist * 0.7f + strafeY * 0.7f) * bossMoveSpeed * dt;
                    }
                    else
                    {
                        enemy.PosX += strafeX * bossMoveSpeed * dt;
                        enemy.PosY += strafeY * bossMoveSpeed * dt;
                    }
                    ClampToArena(ref enemy);

                    if (enemy.AttackCooldown <= 0f && dist < 11.0f)
                    {
                        float baseAimX = dist > 0.0001f ? dx / dist : 1f;
                        float baseAimY = dist > 0.0001f ? dy / dist : 0f;
                        float baseAngle = MathF.Atan2(baseAimY, baseAimX);
                        float damage = 22f + enemy.Rank * 4f;

                        if (phase2)
                        {
                            // Phase 2: tighter 7-way imperial burst with faster projectile speed and fire cadence
                            for (int spread = -3; spread <= 3; spread++)
                            {
                                float angle = baseAngle + spread * 0.12f;
                                float vx = MathF.Cos(angle) * 11.8f;
                                float vy = MathF.Sin(angle) * 11.8f;
                                SpawnProjectile(
                                    CombatEntityTeam.Hostile,
                                    enemy.PosX + MathF.Cos(angle) * enemy.Radius,
                                    enemy.PosY + MathF.Sin(angle) * enemy.Radius,
                                    vx, vy, damage, 14f, 3);
                            }
                            enemy.AttackCooldown = 0.65f;
                        }
                        else
                        {
                            for (int spread = -2; spread <= 2; spread++)
                            {
                                float angle = baseAngle + spread * 0.16f;
                                float vx = MathF.Cos(angle) * 10.5f;
                                float vy = MathF.Sin(angle) * 10.5f;
                                SpawnProjectile(
                                    CombatEntityTeam.Hostile,
                                    enemy.PosX + MathF.Cos(angle) * enemy.Radius,
                                    enemy.PosY + MathF.Sin(angle) * enemy.Radius,
                                    vx, vy, damage, 14f, 3);
                            }
                            enemy.AttackCooldown = 0.95f;
                        }
                    }
                }
                else
                {
                    // ClanPatrol and DemonicCultivator
                    float strafeFactor = enemy.Rank >= 4 ? 0.45f : 0.0f;
                    float strafeX = dist > 0.0001f ? -dy / dist : 0f;
                    float strafeY = dist > 0.0001f ? dx / dist : 1f;

                    if (dist < 3.2f && dist > 0.0001f)
                    {
                        enemy.PosX -= (dx / dist - strafeX * strafeFactor) * enemy.MoveSpeed * dt;
                        enemy.PosY -= (dy / dist - strafeY * strafeFactor) * enemy.MoveSpeed * dt;
                    }
                    else if (dist > 5.5f && dist > 0.0001f)
                    {
                        enemy.PosX += (dx / dist + strafeX * strafeFactor) * enemy.MoveSpeed * dt;
                        enemy.PosY += (dy / dist + strafeY * strafeFactor) * enemy.MoveSpeed * dt;
                    }
                    else if (strafeFactor > 0f)
                    {
                        enemy.PosX += strafeX * enemy.MoveSpeed * strafeFactor * dt;
                        enemy.PosY += strafeY * enemy.MoveSpeed * strafeFactor * dt;
                    }
                    ClampToArena(ref enemy);

                    if (enemy.AttackCooldown <= 0f && dist < 9.5f)
                    {
                        float baseAimX = dist > 0.0001f ? dx / dist : 1f;
                        float baseAimY = dist > 0.0001f ? dy / dist : 0f;
                        int style = enemy.Archetype == CombatEntityArchetype.ClanPatrol ? 3 : 2;

                        if (enemy.Rank >= 4)
                        {
                            float baseAngle = MathF.Atan2(baseAimY, baseAimX);
                            float damage = 10f + enemy.Rank * 3f;
                            for (int spread = -1; spread <= 1; spread++)
                            {
                                float angle = baseAngle + spread * 0.20f;
                                float vx = MathF.Cos(angle) * 9.0f;
                                float vy = MathF.Sin(angle) * 9.0f;
                                SpawnProjectile(
                                    CombatEntityTeam.Hostile,
                                    enemy.PosX + MathF.Cos(angle) * enemy.Radius,
                                    enemy.PosY + MathF.Sin(angle) * enemy.Radius,
                                    vx, vy, damage, 12f, style);
                            }
                            enemy.AttackCooldown = enemy.Archetype == CombatEntityArchetype.ClanPatrol ? 1.2f : 1.0f;
                        }
                        else
                        {
                            float damage = 8f + enemy.Rank * 3f;
                            SpawnProjectile(
                                CombatEntityTeam.Hostile,
                                enemy.PosX + baseAimX * enemy.Radius,
                                enemy.PosY + baseAimY * enemy.Radius,
                                baseAimX * 8.5f,
                                baseAimY * 8.5f,
                                damage,
                                12f,
                                style);
                            enemy.AttackCooldown = enemy.Archetype == CombatEntityArchetype.ClanPatrol ? 1.3f : 1.15f;
                        }
                    }
                }
            }

            for (int i = 0; i < MAX_PROJECTILES; i++)
            {
                ref CombatProjectileData projectile = ref ProjectileBuffer[i];
                if (!projectile.Active) continue;

                projectile.PosX += projectile.VelX * dt;
                projectile.PosY += projectile.VelY * dt;

                float speed = MathF.Sqrt(projectile.VelX * projectile.VelX + projectile.VelY * projectile.VelY);
                projectile.RemainingRange -= speed * dt;

                if (projectile.RemainingRange <= 0f ||
                    MathF.Abs(projectile.PosX) > ARENA_HALF_W ||
                    MathF.Abs(projectile.PosY) > ARENA_HALF_H)
                {
                    projectile.Active = false;
                    continue;
                }

                if (projectile.OwnerTeam == CombatEntityTeam.Hostile)
                {
                    float pdx = projectile.PosX - player.PosX;
                    float pdy = projectile.PosY - player.PosY;
                    float hitRadius = player.Radius + projectile.Radius;
                    if (pdx * pdx + pdy * pdy <= hitRadius * hitRadius)
                    {
                        DamagePlayer(world, ref player, projectile.Damage);
                        projectile.Active = false;
                        WorldSignals.EmitSound(HalalSoundType.BluntImpact);
                    }
                }
                else
                {
                    for (int j = 1; j < MAX_ENTITIES; j++)
                    {
                        ref CombatEntityData enemy = ref EntityBuffer[j];
                        if (!enemy.Active) continue;

                        float edx = projectile.PosX - enemy.PosX;
                        float edy = projectile.PosY - enemy.PosY;
                        float hitRadius = enemy.Radius + projectile.Radius;
                        if (edx * edx + edy * edy > hitRadius * hitRadius) continue;

                        enemy.Health -= projectile.Damage;
                        enemy.HitFlashTimer = 0.15f;
                        if (enemy.Health <= 0f) enemy.Active = false;
                        projectile.Active = false;
                        WorldSignals.EmitSound(HalalSoundType.BluntImpact);
                        break;
                    }
                }
            }

            if (player.Health <= 0f)
            {
                player.Health = 0f;
                IsActive = false;
                Outcome = CombatOutcome.Defeat;
            }
            else if (!IsLethal && EncounterType == ArenaEncounterType.ClanPatrolDuel)
            {
                bool anyAboveThreshold = false;
                for (int i = 1; i < MAX_ENTITIES; i++)
                {
                    ref CombatEntityData enemy = ref EntityBuffer[i];
                    if (!enemy.Active || enemy.Team != CombatEntityTeam.Hostile) continue;
                    if (enemy.Health > enemy.MaxHealth * 0.15f)
                    {
                        anyAboveThreshold = true;
                        break;
                    }
                }

                if (!anyAboveThreshold)
                {
                    IsActive = false;
                    Outcome = CombatOutcome.Victory;
                }
            }
            else
            {
                bool anyHostile = false;
                for (int i = 1; i < MAX_ENTITIES; i++)
                {
                    ref CombatEntityData enemy = ref EntityBuffer[i];
                    if (enemy.Active && enemy.Team == CombatEntityTeam.Hostile)
                    {
                        anyHostile = true;
                        break;
                    }
                }

                if (!anyHostile)
                {
                    IsActive = false;
                    Outcome = CombatOutcome.Victory;
                }
            }

            var playerAperture = world.Get<ApertureComponent>(_playerEntity);
            if (playerAperture != null) playerAperture.CurrentHealth = player.Health;
            return Outcome;
        }

        private void PopulateSkills(WorldStore world)
        {
            for (int equipSlot = 0; equipSlot < 5; equipSlot++)
            {
                EntityId guEntity = EntityId.None;
                GuWormComponent? gu = null;

                foreach (var (eid, ownedGu) in world.GetOwned<GuWormComponent>(_playerEntity))
                {
                    if (!world.TryGet<OwnerComponent>(eid, out var owner) || !owner.IsEquipped) continue;
                    if (owner.EquipSlot != equipSlot) continue;
                    guEntity = eid;
                    gu = ownedGu;
                    break;
                }

                if (gu == null) continue;

                GuWormDefinitionDTO? def = GuDatabase.Get(gu.GuID);
                GuSkillEffectKind effect = gu.Slot switch
                {
                    GuSlotCategory.Attack => GuSkillEffectKind.Projectile,
                    GuSlotCategory.Defense => GuSkillEffectKind.Shield,
                    GuSlotCategory.Movement => GuSkillEffectKind.Dash,
                    GuSlotCategory.Healing => GuSkillEffectKind.Heal,
                    _ => GuSkillEffectKind.AoEPulse
                };

                float damage = def?.CombatStats?.BaseDamage ?? 0f;
                float speed = def?.CombatStats?.Speed ?? 0f;
                float range = def?.CombatStats?.Range ?? 0f;
                float cooldown = def?.CooldownSeconds ?? 0f;

                PlayerSkills[equipSlot] = new CombatSkillSlot
                {
                    IsPopulated = true,
                    GuId = gu.GuID,
                    DisplayName = gu.Name,
                    Rank = gu.Rank,
                    SlotCategory = gu.Slot,
                    EffectKind = effect,
                    BaseBeuCost = gu.BaseEssenceCost,
                    CooldownDuration = cooldown > 0f ? cooldown : 0.8f,
                    CooldownRemaining = 0f,
                    BaseDamage = damage > 0f ? damage : 25f,
                    ProjectileSpeed = speed > 0f ? speed : 12f,
                    Range = range > 0f ? range : 8f,
                    PathType = ResolvePathType(def?.Path)
                };
                _skillGuEntityIds[equipSlot] = guEntity;
            }

            // Synthesize dedicated Killer Move in Slot 5 from active (non-dead, non-hibernating) equipped Gu
            bool hasAnyActiveEquipped = false;
            bool hasPrimaryPath = false;
            int totalRank = 0;
            float maxDamage = 25f;
            DaoPathType primaryPath = DaoPathType.HumanPath;

            for (int s = 0; s < 5; s++)
            {
                if (!PlayerSkills[s].IsPopulated) continue;

                EntityId guEid = _skillGuEntityIds[s];
                if (guEid.IsValid && world.TryGet<GuWormComponent>(guEid, out var eqGu) && eqGu != null)
                {
                    GuStarvationStage stage = eqGu.GetStarvationStage();
                    if (stage == GuStarvationStage.Hibernating || stage == GuStarvationStage.Dead)
                    {
                        continue;
                    }
                }

                hasAnyActiveEquipped = true;
                totalRank += PlayerSkills[s].Rank;
                if (PlayerSkills[s].BaseDamage > maxDamage) maxDamage = PlayerSkills[s].BaseDamage;
                if (!hasPrimaryPath)
                {
                    primaryPath = PlayerSkills[s].PathType;
                    hasPrimaryPath = true;
                }
            }

            if (hasAnyActiveEquipped)
            {
                PlayerSkills[5] = new CombatSkillSlot
                {
                    IsPopulated = true,
                    GuId = "killer_move_resonance",
                    DisplayName = "Resonance Killer Move (杀招·天地共鸣)",
                    Rank = Math.Max(1, totalRank / 2),
                    SlotCategory = GuSlotCategory.Attack,
                    EffectKind = GuSkillEffectKind.AoEPulse,
                    BaseBeuCost = 35f,
                    CooldownDuration = 8.0f,
                    CooldownRemaining = 0f,
                    BaseDamage = Math.Max(60f, maxDamage * 2.5f),
                    ProjectileSpeed = 0f,
                    Range = 6.0f,
                    PathType = primaryPath
                };
                _skillGuEntityIds[5] = EntityId.None;
            }
        }

        private static DaoPathType ResolvePathType(string? path)
        {
            if (string.IsNullOrEmpty(path)) return DaoPathType.HumanPath;
            if (path.IndexOf("moon", StringComparison.OrdinalIgnoreCase) >= 0) return DaoPathType.MoonPath;
            if (path.IndexOf("strength", StringComparison.OrdinalIgnoreCase) >= 0) return DaoPathType.StrengthPath;
            if (path.IndexOf("blood", StringComparison.OrdinalIgnoreCase) >= 0) return DaoPathType.BloodPath;
            if (path.IndexOf("bone", StringComparison.OrdinalIgnoreCase) >= 0) return DaoPathType.BonePath;
            if (path.IndexOf("time", StringComparison.OrdinalIgnoreCase) >= 0) return DaoPathType.TimePath;
            if (path.IndexOf("enslave", StringComparison.OrdinalIgnoreCase) >= 0) return DaoPathType.EnslavementPath;
            if (path.IndexOf("refine", StringComparison.OrdinalIgnoreCase) >= 0) return DaoPathType.RefinementPath;
            return DaoPathType.HumanPath;
        }

        private void SeedEnemies(WorldStore world, IRandomSource rng)
        {
            switch (EncounterType)
            {
                case ArenaEncounterType.WildBoarHunt:
                {
                    int count = 3 + rng.Next(0, 3);
                    for (int i = 0; i < count && i < MAX_ENTITIES - 1; i++)
                    {
                        float y = count == 1 ? 0f : -3.0f + (6.0f * i) / (count - 1);
                        float x = 4.5f + (float)rng.NextDouble() * 3.0f;
                        float health = 55f + rng.Next(0, 21);
                        SpawnHostile(i + 1, CombatEntityArchetype.WildBoarSwarm, x, y, health, 3.6f, 0.50f, 1, 0.4f);
                    }
                    break;
                }
                case ArenaEncounterType.ClanPatrolDuel:
                {
                    int rank = TargetNpcId is { } npcId ? world.Get<ApertureComponent>(npcId)?.Rank ?? 2 : 2;
                    SpawnHostile(1, CombatEntityArchetype.ClanPatrol, 5.0f, 0f, 75f + 45f * rank, 3.4f, 0.60f, rank, 1.3f);
                    break;
                }
                case ArenaEncounterType.DemonicCultivator:
                {
                    int rank = TargetNpcId is { } npcId ? world.Get<ApertureComponent>(npcId)?.Rank ?? 2 : 2;
                    SpawnHostile(1, CombatEntityArchetype.DemonicCultivator, 5.0f, 0f, 95f + 45f * rank, 3.4f, 0.65f, rank, 1.15f);
                    if (rank >= 2)
                    {
                        SpawnHostile(2, CombatEntityArchetype.WildBoarSwarm, 6.5f, -1.8f, 45f, 3.8f, 0.45f, 1, 0.5f);
                        SpawnHostile(3, CombatEntityArchetype.WildBoarSwarm, 6.5f, 1.8f, 45f, 3.8f, 0.45f, 1, 0.5f);
                    }
                    break;
                }
                case ArenaEncounterType.SovereignTrial:
                {
                    SpawnHostile(1, CombatEntityArchetype.SovereignElder, 5.0f, 0f, 650f, 4.0f, 0.75f, 5, 1.0f);
                    SpawnHostile(2, CombatEntityArchetype.DemonicCultivator, 6.5f, -2.2f, 220f, 3.8f, 0.60f, 4, 1.2f);
                    SpawnHostile(3, CombatEntityArchetype.ClanPatrol, 6.5f, 2.2f, 220f, 3.8f, 0.60f, 4, 1.2f);
                    break;
                }
            }
        }

        private void SpawnHostile(
            int slotIndex,
            CombatEntityArchetype archetype,
            float x,
            float y,
            float maxHealth,
            float speed,
            float radius,
            int rank,
            float attackCooldown)
        {
            if (slotIndex < 1 || slotIndex >= MAX_ENTITIES) return;

            ref CombatEntityData enemy = ref EntityBuffer[slotIndex];
            enemy.Active = true;
            enemy.SlotIndex = slotIndex;
            enemy.Team = CombatEntityTeam.Hostile;
            enemy.Archetype = archetype;
            enemy.PosX = x;
            enemy.PosY = y;
            enemy.VelX = 0f;
            enemy.VelY = 0f;
            enemy.AimX = -1f;
            enemy.AimY = 0f;
            enemy.Radius = radius;
            enemy.MaxHealth = maxHealth;
            enemy.Health = maxHealth;
            enemy.MoveSpeed = speed;
            enemy.AttackCooldown = attackCooldown;
            enemy.Rank = rank;
            enemy.ChannelTimer = 0f;
            enemy.StunTimer = 0f;
        }

        private void SpawnProjectile(
            CombatEntityTeam team,
            float x,
            float y,
            float velocityX,
            float velocityY,
            float damage,
            float range,
            int visualStyle)
        {
            for (int i = 0; i < MAX_PROJECTILES; i++)
            {
                ref CombatProjectileData projectile = ref ProjectileBuffer[i];
                if (projectile.Active) continue;

                projectile.Active = true;
                projectile.OwnerTeam = team;
                projectile.PosX = x;
                projectile.PosY = y;
                projectile.VelX = velocityX;
                projectile.VelY = velocityY;
                projectile.Damage = damage;
                projectile.Radius = 0.18f;
                projectile.RemainingRange = range;
                projectile.VisualStyle = visualStyle;
                return;
            }
        }

        private static void TickTimers(ref CombatEntityData entity, float dt)
        {
            if (entity.AttackCooldown > 0f) entity.AttackCooldown = MathF.Max(0f, entity.AttackCooldown - dt);
            if (entity.HitFlashTimer > 0f) entity.HitFlashTimer = MathF.Max(0f, entity.HitFlashTimer - dt);
            if (entity.DashTimer > 0f) entity.DashTimer = MathF.Max(0f, entity.DashTimer - dt);
            if (entity.StunTimer > 0f) entity.StunTimer = MathF.Max(0f, entity.StunTimer - dt);
            if (entity.ShieldTimer > 0f)
            {
                entity.ShieldTimer = MathF.Max(0f, entity.ShieldTimer - dt);
                if (entity.ShieldTimer <= 0f) entity.ShieldHP = 0f;
            }
        }

        public void DamagePlayer(WorldStore world, ref CombatEntityData player, float damage)
        {
            if (!player.Active || damage <= 0f || player.DashTimer > 0f || _playerIFrameTimer > 0f) return;

            bool hadBacklash = false;

            // Killer Move channel interrupt + resonance backlash (routed through mitigation & shields)
            if (player.ChannelTimer > 0f)
            {
                hadBacklash = true;
                player.ChannelTimer = 0f;
                damage += 25f;
                player.StunTimer = 1.5f;

                // Deduct satiety on participating Gu
                for (int s = 0; s < 5; s++)
                {
                    EntityId guEid = _skillGuEntityIds[s];
                    if (guEid.IsValid && world.TryGet<GuWormComponent>(guEid, out var gu) && gu != null)
                    {
                        gu.Satiety = MathF.Max(0f, gu.Satiety - 20f);
                    }
                }

                WorldSignals.EmitSound(HalalSoundType.BluntImpact);
                WorldSignals.EmitTopics(DirtyTopic.Inventory);
            }

            // Apply DaoMarks mitigation
            float mitigation = _playerDaoMarks != null ? _playerDaoMarks.GetDamageMitigationMultiplier() : 1.0f;
            float effectiveDamage = damage * mitigation;

            _playerIFrameTimer = PlayerIFrameDuration;

            if (player.ShieldHP > 0f)
            {
                player.ShieldHP -= effectiveDamage;
                if (player.ShieldHP > 0f)
                {
                    player.HitFlashTimer = hadBacklash ? 0.25f : 0.12f;
                    return;
                }

                effectiveDamage = -player.ShieldHP;
                player.ShieldHP = 0f;
            }

            player.Health -= effectiveDamage;
            player.HitFlashTimer = hadBacklash ? 0.25f : 0.15f;
        }

        public void DamagePlayer(ref CombatEntityData player, float damage)
        {
            if (_lastWorld != null)
            {
                DamagePlayer(_lastWorld, ref player, damage);
            }
            else
            {
                if (!player.Active || damage <= 0f || player.DashTimer > 0f || _playerIFrameTimer > 0f) return;

                bool hadBacklash = false;
                if (player.ChannelTimer > 0f)
                {
                    hadBacklash = true;
                    player.ChannelTimer = 0f;
                    damage += 25f;
                    player.StunTimer = 1.5f;
                }

                float mitigation = _playerDaoMarks != null ? _playerDaoMarks.GetDamageMitigationMultiplier() : 1.0f;
                float effectiveDamage = damage * mitigation;

                _playerIFrameTimer = PlayerIFrameDuration;

                if (player.ShieldHP > 0f)
                {
                    player.ShieldHP -= effectiveDamage;
                    if (player.ShieldHP > 0f)
                    {
                        player.HitFlashTimer = hadBacklash ? 0.25f : 0.12f;
                        return;
                    }
                    effectiveDamage = -player.ShieldHP;
                    player.ShieldHP = 0f;
                }

                player.Health -= effectiveDamage;
                player.HitFlashTimer = hadBacklash ? 0.25f : 0.15f;
            }
        }

        private static void ClampToArena(ref CombatEntityData entity)
        {
            float limitX = ARENA_HALF_W - entity.Radius;
            float limitY = ARENA_HALF_H - entity.Radius;
            if (entity.PosX < -limitX) entity.PosX = -limitX;
            else if (entity.PosX > limitX) entity.PosX = limitX;
            if (entity.PosY < -limitY) entity.PosY = -limitY;
            else if (entity.PosY > limitY) entity.PosY = limitY;
        }

        private static string GetDefaultEnemyName(ArenaEncounterType encounter) => encounter switch
        {
            ArenaEncounterType.WildBoarHunt => "Wild Boar Pack (野彘群)",
            ArenaEncounterType.ClanPatrolDuel => "Clan Patrol Duellist (卫队修士)",
            ArenaEncounterType.DemonicCultivator => "Demonic Cultivator (魔修)",
            ArenaEncounterType.SovereignTrial => "Sovereign Patriarch (峰主大能)",
            _ => "Unknown Foe"
        };
    }
}
