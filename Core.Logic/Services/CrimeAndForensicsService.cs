using System;
using System.Collections.Generic;
using TaiwuGu.Map;
using TaiwuGu.Combat;
using TaiwuGu.Psychology;

namespace TaiwuGu.Core
{
    /// <summary>
    /// Phase 5 Domain Service: Crime & Forensics Engine under Constitutional Laws 1, 3, and 4.
    /// Pure Domain Service managing mortal physical evidence and temporal forensic footprints.
    /// </summary>
    public class CrimeAndForensicsService : IDisposable
    {
        private GameStateContext? _context;

        public static Action<string, Vector2Int, CultivatorData, CrimeSceneData>? OnProcessVictimDeathHook;
        public static Action<Vector2Int>? OnProcessMonthlyPursuitHook;

        public void Initialize(GameStateContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));

            GameEventManager.OnTamperCrimeEvidenceRequested += HandleTamperCrimeEvidence;
            GameEventManager.OnSolarTermAdvanced           += HandleSolarTermAdvanced;
            GameEventManager.OnVictimExecuted              += HandleVictimExecuted;
            GameEventManager.OnCombatConcluded             += HandleCombatConcluded;
            GameEventManager.OnPOIArrived                  += HandlePOIArrived;

            Debug.Log("[CrimeAndForensicsService] Initialized and listening for mortal forensic events.");
        }

        public void Dispose()
        {
            GameEventManager.OnTamperCrimeEvidenceRequested -= HandleTamperCrimeEvidence;
            GameEventManager.OnSolarTermAdvanced           -= HandleSolarTermAdvanced;
            GameEventManager.OnVictimExecuted              -= HandleVictimExecuted;
            GameEventManager.OnCombatConcluded             -= HandleCombatConcluded;
            GameEventManager.OnPOIArrived                  -= HandlePOIArrived;
        }

        public CrimeSceneData? GetActiveCrimeSceneAt(Vector2Int pos)
        {
            if (_context?.Forensics?.ActiveCrimeScenes == null) return null;
            return _context.Forensics.ActiveCrimeScenes.Find(c => c.GridPos == pos && !c.IsTamperedClean && c.LingeringSoulQiTerms > 0);
        }

        public CrimeSceneData? SpawnCrimeScene(Vector2Int pos, string victimName, CrimeSignature signature, int terms = 2)
        {
            if (_context?.Forensics == null) return null;

            var existing = _context.Forensics.ActiveCrimeScenes.Find(c => c.GridPos == pos && !c.IsTamperedClean);
            if (existing != null)
            {
                existing.VictimName = victimName;
                existing.Signature = signature;
                existing.LingeringSoulQiTerms = terms;
                existing.IsTamperedClean = false;
                existing.FramedFaction = "";
                Debug.Log($"[Forensics] Refreshed Crime Scene at ({pos.x}, {pos.y}) | Victim: {victimName} | Sig: {signature} | Terms: {terms}");
                GameEventManager.TriggerCrimeSceneUpdated(pos);
                return existing;
            }

            CrimeSceneData scene = new CrimeSceneData(pos, victimName, signature, terms: terms);
            _context.Forensics.ActiveCrimeScenes.Add(scene);
            Debug.Log($"[CRIME SCENE CREATED] at ({pos.x}, {pos.y}) | Victim: {victimName} | Signature: {signature} | Residual Soul Qi: {terms} Solar Terms");
            GameEventManager.TriggerCrimeSceneUpdated(pos);
            return scene;
        }

        private void HandleVictimExecuted(string victimName, Vector2Int pos, CrimeSignature signature, bool didDetonate)
        {
            CrimeSignature finalSig = didDetonate ? CrimeSignature.ScorchedCrater : signature;
            var scene = SpawnCrimeScene(pos, victimName, finalSig, terms: 2);

            if (_context?.Cultivator != null && scene != null)
            {
                var player = _context.Cultivator.ToCultivatorData();
                OnProcessVictimDeathHook?.Invoke(victimName, pos, player, scene);
            }
        }

        private void HandleCombatConcluded(bool playerWon, Vector2Int tilePos, string enemyName, ArenaEncounterType encounter)
        {
            if (!playerWon) return;

            if (encounter == ArenaEncounterType.WildBoarHunt)
            {
                SpawnCrimeScene(tilePos, enemyName, CrimeSignature.BeastRemains, terms: 2);
            }
        }

        private void HandleTamperCrimeEvidence(Vector2Int pos, CrimeTamperChoice choice)
        {
            if (_context == null) return;

            var scene = GetActiveCrimeSceneAt(pos);
            if (scene == null)
            {
                Debug.LogWarning($"[Forensics] No active crime scene at ({pos.x}, {pos.y}) to tamper!");
                return;
            }

            if (_context.Calendar.ActionPointsRemaining < 1)
            {
                Debug.LogWarning("[Forensics] Insufficient Action Points to tamper crime scene evidence (Requires 1 AP)!");
                return;
            }

            GameEventManager.RaiseRequestDeductActionPoints(1);

            switch (choice)
            {
                case CrimeTamperChoice.Cleanse:
                    scene.IsTamperedClean = true;
                    scene.LingeringSoulQiTerms = 0;
                    Debug.Log($"[CRIME SCENE CLEANSED] Spent 1 AP to incinerate evidence at ({pos.x}, {pos.y})! All soul qi erased.");
                    break;

                case CrimeTamperChoice.FrameWildBeasts:
                    scene.Signature = CrimeSignature.FramedWildBeast;
                    scene.LingeringSoulQiTerms = 0;
                    scene.FramedFaction = "Wild Boar Pack (野彘群)";
                    Debug.Log("[FORENSIC MISDIRECTION] Spent 1 AP to carve claw incisions! Investigators will be diverted to Beast Dens!");
                    break;

                case CrimeTamperChoice.FrameRivalClan:
                    scene.Signature = CrimeSignature.FramedRivalClan;
                    scene.LingeringSoulQiTerms = 0;
                    scene.FramedFaction = "Xiong Clan (熊家寨)";
                    Debug.Log("[FORENSIC FRAME-JOB] Spent 1 AP to plant Xiong Clan bear-claw tokens! Hostility redirected to Xiong Clan!");
                    break;
            }

            GameEventManager.TriggerCrimeSceneUpdated(pos);
        }

        private void HandleSolarTermAdvanced(int year, SolarTerm term, string termName)
        {
            if (_context?.Forensics?.ActiveCrimeScenes == null) return;

            for (int i = _context.Forensics.ActiveCrimeScenes.Count - 1; i >= 0; i--)
            {
                var scene = _context.Forensics.ActiveCrimeScenes[i];
                if (scene.IsTamperedClean)
                {
                    _context.Forensics.ActiveCrimeScenes.RemoveAt(i);
                }
                GameEventManager.TriggerCrimeSceneUpdated(scene.GridPos);
            }

            if (_context?.WorldMap != null)
            {
                OnProcessMonthlyPursuitHook?.Invoke(_context.WorldMap.PlayerGridPos);
            }
        }

        private void HandlePOIArrived(TilePOIType poi, Vector2Int pos, string title, string desc, int dangerRank)
        {
            var scene = GetActiveCrimeSceneAt(pos);
            if (scene != null)
            {
                GameEventManager.TriggerCrimeScenePresent(pos, scene.LingeringSoulQiTerms);
            }
        }
    }
}
