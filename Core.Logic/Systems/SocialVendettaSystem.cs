using System;
using System.Collections.Generic;
using TaiwuGu.Combat;
using TaiwuGu.Core;
using TaiwuGu.Entities;
using TaiwuGu.Map;
using TaiwuGu.Psychology;

namespace TaiwuGu.Systems
{
    /// <summary>
    /// Stateless ECS System governing NPC wandering, disposition dialogue, social actions (Spar, Barter, Extort, Ambush), and Kinship Soul Tablet retribution.
    /// </summary>
    public static class SocialVendettaSystem
    {
        public static string GetGreeting(CultivatorComponent npc) => npc.Disposition switch
        {
            TaiwuDisposition.Righteous  => "\"Cultivator, uphold the clan tenets. Speak your intent clearly.\"",
            TaiwuDisposition.Benevolent => "\"Peace be with you, friend. May our meeting bring safe travels.\"",
            TaiwuDisposition.Pragmatic  => "\"Time is primeval essence. If you have business, make it brief.\"",
            TaiwuDisposition.Rebellious => "\"Hmph, what are you looking at? Mind your own path, rogue.\"",
            TaiwuDisposition.Egoist     => "\"Unless you bring primeval stones or rare Gu, don't waste my breath.\"",
            _                           => "\"Greetings.\""
        };

        public static List<(EntityId Id, CultivatorComponent Cult, ApertureComponent? Ap)> GetAliveNPCsAt(WorldStore world, GridCoord pos, string? sectorKey = null)
        {
            var list = new List<(EntityId, CultivatorComponent, ApertureComponent?)>();
            string sector = sectorKey ?? world.CurrentSectorKey;

            foreach (var eid in world.GetEntitiesAt(pos, sector))
            {
                if (world.Has<PlayerTagComponent>(eid)) continue;
                if (world.TryGet<CultivatorComponent>(eid, out var cult) && cult.IsAlive)
                {
                    list.Add((eid, cult, world.Get<ApertureComponent>(eid)));
                }
            }
            return list;
        }

        public static List<GridCoord> GetOccupiedNPCTiles(WorldStore world, string? sectorKey = null)
        {
            string sector = sectorKey ?? world.CurrentSectorKey;
            var occupied = new List<GridCoord>();
            var seen = new HashSet<GridCoord>();

            foreach (var (eid, cult) in world.GetAll<CultivatorComponent>())
            {
                if (!cult.IsAlive || world.Has<PlayerTagComponent>(eid)) continue;
                if (world.TryGet<PositionComponent>(eid, out var pos))
                {
                    if (string.IsNullOrEmpty(pos.SectorKey) || string.Equals(pos.SectorKey, sector, StringComparison.Ordinal))
                    {
                        if (seen.Add(pos.Coord)) occupied.Add(pos.Coord);
                    }
                }
            }
            return occupied;
        }

        public static void ExecuteSpar(WorldStore world, EntityId npcId)
        {
            if (!world.TryGet<CultivatorComponent>(npcId, out var npc)) return;

            GridCoord pos = world.Get<PositionComponent>(npcId)?.Coord ?? world.GetPlayerPosition();
            WorldSignals.EmitDialogueFeedback($"[Spar Commenced] You cross stances with {npc.Name}. A fair duel begins!");
            CombatSystem.StartCombat(
                world,
                ArenaEncounterType.ClanPatrolDuel,
                pos,
                targetNpcId: npcId,
                isLethal: false,
                enemyName: npc.Name);
        }

        public static void ExecuteBarter(WorldStore world, EntityId npcId)
        {
            if (!world.TryGet<CultivatorComponent>(npcId, out var npc) ||
                !world.TryGet<CultivatorComponent>(world.PlayerEntity, out var player))
                return;

            int cost = npc.Disposition == TaiwuDisposition.Benevolent ? 15 :
                       (npc.Disposition == TaiwuDisposition.Egoist ? 30 : 20);

            if (player.PrimevalStones < cost)
            {
                WorldSignals.EmitDialogueFeedback($"[Barter Failed] Insufficient Primeval Stones! {npc.Name} refuses to trade.");
                return;
            }

            player.PrimevalStones -= cost;
            npc.PrimevalStones += cost;
            npc.Favorability = Math.Min(300, npc.Favorability + 10);
            npc.AddMemory($"Traded rations with player for {cost} stones.");

            if (world.TryGet<InventoryComponent>(world.PlayerEntity, out var inv))
            {
                inv.AddFood(GuDietType.MoonOrchidPetals, 2);
            }

            FactionID? factionId = FactionComponent.ResolveFactionFromClan(npc.Clan);
            if (factionId.HasValue)
            {
                world.GetFaction(factionId.Value)?.ModifyReputation(5);
            }

            WorldSignals.EmitDialogueFeedback($"[Barter Success] Exchanged {cost} Stones for Moon Orchid Petals & Rations. Favorability +10, Clan Reputation +5!");
            WorldSignals.EmitTopics(DirtyTopic.Cultivator | DirtyTopic.Inventory | DirtyTopic.Factions | DirtyTopic.NPCs);
        }

