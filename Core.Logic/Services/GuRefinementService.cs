using System;

namespace TaiwuGu.Core
{
    /// <summary>
    /// Phase 4: Canonical Mortal Gu Refinement & Alchemical Cauldron Service.
    /// Manages:
    /// - 3-Phase Alchemical State Machine: Extraction -> Will Fusion -> Rapid Condensation.
    /// - Pre-Refinement Fuel Check (CalculateMinStonesRequired, Primeval Sea check).
    /// - Vital Gu Immunity Safeguard vs Auxiliary Fragility (Vital Gu enters trauma sleep; auxiliary vaporized).
    /// - Interactive manipulation via CQRS-Lite Commands (Pulse Heat, Adjust Will Focus, Suppress Will, Crush Stone).
    /// - Mutates CauldronRefinementState inside GameStateContext (Constitutional Law 4).
    /// </summary>
    public class GuRefinementService : IDisposable
    {
        private IGameStateProvider? _provider;
        private GameStateContext _context => _provider!.Current;
        private bool _isInitialized = false;

        public bool IsInitialized => _isInitialized;
        public CauldronRefinementState? State => _context?.Cauldron;

        public void Initialize(GameStateContext context, IGameStateProvider? provider = null)
        {
            _provider = provider ?? new StaticGameStateProvider(context);
            _isInitialized = true;

            GameEventManager.OnOpenCauldronRequested           += HandleOpenCauldronRequested;
            GameEventManager.OnCloseCauldronRequested          += HandleCloseCauldronRequested;
            GameEventManager.OnInitiateRefinementRequested     += HandleInitiateRefinementRequested;
            GameEventManager.OnRefineGuRequested               += HandleRefineGuRequested;
            GameEventManager.OnCauldronPulseHeatRequested      += HandleCauldronPulseHeatRequested;
            GameEventManager.OnCauldronAdjustWillFocusRequested += HandleCauldronAdjustWillFocusRequested;
            GameEventManager.OnCauldronSuppressWillRequested   += HandleCauldronSuppressWillRequested;
            GameEventManager.OnCauldronCrushStoneRequested     += HandleCauldronCrushStoneRequested;

            Debug.Log("[GuRefinementService] Initialized. Alchemical Cauldron ready.");
        }

        public void Dispose()
        {
            GameEventManager.OnOpenCauldronRequested           -= HandleOpenCauldronRequested;
            GameEventManager.OnCloseCauldronRequested          -= HandleCloseCauldronRequested;
            GameEventManager.OnInitiateRefinementRequested     -= HandleInitiateRefinementRequested;
            GameEventManager.OnRefineGuRequested               -= HandleRefineGuRequested;
            GameEventManager.OnCauldronPulseHeatRequested      -= HandleCauldronPulseHeatRequested;
            GameEventManager.OnCauldronAdjustWillFocusRequested -= HandleCauldronAdjustWillFocusRequested;
            GameEventManager.OnCauldronSuppressWillRequested   -= HandleCauldronSuppressWillRequested;
            GameEventManager.OnCauldronCrushStoneRequested     -= HandleCauldronCrushStoneRequested;

            _isInitialized = false;
        }

        public void BroadcastCurrentState()
        {
            if (!_isInitialized || _context?.Cauldron == null) return;
            GameEventManager.TriggerCauldronStateUpdated(_context.Cauldron);
        }

        private void HandleOpenCauldronRequested()
        {
            if (!_isInitialized || _context == null) return;

            var cState = _context.Cauldron;
            cState.IsActive = true;
            cState.CurrentPhase = RefinementPhase.Idle;
            cState.LastFailureTier = RefinementFailureTier.None;

            if (cState.ActiveRecipe == null)
            {
                cState.ActiveRecipe = new GuRefinementRecipe();
            }

            GameEventManager.TriggerCauldronOpened(cState);
            GameEventManager.TriggerCauldronModalToggled(true);
            Debug.Log("[GuRefinementService] Cauldron opened. Mode: Idle.");
        }

