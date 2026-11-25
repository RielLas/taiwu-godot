using System;
using TaiwuGu.Core;
using TaiwuGu.Entities;

namespace TaiwuGu.Systems
{
    /// <summary>
    /// Stateless ECS System governing the 3-Phase Alchemical Gu Refinement Cauldron.
    /// </summary>
    public static class RefinementSystem
    {
        public const float MAX_TICK_SECONDS = 0.1f;

        public static CauldronSessionComponent GetOrCreateSession(WorldStore world)
        {
            var session = world.Get<CauldronSessionComponent>(world.PlayerEntity);
            if (session == null)
            {
                session = world.Set(world.PlayerEntity, new CauldronSessionComponent());
            }
            return session;
        }

        public static void OpenCauldron(WorldStore world)
        {
            var cState = GetOrCreateSession(world);
            cState.IsActive = true;
            cState.CurrentPhase = RefinementPhase.Idle;
            cState.LastFailureTier = RefinementFailureTier.None;
            cState.ActiveRecipe ??= new GuRefinementRecipe();

            WorldSignals.EmitUIModalActive(true);
            WorldSignals.EmitCauldronModalToggled(true);
            WorldSignals.EmitTopics(DirtyTopic.Cauldron);
        }

        public static void CloseCauldron(WorldStore world)
        {
            var cState = GetOrCreateSession(world);
            if (cState.IsActive &&
                (cState.CurrentPhase == RefinementPhase.Phase2_WillFusion ||
                 cState.CurrentPhase == RefinementPhase.Phase3_Condensation))
            {
                ResolveFailure(
                    world,
                    cState,
                    RefinementFailureTier.Tier2_MaterialRupture,
                    "Cauldron aborted mid-fusion! Unstable essence ruptured the crucible.");
            }
            else
            {
                cState.CurrentPhase = RefinementPhase.Idle;
            }

            cState.IsActive = false;

            WorldSignals.EmitUIModalActive(false);
            WorldSignals.EmitCauldronModalToggled(false);
            WorldSignals.EmitTopics(DirtyTopic.Cauldron);
        }

        public static bool SelectRecipe(WorldStore world, string recipeOrResultId)
        {
            if (GuDatabase.TryFindRecipeByResult(recipeOrResultId, out var sourceGu, out var path, out var resultGu) && resultGu != null)
            {
                ApplyRecipe(world, sourceGu, path!, resultGu);
                return true;
            }
            return false;
        }

        public static bool SelectRecipeByMaterials(WorldStore world, string primaryGuId, string secondaryGuId)
        {
            if (GuDatabase.TryFindRecipe(primaryGuId, secondaryGuId, out var path, out var resultGu) && resultGu != null)
            {
                ApplyRecipe(world, GuDatabase.Get(primaryGuId), path!, resultGu);
                return true;
            }
            return false;
        }

        private static void ApplyRecipe(WorldStore world, GuWormDefinitionDTO? sourceGu, GuRefinementPathDTO path, GuWormDefinitionDTO resultGu)
        {
            var cState = GetOrCreateSession(world);
            float baseRate = path.BaseSuccessRate > 0f ? Math.Clamp(path.BaseSuccessRate, 0.1f, 1.0f) : 0.85f;
            float decayScale = Math.Clamp(1.25f - baseRate * 0.35f, 0.85f, 1.40f);
            cState.ActiveRecipe = new GuRefinementRecipe
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
                BaseSuccessRate = baseRate,
                Phase1Duration = 4.0f + (resultGu.Rank - 1) * 1.5f,
                Phase2Duration = 4.5f + (resultGu.Rank - 1) * 2.0f,
                Phase3Duration = 3.5f + (resultGu.Rank - 1) * 1.5f,
                DecayRateK = (18.0f + (resultGu.Rank - 1) * 5.0f) * decayScale
            };
            WorldSignals.EmitTopics(DirtyTopic.Cauldron);
        }

