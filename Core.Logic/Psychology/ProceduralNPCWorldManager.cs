using System;
using System.Collections.Generic;
using TaiwuGu.Core;
using TaiwuGu.Combat;

namespace TaiwuGu.Psychology
{
    /// <summary>
    /// Phase 2 / Milestone 2.3: Procedural NPC World Manager & Social Matrix Engine (太吾式动态人际天网与宗族交互引擎).
    /// Pure C# Domain Service implementing CQRS pattern and IDisposable.
    /// Manages:
    /// - Living population of roaming cultivators across Qing Mao Mountain.
    /// - Overworld monthly tick simulation: tile wandering, stone generation, cultivation growth.
    /// - Social Interactions: Sparring (切磋), Bartering (交易), Extortion (勒索), Ambush (截杀).
    /// - Kinship vendettas & Soul Tablet tracking via SocialVendettaNetwork.
    /// </summary>
    public class ProceduralNPCWorldManager : IDisposable
    {
        private GameStateContext? _stateContext;
        private bool _isInitialized = false;

        private readonly List<CultivatorEntityData> _livingCultivators = new List<CultivatorEntityData>();
        private readonly SocialVendettaNetwork _vendettaNetwork;
        private readonly Random _rng = new Random();

        private CultivatorEntityData? _activeNPC;
        private bool _isInteractionOpen = false;

        public IReadOnlyList<CultivatorEntityData> LivingCultivators => _livingCultivators;
        public SocialVendettaNetwork VendettaNetwork => _vendettaNetwork;
        public bool IsInteractionOpen => _isInteractionOpen;
        public CultivatorEntityData? ActiveNPC => _activeNPC;

        public ProceduralNPCWorldManager()
        {
            _vendettaNetwork = new SocialVendettaNetwork();
        }

        public void Initialize(GameStateContext context)
        {
            _stateContext = context ?? throw new ArgumentNullException(nameof(context));
            _isInitialized = true;

            // Subscribe to master event bus
            GameEventManager.OnSolarTermAdvanced          += HandleSolarTermAdvanced;
            GameEventManager.OnPlayerMoved                += HandlePlayerMoved;
            GameEventManager.OnCultivatorsWandered        += HandleCultivatorsWandered;
            GameEventManager.OnInteractWithNPCRequested   += HandleInteractWithNPCRequested;
            GameEventManager.OnNPCSparRequested           += HandleNPCSparRequested;
            GameEventManager.OnNPCBarterRequested         += HandleNPCBarterRequested;
            GameEventManager.OnNPCExtortRequested         += HandleNPCExtortRequested;
            GameEventManager.OnNPCAmbushRequested         += HandleNPCAmbushRequested;
            GameEventManager.OnCloseNPCDialogueRequested  += HandleCloseNPCDialogueRequested;

            if (_stateContext.LivingNPCs != null && _stateContext.LivingNPCs.Count > 0)
            {
                _livingCultivators.Clear();
                _livingCultivators.AddRange(_stateContext.LivingNPCs);
            }
            else
            {
                SeedInitialPopulation();
                _stateContext.LivingNPCs.Clear();
                _stateContext.LivingNPCs.AddRange(_livingCultivators);
            }
            _vendettaNetwork.RegisterProceduralWorldNPCs(_livingCultivators);

            // Broadcast initial map tokens and check tile
            BroadcastCultivatorMapTokens();
            if (_stateContext.WorldMap != null)
            {
                ResolveTileCultivators(new Vector2Int(_stateContext.WorldMap.PlayerGridPos.X, _stateContext.WorldMap.PlayerGridPos.Y));
            }

            Debug.Log($"[ProceduralNPCWorldManager] Initialized with {_livingCultivators.Count} living cultivators across Qing Mao Mountain.");
        }

