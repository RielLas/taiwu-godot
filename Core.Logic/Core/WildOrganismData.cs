using System;

namespace TaiwuGu.Core
{
    public enum WildOrganismTemperament
    {
        Docile,     // 温顺: Slower will wander, low backlash
        Ferocious,  // 凶煞: Fast aggressive will spikes, high aperture backlash damage
        Cunning     // 狡黠: Erratic needle jumps, rapid resistance regeneration
    }

    public enum SubjugationState
    {
        Inactive,
        Clashing,
        Success,
        FailedBacklash,
        FailedEscaped
    }

    [Serializable]
    public class WildOrganismData
    {
        public string OrganismID { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Rank { get; set; } = 1;
        public GuSlotCategory SlotCategory { get; set; } = GuSlotCategory.Attack;
        public GuDietType Diet { get; set; } = GuDietType.MoonOrchidPetals;
        public float BaseEssenceCost { get; set; } = 10f;
        public WildOrganismTemperament Temperament { get; set; } = WildOrganismTemperament.Docile;
        public float FeralWillPower { get; set; } = 100f; // [0..100]
        public string DiscoveryLore { get; set; } = string.Empty;

        public WildOrganismData() { }

        public WildOrganismData(string id, string name, int rank, GuSlotCategory slot, GuDietType diet, float cost, WildOrganismTemperament temp, string lore)
        {
            OrganismID = id;
            Name = name;
            Rank = rank;
            SlotCategory = slot;
            Diet = diet;
            BaseEssenceCost = cost;
            Temperament = temp;
            FeralWillPower = 100f;
            DiscoveryLore = lore;
        }

        public static WildOrganismData CreateFromGuDefinition(GuWormDefinitionDTO def, WildOrganismTemperament temperament = WildOrganismTemperament.Docile)
        {
            GuDietType diet = def.Diet != null ? (GuDietType)def.Diet.DietType : GuDietType.MoonOrchidPetals;
            GuSlotCategory slot = (GuSlotCategory)Math.Clamp(def.SlotCategory, 0, 6);
            string name = !string.IsNullOrEmpty(def.ChineseName) ? $"{def.CanonicalName} ({def.ChineseName})" : def.CanonicalName;

            string lore = !string.IsNullOrEmpty(def.Description)
                ? def.Description
                : $"A wild {name} nestled in primeval mountain qi, its feral will unyielding.";

            return new WildOrganismData(
                def.Id,
                name,
                def.Rank,
                slot,
                diet,
                def.BaseBeuCost > 0 ? def.BaseBeuCost : 10f,
                temperament,
                lore
            );
        }

        public static WildOrganismData CreateWildOrganism(string guId, WildOrganismTemperament temperament = WildOrganismTemperament.Docile)
        {
            if (GuDatabase.TryGet(guId, out var def) && def != null)
            {
                return CreateFromGuDefinition(def, temperament);
            }

            return new WildOrganismData(
                guId,
                "Wild Moonlight Gu (野生月光蛊)",
                1,
                GuSlotCategory.Attack,
                GuDietType.MoonOrchidPetals,
                10f,
                temperament,
                "A wild translucent crescent-shaped Gu worm glowing with faint lunar luminescence."
            );
        }
    }

    /// <summary>
    /// Immutable snapshot representing the active will subjugation minigame state.
    /// Broadcasted across GameEventManager under CQRS-Lite.
    /// </summary>
    public readonly struct SubjugationSessionSnapshot
    {
        public WildOrganismData Target { get; }
        public int DeltaRank { get; }
        public SubjugationState State { get; }
        public float FeralWillResistance { get; }
        public float PlayerWillAlignment { get; }
        public float PlayerWillNeedle { get; }
        public float FeralWillFrequency { get; }
        public float HarmonicTolerance { get; }
        public float ClashTimer { get; }
        public string ResolutionMessage { get; }
        public bool IsInstantAvailable { get; }

        public SubjugationSessionSnapshot(
            WildOrganismData target,
            int deltaRank,
            SubjugationState state,
            float feralWillResistance,
            float playerWillAlignment,
            float playerWillNeedle,
            float feralWillFrequency,
            float harmonicTolerance,
            float clashTimer,
            string resolutionMessage,
            bool isInstantAvailable)
        {
            Target = target;
            DeltaRank = deltaRank;
            State = state;
            FeralWillResistance = feralWillResistance;
            PlayerWillAlignment = playerWillAlignment;
            PlayerWillNeedle = playerWillNeedle;
            FeralWillFrequency = feralWillFrequency;
            HarmonicTolerance = harmonicTolerance;
            ClashTimer = clashTimer;
            ResolutionMessage = resolutionMessage;
            IsInstantAvailable = isInstantAvailable;
        }
    }
}
