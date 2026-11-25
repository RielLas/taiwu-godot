using System;
using System.Collections.Generic;
using Godot;
using TaiwuGu.Core;
using TaiwuGu.Entities;
using TaiwuGu.Map;
using TaiwuGu.Serialization;
using Taiwu_Godot.Scripts.Map;

namespace Taiwu_Godot.Scripts.Verification
{
    /// <summary>
    /// Headless acceptance checks over the real scene graph, ECS WorldStore, and SaveDiskIO v3.0.0 pipeline.
    /// Usage:
    ///   godot --headless --path . -- --verify --strict-poi
    /// </summary>
    public static class VerificationRunner
    {
        private const string VerifySlotId = "__verify__";

        public static int Run(TaiwuGameBootstrapper? bootstrapper, bool strictPoi)
        {
            var failures = new List<string>();
            var notes = new List<string>();

            if (bootstrapper?.StateProvider?.Current == null)
            {
                GD.PrintErr("[verify] FAIL: Bootstrapper or WorldStore is null; nothing to verify.");
                return 1;
            }

            CheckStateShape(bootstrapper, failures);
            CheckRenderedGridShape(bootstrapper, failures);
            CheckSaveRoundTrip(bootstrapper, failures);
            CheckPoiParity(bootstrapper, strictPoi, failures, notes);

            foreach (string note in notes)
            {
                GD.Print($"[verify] note: {note}");
            }

            if (failures.Count == 0)
            {
                GD.Print("[verify] PASS: ECS WorldStore shape, rendered grid, spatial index, and v3.0.0 save round-trip are consistent.");
                return 0;
            }

            foreach (string failure in failures)
            {
                GD.PrintErr($"[verify] FAIL: {failure}");
            }
            GD.PrintErr($"[verify] {failures.Count} check(s) failed.");
            return 1;
        }

        private static void CheckStateShape(TaiwuGameBootstrapper bootstrapper, List<string> failures)
        {
            var world = bootstrapper.World;
            var sector = world.GetCurrentSector();
            if (sector == null)
            {
                failures.Add("WorldStore.GetCurrentSector() is null.");
                return;
            }

            var tiles = sector.EnsureHydrated();
            int expected = sector.GridWidth * sector.GridHeight;
            if (tiles.Length != expected)
            {
                failures.Add($"Sector.Tiles length {tiles.Length} does not match {sector.GridWidth}x{sector.GridHeight} ({expected}).");
            }

            if (tiles.Length == 0)
            {
                failures.Add("Sector.Tiles is empty.");
                return;
            }

            int clanPosts = 0;
            for (int i = 0; i < tiles.Length; i++)
            {
                if (tiles[i].POI == TilePOIType.ClanPost) clanPosts++;
            }

            if (clanPosts == 0)
            {
                failures.Add("No ClanPost tile exists in SectorGridComponent.");
            }

            int factionCount = 0;
            foreach (var _ in world.GetAll<FactionComponent>()) factionCount++;
            if (factionCount < 6)
            {
                failures.Add($"Expected 6 faction entities, found {factionCount}.");
            }

            int guCount = 0;
            foreach (var _ in world.GetOwned<GuWormComponent>(world.PlayerEntity)) guCount++;
            if (guCount == 0)
            {
                failures.Add("Player owns zero starter GuWormComponent entities.");
            }
        }

        private static void CheckRenderedGridShape(TaiwuGameBootstrapper bootstrapper, List<string> failures)
        {
            IsometricTileManager? tiles = bootstrapper.TileManager;
            SectorGridComponent? sector = bootstrapper.World.GetCurrentSector();

            if (tiles == null)
            {
                failures.Add("TileManager export is not wired; the scene cannot render the map.");
                return;
            }

            if (sector == null) return;

            if (tiles.GridWidth != sector.GridWidth || tiles.GridHeight != sector.GridHeight)
            {
                failures.Add($"Rendered grid {tiles.GridWidth}x{tiles.GridHeight} does not match SectorGridComponent {sector.GridWidth}x{sector.GridHeight}.");
                return;
            }

            for (int x = 0; x < sector.GridWidth; x++)
            {
                for (int y = 0; y < sector.GridHeight; y++)
                {
                    TileNode rendered = tiles.GetTile(x, y);
                    if (rendered.GridPos.X != x || rendered.GridPos.Y != y)
                    {
                        failures.Add($"Rendered tile at ({x},{y}) reports GridPos ({rendered.GridPos.X},{rendered.GridPos.Y}); grid indexing is transposed.");
                        return;
                    }
                }
            }
        }

