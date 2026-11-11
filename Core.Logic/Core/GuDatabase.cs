using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using TaiwuGu.Entities;

namespace TaiwuGu.Core
{
    /// <summary>
    /// Static In-Memory Database for all canonical Gu worms.
    /// Loaded dynamically from JSON content.
    /// </summary>
    public static class GuDatabase
    {
        private static readonly Dictionary<string, GuWormDefinitionDTO> _database = new Dictionary<string, GuWormDefinitionDTO>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, (GuRefinementPathDTO path, GuWormDefinitionDTO? result)> _recipeIndex = new Dictionary<string, (GuRefinementPathDTO, GuWormDefinitionDTO?)>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, (GuWormDefinitionDTO source, GuRefinementPathDTO path, GuWormDefinitionDTO? result)> _resultIndex = new Dictionary<string, (GuWormDefinitionDTO, GuRefinementPathDTO, GuWormDefinitionDTO?)>(StringComparer.OrdinalIgnoreCase);
        private static bool _isLoaded = false;

        private static string MakeRecipeKey(string m1, string m2)
        {
            m1 = m1?.ToLowerInvariant() ?? "";
            m2 = m2?.ToLowerInvariant() ?? "";
            return string.CompareOrdinal(m1, m2) <= 0 ? $"{m1}+{m2}" : $"{m2}+{m1}";
        }

        public static bool IsLoaded => _isLoaded;
        public static int Count => _database.Count;

        /// <summary>
        /// Loads the canonical Gu catalog from JSON supplied by the host.
        /// The database deliberately does not search the filesystem: the host decides where content
        /// lives (Godot: res://Resources/Data) so domain behavior never depends on the process cwd
        /// or on unrelated folders that happen to sit next to the executable.
        /// </summary>
        public static bool Initialize(string? jsonContent)
        {
            _database.Clear();
            _recipeIndex.Clear();
            _resultIndex.Clear();
            _isLoaded = false;

            if (string.IsNullOrEmpty(jsonContent))
            {
                Debug.LogError("[GuDatabase] Initialize requires JSON content. No catalog was supplied, so the Gu database is empty.");
                return false;
            }

            try
            {
                List<GuWormDefinitionDTO>? list = JsonConvert.DeserializeObject<List<GuWormDefinitionDTO>>(jsonContent);
                if (list == null)
                {
                    Debug.LogError("[GuDatabase] gu_worms_database JSON deserialized to null.");
                    return false;
                }

                foreach (var def in list)
                {
                    if (!string.IsNullOrEmpty(def.Id))
                    {
                        _database[def.Id] = def;
                    }
                }

                foreach (var def in _database.Values)
                {
                    if (def.RefinementPaths == null) continue;
                    foreach (var path in def.RefinementPaths)
                    {
                        if (path.RequiredMaterials == null || path.RequiredMaterials.Count == 0) continue;
                        string m1 = path.RequiredMaterials[0];
                        string? m2 = path.RequiredMaterials.Count >= 2 ? path.RequiredMaterials[1] : null;
                        _database.TryGetValue(path.ResultGuId, out var resultDef);
                        _recipeIndex[MakeRecipeKey(m1, m2 ?? "")] = (path, resultDef);

                        if (!string.IsNullOrEmpty(path.ResultGuId) && !_resultIndex.ContainsKey(path.ResultGuId))
                        {
                            _resultIndex[path.ResultGuId] = (def, path, resultDef);
                        }
                    }
                }

                _isLoaded = true;
                Debug.Log($"[GuDatabase] Successfully loaded {_database.Count} canonical Gu worms from JSON database.");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GuDatabase] Failed to parse gu_worms_database JSON: {ex.Message}");
                return false;
            }
        }

        public static GuWormDefinitionDTO? Get(string guId)
        {
            if (string.IsNullOrEmpty(guId)) return null;

            _database.TryGetValue(guId, out var def);
            return def;
        }

        public static bool TryGet(string guId, out GuWormDefinitionDTO? def)
        {
            if (string.IsNullOrEmpty(guId))
            {
                def = null;
                return false;
            }
            return _database.TryGetValue(guId, out def);
        }

        public static IEnumerable<GuWormDefinitionDTO> GetAll()
        {
            return _database.Values;
        }

        public static bool TryFindRecipe(string primaryGuId, string secondaryGuId, out GuRefinementPathDTO? foundPath, out GuWormDefinitionDTO? resultGu)
        {
            if (_recipeIndex.TryGetValue(MakeRecipeKey(primaryGuId, secondaryGuId), out var entry))
            {
                foundPath = entry.path;
                resultGu = entry.result;
                return true;
            }

            foundPath = null;
            resultGu = null;
            return false;
        }

        public static bool TryFindRecipeByResult(string resultGuId, out GuWormDefinitionDTO? sourceGu, out GuRefinementPathDTO? foundPath, out GuWormDefinitionDTO? resultGu)
        {
            if (!string.IsNullOrEmpty(resultGuId) && _resultIndex.TryGetValue(resultGuId, out var entry))
            {
                sourceGu = entry.source;
                foundPath = entry.path;
                resultGu = entry.result;
                return true;
            }

            sourceGu = null;
            foundPath = null;
            resultGu = null;
            return false;
        }

        public static GuWormComponent? CreateGuWormComponent(string guId, bool isWild = false)
        {
            if (!TryGet(guId, out var def) || def == null) return null;

            GuDietType diet = def.Diet != null ? (GuDietType)def.Diet.DietType : GuDietType.None;
            GuSlotCategory slot = (GuSlotCategory)Mathf.Clamp(def.SlotCategory, 0, 6);

            float satiety = isWild ? (float)Random.Shared.Next(40, 91) : 100f;

            return new GuWormComponent
            {
                GuID = def.Id,
                Name = !string.IsNullOrEmpty(def.ChineseName) ? $"{def.CanonicalName} ({def.ChineseName})" : def.CanonicalName,
                Rank = def.Rank,
                Slot = slot,
                Diet = diet,
                BaseEssenceCost = def.BaseBeuCost > 0 ? def.BaseBeuCost : 10f,
                Satiety = satiety,
                IsVitalGu = false,
                HasHostileWill = isWild,
                IsPredatory = false
            };
        }
    }
}