        public static void ExecuteExtort(WorldStore world, EntityId npcId)
        {
            if (!world.TryGet<CultivatorComponent>(npcId, out var npc) ||
                !world.TryGet<CultivatorComponent>(world.PlayerEntity, out var player))
                return;

            int playerRank = world.Get<ApertureComponent>(world.PlayerEntity)?.Rank ?? 1;
            int npcRank = world.Get<ApertureComponent>(npcId)?.Rank ?? 1;
            int deltaR = playerRank - npcRank;

            FactionID? factionId = FactionComponent.ResolveFactionFromClan(npc.Clan);

            if (npc.Disposition == TaiwuDisposition.Righteous)
            {
                npc.Favorability = Math.Max(-300, npc.Favorability - 80);
                npc.AddMemory("Outraged by extortion attempt! Declared player a villain.");
                if (factionId.HasValue) world.GetFaction(factionId.Value)?.ModifyReputation(-30);

                WorldSignals.EmitDialogueFeedback($"\"{npc.Name} draws a blade! 'Scoundrel, you dare extort a righteous cultivator?!' Favorability -80, Clan Reputation -30!\"");
            }
            else if (deltaR >= 1 || npc.Disposition == TaiwuDisposition.Benevolent)
            {
                int stolen = Math.Min(npc.PrimevalStones, 35);
                npc.PrimevalStones -= stolen;
                player.PrimevalStones += stolen;
                npc.Favorability = Math.Max(-300, npc.Favorability - 40);
                npc.AddMemory($"Extorted {stolen} stones by player (-40 Favor).");
                if (factionId.HasValue) world.GetFaction(factionId.Value)?.ModifyReputation(-15);

                WorldSignals.EmitDialogueFeedback($"[Extortion Success] Intimidated {npc.Name}! Handed over {stolen} Primeval Stones under duress. Favorability -40, Clan Reputation -15.");
            }
            else
            {
                npc.Favorability = Math.Max(-300, npc.Favorability - 25);
                WorldSignals.EmitDialogueFeedback($"\"{npc.Name} scoffs: 'A mere Rank {playerRank} dares threaten me?!' Extortion rebuffed!\"");
            }

            WorldSignals.EmitTopics(DirtyTopic.Cultivator | DirtyTopic.Factions | DirtyTopic.NPCs);
        }

        public static void ExecuteAmbush(WorldStore world, EntityId npcId)
        {
            if (!world.TryGet<CultivatorComponent>(npcId, out var npc)) return;

            GridCoord pos = world.Get<PositionComponent>(npcId)?.Coord ?? world.GetPlayerPosition();
            CombatSystem.StartCombat(
                world,
                ArenaEncounterType.DemonicCultivator,
                pos,
                targetNpcId: npcId,
                isLethal: true,
                enemyName: npc.Name);
        }