        private static void CheckSaveRoundTrip(TaiwuGameBootstrapper bootstrapper, List<string> failures)
        {
            WorldStore world = bootstrapper.World;
            try
            {
                if (!SaveDiskIO.SaveWorld(VerifySlotId, world))
                {
                    failures.Add("SaveDiskIO.SaveWorld returned false; no save was written.");
                    return;
                }

                WorldStore? loaded = SaveDiskIO.LoadWorld(VerifySlotId);
                if (loaded == null)
                {
                    failures.Add("SaveDiskIO.LoadWorld returned null for a save that was just written.");
                    return;
                }

                CompareWorld(world, loaded, failures);
            }
            catch (Exception ex)
            {
                failures.Add($"Save round-trip threw {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                SaveDiskIO.DeleteSlot(VerifySlotId);
            }
        }

        private static void CompareWorld(WorldStore before, WorldStore after, List<string> failures)
        {
            var cultA = before.Get<CultivatorComponent>(before.PlayerEntity);
            var cultB = after.Get<CultivatorComponent>(after.PlayerEntity);
            var apA = before.Get<ApertureComponent>(before.PlayerEntity);
            var apB = after.Get<ApertureComponent>(after.PlayerEntity);

            if (cultA == null || cultB == null || apA == null || apB == null)
            {
                failures.Add("Player CultivatorComponent or ApertureComponent missing before/after save.");
            }
            else
            {
                if (cultA.Name != cultB.Name)
                    failures.Add($"Player name changed across save: '{cultA.Name}' -> '{cultB.Name}'.");
                if (apA.Rank != apB.Rank)
                    failures.Add($"Player rank changed across save: {apA.Rank} -> {apB.Rank}.");
                if (apA.Substage != apB.Substage)
                    failures.Add($"Player substage changed across save: {apA.Substage} -> {apB.Substage}.");
                if (cultA.PrimevalStones != cultB.PrimevalStones)
                    failures.Add($"Primeval stones changed across save: {cultA.PrimevalStones} -> {cultB.PrimevalStones}.");
                if (!NearlyEqual(apA.CurrentEssenceSea, apB.CurrentEssenceSea))
                    failures.Add($"Essence sea changed across save: {apA.CurrentEssenceSea} -> {apB.CurrentEssenceSea}.");
                if (!NearlyEqual(cultA.AptitudePercent, cultB.AptitudePercent))
                    failures.Add($"Aptitude changed across save: {cultA.AptitudePercent} -> {cultB.AptitudePercent}.");
            }

            var clockA = before.GetSingleton<WorldClockComponent>();
            var clockB = after.GetSingleton<WorldClockComponent>();
            if (clockA == null || clockB == null)
            {
                failures.Add("WorldClockComponent missing before or after save.");
            }
            else
            {
                if (clockA.CurrentYear != clockB.CurrentYear)
                    failures.Add($"Calendar year changed across save: {clockA.CurrentYear} -> {clockB.CurrentYear}.");
                if (clockA.CurrentSolarTermIndex != clockB.CurrentSolarTermIndex)
                    failures.Add($"Solar term changed across save: {clockA.CurrentSolarTermIndex} -> {clockB.CurrentSolarTermIndex}.");
                if (clockA.ActionPointsRemaining != clockB.ActionPointsRemaining)
                    failures.Add($"Action points changed across save: {clockA.ActionPointsRemaining} -> {clockB.ActionPointsRemaining}.");
            }

            int beforeGu = CountOwnedGu(before, before.PlayerEntity);
            int afterGu = CountOwnedGu(after, after.PlayerEntity);
            if (beforeGu != afterGu)
            {
                failures.Add($"Player owned Gu entity count changed across save: {beforeGu} -> {afterGu}.");
            }

            // Verify spatial index rebuilt cleanly after load
            int spatialBefore = CountEntitiesAt(before, new GridCoord(15, 14));
            int spatialAfter = CountEntitiesAt(after, new GridCoord(15, 14));
            if (spatialBefore != spatialAfter || spatialAfter == 0)
            {
                failures.Add($"Spatial index at (15,14) mismatch across save: {spatialBefore} -> {spatialAfter}.");
            }

            CompareTiles(before.GetCurrentSector(), after.GetCurrentSector(), failures);
        }

        private static int CountOwnedGu(WorldStore world, EntityId ownerId)
        {
            int count = 0;
            foreach (var _ in world.GetOwned<GuWormComponent>(ownerId)) count++;
            return count;
        }

        private static int CountEntitiesAt(WorldStore world, GridCoord coord)
        {
            int count = 0;
            foreach (var _ in world.GetEntitiesAt(coord, world.CurrentSectorKey)) count++;
            return count;
        }

        private static void CompareTiles(SectorGridComponent? before, SectorGridComponent? after, List<string> failures)
        {
            var tilesA = before?.EnsureHydrated();
            var tilesB = after?.EnsureHydrated();
            if (tilesA == null || tilesB == null)
            {
                failures.Add("SectorGridComponent.Tiles is missing before or after save.");
                return;
            }

            if (tilesA.Length != tilesB.Length)
            {
                failures.Add($"Tile count changed across save: {tilesA.Length} -> {tilesB.Length}.");
                return;
            }

            int mismatches = 0;
            string firstMismatch = string.Empty;

            for (int i = 0; i < tilesA.Length; i++)
            {
                TileNode a = tilesA[i];
                TileNode b = tilesB[i];

                bool same = a.Biome == b.Biome
                    && a.POI == b.POI
                    && a.DangerRank == b.DangerRank
                    && a.IsExplored == b.IsExplored
                    && a.IsVisible == b.IsVisible
                    && a.IsActive == b.IsActive
                    && string.Equals(a.CustomLandmarkId, b.CustomLandmarkId, StringComparison.Ordinal)
                    && a.GridPos.X == b.GridPos.X
                    && a.GridPos.Y == b.GridPos.Y;

                if (!same)
                {
                    if (mismatches == 0)
                    {
                        firstMismatch = $"index {i} at ({a.GridPos.X},{a.GridPos.Y}): before POI={a.POI} biome={a.Biome} active={a.IsActive}; after POI={b.POI} biome={b.Biome} active={b.IsActive}";
                    }
                    mismatches++;
                }
            }

            if (mismatches > 0)
            {
                failures.Add($"{mismatches}/{tilesA.Length} tiles changed across save/load. First: {firstMismatch}");
            }
        }

        private static void CheckPoiParity(
            TaiwuGameBootstrapper bootstrapper,
            bool strictPoi,
            List<string> failures,
            List<string> notes)
        {
            SectorGridComponent? sector = bootstrapper.World.GetCurrentSector();
            TileLandmarkVisualManager? landmarks = bootstrapper.LandmarkManager;
            if (sector == null) return;

            if (landmarks == null)
            {
                string msg = "POI parity: LandmarkManager export is not wired; cannot verify spawned POI landmarks.";
                if (strictPoi) failures.Add(msg);
                else notes.Add(msg);
                return;
            }

            var sectorTiles = sector.EnsureHydrated();
            int expectedLandmarks = 0;
            int missingLandmarks = 0;

            for (int y = 0; y < sector.GridHeight; y++)
            {
                for (int x = 0; x < sector.GridWidth; x++)
                {
                    TileNode tile = sectorTiles[y * sector.GridWidth + x];
                    if (!tile.IsActive || tile.POI == TilePOIType.None) continue;

                    var entry = landmarks.VisualDatabase?.GetEntry(tile.POI);
                    if (entry == null || entry.DioramaTexture == null) continue;

                    expectedLandmarks++;
                    if (!landmarks.HasLandmarkAt(new Vector2I(x, y)))
                    {
                        missingLandmarks++;
                    }
                }
            }

            if (missingLandmarks == 0 && landmarks.ActiveLandmarkCount == expectedLandmarks) return;

            string message = $"POI parity: expected {expectedLandmarks} active POI landmark(s) in TileLandmarkVisualManager, found {landmarks.ActiveLandmarkCount} ({missingLandmarks} missing from SectorGridComponent).";
            if (strictPoi) failures.Add(message);
            else notes.Add(message);
        }

        private static bool NearlyEqual(float a, float b) => MathF.Abs(a - b) <= 1e-4f;
    }
}
