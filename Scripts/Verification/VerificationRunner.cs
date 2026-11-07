using System;
using System.Collections.Generic;
using Godot;
using TaiwuGu.Core;
using TaiwuGu.Map;
using TaiwuGu.Serialization;
using Taiwu_Godot.Scripts.Map;

namespace Taiwu_Godot.Scripts.Verification
{
    /// <summary>
    /// Headless acceptance checks over the real scene graph and the real save pipeline.
    /// Returns a process exit code, so a broken boot, a desynced map, or a lossy save fails the run
    /// instead of producing a screenshot for a human to squint at.
    ///
    /// Usage:
    ///   godot --headless --path . -- --verify
    ///   godot --headless --path . -- --verify --strict-poi
    ///
    /// --strict-poi adds POI parity between WorldMapState and the rendered grid. It is expected to
    /// FAIL until the map has a single authority (REFACTOR_PLAN.md, Phase 2); that is the point of
    /// the gate.
    /// </summary>
    public static class VerificationRunner
    {
        private const string VerifySlotId = "__verify__";

        public static int Run(TaiwuGameBootstrapper? bootstrapper, bool strictPoi)
        {
            var failures = new List<string>();
            var notes = new List<string>();

            if (bootstrapper?.StateContext == null)
            {
                GD.PrintErr("[verify] FAIL: Bootstrapper or StateContext is null; nothing to verify.");
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
                GD.Print("[verify] PASS: state shape, rendered grid, and save round-trip are consistent.");
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
            WorldMapState? map = bootstrapper.StateContext?.WorldMap;
            if (map?.Tiles == null)
            {
                failures.Add("WorldMap.Tiles is null; the domain map was never generated.");
                return;
            }

            int expected = map.GridWidth * map.GridHeight;
            if (map.Tiles.Length != expected)
            {
                failures.Add($"WorldMap.Tiles length {map.Tiles.Length} does not match {map.GridWidth}x{map.GridHeight} ({expected}).");
            }

            if (map.Tiles.Length == 0)
            {
                failures.Add("WorldMap.Tiles is empty.");
                return;
            }

            int clanPosts = 0;
            for (int i = 0; i < map.Tiles.Length; i++)
            {
                if (map.Tiles[i].POI == TilePOIType.ClanPost) clanPosts++;
            }

            if (clanPosts == 0)
            {
                failures.Add("No ClanPost tile exists in the domain map; the starting sector is not seeded.");
            }
        }

        private static void CheckRenderedGridShape(TaiwuGameBootstrapper bootstrapper, List<string> failures)
        {
            IsometricTileManager? tiles = bootstrapper.TileManager;
            WorldMapState? map = bootstrapper.StateContext?.WorldMap;

            if (tiles == null)
            {
                failures.Add("TileManager export is not wired; the scene cannot render the map.");
                return;
            }

            if (map == null) return;

            if (tiles.GridWidth != map.GridWidth || tiles.GridHeight != map.GridHeight)
            {
                failures.Add($"Rendered grid {tiles.GridWidth}x{tiles.GridHeight} does not match state grid {map.GridWidth}x{map.GridHeight}.");
                return;
            }

            // Catches transposed indexing: every rendered cell must report its own coordinates.
            for (int x = 0; x < map.GridWidth; x++)
            {
                for (int y = 0; y < map.GridHeight; y++)
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
            GameStateContext? ctx = bootstrapper.StateContext;
            if (ctx == null) return;

            try
            {
                if (!SaveLoadEngine.SaveContext(VerifySlotId, ctx))
                {
                    failures.Add("SaveLoadEngine.SaveContext returned false; no save was written.");
                    return;
                }

                GameStateContext? loaded = SaveLoadEngine.LoadContext(VerifySlotId);
                if (loaded == null)
                {
                    failures.Add("SaveLoadEngine.LoadContext returned null for a save that was just written.");
                    return;
                }

                CompareContext(ctx, loaded, failures);
            }
            catch (Exception ex)
            {
                failures.Add($"Save round-trip threw {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                SaveLoadEngine.DeleteSlot(VerifySlotId);
            }
        }

        private static void CompareContext(GameStateContext before, GameStateContext after, List<string> failures)
        {
            if (before.Cultivator == null || after.Cultivator == null)
            {
                failures.Add("Cultivator state is missing before or after save.");
            }
            else
            {
                if (before.Cultivator.Name != after.Cultivator.Name)
                    failures.Add($"Cultivator name changed across save: '{before.Cultivator.Name}' -> '{after.Cultivator.Name}'.");
                if (before.Cultivator.Rank != after.Cultivator.Rank)
                    failures.Add($"Cultivator rank changed across save: {before.Cultivator.Rank} -> {after.Cultivator.Rank}.");
                if (before.Cultivator.Substage != after.Cultivator.Substage)
                    failures.Add($"Cultivator substage changed across save: {before.Cultivator.Substage} -> {after.Cultivator.Substage}.");
                if (before.Cultivator.PrimevalStones != after.Cultivator.PrimevalStones)
                    failures.Add($"Primeval stones changed across save: {before.Cultivator.PrimevalStones} -> {after.Cultivator.PrimevalStones}.");
                if (!NearlyEqual(before.Cultivator.CurrentEssenceSea, after.Cultivator.CurrentEssenceSea))
                    failures.Add($"Essence sea changed across save: {before.Cultivator.CurrentEssenceSea} -> {after.Cultivator.CurrentEssenceSea}.");
                if (!NearlyEqual(before.Cultivator.AptitudePercent, after.Cultivator.AptitudePercent))
                    failures.Add($"Aptitude changed across save: {before.Cultivator.AptitudePercent} -> {after.Cultivator.AptitudePercent}.");
            }

            if (before.Calendar == null || after.Calendar == null)
            {
                failures.Add("Calendar state is missing before or after save.");
            }
            else
            {
                if (before.Calendar.CurrentYear != after.Calendar.CurrentYear)
                    failures.Add($"Calendar year changed across save: {before.Calendar.CurrentYear} -> {after.Calendar.CurrentYear}.");
                if (before.Calendar.CurrentSolarTermIndex != after.Calendar.CurrentSolarTermIndex)
                    failures.Add($"Solar term changed across save: {before.Calendar.CurrentSolarTermIndex} -> {after.Calendar.CurrentSolarTermIndex}.");
                if (before.Calendar.ActionPointsRemaining != after.Calendar.ActionPointsRemaining)
                    failures.Add($"Action points changed across save: {before.Calendar.ActionPointsRemaining} -> {after.Calendar.ActionPointsRemaining}.");
            }

            int beforeGu = GuCount(before);
            int afterGu = GuCount(after);
            if (beforeGu != afterGu)
            {
                failures.Add($"Gu inventory size changed across save: {beforeGu} -> {afterGu}.");
            }

            CompareTiles(before.WorldMap, after.WorldMap, failures);
        }

        private static int GuCount(GameStateContext ctx)
        {
            return (ctx.GuInventory?.EquippedGu?.Count ?? 0) + (ctx.GuInventory?.ReserveGu?.Count ?? 0);
        }

        private static void CompareTiles(WorldMapState? before, WorldMapState? after, List<string> failures)
        {
            if (before?.Tiles == null || after?.Tiles == null)
            {
                failures.Add("WorldMap.Tiles is missing before or after save.");
                return;
            }

            if (before.Tiles.Length != after.Tiles.Length)
            {
                failures.Add($"Tile count changed across save: {before.Tiles.Length} -> {after.Tiles.Length}.");
                return;
            }

            int mismatches = 0;
            string firstMismatch = string.Empty;

            for (int i = 0; i < before.Tiles.Length; i++)
            {
                TileNode a = before.Tiles[i];
                TileNode b = after.Tiles[i];

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
                failures.Add($"{mismatches}/{before.Tiles.Length} tiles changed across save/load. First: {firstMismatch}");
            }
        }

        private static void CheckPoiParity(
            TaiwuGameBootstrapper bootstrapper,
            bool strictPoi,
            List<string> failures,
            List<string> notes)
        {
            IsometricTileManager? tiles = bootstrapper.TileManager;
            WorldMapState? map = bootstrapper.StateContext?.WorldMap;
            if (tiles == null || map?.Tiles == null) return;

            int width = Math.Min(tiles.GridWidth, map.GridWidth);
            int height = Math.Min(tiles.GridHeight, map.GridHeight);
            int mismatches = 0;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (map.GetTile(x, y).POI != tiles.GetTile(x, y).POI) mismatches++;
                }
            }

            if (mismatches == 0) return;

            string message = $"POI parity: {mismatches}/{width * height} tiles differ between WorldMapState and IsometricTileManager; two tile generators still exist.";
            if (strictPoi)
            {
                failures.Add(message);
            }
            else
            {
                notes.Add(message + " Not gating this run; use --strict-poi once Phase 2 lands.");
            }
        }

        private static bool NearlyEqual(float a, float b)
        {
            return MathF.Abs(a - b) <= 1e-4f;
        }
    }
}
