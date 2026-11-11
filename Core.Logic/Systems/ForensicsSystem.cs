using System;
using System.Collections.Generic;
using TaiwuGu.Core;
using TaiwuGu.Entities;
using TaiwuGu.Psychology;

namespace TaiwuGu.Systems
{
    /// <summary>
    /// Stateless ECS System managing mortal Crime Scenes, lingering Soul Qi dissipation, and evidence tampering.
    /// </summary>
    public static class ForensicsSystem
    {
        public static (EntityId Id, CrimeSceneComponent Scene)? GetActiveCrimeSceneAt(WorldStore world, GridCoord pos, string? sectorKey = null)
        {
            string activeSector = sectorKey ?? world.CurrentSectorKey;
            foreach (var eid in world.GetEntitiesAt(pos, activeSector))
            {
                if (world.TryGet<CrimeSceneComponent>(eid, out var scene) &&
                    !scene.IsTamperedClean &&
                    scene.LingeringSoulQiTerms > 0)
                {
                    return (eid, scene);
                }
            }
            return null;
        }

        public static EntityId SpawnCrimeScene(WorldStore world, GridCoord pos, string victimName, CrimeSignature signature, int terms = 2, string? sectorKey = null)
        {
            string activeSector = sectorKey ?? world.CurrentSectorKey;
            var existing = GetActiveCrimeSceneAt(world, pos, activeSector);
            if (existing.HasValue)
            {
                var scene = existing.Value.Scene;
                scene.VictimName = victimName;
                scene.Signature = signature;
                scene.LingeringSoulQiTerms = terms;
                scene.IsTamperedClean = false;
                scene.FramedFaction = string.Empty;
                WorldSignals.EmitTopics(DirtyTopic.Forensics);
                return existing.Value.Id;
            }

            EntityId eid = world.CreateEntity();
            var comp = new CrimeSceneComponent(victimName, signature, terms);
            world.Set(eid, new PositionComponent(pos, activeSector));
            world.Set(eid, comp);

            Debug.Log($"[ForensicsSystem] Spawned Crime Scene at {pos} | Victim: {victimName} | Sig: {signature}");
            WorldSignals.EmitTopics(DirtyTopic.Forensics);
            return eid;
        }

        public static bool TamperCrimeScene(WorldStore world, GridCoord pos, CrimeTamperChoice choice)
        {
            var found = GetActiveCrimeSceneAt(world, pos);
            if (!found.HasValue) return false;

            var clock = world.GetSingleton<WorldClockComponent>();
            if (clock == null || clock.ActionPointsRemaining < 1)
            {
                Debug.LogWarning("[ForensicsSystem] Insufficient Action Points to tamper evidence (Requires 1 AP)!");
                return false;
            }

            TimeSystem.DeductActionPoints(world, 1);

            var scene = found.Value.Scene;
            switch (choice)
            {
                case CrimeTamperChoice.Cleanse:
                    scene.IsTamperedClean = true;
                    scene.LingeringSoulQiTerms = 0;
                    world.DestroyEntity(found.Value.Id);
                    Debug.Log($"[ForensicsSystem] Cleansed evidence at {pos}! All soul qi erased.");
                    break;

                case CrimeTamperChoice.FrameWildBeasts:
                    scene.Signature = CrimeSignature.FramedWildBeast;
                    scene.LingeringSoulQiTerms = 0;
                    scene.FramedFaction = "Wild Boar Pack (野彘群)";
                    Debug.Log($"[ForensicsSystem] Framed wild beasts at {pos}.");
                    break;

                case CrimeTamperChoice.FrameRivalClan:
                    scene.Signature = CrimeSignature.FramedRivalClan;
                    scene.LingeringSoulQiTerms = 0;
                    scene.FramedFaction = "Xiong Clan (熊家寨)";
                    Debug.Log($"[ForensicsSystem] Planted Xiong Clan tokens at {pos}.");
                    break;
            }

            WorldSignals.EmitTopics(DirtyTopic.Forensics | DirtyTopic.Calendar);
            return true;
        }

        public static void AdvanceCrimeScenes(WorldStore world)
        {
            var toDestroy = new List<EntityId>();
            foreach (var (eid, scene) in world.GetAll<CrimeSceneComponent>())
            {
                if (scene.IsTamperedClean)
                {
                    toDestroy.Add(eid);
                    continue;
                }

                scene.LingeringSoulQiTerms--;
                if (scene.LingeringSoulQiTerms <= 0)
                {
                    toDestroy.Add(eid);
                }
            }

            foreach (var eid in toDestroy)
            {
                world.DestroyEntity(eid);
            }

            WorldSignals.EmitTopics(DirtyTopic.Forensics);
        }
    }
}