        public void Dispose()
        {
            GameEventManager.OnSolarTermAdvanced          -= HandleSolarTermAdvanced;
            GameEventManager.OnPlayerMoved                -= HandlePlayerMoved;
            GameEventManager.OnCultivatorsWandered        -= HandleCultivatorsWandered;
            GameEventManager.OnInteractWithNPCRequested   -= HandleInteractWithNPCRequested;
            GameEventManager.OnNPCSparRequested           -= HandleNPCSparRequested;
            GameEventManager.OnNPCBarterRequested         -= HandleNPCBarterRequested;
            GameEventManager.OnNPCExtortRequested         -= HandleNPCExtortRequested;
            GameEventManager.OnNPCAmbushRequested         -= HandleNPCAmbushRequested;
            GameEventManager.OnCloseNPCDialogueRequested  -= HandleCloseNPCDialogueRequested;

            if (_isInteractionOpen)
            {
                _isInteractionOpen = false;
                GameEventManager.TriggerUIModalActive(false);
                GameEventManager.TriggerNPCDialogueClosed();
            }
            _isInitialized = false;
        }

        private void SeedInitialPopulation()
        {
            _livingCultivators.Clear();

            // 1. Gu Yue Clan disciples & elders
            _livingCultivators.Add(new CultivatorEntityData(
                "npc_chi_disciple", "Gu Yue Chi Cheng (古月赤成)", "Gu Yue Clan (古月氏)",
                1, 2, TaiwuDisposition.Rebellious, new Vector2Int(15, 14), age: 16, lifespanYears: 100)
            {
                PrimevalStones = 85,
                SoulTabletBound = true,
                Favorability = 10
            });

            _livingCultivators.Add(new CultivatorEntityData(
                "npc_mo_elder", "Gu Yue Mo Chen (古月漠尘)", "Gu Yue Clan (古月氏)",
                3, 1, TaiwuDisposition.Pragmatic, new Vector2Int(14, 15), age: 48, lifespanYears: 120)
            {
                PrimevalStones = 420,
                SoulTabletBound = true,
                Favorability = 0
            });

            _livingCultivators.Add(new CultivatorEntityData(
                "npc_elder_yan", "Elder Gu Yue Yan (古月阎长老)", "Gu Yue Clan (古月氏)",
                2, 2, TaiwuDisposition.Righteous, new Vector2Int(15, 15), age: 42, lifespanYears: 110)
            {
                PrimevalStones = 260,
                SoulTabletBound = true,
                Favorability = 15
            });

            _livingCultivators.Add(new CultivatorEntityData(
                "npc_enforcer_01", "Gu Yue Clan Enforcer (古月刑堂修士)", "Gu Yue Clan (古月氏)",
                1, 4, TaiwuDisposition.Righteous, new Vector2Int(15, 17), age: 24, lifespanYears: 100)
            {
                PrimevalStones = 110,
                SoulTabletBound = true,
                Favorability = 0
            });

            // 2. Bai Clan & Xiong Clan representatives
            _livingCultivators.Add(new CultivatorEntityData(
                "npc_bai_scout", "Bai Ning Bing (白凝冰)", "Bai Clan (白家寨)",
                2, 3, TaiwuDisposition.Rebellious, new Vector2Int(14, 16), age: 16, lifespanYears: 100)
            {
                PrimevalStones = 310,
                SoulTabletBound = true,
                Favorability = -20
            });

            _livingCultivators.Add(new CultivatorEntityData(
                "npc_xiong_warrior", "Xiong Li (熊力)", "Xiong Clan (熊家寨)",
                2, 2, TaiwuDisposition.Righteous, new Vector2Int(14, 17), age: 22, lifespanYears: 105)
            {
                PrimevalStones = 190,
                SoulTabletBound = true,
                Favorability = -10
            });

            // 3. Demonic Rogues & Caravan Travelers
            _livingCultivators.Add(new CultivatorEntityData(
                "npc_rogue_demon", "Scarred Vulture (苍疤鹫)", "Demonic Rogues (散修魔修)",
                2, 1, TaiwuDisposition.Egoist, new Vector2Int(18, 14), age: 35, lifespanYears: 95)
            {
                PrimevalStones = 160,
                SoulTabletBound = false,
                Favorability = -50
            });

            _livingCultivators.Add(new CultivatorEntityData(
                "npc_tie_detective", "Tie Ruo Nan (铁若男)", "Tie Clan (铁家)",
                2, 1, TaiwuDisposition.Righteous, new Vector2Int(13, 14), age: 17, lifespanYears: 110)
            {
                PrimevalStones = 240,
                SoulTabletBound = true,
                Favorability = 20
            });

            _livingCultivators.Add(new CultivatorEntityData(
                "npc_jia_steward", "Jia Fu (贾富)", "Jia Consortium (贾家商会)",
                2, 4, TaiwuDisposition.Benevolent, new Vector2Int(13, 15), age: 38, lifespanYears: 100)
            {
                PrimevalStones = 950,
                SoulTabletBound = false,
                Favorability = 30
            });
        }