        public static bool CanInitiateRefinement(WorldStore world, out string validationReason)
        {
            validationReason = string.Empty;
            var cState = GetOrCreateSession(world);
            var recipe = cState.ActiveRecipe;
            if (recipe == null)
            {
                validationReason = "No active recipe configured.";
                return false;
            }

            if (GuEcologySystem.FindOwnedGuEntity(world, world.PlayerEntity, recipe.PrimaryGuID) == null)
            {
                validationReason = $"Missing primary catalyst Gu: {recipe.PrimaryGuID}";
                return false;
            }

            if (!string.IsNullOrEmpty(recipe.SecondaryGuID))
            {
                EntityId? secId = GuEcologySystem.FindOwnedGuEntity(world, world.PlayerEntity, recipe.SecondaryGuID);
                if (secId == null)
                {
                    validationReason = $"Missing secondary catalyst Gu: {recipe.SecondaryGuID}";
                    return false;
                }
                if (world.TryGet<GuWormComponent>(secId.Value, out var secGu) && secGu.IsVitalGu)
                {
                    validationReason = "Cannot consume your Vital Gu as a secondary catalyst.";
                    return false;
                }
            }

            var cult = world.Get<CultivatorComponent>(world.PlayerEntity);
            var aperture = world.Get<ApertureComponent>(world.PlayerEntity);
            if (cult == null || aperture == null)
            {
                validationReason = "Missing cultivator.";
                return false;
            }

            int minStones = cState.CalculateMinStonesRequired();
            if (cult.PrimevalStones < minStones)
            {
                validationReason = $"Insufficient Primeval Stones! Requires {minStones} stones (have {cult.PrimevalStones}).";
                return false;
            }

            if (aperture.CurrentEssenceSea < 25f)
            {
                validationReason = $"Insufficient Primeval Essence sea! Need >= 25% (have {aperture.CurrentEssenceSea:F1}%).";
                return false;
            }

            return true;
        }

        public static bool InitiateRefinement(WorldStore world, string? recipeResultId = null)
        {
            if (!string.IsNullOrEmpty(recipeResultId)) SelectRecipe(world, recipeResultId);

            var cState = GetOrCreateSession(world);
            if (!CanInitiateRefinement(world, out string reason))
            {
                Debug.LogWarning($"[RefinementSystem] Initiation blocked: {reason}");
                return false;
            }

            cState.IsActive = true;
            cState.CurrentPhase = RefinementPhase.Phase1_Extraction;
            cState.CauldronTemperature = 50f;
            cState.Phase1Timer = 0f;
            cState.OutOfBoundsTimer = 0f;
            cState.LastFailureTier = RefinementFailureTier.None;

            WorldSignals.EmitTopics(DirtyTopic.Cauldron);
            return true;
        }

        public static void PulseHeat(WorldStore world)
        {
            var cState = GetOrCreateSession(world);
            if (!cState.IsActive || cState.CurrentPhase != RefinementPhase.Phase1_Extraction) return;
            var aperture = world.Get<ApertureComponent>(world.PlayerEntity);
            if (aperture == null || aperture.CurrentEssenceSea < 2.5f) return;

            aperture.CurrentEssenceSea -= 2.5f;
            cState.CauldronTemperature = MathF.Min(100f, cState.CauldronTemperature + 12f);

            WorldSignals.EmitSound(HalalSoundType.CauldronFire);
            WorldSignals.EmitTopics(DirtyTopic.Aperture | DirtyTopic.Cauldron);
        }

        public static void AdjustWillFocus(WorldStore world, float delta)
        {
            var cState = GetOrCreateSession(world);
            if (!cState.IsActive || cState.CurrentPhase != RefinementPhase.Phase2_WillFusion) return;
            cState.MasterWillFocus = Math.Clamp(cState.MasterWillFocus + delta, 0f, 1f);
            WorldSignals.EmitTopics(DirtyTopic.Cauldron);
        }

        public static void SuppressWill(WorldStore world)
        {
            var cState = GetOrCreateSession(world);
            if (!cState.IsActive || cState.CurrentPhase != RefinementPhase.Phase2_WillFusion) return;
            var aperture = world.Get<ApertureComponent>(world.PlayerEntity);
            if (aperture == null || aperture.CurrentEssenceSea < 1.5f) return;

            aperture.CurrentEssenceSea -= 1.5f;
            float dist = MathF.Abs(cState.MasterWillFocus - cState.WildWillFrequency);
            if (dist <= 0.16f)
            {
                cState.WillAlignmentProgress = MathF.Min(100f, cState.WillAlignmentProgress + 18f);
                cState.WildWillResistance = MathF.Max(0f, cState.WildWillResistance - 22f);
            }

            WorldSignals.EmitSound(HalalSoundType.CauldronSteam);
            WorldSignals.EmitTopics(DirtyTopic.Aperture | DirtyTopic.Cauldron);
        }

