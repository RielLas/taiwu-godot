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
    /// Stateless ECS system governing World Calamities (Beast Tides, Ruin manifestations, Skirmishes)
    /// and Active Bounty Hunters hunting the player across the regional map.
    /// </summary>
    public static class WorldDirectorSystem
    {
        public const int DEFAULT_CALAMITY_INTERVAL_TERMS = 6;
        public const int DEFAULT_FORESHADOW_DURATION_TERMS = 2;
        public const int DEFAULT_ACTIVE_CALAMITY_DURATION_TERMS = 2;

        public static void TickSolarTerm(WorldStore world, IRandomSource? rngOverride = null)
        {
            var clock = world.GetSingleton<WorldClockComponent>();
            int termIndex = clock?.CurrentSolarTermIndex ?? 1;
            int year = clock?.CurrentYear ?? 1;

            IRandomSource rng = rngOverride ?? new SeededRandom(
                world.RunSeed ^ (year * 10007) ^ (termIndex * 397));

            AdvanceCalamities(world, rng);
            AdvanceBountyHunters(world, rng);

            WorldSignals.EmitTopics(DirtyTopic.Director | DirtyTopic.Bounty);
        }

        private static void AdvanceCalamities(WorldStore world, IRandomSource rng)
        {
            var eventComp = world.GetOrCreateSingleton<WorldEventComponent>();

            // Advance existing active calamities BEFORE spawning any new calamity on this tick
            // so a newly spawned calamity does not lose a term on its spawn tick.
            var toRemove = new List<WorldEventEntry>();
            foreach (var ev in eventComp.ActiveEvents)
            {
                ev.RemainingTerms--;
                if (ev.RemainingTerms <= 0)
                {
                    if (ev.Phase == WorldCalamityPhase.Foreshadowed)
                    {
                        // Transition Foreshadowed -> Active
                        ev.Phase = WorldCalamityPhase.Active;
                        ev.RemainingTerms = DEFAULT_ACTIVE_CALAMITY_DURATION_TERMS;

                        // Temporary POI AncientGuCave placement on sector tile
                        ApplyCalamityPOI(world, eventComp, ev);

                        WorldSignals.EmitWorldCalamityUpdated(ev);
                        WorldSignals.EmitTopics(DirtyTopic.Director | DirtyTopic.Map);
                    }
                    else if (ev.Phase == WorldCalamityPhase.Active)
                    {
                        // Transition Active -> Expired
                        ev.Phase = WorldCalamityPhase.Expired;
                        ev.RemainingTerms = 0;

                        // Clean restoration of previous POI on Active -> Expired
                        RestorePreviousPOI(world, eventComp, ev);

                        WorldSignals.EmitWorldCalamityUpdated(ev);
                        WorldSignals.EmitTopics(DirtyTopic.Director | DirtyTopic.Map);
                        toRemove.Add(ev);
                    }
                    else if (ev.Phase == WorldCalamityPhase.Resolved)
                    {
                        RestorePreviousPOI(world, eventComp, ev);
                        toRemove.Add(ev);
                    }
                }
            }

            foreach (var deadEvent in toRemove)
            {
                eventComp.ActiveEvents.Remove(deadEvent);
            }

            // Countdown to next scheduled world calamity
            eventComp.NextEventCountdownTerms--;
            if (eventComp.NextEventCountdownTerms <= 0)
            {
                eventComp.NextEventCountdownTerms = DEFAULT_CALAMITY_INTERVAL_TERMS;
                SpawnScheduledCalamity(world, eventComp, rng);
            }
        }

        private static void SpawnScheduledCalamity(WorldStore world, WorldEventComponent eventComp, IRandomSource rng)
        {
            string sectorKey = world.CurrentSectorKey;
            var sector = world.GetCurrentSector();
            int width = sector?.GridWidth ?? 30;
            int height = sector?.GridHeight ?? 30;

            WorldCalamityKind[] kinds = (WorldCalamityKind[])Enum.GetValues(typeof(WorldCalamityKind));
            WorldCalamityKind kind = kinds[rng.Next(0, kinds.Length)];

            GridCoord targetCoord = new GridCoord(
                rng.Next(2, Math.Max(3, width - 2)),
                rng.Next(2, Math.Max(3, height - 2)));

            for (int attempt = 0; attempt < 8 && IsTileOccupiedByCalamity(eventComp, sectorKey, targetCoord, exclude: null); attempt++)
            {
                targetCoord = new GridCoord(
                    rng.Next(2, Math.Max(3, width - 2)),
                    rng.Next(2, Math.Max(3, height - 2)));
            }

            string title = kind switch
            {
                WorldCalamityKind.BeastTide => "Beast Tide (兽潮涌动)",
                WorldCalamityKind.InheritanceRuins => "Inheritance Ruins (古修传承洞府)",
                WorldCalamityKind.GrandCaravanAuction => "Grand Caravan Auction (商队大拍)",
                WorldCalamityKind.InterClanSkirmish => "Inter-Clan Skirmish (部族交锋)",
                _ => "World Calamity"
            };

            var clock = world.GetSingleton<WorldClockComponent>();
            int seq = eventComp.NextEventSequence++;
            string eventId = $"calamity_{clock?.CurrentYear ?? 1}_{clock?.CurrentSolarTermIndex ?? 1}_{seq}";
            string desc = $"Heavenly omens reveal a {title} taking shape near {targetCoord}!";
            var entry = new WorldEventEntry(
                kind,
                WorldCalamityPhase.Foreshadowed,
                sectorKey,
                targetCoord,
                remainingTerms: DEFAULT_FORESHADOW_DURATION_TERMS,
                title,
                desc,
                eventId);

            eventComp.ActiveEvents.Add(entry);
            WorldSignals.EmitWorldCalamityUpdated(entry);
            WorldSignals.EmitTopics(DirtyTopic.Director);
        }

        private static bool IsTileOccupiedByCalamity(WorldEventComponent eventComp, string sectorKey, GridCoord coord, WorldEventEntry? exclude, bool activeOnly = false)
        {
            foreach (var other in eventComp.ActiveEvents)
            {
                if (ReferenceEquals(other, exclude)) continue;
                if (activeOnly && other.Phase != WorldCalamityPhase.Active) continue;
                if (string.Equals(other.SectorKey, sectorKey, StringComparison.Ordinal) && other.TargetCoord == coord)
                {
                    return true;
                }
            }
            return false;
        }

        private static void ApplyCalamityPOI(WorldStore world, WorldEventComponent eventComp, WorldEventEntry ev)
        {
            if (IsTileOccupiedByCalamity(eventComp, ev.SectorKey, ev.TargetCoord, exclude: ev, activeOnly: true))
            {
                return;
            }

            var sector = world.GetSector(ev.SectorKey) ?? world.GetCurrentSector();
            if (sector == null || !sector.IsWithinBounds(ev.TargetCoord)) return;

            var tile = sector.GetTile(ev.TargetCoord.X, ev.TargetCoord.Y);
            ev.PreviousPOI = tile.POI;
            ev.PreviousCustomLandmarkId = tile.CustomLandmarkId;

            tile.POI = TilePOIType.AncientGuCave;
            sector.SetTile(ev.TargetCoord.X, ev.TargetCoord.Y, tile);
            sector.CommitTilesToSnapshot();
        }

        private static void RestorePreviousPOI(WorldStore world, WorldEventComponent eventComp, WorldEventEntry ev)
        {
            if (IsTileOccupiedByCalamity(eventComp, ev.SectorKey, ev.TargetCoord, exclude: ev, activeOnly: true))
            {
                return;
            }

            var sector = world.GetSector(ev.SectorKey) ?? world.GetCurrentSector();
            if (sector == null || !sector.IsWithinBounds(ev.TargetCoord)) return;

            var tile = sector.GetTile(ev.TargetCoord.X, ev.TargetCoord.Y);
            tile.POI = ev.PreviousPOI;
            tile.CustomLandmarkId = ev.PreviousCustomLandmarkId;

            sector.SetTile(ev.TargetCoord.X, ev.TargetCoord.Y, tile);
            sector.CommitTilesToSnapshot();
        }

        private static void AdvanceBountyHunters(WorldStore world, IRandomSource rng)
        {
            var tracker = world.GetOrCreateSingleton<BountyTrackerComponent>();

            // Update state of dead or defeated hunters
            foreach (var h in tracker.Hunters)
            {
                if (h.State == BountyHunterState.Pursuing || h.State == BountyHunterState.Engaged)
                {
                    EntityId hEid = new EntityId(h.HunterEntityId);
                    if (!world.IsAlive(hEid) ||
                        (world.TryGet<CultivatorComponent>(hEid, out var cult) && !cult.IsAlive))
                    {
                        h.State = BountyHunterState.Defeated;
                    }
                }
            }

            // Check if a new hunter should spawn
            FactionID? hostileFaction = null;
            foreach (var (_, faction) in world.GetAll<FactionComponent>())
            {
                if (faction.GetStandingTier() <= FactionStandingTier.Hostile)
                {
                    hostileFaction = faction.ID;
                    break;
                }
            }

            bool bloodBountyExists = false;
            var kinship = world.GetSingleton<KinshipLedgerComponent>();
            if (kinship != null && kinship.ActiveBounties.Count > 0)
            {
                bloodBountyExists = true;
                if (!hostileFaction.HasValue)
                {
                    string issuer = kinship.ActiveBounties[0].IssuerClan;
                    hostileFaction = FactionComponent.ResolveFactionFromClan(issuer) ?? FactionID.TieClan;
                }
            }

            bool hasActiveHunter = tracker.Hunters.Exists(h =>
                h.State == BountyHunterState.Pursuing ||
                h.State == BountyHunterState.Engaged ||
                h.State == BountyHunterState.Bribed);
            if ((hostileFaction.HasValue || bloodBountyExists) && !hasActiveHunter)
            {
                SpawnBountyHunter(world, tracker, hostileFaction ?? FactionID.TieClan, rng);
            }

            // Path pursuing hunters 1-2 tiles toward player
            var playerPos = world.Get<PositionComponent>(world.PlayerEntity);
            if (playerPos == null) return;

            foreach (var hunter in tracker.Hunters)
            {
                if (hunter.State != BountyHunterState.Pursuing) continue;

                if (hunter.CurrentSectorKey != playerPos.SectorKey)
                {
                    hunter.CurrentSectorKey = playerPos.SectorKey;
                }

                var sector = world.GetSector(hunter.CurrentSectorKey);

                // Step 1 to 2 tiles toward player
                int steps = rng.Next(1, 3);
                for (int step = 0; step < steps; step++)
                {
                    if (hunter.CurrentCoord == playerPos.Coord) break;

                    int dx = playerPos.Coord.X - hunter.CurrentCoord.X;
                    int dy = playerPos.Coord.Y - hunter.CurrentCoord.Y;

                    int primaryX = 0;
                    int primaryY = 0;
                    int altX = 0;
                    int altY = 0;

                    if (Math.Abs(dx) >= Math.Abs(dy))
                    {
                        if (dx != 0) primaryX = Math.Sign(dx);
                        if (dy != 0) altY = Math.Sign(dy);
                    }
                    else
                    {
                        if (dy != 0) primaryY = Math.Sign(dy);
                        if (dx != 0) altX = Math.Sign(dx);
                    }

                    GridCoord primaryCoord = new GridCoord(hunter.CurrentCoord.X + primaryX, hunter.CurrentCoord.Y + primaryY);
                    GridCoord altCoord = new GridCoord(hunter.CurrentCoord.X + altX, hunter.CurrentCoord.Y + altY);

                    GridCoord chosen = hunter.CurrentCoord;
                    bool currentIsTrapped = sector != null && !sector.IsWalkable(hunter.CurrentCoord);

                    if (sector == null || sector.IsWalkable(primaryCoord) || primaryCoord == playerPos.Coord || currentIsTrapped)
                    {
                        chosen = primaryCoord;
                    }
                    else if (altCoord != hunter.CurrentCoord && (sector.IsWalkable(altCoord) || altCoord == playerPos.Coord))
                    {
                        chosen = altCoord;
                    }

                    if (chosen != hunter.CurrentCoord)
                    {
                        hunter.CurrentCoord = chosen;
                        world.SetPosition(new EntityId(hunter.HunterEntityId), chosen, hunter.CurrentSectorKey);
                    }
                }

                // Intercept check: hunter arrived on player tile
                if (hunter.CurrentCoord == playerPos.Coord)
                {
                    hunter.State = BountyHunterState.Engaged;
                    WorldSignals.EmitBountyHunterAmbush(hunter, playerPos.Coord);
                    CombatSystem.StartCombat(
                        world,
                        ArenaEncounterType.DemonicCultivator,
                        playerPos.Coord,
                        new EntityId(hunter.HunterEntityId),
                        isLethal: true,
                        enemyName: hunter.HunterName,
                        rng: rng);
                    WorldSignals.EmitTopics(DirtyTopic.Bounty);
                    break;
                }
            }
        }

        private static void SpawnBountyHunter(WorldStore world, BountyTrackerComponent tracker, FactionID issuingFaction, IRandomSource rng)
        {
            var playerPos = world.Get<PositionComponent>(world.PlayerEntity);
            GridCoord pCoord = playerPos?.Coord ?? new GridCoord(15, 15);
            string sectorKey = playerPos?.SectorKey ?? world.CurrentSectorKey;
            var sector = world.GetSector(sectorKey);

            int width = sector?.GridWidth ?? 30;
            int height = sector?.GridHeight ?? 30;

            int signX = rng.Next(0, 2) == 0 ? 1 : -1;
            int signY = rng.Next(0, 2) == 0 ? 1 : -1;
            int dist = rng.Next(4, 7);

            int candidateX = Math.Clamp(pCoord.X + (signX * dist), 2, Math.Max(2, width - 3));
            int candidateY = Math.Clamp(pCoord.Y + (signY * dist), 2, Math.Max(2, height - 3));
            GridCoord spawnCoord = FindWalkableSpawnCoord(sector, pCoord, candidateX, candidateY, width, height);

            EntityId hunterEid = world.CreateEntity();
            string hunterName = issuingFaction switch
            {
                FactionID.TieClan => "Tie Clan Divine Enforcer (铁家神捕)",
                FactionID.XiongClan => "Xiong Clan Enforcer (熊家追捕长)",
                FactionID.BaiClan => "Bai Clan Bone-Hunter (白家诛戮修)",
                FactionID.GuYueClan => "Gu Yue Executioner (古月刑堂长老)",
                _ => "Clan Bounty Hunter (追杀死士)"
            };

            int playerRank = world.Get<ApertureComponent>(world.PlayerEntity)?.Rank ?? 1;

            var hunterCult = new CultivatorComponent
            {
                Name = hunterName,
                Clan = issuingFaction.ToString(),
                Aptitude = AptitudeGrade.B_Grade,
                AptitudePercent = 75f,
                Age = 32,
                LifespanYears = 80,
                PrimevalStones = 180,
                Favorability = -180
            };

            world.Set(hunterEid, hunterCult);
            world.Set(hunterEid, new ApertureComponent
            {
                Rank = playerRank,
                Substage = 1,
                CurrentHealth = 100f,
                MaxHealth = 100f,
                CurrentEssenceSea = 75f
            });
            world.Set(hunterEid, new PositionComponent(spawnCoord, sectorKey));

            int bribeCost = 100 * playerRank;
            var record = new BountyHunterRecord(
                hunterEid.Value,
                world.PlayerId,
                issuingFaction,
                hunterName,
                sectorKey,
                spawnCoord,
                bribeCost,
                playerRank);

            tracker.Hunters.Add(record);
            WorldSignals.EmitTopics(DirtyTopic.Bounty | DirtyTopic.NPCs);
        }

        public static bool TryBribeHunter(WorldStore world, EntityId hunterId)
        {
            var tracker = world.GetSingleton<BountyTrackerComponent>();
            if (tracker == null) return false;

            var hunter = tracker.Hunters.Find(h => h.HunterEntityId == hunterId.Value);
            if (hunter == null || hunter.State == BountyHunterState.Bribed || hunter.State == BountyHunterState.Defeated)
            {
                return false;
            }

            var playerCult = world.Get<CultivatorComponent>(world.PlayerEntity);
            if (playerCult == null || playerCult.PrimevalStones < hunter.BribeCostStones)
            {
                return false;
            }

            playerCult.PrimevalStones -= hunter.BribeCostStones;
            hunter.State = BountyHunterState.Bribed;

            if (world.IsAlive(hunterId) && world.TryGet<CultivatorComponent>(hunterId, out var hc))
            {
                hc.Favorability = 0;
            }

            WorldSignals.EmitTopics(DirtyTopic.Bounty | DirtyTopic.Cultivator | DirtyTopic.Inventory | DirtyTopic.NPCs);
            return true;
        }

        public static bool ResolveHunterDefeated(WorldStore world, EntityId hunterId)
        {
            var tracker = world.GetSingleton<BountyTrackerComponent>();
            if (tracker == null) return false;

            var hunter = tracker.Hunters.Find(h => h.HunterEntityId == hunterId.Value);
            if (hunter == null) return false;

            hunter.State = BountyHunterState.Defeated;
            WorldSignals.EmitTopics(DirtyTopic.Bounty);
            return true;
        }

        public static void ResolveHunterDisengaged(WorldStore world, int hunterEntityId)
        {
            if (hunterEntityId < 0) return;
            ResolveHunterDisengaged(world, (ulong)hunterEntityId);
        }

        public static void ResolveHunterDisengaged(WorldStore world, ulong hunterEntityId)
        {
            var tracker = world.GetSingleton<BountyTrackerComponent>();
            if (tracker == null) return;

            var hunter = tracker.Hunters.Find(h => h.HunterEntityId == hunterEntityId);
            if (hunter == null || hunter.State != BountyHunterState.Engaged) return;

            hunter.State = BountyHunterState.Pursuing;
            WorldSignals.EmitTopics(DirtyTopic.Bounty);
        }

        private static GridCoord FindWalkableSpawnCoord(
            SectorGridComponent? sector,
            GridCoord pCoord,
            int candidateX,
            int candidateY,
            int width,
            int height)
        {
            GridCoord candidate = new GridCoord(
                Math.Clamp(candidateX, 0, width - 1),
                Math.Clamp(candidateY, 0, height - 1));

            if (sector == null || (sector.IsWalkable(candidate) && candidate != pCoord))
            {
                return candidate;
            }

            // Attempt stepping toward player until a walkable tile is found
            GridCoord walk = candidate;
            while (walk != pCoord)
            {
                int dx = pCoord.X - walk.X;
                int dy = pCoord.Y - walk.Y;
                if (Math.Abs(dx) >= Math.Abs(dy) && dx != 0)
                {
                    walk = new GridCoord(walk.X + Math.Sign(dx), walk.Y);
                }
                else if (dy != 0)
                {
                    walk = new GridCoord(walk.X, walk.Y + Math.Sign(dy));
                }
                else
                {
                    break;
                }

                if (sector.IsWalkable(walk) && walk != pCoord)
                {
                    return walk;
                }
            }

            // Search radially outward from player at radius 2..6
            for (int r = 2; r <= 6; r++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dy = -r; dy <= r; dy++)
                    {
                        if (Math.Abs(dx) != r && Math.Abs(dy) != r) continue;
                        GridCoord test = new GridCoord(
                            Math.Clamp(pCoord.X + dx, 0, width - 1),
                            Math.Clamp(pCoord.Y + dy, 0, height - 1));

                        if (sector.IsWalkable(test) && test != pCoord)
                        {
                            return test;
                        }
                    }
                }
            }

            // Fallback: adjacent offset
            (int ox, int oy)[] offsets = { (1, 0), (-1, 0), (0, 1), (0, -1) };
            foreach (var (ox, oy) in offsets)
            {
                GridCoord adj = new GridCoord(
                    Math.Clamp(pCoord.X + ox, 0, width - 1),
                    Math.Clamp(pCoord.Y + oy, 0, height - 1));
                if (sector.IsWalkable(adj) && adj != pCoord)
                {
                    return adj;
                }
            }

            return candidate;
        }

        public static void TriggerFramedSkirmish(WorldStore world, string sectorKey, FactionID framedFaction)
        {
            var eventComp = world.GetOrCreateSingleton<WorldEventComponent>();
            var sector = world.GetSector(sectorKey) ?? world.GetCurrentSector();

            int width = sector?.GridWidth ?? 30;
            int height = sector?.GridHeight ?? 30;
            GridCoord skirmishCoord = new GridCoord(width / 2, height / 2);

            string title = $"Inter-Clan Skirmish: {framedFaction}";
            string desc = $"Framed evidence ignited active skirmishes involving {framedFaction}!";

            var clock = world.GetSingleton<WorldClockComponent>();
            int seq = eventComp.NextEventSequence++;
            string eventId = $"skirmish_{clock?.CurrentYear ?? 1}_{clock?.CurrentSolarTermIndex ?? 1}_{seq}";

            var entry = new WorldEventEntry(
                WorldCalamityKind.InterClanSkirmish,
                WorldCalamityPhase.Active,
                sectorKey,
                skirmishCoord,
                remainingTerms: 3,
                title,
                desc,
                eventId);

            ApplyCalamityPOI(world, eventComp, entry);
            eventComp.ActiveEvents.Add(entry);

            foreach (var (_, f) in world.GetAll<FactionComponent>())
            {
                if (f.ID == framedFaction)
                {
                    f.ModifyReputation(-30);
                }
            }

            WorldSignals.EmitWorldCalamityUpdated(entry);
            WorldSignals.EmitTopics(DirtyTopic.Director | DirtyTopic.Factions | DirtyTopic.Map);
        }
    }
}