        private void HandleSolarTermAdvanced(int year, SolarTerm term, string termName)
        {
            if (!_isInitialized) return;

            // Monthly simulation tick for all autonomous cultivators
            foreach (var npc in _livingCultivators)
            {
                if (!npc.IsAlive) continue;

                // 1. Economic & Cultivation Growth
                npc.PrimevalStones += _rng.Next(8, 22);
                npc.CurrentEssenceSea = Math.Min(npc.AptitudePercent, npc.CurrentEssenceSea + 15f);

                // 2. Overworld Wandering: 65% chance to drift to adjacent tile
                if (_rng.NextDouble() < 0.65)
                {
                    int dx = _rng.Next(-1, 2);
                    int dy = _rng.Next(-1, 2);
                    int nx = Math.Clamp(npc.CurrentTilePos.X + dx, 2, 28);
                    int ny = Math.Clamp(npc.CurrentTilePos.Y + dy, 2, 28);
                    npc.CurrentTilePos = new Vector2Int(nx, ny);
                }
            }

            BroadcastCultivatorMapTokens();
            if (_stateContext?.WorldMap != null)
            {
                ResolveTileCultivators(new Vector2Int(_stateContext.WorldMap.PlayerGridPos.X, _stateContext.WorldMap.PlayerGridPos.Y));
            }
        }

        private void HandlePlayerMoved(Vector2Int newGridPos)
        {
            ResolveTileCultivators(newGridPos);
        }

        private void HandleCultivatorsWandered()
        {
            BroadcastCultivatorMapTokens();
            if (_stateContext?.WorldMap != null)
            {
                ResolveTileCultivators(new Vector2Int(_stateContext.WorldMap.PlayerGridPos.X, _stateContext.WorldMap.PlayerGridPos.Y));
            }
        }

        public void BroadcastCultivatorMapTokens()
        {
            var occupied = new List<Vector2Int>();
            foreach (var npc in _livingCultivators)
            {
                if (npc.IsAlive && !occupied.Contains(npc.CurrentTilePos))
                {
                    occupied.Add(npc.CurrentTilePos);
                }
            }
            GameEventManager.TriggerCultivatorMapTokensUpdated(occupied);
        }

        public void ResolveTileCultivators(Vector2Int tilePos)
        {
            var onTile = new List<CultivatorEntityData>();
            foreach (var npc in _livingCultivators)
            {
                if (npc.IsAlive && npc.CurrentTilePos.X == tilePos.X && npc.CurrentTilePos.Y == tilePos.Y)
                {
                    onTile.Add(npc);
                }
            }
            GameEventManager.TriggerTileCultivatorsUpdated(tilePos, onTile);
        }

