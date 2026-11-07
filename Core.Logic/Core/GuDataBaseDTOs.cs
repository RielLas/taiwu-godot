using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace TaiwuGu.Core
{
    [Serializable]
    public class GuWormDefinitionDTO
    {
        [JsonProperty("id")]
        public string Id { get; set; } = "";

        [JsonProperty("canonical_name")]
        public string CanonicalName { get; set; } = "";

        [JsonProperty("chinese_name")]
        public string ChineseName { get; set; } = "";

        [JsonProperty("path")]
        public string Path { get; set; } = "";

        [JsonProperty("rank")]
        public int Rank { get; set; }

        [JsonProperty("slot_category")]
        public int SlotCategory { get; set; }

        [JsonProperty("base_beu_cost")]
        public float BaseBeuCost { get; set; }

        [JsonProperty("cooldown_seconds")]
        public float CooldownSeconds { get; set; }

        [JsonProperty("diet")]
        public GuDietDTO? Diet { get; set; }

        [JsonProperty("combat_stats")]
        public GuCombatStatsDTO? CombatStats { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; } = "";

        [JsonProperty("refinement_paths")]
        public List<GuRefinementPathDTO> RefinementPaths { get; set; } = new List<GuRefinementPathDTO>();
    }

    [Serializable]
    public class GuDietDTO
    {
        [JsonProperty("diet_type")]
        public int DietType { get; set; }

        [JsonProperty("feed_frequency_days")]
        public int FeedFrequencyDays { get; set; }

        [JsonProperty("ration_amount")]
        public int RationAmount { get; set; }

        [JsonProperty("starvation_grace_days")]
        public int StarvationGraceDays { get; set; }
    }

    [Serializable]
    public class GuCombatStatsDTO
    {
        [JsonProperty("projectile_prefab")]
        public string ProjectilePrefab { get; set; } = "";

        [JsonProperty("base_damage")]
        public float BaseDamage { get; set; }

        [JsonProperty("speed")]
        public float Speed { get; set; }

        [JsonProperty("range")]
        public float Range { get; set; }
    }

    [Serializable]
    public class GuRefinementPathDTO
    {
        [JsonProperty("result_gu_id")]
        public string ResultGuId { get; set; } = "";

        [JsonProperty("required_materials")]
        public List<string> RequiredMaterials { get; set; } = new List<string>();

        [JsonProperty("catalysts")]
        public List<string> Catalysts { get; set; } = new List<string>();

        [JsonProperty("base_success_rate")]
        public float BaseSuccessRate { get; set; }
    }
}