        private void HandleCloseCauldronRequested()
        {
            if (!_isInitialized || _context == null) return;

            var cState = _context.Cauldron;
            cState.IsActive = false;
            cState.CurrentPhase = RefinementPhase.Idle;

            GameEventManager.TriggerCauldronClosed();
            GameEventManager.TriggerCauldronModalToggled(false);
            Debug.Log("[GuRefinementService] Cauldron closed. Handover to Overworld mode.");
        }

        private void HandleRefineGuRequested(string recipeId)
        {
            if (!string.IsNullOrEmpty(recipeId))
            {
                SelectRecipe(recipeId);
            }
            HandleOpenCauldronRequested();
            HandleInitiateRefinementRequested();
        }

        public bool SelectRecipe(string recipeOrResultId)
        {
            if (!_isInitialized || _context?.Cauldron == null) return false;

            if (GuDatabase.TryFindRecipeByResult(recipeOrResultId, out var sourceGu, out var path, out var resultGu) && resultGu != null)
            {
                ApplyRecipeToCauldron(sourceGu, path!, resultGu);
                return true;
            }

            return false;
        }

        public bool SelectRecipeByMaterials(string primaryGuId, string secondaryGuId)
        {
            if (!_isInitialized || _context?.Cauldron == null) return false;

            if (GuDatabase.TryFindRecipe(primaryGuId, secondaryGuId, out var path, out var resultGu) && resultGu != null)
            {
                var sourceGu = GuDatabase.Get(primaryGuId);
                ApplyRecipeToCauldron(sourceGu, path!, resultGu);
                return true;
            }

            return false;
        }

        private void ApplyRecipeToCauldron(GuWormDefinitionDTO? sourceGu, GuRefinementPathDTO path, GuWormDefinitionDTO resultGu)
        {
            if (_context == null) return;

            var recipe = new GuRefinementRecipe
            {
                RecipeName = $"{resultGu.CanonicalName} Refinement ({resultGu.ChineseName}合炼)",
                PrimaryGuID = (path.RequiredMaterials != null && path.RequiredMaterials.Count > 0) ? path.RequiredMaterials[0] : (sourceGu?.Id ?? ""),
                SecondaryGuID = (path.RequiredMaterials != null && path.RequiredMaterials.Count > 1) ? path.RequiredMaterials[1] : "",
                ResultGuID = resultGu.Id,
                ResultGuName = $"{resultGu.CanonicalName} ({resultGu.ChineseName})",
                TargetRank = resultGu.Rank,
                SlotCategory = (GuSlotCategory)resultGu.SlotCategory,
                Diet = resultGu.Diet != null ? (GuDietType)resultGu.Diet.DietType : GuDietType.None,
                BaseEssenceCost = resultGu.BaseBeuCost,
                Phase1Duration = 4.0f + (resultGu.Rank - 1) * 1.5f,
                Phase2Duration = 4.5f + (resultGu.Rank - 1) * 2.0f,
                Phase3Duration = 3.5f + (resultGu.Rank - 1) * 1.5f,
                DecayRateK = 18.0f + (resultGu.Rank - 1) * 5.0f
            };

            _context.Cauldron.ActiveRecipe = recipe;
            GameEventManager.TriggerCauldronStateUpdated(_context.Cauldron);
            Debug.Log($"[GuRefinementService] Configured active cauldron recipe: {recipe.RecipeName} (Target Rank {recipe.TargetRank}).");
        }

        private void HandleInitiateRefinementRequested()
        {
            if (!_isInitialized || _context == null) return;

            if (!CanInitiateRefinement(out string reason))
            {
                Debug.LogWarning($"[GuRefinementService] Initiation blocked: {reason}");
                return;
            }

            var cState = _context.Cauldron;
            cState.IsActive = true;
            cState.CurrentPhase = RefinementPhase.Phase1_Extraction;
            cState.CauldronTemperature = 50f;
            cState.Phase1Timer = 0f;
            cState.OutOfBoundsTimer = 0f;
            cState.LastFailureTier = RefinementFailureTier.None;

            GameEventManager.TriggerCauldronStateUpdated(cState);
            Debug.Log("[GuRefinementService] REFINEMENT INITIATED: Phase 1 (Material Extraction)! Maintain equilibrium [20..80]°C.");
        }