        private void HandleInteractWithNPCRequested(string npcEntityId)
        {
            CultivatorEntityData? target = null;
            foreach (var c in _livingCultivators)
            {
                if (c.EntityId == npcEntityId || c.Name == npcEntityId)
                {
                    target = c;
                    break;
                }
            }

            if (target == null) return;

            _activeNPC = target;
            _isInteractionOpen = true;
            GameEventManager.TriggerUIModalActive(true);

            string greeting = GetGreetingByDisposition(target);
            GameEventManager.TriggerNPCDialogueOpened(target, greeting);
            Debug.Log($"[ProceduralNPCWorldManager] Opened Dialogue with {target.Name} ({target.Clan}, Rank {target.Rank}).");
        }

        private string GetGreetingByDisposition(CultivatorEntityData npc)
        {
            return npc.Disposition switch
            {
                TaiwuDisposition.Righteous => "\"Cultivator, uphold the clan tenets. Speak your intent clearly.\"",
                TaiwuDisposition.Benevolent => "\"Peace be with you, friend. May our meeting bring safe travels.\"",
                TaiwuDisposition.Pragmatic => "\"Time is primeval essence. If you have business, make it brief.\"",
                TaiwuDisposition.Rebellious => "\"Hmph, what are you looking at? Mind your own path, rogue.\"",
                TaiwuDisposition.Egoist => "\"Unless you bring primeval stones or rare Gu, don't waste my breath.\"",
                _ => "\"Greetings.\""
            };
        }

        private void HandleNPCSparRequested(string npcId)
        {
            if (_activeNPC == null) return;

            _activeNPC.Favorability = Math.Min(300, _activeNPC.Favorability + 25);
            _activeNPC.AddMemory("Sparred amicably with player (+25 Favor).");

            string msg = $"[Spar Commenced] You cross blades with {_activeNPC.Name}. Mutual martial respect formed (+25 Favor)!";
            GameEventManager.TriggerNPCDialogueFeedback(msg);

            Vector2Int pos = _activeNPC.CurrentTilePos;
            CloseInteraction();
            GameEventManager.RaiseRequestEnterCombat(ArenaEncounterType.ClanPatrolDuel, pos);
        }

        private void HandleNPCBarterRequested(string npcId)
        {
            if (_activeNPC == null || _stateContext?.Cultivator == null) return;

            var player = _stateContext.Cultivator;
            int cost = _activeNPC.Disposition == TaiwuDisposition.Benevolent ? 15 :
                       (_activeNPC.Disposition == TaiwuDisposition.Egoist ? 30 : 20);

            if (player.PrimevalStones >= cost)
            {
                player.PrimevalStones -= cost;
                _activeNPC.PrimevalStones += cost;
                _activeNPC.Favorability = Math.Min(300, _activeNPC.Favorability + 10);
                _activeNPC.AddMemory($"Traded rations with player for {cost} stones.");

                GameEventManager.TriggerPrimevalStonesChanged(player.PrimevalStones);

                // Clan reputation bonus
                var factionId = ResolveFactionFromClan(_activeNPC.Clan);
                if (factionId.HasValue && _stateContext.Faction != null)
                {
                    int newScore = _stateContext.Faction.ModifyReputation(factionId.Value, 5);
                    GameEventManager.TriggerFactionReputationChanged(factionId.Value, newScore, 5);
                }

                string msg = $"[Barter Success] Exchanged {cost} Stones for Moon Orchid Petals & Rations. Favorability +10, Clan Reputation +5!";
                GameEventManager.TriggerNPCDialogueFeedback(msg);
            }
            else
            {
                string msg = $"[Barter Failed] Insufficient Primeval Stones! {_activeNPC.Name} refuses to trade.";
                GameEventManager.TriggerNPCDialogueFeedback(msg);
            }
        }

