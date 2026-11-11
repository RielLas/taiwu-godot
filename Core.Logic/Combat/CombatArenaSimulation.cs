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
        public const int MAX_SKILLS = 5;

        public const float ARENA_HALF_W = 9.5f;
        public const float ARENA_HALF_H = 5.2f;

        public readonly CombatEntityData[] EntityBuffer = new CombatEntityData[MAX_ENTITIES];
        public readonly CombatProjectileData[] ProjectileBuffer = new CombatProjectileData[MAX_PROJECTILES];
        public readonly CombatSkillSlot[] PlayerSkills = new CombatSkillSlot[MAX_SKILLS];

        private readonly EntityId[] _skillGuEntityIds = new EntityId[MAX_SKILLS];
        private EntityId _playerEntity;
        private ApertureComponent? _playerAperture;

        public bool IsActive;
        public CombatOutcome Outcome;
        public ArenaEncounterType EncounterType;
        public GridCoord SourceTilePos;
        public EntityId? TargetNpcId { get; private set; }
        public bool IsLethal;
        public string EnemyDisplayName = string.Empty;
        public float StoneCrushCooldown;
        public float ElapsedTime;

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
            EnemyDisplayName = enemyName ?? GetDefaultEnemyName(encounter);

            _playerEntity = world.PlayerEntity;
            ApertureComponent? aperture = world.Get<ApertureComponent>(_playerEntity);
            _playerAperture = aperture;

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
                moveX /= moveMag;
                moveY /= moveMag;
                if (player.DashTimer <= 0f)
                {
                    player.VelX = moveX * player.MoveSpeed;
                    player.VelY = moveY * player.MoveSpeed;
                }
            }
            else if (player.DashTimer <= 0f)
            {
                player.VelX = 0f;
                player.VelY = 0f;
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
            if (!player.Active) return false;

            if (!world.TryGet<ApertureComponent>(_playerEntity, out var aperture) || aperture == null) return false;
            if (!world.TryGet<GuWormComponent>(_skillGuEntityIds[slotIndex], out var gu) || gu == null) return false;

            GuStarvationStage stage = gu.GetStarvationStage();
            if (stage == GuStarvationStage.Hibernating || stage == GuStarvationStage.Dead)
            {
                WorldSignals.EmitSound(HalalSoundType.WoodenKnock);
                return false;
            }

            float realCostPct = gu.GetRealEssenceCost(aperture.Rank, aperture.Substage);
            if (aperture.CurrentEssenceSea < realCostPct)
            {
                WorldSignals.EmitSound(HalalSoundType.WoodenKnock);
                return false;
            }

            aperture.CurrentEssenceSea -= realCostPct;
            WorldSignals.EmitTopics(DirtyTopic.Aperture);

            float mult = gu.GetPowerMultiplier();
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
                        skill.BaseDamage * mult,
                        skill.Range > 0f ? skill.Range : 8f,
                        style);
                    WorldSignals.EmitSound(HalalSoundType.BladeSlash);
                    break;
                }
                case GuSkillEffectKind.Shield:
                    player.ShieldHP = (25f + skill.Rank * 15f) * mult;
                    player.ShieldTimer = 4.0f;
                    WorldSignals.EmitSound(HalalSoundType.WoodenKnock);
                    break;
                case GuSkillEffectKind.Dash:
                    player.VelX = player.AimX * 12.5f;
                    player.VelY = player.AimY * 12.5f;
                    player.DashTimer = 0.25f;
                    WorldSignals.EmitSound(HalalSoundType.DashWhoosh);
                    break;
                case GuSkillEffectKind.Heal:
                    player.Health = MathF.Min(player.MaxHealth, player.Health + 20f * skill.Rank * mult);
                    if (_playerAperture != null) _playerAperture.CurrentHealth = player.Health;
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

                        enemy.Health -= 18f * skill.Rank * mult;
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

            ElapsedTime += dt;
            if (StoneCrushCooldown > 0f) StoneCrushCooldown = MathF.Max(0f, StoneCrushCooldown - dt);

            for (int s = 0; s < MAX_SKILLS; s++)
            {
                if (PlayerSkills[s].CooldownRemaining > 0f)
                {
                    PlayerSkills[s].CooldownRemaining = MathF.Max(0f, PlayerSkills[s].CooldownRemaining - dt);
                }
            }

            ref CombatEntityData player = ref EntityBuffer[0];
            if (!player.Active)
            {
                IsActive = false;
                Outcome = CombatOutcome.Defeat;
                return Outcome;
            }

            TickTimers(ref player, dt);

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
                        DamagePlayer(ref player, 10f);
                        enemy.AttackCooldown = 1.8f;
                    }
                    ClampToArena(ref enemy);
                }
                else
                {
                    if (dist < 3.2f && dist > 0.0001f)
                    {
                        enemy.PosX -= dx / dist * enemy.MoveSpeed * dt;
                        enemy.PosY -= dy / dist * enemy.MoveSpeed * dt;
                    }
                    else if (dist > 5.5f && dist > 0.0001f)
                    {
                        enemy.PosX += dx / dist * enemy.MoveSpeed * dt;
                        enemy.PosY += dy / dist * enemy.MoveSpeed * dt;
                    }
                    ClampToArena(ref enemy);

                    if (enemy.AttackCooldown <= 0f && dist < 9.0f)
                    {
                        float aimX = dist > 0.0001f ? dx / dist : 1f;
                        float aimY = dist > 0.0001f ? dy / dist : 0f;
                        int style = enemy.Archetype == CombatEntityArchetype.ClanPatrol ? 3 : 2;
                        float damage = 8f + enemy.Rank * 3f;
                        SpawnProjectile(
                            CombatEntityTeam.Hostile,
                            enemy.PosX + aimX * enemy.Radius,
                            enemy.PosY + aimY * enemy.Radius,
                            aimX * 8.5f,
                            aimY * 8.5f,
                            damage,
                            12f,
                            style);
                        enemy.AttackCooldown = enemy.Archetype == CombatEntityArchetype.ClanPatrol ? 1.3f : 1.15f;
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
                        DamagePlayer(ref player, projectile.Damage);
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

            if (_playerAperture != null) _playerAperture.CurrentHealth = player.Health;
            return Outcome;
        }

        private void PopulateSkills(WorldStore world)
        {
            for (int equipSlot = 0; equipSlot < MAX_SKILLS; equipSlot++)
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
                    Range = range > 0f ? range : 8f
                };
                _skillGuEntityIds[equipSlot] = guEntity;
            }
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
            if (entity.ShieldTimer > 0f)
            {
                entity.ShieldTimer = MathF.Max(0f, entity.ShieldTimer - dt);
                if (entity.ShieldTimer <= 0f) entity.ShieldHP = 0f;
            }
        }

        private static void DamagePlayer(ref CombatEntityData player, float damage)
        {
            if (!player.Active || damage <= 0f || player.DashTimer > 0f) return;

            if (player.ShieldHP > 0f)
            {
                player.ShieldHP -= damage;
                if (player.ShieldHP > 0f)
                {
                    player.HitFlashTimer = 0.12f;
                    return;
                }

                damage = -player.ShieldHP;
                player.ShieldHP = 0f;
            }

            player.Health -= damage;
            player.HitFlashTimer = 0.15f;
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
            _ => "Unknown Foe"
        };
    }
}
