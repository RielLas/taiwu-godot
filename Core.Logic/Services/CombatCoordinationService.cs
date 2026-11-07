using System;
using TaiwuGu.Combat;
using TaiwuGu.Map;
using TaiwuGu.Psychology;

namespace TaiwuGu.Core
{
    /// <summary>
    /// Phase 5 Domain Service: Combat Coordination Engine under Constitutional Laws 1, 3, and 4.
    /// Coordinates overworld combat encounters, transitions to the isolated arena,
    /// and processes combat rewards (Primeval Stones, Gu rations) and AP depletion upon victory.
    /// </summary>
    public class CombatCoordinationService : IDisposable
    {
        private GameStateContext? _context;

        public void Initialize(GameStateContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));

            GameEventManager.OnPOIActionRequested += HandlePOIActionRequested;
            GameEventManager.OnCombatConcluded    += HandleCombatConcluded;

            Debug.Log("[CombatCoordinationService] Initialized and coordinating combat transitions.");
        }

        public void Dispose()
        {
            GameEventManager.OnPOIActionRequested -= HandlePOIActionRequested;
            GameEventManager.OnCombatConcluded    -= HandleCombatConcluded;
        }

        public void InitiateCombat(ArenaEncounterType encounter, Vector2Int tilePos)
        {
            GameEventManager.RaiseRequestEnterCombat(encounter, tilePos);
        }

        private void HandlePOIActionRequested(TilePOIType poi, Vector2Int pos, string actionId)
        {
            if (actionId == "beast_den_combat")
            {
                Debug.Log($"[CombatCoordinationService] Entering Beast Den combat encounter at ({pos.x}, {pos.y})...");
                InitiateCombat(ArenaEncounterType.WildBoarHunt, pos);
            }
            else if (actionId == "cave_explore")
            {
                Debug.Log($"[CombatCoordinationService] Entering Ancient Gu Cave expedition at ({pos.x}, {pos.y})...");
                InitiateCombat(ArenaEncounterType.DemonicCultivator, pos);
            }
        }

        private void HandleCombatConcluded(bool playerWon, Vector2Int tilePos, string enemyName, ArenaEncounterType encounter)
        {
            if (_context == null) return;

            if (playerWon)
            {
                // Spend 1 AP for the grueling battle via central temporal event
                GameEventManager.RaiseRequestDeductActionPoints(1);

                // Award combat spoils and wild Gu from JSON database
                if (encounter == ArenaEncounterType.WildBoarHunt)
                {
                    _context.Cultivator.PrimevalStones += 15;
                    _context.GuInventory.AddFood(GuDietType.FreshBoarFlesh, 2);

                    // 40% chance of wild Strength Gu capture (White Boar Gu or Black Boar Gu)
                    string droppedGuId = Random.Shared.NextDouble() < 0.5 ? "gu_white_boar_r1" : "gu_black_boar_r1";
                    if (Random.Shared.NextDouble() < 0.40)
                    {
                        var wildGu = GuDatabase.CreateGuWormData(droppedGuId, isWild: true);
                        if (wildGu != null)
                        {
                            _context.GuInventory.ReserveGu.Add(wildGu);
                            Debug.Log($"★ [WILD GU CAPTURED] Discovered wild {wildGu.Name} lurking within beast carcass! Added to Reserve Gu.");
                        }
                    }

                    GameEventManager.TriggerPrimevalStonesChanged(_context.Cultivator.PrimevalStones);
                    GameEventManager.TriggerGuInventoryUpdated(_context.GuInventory.EquippedGu, _context.GuInventory.ReserveGu);

                    Debug.Log("[COMBAT SPOILS] Slew Frenzied Razor Boar! Harvested +15 Primeval Stones & 2x Fresh Boar Flesh!");
                }
                else if (encounter == ArenaEncounterType.DemonicCultivator)
                {
                    _context.Cultivator.PrimevalStones += 40;

                    string[] caveLootPool = new[] { "gu_liquor_worm_r1", "gu_white_jade_r1", "gu_little_light_r1", "gu_bronze_skin_r1" };
                    string lootId = caveLootPool[Random.Shared.Next(0, caveLootPool.Length)];
                    var wildGu = GuDatabase.CreateGuWormData(lootId, isWild: true);
                    if (wildGu != null)
                    {
                        _context.GuInventory.ReserveGu.Add(wildGu);
                        Debug.Log($"★ [ANCIENT GU RECOVERED] Unearthed {wildGu.Name} from subterranean chamber! Added to Reserve Gu.");
                    }

                    _context.GuInventory.AddFood(GuDietType.VintageWine, 2);
                    GameEventManager.TriggerPrimevalStonesChanged(_context.Cultivator.PrimevalStones);
                    GameEventManager.TriggerGuInventoryUpdated(_context.GuInventory.EquippedGu, _context.GuInventory.ReserveGu);
                    Debug.Log($"[COMBAT SPOILS] Vanquished {enemyName}! Acquired +40 Primeval Stones & ancient relics!");
                }
                else if (encounter == ArenaEncounterType.ClanPatrolDuel)
                {
                    _context.Cultivator.PrimevalStones += 25;
                    _context.GuInventory.AddFood(GuDietType.MoonOrchidPetals, 3);
                    GameEventManager.TriggerPrimevalStonesChanged(_context.Cultivator.PrimevalStones);
                    GameEventManager.TriggerGuInventoryUpdated(_context.GuInventory.EquippedGu, _context.GuInventory.ReserveGu);
                    Debug.Log($"[COMBAT SPOILS] Vanquished {enemyName}! Acquired +25 Primeval Stones & Moon Orchid Petals from enemy pouch!");
                }
            }
            else
            {
                _context.Cultivator.CurrentEssenceSea = Mathf.Max(0f, _context.Cultivator.CurrentEssenceSea - 15f);
                GameEventManager.TriggerPrimevalSeaChanged(_context.Cultivator.CurrentEssenceSea, _context.Cultivator.AptitudePercent);
                Debug.LogWarning("[COMBAT RETREAT] Suffered battle backlash! Primeval Sea drained by 15%.");
            }
        }
    }
}