        public static void CrushStone(WorldStore world)
        {
            var cState = GetOrCreateSession(world);
            if (!cState.IsActive || cState.CurrentPhase != RefinementPhase.Phase3_Condensation) return;
            var cult = world.Get<CultivatorComponent>(world.PlayerEntity);
            if (cult == null || cult.PrimevalStones <= 0) return;

            cult.PrimevalStones--;
            cState.StonesCrushed++;
            cState.NascentStability = MathF.Min(100f, cState.NascentStability + 14f);

            WorldSignals.EmitSound(HalalSoundType.StoneShatter);
            WorldSignals.EmitTopics(DirtyTopic.Cultivator | DirtyTopic.Cauldron);
        }

        public static void Tick(WorldStore world, float deltaTime, IRandomSource? rngOverride = null)
        {
            float dt = MathF.Min(deltaTime, MAX_TICK_SECONDS);
            if (dt <= 0f) return;

            var cState = world.Get<CauldronSessionComponent>(world.PlayerEntity);
            if (cState == null || !cState.IsActive ||
                cState.CurrentPhase == RefinementPhase.Idle ||
                cState.CurrentPhase == RefinementPhase.Success ||
                cState.CurrentPhase == RefinementPhase.Failure)
                return;

            switch (cState.CurrentPhase)
            {
                case RefinementPhase.Phase1_Extraction:
                    TickPhase1(world, cState, dt, rngOverride);
                    break;
                case RefinementPhase.Phase2_WillFusion:
                    TickPhase2(world, cState, dt);
                    break;
                case RefinementPhase.Phase3_Condensation:
                    TickPhase3(world, cState, dt);
                    break;
            }
        }

        private static void TickPhase1(WorldStore world, CauldronSessionComponent cState, float dt, IRandomSource? rngOverride)
        {
            var recipe = cState.ActiveRecipe;
            cState.CauldronTemperature -= 15f * dt;
            cState.Phase1Timer += dt;

            if (cState.CauldronTemperature < 20f || cState.CauldronTemperature > 80f)
            {
                cState.OutOfBoundsTimer += dt;
                if (cState.OutOfBoundsTimer > 1.5f)
                {
                    ResolveFailure(world, cState, RefinementFailureTier.Tier1_CleanFail, "Cauldron temperature destabilized! Thermal equilibrium collapsed.");
                    return;
                }
            }
            else
            {
                cState.OutOfBoundsTimer = MathF.Max(0f, cState.OutOfBoundsTimer - dt);
            }

            if (cState.Phase1Timer >= recipe.Phase1Duration)
            {
                var clock = world.GetSingleton<WorldClockComponent>();
                int phase1Seed = unchecked(
                    world.RunSeed ^
                    ((clock?.CurrentYear ?? 1) * 10007) ^
                    ((clock?.CurrentSolarTermIndex ?? 1) * 397) ^
                    ((clock?.ActionPointsRemaining ?? 0) * 31));
                IRandomSource rng = rngOverride ?? new SeededRandom(phase1Seed);
                float baseRate = recipe.BaseSuccessRate > 0f ? Math.Clamp(recipe.BaseSuccessRate, 0.1f, 1.0f) : 0.85f;
                cState.CurrentPhase = RefinementPhase.Phase2_WillFusion;
                cState.Phase2Timer = 0f;
                cState.WildWillResistance = 50f + (1.0f - baseRate) * 20f;
                cState.WillAlignmentProgress = 0f;
                cState.MasterWillFocus = 0.5f;
                cState.WildWillFrequency = 0.35f + (float)rng.NextDouble() * 0.30f;
                cState.WildNeedleMovingRight = true;
            }

            WorldSignals.EmitTopics(DirtyTopic.Cauldron);
        }

        private static void TickPhase2(WorldStore world, CauldronSessionComponent cState, float dt)
        {
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

            float dist = MathF.Abs(cState.MasterWillFocus - cState.WildWillFrequency);
            if (dist <= 0.14f)
            {
                cState.WillAlignmentProgress += 32f * dt;
                cState.WildWillResistance = MathF.Max(0f, cState.WildWillResistance - 20f * dt);
            }
            else
            {
                cState.WildWillResistance += 12f * dt;
            }

            if (cState.WildWillResistance >= 100f)
            {
                ResolveFailure(world, cState, RefinementFailureTier.Tier3_CatastrophicBacklash, "Wild catalyst will overpowered master's soul! Psychic blowout!");
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
                }
                else
                {
                    ResolveFailure(world, cState, RefinementFailureTier.Tier2_MaterialRupture, "Will alignment failed to reach 50% threshold in time! Crucible ruptured.");
                    return;
                }
            }