        private void HandleCauldronPulseHeatRequested()
        {
            if (!_isInitialized || _context == null) return;
            var cState = _context.Cauldron;
            if (!cState.IsActive || cState.CurrentPhase != RefinementPhase.Phase1_Extraction) return;

            float essenceCost = 2.5f;
            if (_context.Cultivator.CurrentEssenceSea >= essenceCost)
            {
                _context.Cultivator.CurrentEssenceSea -= essenceCost;
                cState.CauldronTemperature = Mathf.Min(100f, cState.CauldronTemperature + 12f);

                GameEventManager.TriggerPrimevalSeaChanged(_context.Cultivator.CurrentEssenceSea, _context.Cultivator.AptitudePercent);
                GameEventManager.TriggerCauldronStateUpdated(cState);
            }
            else
            {
                Debug.LogWarning("[GuRefinementService] Insufficient Primeval Sea to pulse bellows flame!");
            }
        }

        private void HandleCauldronAdjustWillFocusRequested(float delta)
        {
            if (!_isInitialized || _context == null) return;
            var cState = _context.Cauldron;
            if (!cState.IsActive || cState.CurrentPhase != RefinementPhase.Phase2_WillFusion) return;

            cState.MasterWillFocus = Mathf.Clamp01(cState.MasterWillFocus + delta);
            GameEventManager.TriggerCauldronStateUpdated(cState);
        }

        private void HandleCauldronSuppressWillRequested()
        {
            if (!_isInitialized || _context == null) return;
            var cState = _context.Cauldron;
            if (!cState.IsActive || cState.CurrentPhase != RefinementPhase.Phase2_WillFusion) return;

            float essenceCost = 1.5f;
            if (_context.Cultivator.CurrentEssenceSea >= essenceCost)
            {
                _context.Cultivator.CurrentEssenceSea -= essenceCost;
                float dist = Mathf.Abs(cState.MasterWillFocus - cState.WildWillFrequency);

                if (dist <= 0.16f)
                {
                    cState.WillAlignmentProgress = Mathf.Min(100f, cState.WillAlignmentProgress + 18f);
                    cState.WildWillResistance = Mathf.Max(0f, cState.WildWillResistance - 22f);
                    Debug.Log("[GuRefinementService] Will Strike Direct Hit! Alignment +18%, Resistance -22%.");
                }
                else
                {
                    Debug.LogWarning("[GuRefinementService] Mental strike glanced off! Missed wild frequency.");
                }

                GameEventManager.TriggerPrimevalSeaChanged(_context.Cultivator.CurrentEssenceSea, _context.Cultivator.AptitudePercent);
                GameEventManager.TriggerCauldronStateUpdated(cState);
            }
        }

        private void HandleCauldronCrushStoneRequested()
        {
            if (!_isInitialized || _context == null) return;
            var cState = _context.Cauldron;
            if (!cState.IsActive || cState.CurrentPhase != RefinementPhase.Phase3_Condensation) return;

            if (_context.Cultivator.PrimevalStones > 0)
            {
                _context.Cultivator.PrimevalStones--;
                cState.StonesCrushed++;
                cState.NascentStability = Mathf.Min(100f, cState.NascentStability + 14f);

                GameEventManager.TriggerPrimevalStonesChanged(_context.Cultivator.PrimevalStones);
                GameEventManager.TriggerCauldronStateUpdated(cState);
                Debug.Log($"[GuRefinementService] Crushed Primeval Stone. Stability +14% (Now {cState.NascentStability:F0}%). Remaining: {_context.Cultivator.PrimevalStones}");
            }
            else
            {
                Debug.LogWarning("[GuRefinementService] Out of Primeval Stones to crush!");
            }
        }

