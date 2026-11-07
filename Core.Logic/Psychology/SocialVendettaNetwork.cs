using System;
using System.Collections.Generic;
using TaiwuGu.Core;

namespace TaiwuGu.Psychology
{
    /// <summary>
    /// Phase 2 / Milestone 2.3: Social Vendetta Network & Kinship Retribution Engine.
    /// Pure C# Domain Service managing:
    /// - SoulTabletBound discovery logic (silent demise vs immediate clan ancestral alert).
    /// - Crime scene evidence & framing resolution.
    /// - Kinship graph traversals (Parent, Disciple, Sibling, Sworn Brother) with grudge multipliers.
    /// - Institutional retribution hierarchy (No suicide attacks; evaluates rank before dispatching ambushes or bounties).
    /// </summary>
    public class SocialVendettaNetwork
    {
        private readonly Dictionary<string, NPCProfile> _npcRegistry = new Dictionary<string, NPCProfile>();
        private readonly Dictionary<string, List<KinshipEdge>> _kinshipGraphs = new Dictionary<string, List<KinshipEdge>>();
        private readonly List<ClanBountyNotice> _activeBounties = new List<ClanBountyNotice>();

        public IReadOnlyDictionary<string, NPCProfile> NPCRegistry => _npcRegistry;
        public IReadOnlyList<ClanBountyNotice> ActiveBounties => _activeBounties;

        public event Action<NPCProfile, VengeancePursuitType>? OnVengeanceTriggered;
        public event Action<ClanBountyNotice>? OnBountyIssued;

        public SocialVendettaNetwork()
        {
            SeedInitialNPCNetwork();
        }

        public void ExportToState(WorldSocialVendettaState state)
        {
            if (state == null) return;
            state.NPCs.Clear();
            foreach (var npc in _npcRegistry.Values)
            {
                state.NPCs.Add(npc);
            }

            state.KinshipEdges.Clear();
            foreach (var kvp in _kinshipGraphs)
            {
                foreach (var edge in kvp.Value)
                {
                    state.KinshipEdges.Add(edge);
                }
            }

            state.ActiveBounties.Clear();
            state.ActiveBounties.AddRange(_activeBounties);
        }

        public void ImportFromState(WorldSocialVendettaState state)
        {
            if (state == null) return;

            _npcRegistry.Clear();
            _kinshipGraphs.Clear();
            _activeBounties.Clear();

            if (state.NPCs != null)
            {
                foreach (var npc in state.NPCs)
                {
                    if (npc != null && !string.IsNullOrEmpty(npc.EntityId))
                    {
                        _npcRegistry[npc.EntityId] = npc;
                    }
                }
            }

            if (state.KinshipEdges != null)
            {
                foreach (var edge in state.KinshipEdges)
                {
                    if (!string.IsNullOrEmpty(edge.SourceEntityId))
                    {
                        if (!_kinshipGraphs.ContainsKey(edge.SourceEntityId))
                        {
                            _kinshipGraphs[edge.SourceEntityId] = new List<KinshipEdge>();
                        }
                        _kinshipGraphs[edge.SourceEntityId].Add(edge);
                    }
                }
            }

            if (state.ActiveBounties != null)
            {
                _activeBounties.AddRange(state.ActiveBounties);
            }

            if (_npcRegistry.Count == 0)
            {
                SeedInitialNPCNetwork();
            }
        }