        public static void ProcessVictimExecution(WorldStore world, string victimNameOrExternalId, GridCoord deathPos, CrimeSignature sig = CrimeSignature.CleanIncisions, bool didDetonate = false)
        {
            CrimeSignature finalSig = didDetonate ? CrimeSignature.ScorchedCrater : sig;
            ForensicsSystem.SpawnCrimeScene(world, deathPos, victimNameOrExternalId, finalSig, terms: 2);

            EntityId victimId = world.FindByExternalId(victimNameOrExternalId);
            if (!victimId.IsValid)
            {
                foreach (var (eid, cult) in world.GetAll<CultivatorComponent>())
                {
                    if (!world.Has<PlayerTagComponent>(eid) && cult.Name == victimNameOrExternalId)
                    {
                        victimId = eid;
                        break;
                    }
                }
            }

            if (!victimId.IsValid || !world.TryGet<CultivatorComponent>(victimId, out var victim)) return;
            if (!victim.SoulTabletBound)
            {
                Debug.Log($"[SocialVendettaSystem] Silent Demise: {victim.Name} had no Soul Tablet bound.");
                return;
            }

            var kinship = world.GetSingleton<KinshipLedgerComponent>();
            var player = world.Get<CultivatorComponent>(world.PlayerEntity);
            int playerRank = world.Get<ApertureComponent>(world.PlayerEntity)?.Rank ?? 1;
            if (kinship == null || player == null) return;

            Debug.LogWarning($"[SOUL TABLET SHATTERED] {victim.Name}'s Soul Tablet cracked! Dispatching clan retribution to {deathPos}.");

            foreach (var edge in kinship.KinshipEdges)
            {
                if (!string.Equals(edge.SourceEntityId, victim.ExternalId, StringComparison.Ordinal)) continue;

                EntityId avengerId = world.FindByExternalId(edge.TargetEntityId);
                if (!avengerId.IsValid || !world.TryGet<CultivatorComponent>(avengerId, out var avenger)) continue;

                float hateScore = 100f * edge.EdgeWeightLambda * edge.FacadeRetributionMultiplier;
                avenger.Favorability = (int)Math.Clamp(avenger.Favorability - hateScore, -300f, 300f);

                int avengerRank = world.Get<ApertureComponent>(avengerId)?.Rank ?? 1;
                if (avengerRank >= playerRank)
                {
                    avenger.PursuitState = VengeancePursuitType.SoloAmbush;
                    avenger.TargetSlayerId = player.Name;
                    avenger.InvestigationTargetPos = deathPos;
                }
                else
                {
                    avenger.PursuitState = VengeancePursuitType.ClanBountyWarrant;
                    kinship.ActiveBounties.Add(new ClanBountyNotice
                    {
                        TargetId = player.Name,
                        IssuerClan = avenger.Clan,
                        RewardStones = 150 * playerRank,
                        ThreatRank = playerRank,
                        Reason = $"Blood debt for slaying {victim.Name}"
                    });
                }
            }

            WorldSignals.EmitTopics(DirtyTopic.NPCs | DirtyTopic.Forensics);
        }

        public static void TickMonthlyNPCs(WorldStore world, IRandomSource? rng = null)
        {
            var clock = world.GetSingleton<WorldClockComponent>();
            int seed = unchecked(
                world.RunSeed ^
                ((clock?.CurrentYear ?? 1) * 10007) ^
                ((clock?.CurrentSolarTermIndex ?? 1) * 397));
            IRandomSource random = rng ?? new SeededRandom(seed);

            var moves = new List<(EntityId Id, GridCoord Next, string SectorKey)>();
            foreach (var (eid, cult) in world.GetAll<CultivatorComponent>())
            {
                if (world.Has<PlayerTagComponent>(eid) || !cult.IsAlive) continue;

                cult.PrimevalStones += random.Next(8, 22);

                if (random.NextDouble() < 0.65 && world.TryGet<PositionComponent>(eid, out var pos))
                {
                    string npcSecKey = string.IsNullOrEmpty(pos.SectorKey) ? world.CurrentSectorKey : pos.SectorKey;
                    var npcSector = world.GetSector(npcSecKey);

                    int secWidth = 30;
                    int secHeight = 30;
                    if (npcSector != null)
                    {
                        secWidth = npcSector.GridWidth;
                        secHeight = npcSector.GridHeight;
                    }
                    else if (WorldAtlasRegistry.TryGet(npcSecKey, out var bp) && bp != null)
                    {
                        secWidth = bp.Width;
                        secHeight = bp.Height;
                    }

                    int dx = random.Next(-1, 2);
                    int dy = random.Next(-1, 2);
                    GridCoord next = new GridCoord(
                        Math.Clamp(pos.Coord.X + dx, 2, Math.Max(2, secWidth - 2)),
                        Math.Clamp(pos.Coord.Y + dy, 2, Math.Max(2, secHeight - 2)));

                    if (npcSector == null || npcSector.IsWalkable(next))
                    {
                        moves.Add((eid, next, npcSecKey));
                    }
                }
            }

            foreach (var (eid, next, secKey) in moves)
            {
                world.SetPosition(eid, next, secKey);
            }

            WorldSignals.EmitTopics(DirtyTopic.NPCs);
        }
    }
}