        public bool CanInitiateRefinement(out string validationReason)
        {
            validationReason = string.Empty;
            if (!_isInitialized || _context == null)
            {
                validationReason = "Service not initialized.";
                return false;
            }

            var recipe = _context.Cauldron.ActiveRecipe;
            if (recipe == null)
            {
                validationReason = "No active recipe configured.";
                return false;
            }

            var primaryGu = FindGu(recipe.PrimaryGuID);
            var secondaryGu = string.IsNullOrEmpty(recipe.SecondaryGuID) ? null : FindGu(recipe.SecondaryGuID);

            if (primaryGu == null)
            {
                validationReason = $"Missing primary catalyst Gu: {recipe.PrimaryGuID}";
                return false;
            }

            if (!string.IsNullOrEmpty(recipe.SecondaryGuID) && secondaryGu == null)
            {
                validationReason = $"Missing secondary catalyst Gu: {recipe.SecondaryGuID}";
                return false;
            }

            int minStones = _context.Cauldron.CalculateMinStonesRequired();
            if (_context.Cultivator.PrimevalStones < minStones)
            {
                validationReason = $"Insufficient Primeval Stones! Requires {minStones} stones to stabilize Dao marks (have {_context.Cultivator.PrimevalStones}).";
                return false;
            }

            if (_context.Cultivator.CurrentEssenceSea < 25f)
            {
                validationReason = $"Insufficient Primeval Essence sea! Need >= 25% (have {_context.Cultivator.CurrentEssenceSea:F1}%).";
                return false;
            }

            return true;
        }

        public void Tick(float deltaTime)
        {
            if (!_isInitialized || _context == null) return;
            var cState = _context.Cauldron;
            if (!cState.IsActive || cState.CurrentPhase == RefinementPhase.Idle ||
                cState.CurrentPhase == RefinementPhase.Success || cState.CurrentPhase == RefinementPhase.Failure)
            {
                return;
            }

            switch (cState.CurrentPhase)
            {
                case RefinementPhase.Phase1_Extraction:
                    TickPhase1(deltaTime);
                    break;
                case RefinementPhase.Phase2_WillFusion:
                    TickPhase2(deltaTime);
                    break;
                case RefinementPhase.Phase3_Condensation:
                    TickPhase3(deltaTime);
                    break;
            }
        }

        private void TickPhase1(float dt)
        {
            if (_context == null) return;
            var cState = _context.Cauldron;
            var recipe = cState.ActiveRecipe;

            cState.CauldronTemperature -= 15f * dt;
            cState.Phase1Timer += dt;

            if (cState.CauldronTemperature < 20f || cState.CauldronTemperature > 80f)
            {
                cState.OutOfBoundsTimer += dt;
                if (cState.OutOfBoundsTimer > 1.5f)
                {
                    ResolveFailure(RefinementFailureTier.Tier1_CleanFail, "Cauldron temperature destabilized! Thermal equilibrium collapsed.");
                    return;
                }
            }
            else
            {
                cState.OutOfBoundsTimer = Mathf.Max(0f, cState.OutOfBoundsTimer - dt);
            }

            if (cState.Phase1Timer >= recipe.Phase1Duration)
            {
                cState.CurrentPhase = RefinementPhase.Phase2_WillFusion;
                cState.Phase2Timer = 0f;
                cState.WildWillResistance = 50f;
                cState.WillAlignmentProgress = 0f;
                cState.MasterWillFocus = 0.5f;
                cState.WildWillFrequency = 0.35f + (float)Random.Shared.NextDouble() * 0.30f;
                cState.WildNeedleMovingRight = true;

                GameEventManager.TriggerCauldronStateUpdated(cState);
                Debug.Log("[GuRefinementService] Phase 1 Complete! Advancing to Phase 2: Will Fusion!");
            }
        }