        private void SeedInitialNPCNetwork()
        {
            RegisterNPC(new NPCProfile
            {
                EntityId = "npc_enforcer_01",
                Name = "Gu Yue Clan Enforcer (古月刑堂修士)",
                Clan = "Gu Yue Clan (古月氏)",
                Rank = 1,
                SoulTabletBound = true,
                CurrentGridPos = new Vector2Int(17, 22)
            });

            RegisterNPC(new NPCProfile
            {
                EntityId = "npc_elder_yan",
                Name = "Elder Gu Yue Yan (古月阎长老)",
                Clan = "Gu Yue Clan (古月氏)",
                Rank = 2,
                PersonalityTrait = "Huduan",
                CurrentGridPos = new Vector2Int(15, 15)
            });

            RegisterNPC(new NPCProfile
            {
                EntityId = "npc_rogue_01",
                Name = "Desperate Demonic Rogue (亡命魔修)",
                Clan = "Demonic Rogues (散修魔修)",
                Rank = 1,
                SoulTabletBound = false,
                CurrentGridPos = new Vector2Int(20, 18)
            });

            RegisterNPC(new NPCProfile
            {
                EntityId = "npc_rogue_brother",
                Name = "Scarface Rogue (刀疤魔修)",
                Clan = "Demonic Rogues (散修魔修)",
                Rank = 1,
                CurrentGridPos = new Vector2Int(22, 19)
            });

            RegisterNPC(new NPCProfile
            {
                EntityId = "npc_tie_detective",
                Name = "Tie Ruo Nan (铁若男)",
                Clan = "Tie Clan (铁家)",
                Rank = 2,
                SoulTabletBound = true,
                CurrentGridPos = new Vector2Int(14, 14)
            });

            RegisterNPC(new NPCProfile
            {
                EntityId = "npc_jia_steward",
                Name = "Jia Fu (贾富)",
                Clan = "Jia Consortium (贾家商会)",
                Rank = 2,
                SoulTabletBound = false,
                CurrentGridPos = new Vector2Int(14, 14)
            });

            RegisterNPC(new NPCProfile
            {
                EntityId = "npc_rogue_demon",
                Name = "Scarred Vulture (苍疤鹫)",
                Clan = "Demonic Rogues (散修魔修)",
                Rank = 2,
                SoulTabletBound = false,
                CurrentGridPos = new Vector2Int(14, 14)
            });

            RegisterNPC(new NPCProfile
            {
                EntityId = "npc_mo_elder",
                Name = "Gu Yue Mo Chen (古月漠尘)",
                Clan = "Gu Yue Clan (古月氏)",
                Rank = 3,
                SoulTabletBound = true,
                PersonalityTrait = "Huduan",
                CurrentGridPos = new Vector2Int(14, 15)
            });

            RegisterNPC(new NPCProfile
            {
                EntityId = "npc_chi_disciple",
                Name = "Gu Yue Chi Cheng (古月赤成)",
                Clan = "Gu Yue Clan (古月氏)",
                Rank = 1,
                SoulTabletBound = true,
                CurrentGridPos = new Vector2Int(15, 15)
            });

            RegisterNPC(new NPCProfile
            {
                EntityId = "npc_bai_elder",
                Name = "Bai Clan Patriarch (白家寨主)",
                Clan = "Bai Clan (白家寨)",
                Rank = 3,
                SoulTabletBound = true,
                PersonalityTrait = "Huduan",
                CurrentGridPos = new Vector2Int(14, 16)
            });

            RegisterNPC(new NPCProfile
            {
                EntityId = "npc_bai_scout",
                Name = "Bai Ning Bing (白凝冰)",
                Clan = "Bai Clan (白家寨)",
                Rank = 2,
                SoulTabletBound = true,
                CurrentGridPos = new Vector2Int(14, 16)
            });

            RegisterNPC(new NPCProfile
            {
                EntityId = "npc_xiong_warrior",
                Name = "Xiong Li (熊力)",
                Clan = "Xiong Clan (熊家寨)",
                Rank = 2,
                SoulTabletBound = true,
                CurrentGridPos = new Vector2Int(16, 16)
            });

            RegisterNPC(new NPCProfile
            {
                EntityId = "npc_xiong_hunter",
                Name = "Xiong Po (熊珀)",
                Clan = "Xiong Clan (熊家寨)",
                Rank = 2,
                SoulTabletBound = false,
                CurrentGridPos = new Vector2Int(15, 16)
            });

            // Kinship Edges with Retribution Multipliers
            AddKinshipEdge("npc_enforcer_01", "npc_elder_yan", KinshipRelationshipType.Parent, 1.8f, 1.5f);
            AddKinshipEdge("npc_rogue_01", "npc_rogue_brother", KinshipRelationshipType.SwornSibling, 1.2f, 1.0f);
            AddKinshipEdge("npc_chi_disciple", "npc_mo_elder", KinshipRelationshipType.Parent, 1.9f, 1.5f);
            AddKinshipEdge("npc_bai_scout", "npc_bai_elder", KinshipRelationshipType.Parent, 1.7f, 1.5f);
            AddKinshipEdge("npc_xiong_hunter", "npc_xiong_warrior", KinshipRelationshipType.SwornSibling, 1.6f, 1.2f);
            AddKinshipEdge("npc_xiong_warrior", "npc_xiong_hunter", KinshipRelationshipType.SwornSibling, 1.6f, 1.2f);
        }

