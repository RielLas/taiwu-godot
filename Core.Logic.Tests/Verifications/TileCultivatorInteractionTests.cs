using System;
using System.Collections.Generic;
using Xunit;
using TaiwuGu.Core;
using TaiwuGu.Entities;
using TaiwuGu.Psychology;

namespace TaiwuGu.Tests
{
    public class TileCultivatorInteractionTests
    {
        [Fact]
        public void Test01_TileReInspectBroadcastsCultivators()
        {
            GameEventManager.ClearAllSubscriptions();
            var ctx = GameStateContext.CreateDefault();

            // Place an NPC at (15, 15)
            var npc = new CultivatorEntityData(
                "npc_elder_01",
                "Gu Yue Bo (Clan Leader)",
                "GuYue",
                4,
                3,
                TaiwuDisposition.Righteous,
                new Vector2Int(15, 15)
            );
            ctx.LivingNPCs = new List<CultivatorEntityData> { npc };

            using var explorationService = new ExplorationService();
            explorationService.Initialize(ctx);

            List<CultivatorEntityData>? receivedCultivators = null;
            Vector2Int? receivedPos = null;

            GameEventManager.OnTileCultivatorsUpdated += (pos, cultivators) =>
            {
                receivedPos = pos;
                receivedCultivators = cultivators;
            };

            // Act: Inspect tile (15, 15)
            GameEventManager.RaiseRequestInspectTile(new GridCoord(15, 15));

            // Assert: Event was triggered and contains the NPC
            Assert.NotNull(receivedCultivators);
            Assert.Single(receivedCultivators);
            Assert.Equal("Gu Yue Bo (Clan Leader)", receivedCultivators[0].Name);
            Assert.Equal(new Vector2Int(15, 15), receivedPos);

            // Re-inspecting same tile again must fire again with the same cultivator
            receivedCultivators = null;
            receivedPos = null;

            GameEventManager.RaiseRequestInspectTile(new GridCoord(15, 15));

            Assert.NotNull(receivedCultivators);
            Assert.Single(receivedCultivators);
            Assert.Equal("Gu Yue Bo (Clan Leader)", receivedCultivators[0].Name);
        }

        [Fact]
        public void Test02_ProceduralNPCWorldManagerSyncsToEntityStore()
        {
            GameEventManager.ClearAllSubscriptions();
            var ctx = GameStateContext.CreateDefault();
            var provider = new GameStateProvider(ctx);

            using var npcManager = new ProceduralNPCWorldManager();
            npcManager.Initialize(ctx, provider);

            // Verify NPCs are present in both LivingNPCs and EntityStore
            Assert.NotEmpty(ctx.LivingNPCs);
            Assert.NotNull(ctx.Entities);

            // Pick first NPC
            var firstNpc = ctx.LivingNPCs[0];
            ulong expectedNumericId = ulong.TryParse(firstNpc.EntityId, out ulong parsed)
                ? parsed
                : ProceduralNPCWorldManager.HashStringToUlong(firstNpc.EntityId);

            // Check EntityStore has it
            var entityId = new EntityId(expectedNumericId);
            Assert.True(ctx.Entities.Has<CultivatorEntityData>(entityId));
            Assert.True(ctx.Entities.Has<PositionComponent>(entityId));

            // Check spatial hash O(1) retrieval
            var coord = new GridCoord(firstNpc.CurrentTilePos.x, firstNpc.CurrentTilePos.y);
            var entitiesAtTile = ctx.Entities.GetEntitiesAt(coord);
            Assert.Contains(entityId, entitiesAtTile);
        }

        [Fact]
        public void Test03_ClosingDialogueReResolvesTileCultivators()
        {
            GameEventManager.ClearAllSubscriptions();
            var ctx = GameStateContext.CreateDefault();
            var provider = new GameStateProvider(ctx);

            using var npcManager = new ProceduralNPCWorldManager();
            npcManager.Initialize(ctx, provider);

            // Position player at (15, 15)
            ctx.WorldMap.PlayerGridPos = new Vector2Int(15, 15);

            // Ensure an NPC is at (15, 15)
            var targetNpc = ctx.LivingNPCs[0];
            targetNpc.CurrentTilePos = new Vector2Int(15, 15);

            List<CultivatorEntityData>? updatedCultivators = null;
            GameEventManager.OnTileCultivatorsUpdated += (pos, list) =>
            {
                if (pos == new Vector2Int(15, 15))
                {
                    updatedCultivators = list;
                }
            };

            // First open dialogue with this NPC
            GameEventManager.RaiseRequestInteractWithNPC(targetNpc.EntityId);

            // Then close dialogue
            GameEventManager.RaiseRequestCloseNPCDialogue();

            // Assert that closing dialogue re-resolved tile cultivators
            Assert.NotNull(updatedCultivators);
            Assert.Contains(updatedCultivators, n => n.EntityId == targetNpc.EntityId);
        }
    }
}