        private void TickPhase2(float dt)
        {
            if (_context == null) return;
            var cState = _context.Cauldron;
            var recipe = cState.ActiveRecipe;

            cState.Phase2Timer += dt;

            float wildSpeed = 1.2f * dt;
            if (cState.WildNeedleMovingRight)
            {
                cState.WildWillFrequency += wildSpeed;
                if (cState.WildWillFrequency >= 0.85f) cState.WildNeedleMovingRight = false;
            }
            else
            {
                cState.WildWillFrequency -= wildSpeed;
                if (cState.WildWillFrequency <= 0.15f) cState.WildNeedleMovingRight = true;
            }

            float dist = Mathf.Abs(cState.MasterWillFocus - cState.WildWillFrequency);
            if (dist <= 0.14f)
            {
                cState.WillAlignmentProgress += 32f * dt;
                cState.WildWillResistance = Mathf.Max(0f, cState.WildWillResistance - 20f * dt);
            }
            else
            {
                cState.WildWillResistance += 12f * dt;
            }

            if (cState.WildWillResistance >= 100f)
            {
                ResolveFailure(RefinementFailureTier.Tier3_CatastrophicBacklash, "Wild catalyst will overpowered master's soul! Psychic blowout!");
                return;
            }

            if (cState.Phase2Timer >= recipe.Phase2Duration)
            {
                if (cState.WillAlignmentProgress >= 50f)
                {
                    cState.CurrentPhase = RefinementPhase.Phase3_Condensation;
                    cState.Phase3Timer = 0f;
                    cState.NascentStability = 100f;
                    cState.StonesCrushed = 0;

                    GameEventManager.TriggerCauldronStateUpdated(cState);
                    Debug.Log("[GuRefinementService] Phase 2 Complete! Advancing to Phase 3: Rapid Condensation!");
                }
                else
                {
                    ResolveFailure(RefinementFailureTier.Tier2_MaterialRupture, "Will alignment failed to reach 50% threshold in time! Crucible ruptured.");
                }
            }
        }

        private void TickPhase3(float dt)
        {
            if (_context == null) return;
            var cState = _context.Cauldron;
            var recipe = cState.ActiveRecipe;

            cState.Phase3Timer += dt;

            float decay = recipe.DecayRateK * recipe.TargetRank * dt;
            cState.NascentStability -= decay;

            if (cState.NascentStability <= 0f)
            {
                ResolveFailure(RefinementFailureTier.Tier2_MaterialRupture, "Nascent Dao marks starved of primeval essence and collapsed.");
                return;
            }

            if (cState.Phase3Timer >= recipe.Phase3Duration)
            {
                ResolveSuccess();
            }
        }

        private void ResolveSuccess()
        {
            if (_context == null) return;
            var cState = _context.Cauldron;
            var recipe = cState.ActiveRecipe;
            cState.CurrentPhase = RefinementPhase.Success;

            if (!string.IsNullOrEmpty(recipe.SecondaryGuID))
            {
                var secGu = FindGu(recipe.SecondaryGuID);
                if (secGu != null)
                {
                    RemoveGu(secGu);
                }
            }

            var priGu = FindGu(recipe.PrimaryGuID);
            if (priGu != null)
            {
                priGu.GuID = recipe.ResultGuID;
                priGu.Name = recipe.ResultGuName;
                priGu.Rank = recipe.TargetRank;
                priGu.Slot = recipe.SlotCategory;
                priGu.Diet = recipe.Diet;
                priGu.BaseEssenceCost = recipe.BaseEssenceCost;
                priGu.Satiety = 100f;
                priGu.IsInTraumaSleep = false;
            }

            GameEventManager.TriggerGuInventoryUpdated(_context.GuInventory.EquippedGu, _context.GuInventory.ReserveGu);
            if (priGu != null) GameEventManager.TriggerCauldronSuccess(priGu);
            GameEventManager.TriggerCauldronStateUpdated(cState);

            Debug.Log($"★★★ [REFINEMENT SUCCESS] Refined Rank {recipe.TargetRank} [{recipe.ResultGuName}]! Used {cState.StonesCrushed} stones during condensation. ★★★");
        }