            WorldSignals.EmitTopics(DirtyTopic.Cauldron);
        }

        private static void TickPhase3(WorldStore world, CauldronSessionComponent cState, float dt)
        {
            var recipe = cState.ActiveRecipe;
            cState.Phase3Timer += dt;
            cState.NascentStability -= recipe.DecayRateK * recipe.TargetRank * dt;

            if (cState.NascentStability <= 0f)
            {
                ResolveFailure(world, cState, RefinementFailureTier.Tier2_MaterialRupture, "Nascent Dao marks starved of primeval essence and collapsed.");
                return;
            }

            if (cState.Phase3Timer >= recipe.Phase3Duration)
            {
                ResolveSuccess(world, cState);
                return;
            }

            WorldSignals.EmitTopics(DirtyTopic.Cauldron);
        }

        private static void ResolveSuccess(WorldStore world, CauldronSessionComponent cState)
        {
            var recipe = cState.ActiveRecipe;
            cState.CurrentPhase = RefinementPhase.Success;

            if (!string.IsNullOrEmpty(recipe.SecondaryGuID))
            {
                EntityId? secId = GuEcologySystem.FindOwnedGuEntity(world, world.PlayerEntity, recipe.SecondaryGuID);
                if (secId.HasValue && (!world.TryGet<GuWormComponent>(secId.Value, out var secGu) || !secGu.IsVitalGu))
                {
                    world.DestroyEntity(secId.Value);
                }
            }

            EntityId? priId = GuEcologySystem.FindOwnedGuEntity(world, world.PlayerEntity, recipe.PrimaryGuID);
            GuWormComponent? priGu = priId.HasValue ? world.Get<GuWormComponent>(priId.Value) : null;
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

            WorldSignals.EmitRefinementResolved(priGu, RefinementFailureTier.None, recipe.ResultGuName);
            WorldSignals.EmitTopics(DirtyTopic.Inventory | DirtyTopic.Cauldron);
        }

        private static void ResolveFailure(WorldStore world, CauldronSessionComponent cState, RefinementFailureTier tier, string reason)
        {
            var recipe = cState.ActiveRecipe;
            cState.CurrentPhase = RefinementPhase.Failure;
            cState.LastFailureTier = tier;

            if (tier >= RefinementFailureTier.Tier2_MaterialRupture && !string.IsNullOrEmpty(recipe.SecondaryGuID))
            {
                EntityId? secId = GuEcologySystem.FindOwnedGuEntity(world, world.PlayerEntity, recipe.SecondaryGuID);
                if (secId.HasValue && (!world.TryGet<GuWormComponent>(secId.Value, out var secGu) || !secGu.IsVitalGu))
                {
                    world.DestroyEntity(secId.Value);
                }
            }

            EntityId? priId = GuEcologySystem.FindOwnedGuEntity(world, world.PlayerEntity, recipe.PrimaryGuID);
            var priGu = priId.HasValue ? world.Get<GuWormComponent>(priId.Value) : null;
            var aperture = world.Get<ApertureComponent>(world.PlayerEntity);

            if (priGu != null)
            {
                if (priGu.IsVitalGu)
                {
                    priGu.Satiety = 0f;
                    priGu.IsInTraumaSleep = true;
                    if (aperture != null)
                    {
                        if (tier == RefinementFailureTier.Tier2_MaterialRupture)
                            aperture.CurrentEssenceSea = MathF.Max(0f, aperture.CurrentEssenceSea - 20f);
                        else if (tier == RefinementFailureTier.Tier3_CatastrophicBacklash)
                            aperture.CurrentEssenceSea = 0f;
                    }
                }
                else if (tier >= RefinementFailureTier.Tier2_MaterialRupture && priId.HasValue)
                {
                    world.DestroyEntity(priId.Value);
                }
            }

            WorldSignals.EmitRefinementResolved(null, tier, reason);
            WorldSignals.EmitTopics(DirtyTopic.Inventory | DirtyTopic.Aperture | DirtyTopic.Cauldron);
        }
    }
}