        private void HandleNPCExtortRequested(string npcId)
        {
            if (_activeNPC == null || _stateContext?.Cultivator == null) return;

            var player = _stateContext.Cultivator;
            int deltaR = player.Rank - _activeNPC.Rank;
            var factionId = ResolveFactionFromClan(_activeNPC.Clan);

            if (_activeNPC.Disposition == TaiwuDisposition.Righteous)
            {
                _activeNPC.Favorability = Math.Max(-300, _activeNPC.Favorability - 80);
                _activeNPC.AddMemory("Outraged by extortion attempt! Declared player a villain.");

                if (factionId.HasValue && _stateContext.Faction != null)
                {
                    int newScore = _stateContext.Faction.ModifyReputation(factionId.Value, -30);
                    GameEventManager.TriggerFactionReputationChanged(factionId.Value, newScore, -30);
                }

                string msg = $"\"{_activeNPC.Name} draws a blade! 'Scoundrel, you dare extort a righteous cultivator?!' Favorability -80, Clan Reputation -30!\"";
                GameEventManager.TriggerNPCDialogueFeedback(msg);
            }
            else if (deltaR >= 1 || _activeNPC.Disposition == TaiwuDisposition.Benevolent)
            {
                int stolenStones = Math.Min(_activeNPC.PrimevalStones, 35);
                _activeNPC.PrimevalStones -= stolenStones;
                player.PrimevalStones += stolenStones;
                _activeNPC.Favorability = Math.Max(-300, _activeNPC.Favorability - 40);
                _activeNPC.AddMemory($"Extorted {stolenStones} stones by player (-40 Favor).");

                GameEventManager.TriggerPrimevalStonesChanged(player.PrimevalStones);

                if (factionId.HasValue && _stateContext.Faction != null)
                {
                    int newScore = _stateContext.Faction.ModifyReputation(factionId.Value, -15);
                    GameEventManager.TriggerFactionReputationChanged(factionId.Value, newScore, -15);
                }

                string msg = $"[Extortion Success] Intimidated {_activeNPC.Name}! Handed over {stolenStones} Primeval Stones under duress. Favorability -40, Clan Reputation -15.";
                GameEventManager.TriggerNPCDialogueFeedback(msg);
            }
            else
            {
                _activeNPC.Favorability = Math.Max(-300, _activeNPC.Favorability - 25);
                string msg = $"\"{_activeNPC.Name} scoffs: 'A mere Rank {player.Rank} dares threaten me?!' Extortion rebuffed!\"";
                GameEventManager.TriggerNPCDialogueFeedback(msg);
            }
        }

        private void HandleNPCAmbushRequested(string npcId)
        {
            if (_activeNPC == null) return;

            Vector2Int pos = _activeNPC.CurrentTilePos;
            CloseInteraction();
            GameEventManager.RaiseRequestEnterCombat(ArenaEncounterType.DemonicCultivator, pos);
        }

        private void HandleCloseNPCDialogueRequested()
        {
            CloseInteraction();
        }

        public void CloseInteraction()
        {
            if (!_isInteractionOpen) return;
            _isInteractionOpen = false;
            _activeNPC = null;
            GameEventManager.TriggerUIModalActive(false);
            GameEventManager.TriggerNPCDialogueClosed();
        }

        private FactionID? ResolveFactionFromClan(string clan)
        {
            if (string.IsNullOrEmpty(clan)) return null;
            if (clan.Contains("Gu Yue") || clan.Contains("古月")) return FactionID.GuYueClan;
            if (clan.Contains("Bai") || clan.Contains("白家")) return FactionID.BaiClan;
            if (clan.Contains("Xiong") || clan.Contains("熊家")) return FactionID.XiongClan;
            if (clan.Contains("Jia") || clan.Contains("贾家")) return FactionID.JiaConsortium;
            if (clan.Contains("Tie") || clan.Contains("铁家")) return FactionID.TieClan;
            if (clan.Contains("Demonic") || clan.Contains("散修") || clan.Contains("魔修")) return FactionID.DemonicRogues;
            return null;
        }
    }
}