        private void ResolveFailure(RefinementFailureTier tier, string reason)
        {
            if (_context == null) return;
            var cState = _context.Cauldron;
            var recipe = cState.ActiveRecipe;
            cState.CurrentPhase = RefinementPhase.Failure;
            cState.LastFailureTier = tier;

            Debug.LogError($"[REFINEMENT FAILED - {tier}] {reason}");

            var secGu = FindGu(recipe.SecondaryGuID);
            if (secGu != null)
            {
                RemoveGu(secGu);
                Debug.LogWarning($"[Auxiliary Fragility] Secondary Gu '{secGu.Name}' was vaporized to ash!");
            }

            var priGu = FindGu(recipe.PrimaryGuID);
            if (priGu != null)
            {
                if (priGu.IsVitalGu)
                {
                    priGu.Satiety = 0f;
                    priGu.IsInTraumaSleep = true;
                    Debug.LogWarning($"[Vital Gu Safeguard] Vital Gu '{priGu.Name}' resisted destruction! Entered Trauma Sleep.");

                    switch (tier)
                    {
                        case RefinementFailureTier.Tier1_CleanFail:
                            break;
                        case RefinementFailureTier.Tier2_MaterialRupture:
                            _context.Cultivator.CurrentEssenceSea = Mathf.Max(0f, _context.Cultivator.CurrentEssenceSea - 20f);
                            Debug.LogWarning("[Somatic Recoil] Light aperture shock: -20% Primeval Sea!");
                            break;
                        case RefinementFailureTier.Tier3_CatastrophicBacklash:
                            _context.Cultivator.CurrentEssenceSea = 0f;
                            Debug.LogError("[Somatic Explosion] Catastrophic aperture rupture! Primeval Sea drained to 0%!");
                            break;
                    }
                }
                else
                {
                    if (tier >= RefinementFailureTier.Tier2_MaterialRupture)
                    {
                        RemoveGu(priGu);
                        Debug.LogError($"[Non-Vital Vaporization] Primary Gu '{priGu.Name}' was destroyed permanently!");
                    }
                }
            }

            GameEventManager.TriggerGuInventoryUpdated(_context.GuInventory.EquippedGu, _context.GuInventory.ReserveGu);
            GameEventManager.TriggerPrimevalSeaChanged(_context.Cultivator.CurrentEssenceSea, _context.Cultivator.AptitudePercent);
            GameEventManager.TriggerCauldronFailed(tier, reason);
            GameEventManager.TriggerCauldronStateUpdated(cState);
        }

        private GuWormData? FindGu(string guId)
        {
            if (string.IsNullOrEmpty(guId) || _context == null) return null;

            string normalizedId = guId.Replace("_r1", "_01").Replace("_r2", "_02").Replace("_r3", "_03");

            var gu = _context.GuInventory.EquippedGu.Find(g => 
                string.Equals(g.GuID, guId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(g.GuID, normalizedId, StringComparison.OrdinalIgnoreCase) ||
                (g.Name != null && g.Name.IndexOf(guId, StringComparison.OrdinalIgnoreCase) >= 0));

            if (gu == null)
            {
                gu = _context.GuInventory.ReserveGu.Find(g => 
                    string.Equals(g.GuID, guId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(g.GuID, normalizedId, StringComparison.OrdinalIgnoreCase) ||
                    (g.Name != null && g.Name.IndexOf(guId, StringComparison.OrdinalIgnoreCase) >= 0));
            }
            return gu;
        }

        private void RemoveGu(GuWormData gu)
        {
            if (_context == null) return;
            if (_context.GuInventory.EquippedGu.Contains(gu))
            {
                _context.GuInventory.EquippedGu.Remove(gu);
            }
            else if (_context.GuInventory.ReserveGu.Contains(gu))
            {
                _context.GuInventory.ReserveGu.Remove(gu);
            }
        }
    }
}