        public void RegisterProceduralWorldNPCs(IEnumerable<CultivatorEntityData> cultivators)
        {
            if (cultivators == null) return;
            foreach (var c in cultivators)
            {
                if (!_npcRegistry.ContainsKey(c.EntityId))
                {
                    RegisterNPC(new NPCProfile
                    {
                        EntityId = c.EntityId,
                        Name = c.Name,
                        Clan = c.Clan,
                        Rank = c.Rank,
                        SoulTabletBound = c.SoulTabletBound,
                        CurrentGridPos = c.CurrentTilePos
                    });
                }
            }
        }

        public void RegisterNPC(NPCProfile npc)
        {
            _npcRegistry[npc.EntityId] = npc;
        }

        public void AddKinshipEdge(string sourceId, string targetId, KinshipRelationshipType rel, float lambda, float retributionMult)
        {
            if (!_kinshipGraphs.ContainsKey(sourceId))
            {
                _kinshipGraphs[sourceId] = new List<KinshipEdge>();
            }
            _kinshipGraphs[sourceId].Add(new KinshipEdge
            {
                SourceEntityId = sourceId,
                TargetEntityId = targetId,
                Relationship = rel,
                EdgeWeightLambda = lambda,
                FacadeRetributionMultiplier = retributionMult
            });
        }

        public void ProcessVictimDeath(string victimName, Vector2Int deathLocation, CultivatorData player, CrimeSceneData? crimeScene)
        {
            NPCProfile? victim = null;
            foreach (var kvp in _npcRegistry.Values)
            {
                if (kvp.Name == victimName || kvp.EntityId == victimName)
                {
                    victim = kvp;
                    break;
                }
            }

            if (victim == null) return;

            if (victim.SoulTabletBound)
            {
                Debug.LogWarning($"[SOUL TABLET SHATTERED] {victim.Name}'s Soul Tablet cracked in the Clan Ancestral Hall! Investigators dispatched to ({deathLocation.x}, {deathLocation.y})!");
                TriggerClanInvestigation(victim, deathLocation, player, crimeScene);
            }
            else
            {
                Debug.Log($"[Silent Demise] {victim.Name} had no Soul Tablet bound. Demise remains unannounced until overworld patrols discover the scene.");
            }
        }

        private void TriggerClanInvestigation(NPCProfile victim, Vector2Int deathLocation, CultivatorData player, CrimeSceneData? scene)
        {
            if (_kinshipGraphs.TryGetValue(victim.EntityId, out var edges))
            {
                foreach (var edge in edges)
                {
                    if (_npcRegistry.TryGetValue(edge.TargetEntityId, out var avenger))
                    {
                        float hateScore = 100f * edge.EdgeWeightLambda * edge.FacadeRetributionMultiplier;
                        avenger.Favorability = (int)Math.Clamp(avenger.Favorability - hateScore, -300f, 300f);

                        // Rank evaluation: equal or higher -> direct ambush; lower -> bounty warrant
                        if (avenger.Rank >= player.Rank)
                        {
                            avenger.PursuitState = VengeancePursuitType.SoloAmbush;
                            avenger.TargetSlayerId = player.Name;
                            avenger.InvestigationTargetPos = deathLocation;
                            Debug.Log($"[Solo Ambush] {avenger.Name} (Rank {avenger.Rank}) departs directly to avenge {victim.Name}!");
                            OnVengeanceTriggered?.Invoke(avenger, VengeancePursuitType.SoloAmbush);
                        }
                        else
                        {
                            avenger.PursuitState = VengeancePursuitType.ClanBountyWarrant;
                            var bounty = new ClanBountyNotice
                            {
                                TargetId = player.Name,
                                IssuerClan = avenger.Clan,
                                RewardStones = 150 * player.Rank,
                                ThreatRank = player.Rank,
                                Reason = $"Blood debt for slaying {victim.Name}"
                            };
                            _activeBounties.Add(bounty);
                            Debug.Log($"[Bounty Issued] {avenger.Name} issued clan bounty of {bounty.RewardStones} stones for {player.Name}!");
                            OnBountyIssued?.Invoke(bounty);
                        }
                    }
                }
            }
        }
    }
}
