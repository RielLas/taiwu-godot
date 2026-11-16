namespace TaiwuGu.Combat
{
    public enum ArenaEncounterType
    {
        WildBoarHunt,       // Wild Beast Den
        ClanPatrolDuel,     // Righteous Clan Outpost
        DemonicCultivator,  // Ancient Cave Ambush
        SovereignTrial      // Peak Rank 5 Sovereign Trial Encounter
    }

    public enum CombatOutcome
    {
        Ongoing,
        Victory,
        Defeat,
        Retreated
    }

    public enum CombatEntityTeam : byte
    {
        Player = 0,
        Hostile = 1
    }

    public enum CombatEntityArchetype : byte
    {
        PlayerCultivator = 0,
        WildBoarSwarm = 1,
        ClanPatrol = 2,
        DemonicCultivator = 3,
        SovereignElder = 4
    }

    public enum GuSkillEffectKind : byte
    {
        Projectile = 0,
        Dash = 1,
        Shield = 2,
        Heal = 3,
        AoEPulse = 4
    }

    /// <summary>
    /// Flat, allocation-free combat entity slot. Lives inside CombatArenaSimulation.EntityBuffer[64].
    /// </summary>
    public struct CombatEntityData
    {
        public bool Active;
        public int SlotIndex;
        public CombatEntityTeam Team;
        public CombatEntityArchetype Archetype;

        public float PosX;
        public float PosY;
        public float VelX;
        public float VelY;
        public float AimX;
        public float AimY;
        public float Radius;

        public float Health;
        public float MaxHealth;
        public float ShieldHP;
        public float ShieldTimer;
        public float MoveSpeed;
        public float AttackCooldown;
        public float DashTimer;
        public float HitFlashTimer;
        public float ChannelTimer;
        public float StunTimer;

        public int Rank;
    }

    /// <summary>
    /// Flat projectile slot. Lives inside CombatArenaSimulation.ProjectileBuffer[128].
    /// VisualStyle: 0 = Moonblade, 1 = BoarChargeRune, 2 = BloodArc, 3 = JadeBolt.
    /// </summary>
    public struct CombatProjectileData
    {
        public bool Active;
        public CombatEntityTeam OwnerTeam;

        public float PosX;
        public float PosY;
        public float VelX;
        public float VelY;
        public float Damage;
        public float Radius;
        public float RemainingRange;

        public int VisualStyle;
    }

    /// <summary>
    /// Flat hotbar slot populated once from the player's equipped Gu.
    /// Strings are filled only during InitializeArena, never inside Step.
    /// </summary>
    public struct CombatSkillSlot
    {
        public bool IsPopulated;
        public string GuId;
        public string DisplayName;
        public int Rank;
        public Core.GuSlotCategory SlotCategory;
        public GuSkillEffectKind EffectKind;
        public float BaseBeuCost;
        public float CooldownDuration;
        public float CooldownRemaining;
        public float BaseDamage;
        public float ProjectileSpeed;
        public float Range;
        public Core.DaoPathType PathType;
    }
}
