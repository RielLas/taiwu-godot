using System;
using System.Collections.Generic;
using TaiwuGu.Map;
using TaiwuGu.Psychology;
using TaiwuGu.Combat;

namespace TaiwuGu.Core
{
    /// <summary>
    /// Discrete procedural Halal acoustic sound types for physics and foley playback.
    /// Strictly excludes musical instruments (strings, flutes, bells, singing bowls).
    /// </summary>
    public enum HalalSoundType
    {
        Footstep,
        ParchmentRustle,
        BladeSlash,
        BluntImpact,
        DashWhoosh,
        CauldronFire,
        StoneShatter,
        CauldronSteam,
        WoodenKnock
    }

    /// <summary>
    /// Master Event Bus under Constitutional Law 1: The Observer Mandate.
    /// Strict Command-Event Split (CQRS-Lite).
    /// </summary>
    public static class GameEventManager
    {
        // =========================================================================
        // 1. COMMAND / INTENT EVENTS (UI/Input/Presentation -> Domain Services)
        // =========================================================================

        public static event Action<Vector2Int>? OnMoveRequested;
        public static event Action? OnAdvanceSolarTermRequested;
        public static event Action<string, GuDietType, int>? OnFeedGuRequested;
        public static event Action<string>? OnRefineGuRequested;
        public static event Action? OnOpenCauldronRequested;
        public static event Action? OnCloseCauldronRequested;
        public static event Action? OnInitiateRefinementRequested;
        public static event Action? OnCauldronPulseHeatRequested;
        public static event Action<float>? OnCauldronAdjustWillFocusRequested;
        public static event Action? OnCauldronSuppressWillRequested;
        public static event Action? OnCauldronCrushStoneRequested;
        public static event Action? OnConsumeStoneRequested;
        public static event Action? OnNurtureSubstageRequested;
        public static event Action? OnBreakthroughMajorRankRequested;
        public static event Action? OnMeditateRequested;
        public static event Action? OnWashApertureWallRequested;
        public static event Action? OnBatterApertureWallRequested;
        public static event Action? OnOpenBreakthroughMinigameRequested;
        public static event Action<float>? OnReleaseEssenceWaveRequested;
        public static event Action? OnBreakthroughCrushStoneRequested;
        public static event Action? OnBreakthroughUseRelicGuRequested;
        public static event Action? OnCloseBreakthroughRequested;
        public static event Action<int>? OnDeductActionPointsRequested;
        public static event Action<Vector2Int, CrimeTamperChoice>? OnTamperCrimeEvidenceRequested;
        public static event Action<TilePOIType, Vector2Int, string>? OnPOIActionRequested;
        public static event Action<Vector2Int>? OnInspectTileRequested;
        public static event Action<string>? OnInteractWithNPCRequested;
        public static event Action<string>? OnNPCSparRequested;
        public static event Action<string>? OnNPCBarterRequested;
        public static event Action<string>? OnNPCExtortRequested;
        public static event Action<string>? OnNPCAmbushRequested;
        public static event Action? OnCloseNPCDialogueRequested;
        public static event Action<string>? OnSaveGameRequested;
        public static event Action<string>? OnLoadGameRequested;
        public static event Action<WorldRegionID, bool>? OnRegionTransitRequested;
        public static event Action<SouthernMountainID>? OnMountainTransitRequested;
        public static event Action<bool>? OnOpenTransitModalRequested;
        public static event Action? OnCloseTransitModalRequested;
        public static event Action<string>? OnDeleteSaveRequested;
        public static event Action<string, string>? OnShowOnWorkNoticeRequested;

        // Wild Organism Will Subjugation Intents
        public static event Action<WildOrganismData>? OnOpenWildGuEncounterRequested;
        public static event Action<float>? OnSetEncounterNeedleRequested;
        public static event Action<float>? OnAdjustEncounterNeedleRequested;
        public static event Action? OnEncounterPulseEssenceRequested;
        public static event Action? OnEncounterInstantSubjugateRequested;
        public static event Action? OnCloseWildGuEncounterRequested;

        // Hardware Senses & Navigation Intents
        public static event Action<Vector2>? OnPanRequested;
        public static event Action<float>? OnZoomRequested;
        public static event Action<Vector2>? OnWorldClickRequested;
        public static event Action? OnEscapeRequested;
        public static bool EscapeHandled { get; set; }
        public static event Action? OnLedgerToggleRequested;
        public static event Action? OnRecenterCameraRequested;

        // Combat Arena Senses & Intents
        public static event Action<Vector2>? OnCombatMoveRequested;
        public static event Action<Vector2>? OnCombatDashRequested;
        public static event Action<int>? OnCombatTriggerGuSlotRequested;
        public static event Action<int>? OnCombatTriggerKillerMoveRequested;
        public static event Action<Vector2>? OnCombatAttackRequested;
        public static event Action<Vector2>? OnCombatSecondaryAttackRequested;
        public static event Action<ArenaEncounterType, Vector2Int>? OnEnterCombatRequested;
        public static event Action? OnExitCombatRequested;

        // High-Level UI State Intents
        public static event Action<bool>? OnPauseToggled;
        public static event Action<bool>? OnUIModalActive;
        public static event Action<bool>? OnBattleStageStateChanged;
        public static event Action<bool>? OnLedgerToggled;
        public static event Action<bool>? OnPOIModalToggled;
        public static event Action<bool>? OnCauldronModalToggled;

        /// <summary>The cultivator's display name, so views never seed a placeholder identity.</summary>
        public static event Action<string>? OnCultivatorIdentityChanged;
        public static event Action? OnBeginJourneyRequested;
        public static event Action? OnReturnToTitleRequested;

        // Halal Acoustic Foley & Audio Intents
        public static event Action<HalalSoundType>? OnPlaySoundRequested;

        // Centralized LIFO Modal Stack Events
        public static event Action<IModalView>? OnModalOpened;
        public static event Action<IModalView>? OnModalClosed;

        // =========================================================================
        // 2. STATE CHANGED EVENTS (Domain Services -> Presentation Observers)
        // =========================================================================

        public static event Action<ArenaEncounterType, string, float>? OnCombatStarted;
        public static event Action<bool, Vector2Int, string, ArenaEncounterType>? OnCombatConcluded;
        public static event Action<string, Vector2Int, CrimeSignature, bool>? OnVictimExecuted;
        public static event Action<Vector2Int, int>? OnCrimeScenePresent;

        public static event Action<int, int>? OnActionPointsChanged;
        public static event Action<int, SolarTerm, string>? OnSolarTermAdvanced;
        public static event Action<float, float>? OnPrimevalSeaChanged;
        public static event Action<int>? OnPrimevalStonesChanged;
        public static event Action<int, int, string>? OnCultivatorCultivationChanged;
        public static event Action<float, float, float, string>? OnApertureWallStateChanged;
        public static event Action<int>? OnHumanPathDaoMarksChanged;
        public static event Action<int, int, int>? OnLifespanChanged;
        public static event Action<string, bool, float>? OnGuFed;
        public static event Action<string, GuStarvationStage>? OnGuStarvationChanged;
        public static event Action<List<GuWormData>, List<GuWormData>>? OnGuInventoryUpdated;
        public static event Action<WorldRegionID, string>? OnRegionChanged;
        public static event Action<Vector2Int>? OnPlayerMoved;
        public static event Action<BiomeType, TilePOIType>? OnTileStepped;
        public static event Action<Vector2Int, int>? OnFogOfWarUpdated;
        public static event Action<Vector2Int?, List<Vector2Int>>? OnTargetTileSelected;
        public static event Action<TilePOIType, Vector2Int, string, string, int>? OnPOIArrived;
        public static event Action<string, string, string, Vector2Int>? OnTileInspected;
        public static event Action<Vector2Int, List<CultivatorEntityData>>? OnTileCultivatorsUpdated;
        public static event Action? OnCultivatorsWandered;
        public static event Action<List<Vector2Int>>? OnCultivatorMapTokensUpdated;
        public static event Action<CultivatorEntityData, string>? OnNPCDialogueOpened;
        public static event Action<string>? OnNPCDialogueFeedback;
        public static event Action? OnNPCDialogueClosed;
        public static event Action<FactionID, int, int>? OnFactionReputationChanged;
        public static event Action<Vector2Int>? OnCrimeSceneUpdated;
        public static event Action<CauldronRefinementState>? OnCauldronOpened;
        public static event Action? OnCauldronClosed;
        public static event Action<CauldronRefinementState>? OnCauldronStateUpdated;
        public static event Action<GuWormData>? OnCauldronSuccess;
        public static event Action<RefinementFailureTier, string>? OnCauldronFailed;

        // Aperture Major Realm Breakthrough Events
        public static event Action<BreakthroughSessionSnapshot>? OnBreakthroughSessionOpened;
        public static event Action? OnBreakthroughSessionClosed;
        public static event Action<BreakthroughSessionSnapshot>? OnBreakthroughStateUpdated;
        public static event Action<bool, bool, float>? OnBreakthroughWaveImpact;
        public static event Action<BreakthroughState, int>? OnBreakthroughResolved;

        public static event Action<string, bool>? OnGameSaved;
        public static event Action<string, bool>? OnGameLoaded;
        public static event Action<string, bool>? OnSaveDeleted;

        // Wild Organism Will Subjugation Events
        public static event Action<SubjugationSessionSnapshot>? OnWildGuEncounterOpened;
        public static event Action<SubjugationSessionSnapshot>? OnWildGuEncounterStateUpdated;
        public static event Action<SubjugationState, GuWormData?, string>? OnWildGuEncounterResolved;
        public static event Action? OnWildGuEncounterClosed;

        // Exploration Hazard & Ambush Events
        public static event Action<Vector2Int, string, int, float>? OnTerrainHazardTriggered;
        public static event Action<Vector2Int, string>? OnWildBeastAmbushed;

        // Sector Transit Events
        public static event Action<SouthernMountainID, string>? OnMountainChanged;
        public static event Action<bool, bool>? OnTransitModalToggled;
        public static event Action<ArenaEncounterType, GridCoord>? OnRoadAmbushOccurred;

        // =========================================================================
        // 3. COMMAND TRIGGERS (Dispatched by UI & Input)
        // =========================================================================

        public static void RaiseRequestMove(Vector2Int targetGrid)                        => OnMoveRequested?.Invoke(targetGrid);
        public static void RaiseRequestAdvanceSolarTerm()                                  => OnAdvanceSolarTermRequested?.Invoke();
        public static void RaiseRequestFeedGu(string guId, GuDietType diet, int qty = 1)   => OnFeedGuRequested?.Invoke(guId, diet, qty);
        public static void RaiseRequestRefineGu(string recipeId)                          => OnRefineGuRequested?.Invoke(recipeId);
        public static void RaiseRequestOpenCauldron()                                     => OnOpenCauldronRequested?.Invoke();
        public static void RaiseRequestCloseCauldron()                                    => OnCloseCauldronRequested?.Invoke();
        public static void RaiseRequestInitiateRefinement()                               => OnInitiateRefinementRequested?.Invoke();
        public static void RaiseRequestCauldronPulseHeat()                                => OnCauldronPulseHeatRequested?.Invoke();
        public static void RaiseRequestCauldronAdjustWillFocus(float delta)               => OnCauldronAdjustWillFocusRequested?.Invoke(delta);
        public static void RaiseRequestCauldronSuppressWill()                             => OnCauldronSuppressWillRequested?.Invoke();
        public static void RaiseRequestCauldronCrushStone()                               => OnCauldronCrushStoneRequested?.Invoke();
        public static void RaiseRequestConsumeStone()                                     => OnConsumeStoneRequested?.Invoke();
        public static void RaiseRequestNurtureSubstage()                                  => OnNurtureSubstageRequested?.Invoke();
        public static void RaiseRequestBreakthroughMajorRank()                             => OnBreakthroughMajorRankRequested?.Invoke();
        public static void RaiseRequestMeditate()                                         => OnMeditateRequested?.Invoke();
        public static void RaiseRequestWashApertureWall()                                 => OnWashApertureWallRequested?.Invoke();
        public static void RaiseRequestBatterApertureWall()                               => OnBatterApertureWallRequested?.Invoke();
        public static void RaiseRequestOpenBreakthroughMinigame()                         => OnOpenBreakthroughMinigameRequested?.Invoke();
        public static void RaiseRequestReleaseEssenceWave(float wavePower = 1.0f)        => OnReleaseEssenceWaveRequested?.Invoke(wavePower);
        public static void RaiseRequestBreakthroughCrushStone()                           => OnBreakthroughCrushStoneRequested?.Invoke();
        public static void RaiseRequestBreakthroughUseRelicGu()                           => OnBreakthroughUseRelicGuRequested?.Invoke();
        public static void RaiseRequestCloseBreakthrough()                               => OnCloseBreakthroughRequested?.Invoke();
        public static void RaiseRequestDeductActionPoints(int apCost)                    => OnDeductActionPointsRequested?.Invoke(apCost);
        public static void RaiseRequestTamperCrimeEvidence(Vector2Int pos, CrimeTamperChoice choice) => OnTamperCrimeEvidenceRequested?.Invoke(pos, choice);
        public static void RaiseRequestPOIAction(TilePOIType poi, Vector2Int pos, string actionId)    => OnPOIActionRequested?.Invoke(poi, pos, actionId);
        public static void RaiseRequestInspectTile(Vector2Int pos)                                     => OnInspectTileRequested?.Invoke(pos);
        public static void RaiseRequestInteractWithNPC(string npcEntityId)                => OnInteractWithNPCRequested?.Invoke(npcEntityId);
        public static void RaiseRequestNPCSpar(string npcEntityId)                       => OnNPCSparRequested?.Invoke(npcEntityId);
        public static void RaiseRequestNPCBarter(string npcEntityId)                     => OnNPCBarterRequested?.Invoke(npcEntityId);
        public static void RaiseRequestNPCExtort(string npcEntityId)                     => OnNPCExtortRequested?.Invoke(npcEntityId);
        public static void RaiseRequestNPCAmbush(string npcEntityId)                     => OnNPCAmbushRequested?.Invoke(npcEntityId);
        public static void RaiseRequestCloseNPCDialogue()                                => OnCloseNPCDialogueRequested?.Invoke();
        public static void RaiseRequestSaveGame(string slotId)                            => OnSaveGameRequested?.Invoke(slotId);
        public static void RaiseRequestLoadGame(string slotId)                            => OnLoadGameRequested?.Invoke(slotId);
        public static void RaiseRequestDeleteSave(string slotId)                          => OnDeleteSaveRequested?.Invoke(slotId);
        public static void RaiseRequestRegionTransit(WorldRegionID targetRegion, bool sealAperture) => OnRegionTransitRequested?.Invoke(targetRegion, sealAperture);
        public static void RaiseRequestShowOnWorkNotice(string title, string detail)     => OnShowOnWorkNoticeRequested?.Invoke(title, detail);
        public static void RaiseRequestOpenWildGuEncounter(WildOrganismData organism)      => OnOpenWildGuEncounterRequested?.Invoke(organism);
        public static void RaiseRequestSetEncounterNeedle(float pos)                      => OnSetEncounterNeedleRequested?.Invoke(pos);
        public static void RaiseRequestAdjustEncounterNeedle(float delta)                 => OnAdjustEncounterNeedleRequested?.Invoke(delta);
        public static void RaiseRequestEncounterPulseEssence()                            => OnEncounterPulseEssenceRequested?.Invoke();
        public static void RaiseRequestEncounterInstantSubjugate()                        => OnEncounterInstantSubjugateRequested?.Invoke();
        public static void RaiseRequestCloseWildGuEncounter()                             => OnCloseWildGuEncounterRequested?.Invoke();
        public static void RaiseRequestMountainTransit(SouthernMountainID targetMountain)  => OnMountainTransitRequested?.Invoke(targetMountain);
        public static void RaiseRequestOpenTransitModal(bool regionalMode = false)        => OnOpenTransitModalRequested?.Invoke(regionalMode);
        public static void RaiseRequestCloseTransitModal()                                => OnCloseTransitModalRequested?.Invoke();

        public static void RaiseRequestCombatMove(Vector2 dir)                            => OnCombatMoveRequested?.Invoke(dir);
        public static void RaiseRequestCombatDash(Vector2 dir)                            => OnCombatDashRequested?.Invoke(dir);
        public static void RaiseRequestCombatTriggerGuSlot(int slotIndex)                 => OnCombatTriggerGuSlotRequested?.Invoke(slotIndex);
        public static void RaiseRequestCombatTriggerKillerMove(int moveIndex)             => OnCombatTriggerKillerMoveRequested?.Invoke(moveIndex);
        public static void RaiseRequestCombatAttack(Vector2 screenPos)                    => OnCombatAttackRequested?.Invoke(screenPos);
        public static void RaiseRequestCombatSecondaryAttack(Vector2 screenPos)           => OnCombatSecondaryAttackRequested?.Invoke(screenPos);
        public static void RaiseRequestEnterCombat(ArenaEncounterType enc, Vector2Int pos) => OnEnterCombatRequested?.Invoke(enc, pos);
        public static void RaiseRequestExitCombat()                                       => OnExitCombatRequested?.Invoke();
        public static void RaiseRequestPlaySound(HalalSoundType sound)                     => OnPlaySoundRequested?.Invoke(sound);
        public static void RaiseModalOpened(IModalView modal)                              => OnModalOpened?.Invoke(modal);
        public static void RaiseModalClosed(IModalView modal)                              => OnModalClosed?.Invoke(modal);

        public static void TriggerPan(Vector2 screenDelta)                                 => OnPanRequested?.Invoke(screenDelta);
        public static void TriggerZoom(float delta)                                       => OnZoomRequested?.Invoke(delta);
        public static void TriggerWorldClick(Vector2 screenPos)                           => OnWorldClickRequested?.Invoke(screenPos);
        public static void TriggerEscape()
        {
            EscapeHandled = false;
            OnEscapeRequested?.Invoke();
        }
        public static void TriggerLedgerToggle()                                          => OnLedgerToggleRequested?.Invoke();
        public static void TriggerRecenterCamera()                                        => OnRecenterCameraRequested?.Invoke();
        public static void TriggerPauseToggled(bool isPaused)                             => OnPauseToggled?.Invoke(isPaused);
        public static void TriggerUIModal(bool isActive)                                   => OnUIModalActive?.Invoke(isActive);
        public static void TriggerUIModalActive(bool isActive)                             => OnUIModalActive?.Invoke(isActive);
        public static void TriggerBattleStageStateChanged(bool inBattle)                  => OnBattleStageStateChanged?.Invoke(inBattle);
        public static void TriggerLedgerToggled(bool isOpen)                              => OnLedgerToggled?.Invoke(isOpen);
        public static void TriggerPOIModalToggled(bool isOpen)                           => OnPOIModalToggled?.Invoke(isOpen);
        public static void TriggerCauldronModalToggled(bool isOpen)                       => OnCauldronModalToggled?.Invoke(isOpen);
        public static void TriggerCultivatorIdentityChanged(string cultivatorName)        => OnCultivatorIdentityChanged?.Invoke(cultivatorName);
        public static void TriggerBeginJourney()                                          => OnBeginJourneyRequested?.Invoke();
        public static void TriggerReturnToTitle()                                         => OnReturnToTitleRequested?.Invoke();

        // =========================================================================
        // 4. STATE CHANGED TRIGGERS (Dispatched by Domain Services)
        // =========================================================================

        public static void TriggerCombatStarted(ArenaEncounterType enc, string enemy, float hp) => OnCombatStarted?.Invoke(enc, enemy, hp);
        public static void TriggerCombatConcluded(bool won, Vector2Int pos, string enemy, ArenaEncounterType enc) => OnCombatConcluded?.Invoke(won, pos, enemy, enc);
        public static void TriggerVictimExecuted(string victim, Vector2Int pos, CrimeSignature sig, bool det) => OnVictimExecuted?.Invoke(victim, pos, sig, det);
        public static void TriggerCrimeScenePresent(Vector2Int pos, int terms)            => OnCrimeScenePresent?.Invoke(pos, terms);

        public static void TriggerActionPointsChanged(int currentAP, int maxBudget)        => OnActionPointsChanged?.Invoke(currentAP, maxBudget);
        public static void TriggerSolarTermAdvanced(int year, SolarTerm term, string name) => OnSolarTermAdvanced?.Invoke(year, term, name);
        public static void TriggerPrimevalSeaChanged(float currentSea, float maxCapacity)  => OnPrimevalSeaChanged?.Invoke(currentSea, maxCapacity);
        public static void TriggerPrimevalStonesChanged(int stones)                       => OnPrimevalStonesChanged?.Invoke(stones);
        public static void TriggerCultivatorCultivationChanged(int rank, int substage, string name) => OnCultivatorCultivationChanged?.Invoke(rank, substage, name);
        public static void TriggerApertureWallStateChanged(float washingProgress, float durability, float maxDurability, string membraneName) => OnApertureWallStateChanged?.Invoke(washingProgress, durability, maxDurability, membraneName);
        public static void TriggerHumanPathDaoMarksChanged(int marks)                     => OnHumanPathDaoMarksChanged?.Invoke(marks);
        public static void TriggerLifespanChanged(int age, int lifespanYears, int remainingYears) => OnLifespanChanged?.Invoke(age, lifespanYears, remainingYears);
        public static void TriggerGuFed(string guId, bool wasFed, float newSatiety)       => OnGuFed?.Invoke(guId, wasFed, newSatiety);
        public static void TriggerGuStarvationChanged(string guId, GuStarvationStage st)   => OnGuStarvationChanged?.Invoke(guId, st);
        public static void TriggerGuInventoryUpdated(List<GuWormData> equipped, List<GuWormData>? reserve = null) => OnGuInventoryUpdated?.Invoke(equipped, reserve ?? new List<GuWormData>());
        public static void TriggerPlayerMoved(Vector2Int newGridPos)                      => OnPlayerMoved?.Invoke(newGridPos);
        public static void TriggerTileStepped(BiomeType biome, TilePOIType poi)           => OnTileStepped?.Invoke(biome, poi);
        public static void TriggerFogOfWarUpdated(Vector2Int playerPos, int radius)       => OnFogOfWarUpdated?.Invoke(playerPos, radius);
        public static void TriggerTargetTileSelected(Vector2Int? targetPos, List<Vector2Int> path) => OnTargetTileSelected?.Invoke(targetPos, path);
        public static void TriggerPOIArrived(TilePOIType poi, Vector2Int pos, string t, string d, int r) => OnPOIArrived?.Invoke(poi, pos, t, d, r);
        public static void TriggerTileInspected(string title, string seal, string lore, Vector2Int pos) => OnTileInspected?.Invoke(title, seal, lore, pos);
        public static void TriggerTileCultivatorsUpdated(Vector2Int pos, List<CultivatorEntityData> cultivators) => OnTileCultivatorsUpdated?.Invoke(pos, cultivators);
        public static void TriggerCultivatorsWandered()                                   => OnCultivatorsWandered?.Invoke();
        public static void TriggerCultivatorMapTokensUpdated(List<Vector2Int> occupiedTiles) => OnCultivatorMapTokensUpdated?.Invoke(occupiedTiles);
        public static void TriggerNPCDialogueOpened(CultivatorEntityData npc, string greeting) => OnNPCDialogueOpened?.Invoke(npc, greeting);
        public static void TriggerNPCDialogueFeedback(string feedback)                   => OnNPCDialogueFeedback?.Invoke(feedback);
        public static void TriggerNPCDialogueClosed()                                     => OnNPCDialogueClosed?.Invoke();
        public static void TriggerCrimeSceneUpdated(Vector2Int pos)                       => OnCrimeSceneUpdated?.Invoke(pos);
        public static void TriggerFactionReputationChanged(FactionID faction, int newScore, int delta) => OnFactionReputationChanged?.Invoke(faction, newScore, delta);
        public static void TriggerCauldronOpened(CauldronRefinementState state)            => OnCauldronOpened?.Invoke(state);
        public static void TriggerCauldronClosed()                                         => OnCauldronClosed?.Invoke();
        public static void TriggerCauldronStateUpdated(CauldronRefinementState state)     => OnCauldronStateUpdated?.Invoke(state);
        public static void TriggerCauldronSuccess(GuWormData refinedGu)                   => OnCauldronSuccess?.Invoke(refinedGu);
        public static void TriggerCauldronFailed(RefinementFailureTier tier, string msg)  => OnCauldronFailed?.Invoke(tier, msg);

        public static void TriggerBreakthroughSessionOpened(BreakthroughSessionSnapshot snapshot) => OnBreakthroughSessionOpened?.Invoke(snapshot);
        public static void TriggerBreakthroughSessionClosed()                                    => OnBreakthroughSessionClosed?.Invoke();
        public static void TriggerBreakthroughStateUpdated(BreakthroughSessionSnapshot snapshot) => OnBreakthroughStateUpdated?.Invoke(snapshot);
        public static void TriggerBreakthroughWaveImpact(bool isHarmonic, bool isDispersion, float damage) => OnBreakthroughWaveImpact?.Invoke(isHarmonic, isDispersion, damage);
        public static void TriggerBreakthroughResolved(BreakthroughState state, int newRank)    => OnBreakthroughResolved?.Invoke(state, newRank);

        public static void TriggerGameSaved(string slotId, bool success)                  => OnGameSaved?.Invoke(slotId, success);
        public static void TriggerGameLoaded(string slotId, bool success)                 => OnGameLoaded?.Invoke(slotId, success);
        public static void TriggerSaveDeleted(string slotId, bool success)                => OnSaveDeleted?.Invoke(slotId, success);
        public static void TriggerRegionChanged(WorldRegionID region, string regionName)  => OnRegionChanged?.Invoke(region, regionName);

        public static void TriggerWildGuEncounterOpened(SubjugationSessionSnapshot snapshot) => OnWildGuEncounterOpened?.Invoke(snapshot);
        public static void TriggerWildGuEncounterStateUpdated(SubjugationSessionSnapshot snapshot) => OnWildGuEncounterStateUpdated?.Invoke(snapshot);
        public static void TriggerWildGuEncounterResolved(SubjugationState state, GuWormData? gu, string msg) => OnWildGuEncounterResolved?.Invoke(state, gu, msg);
        public static void TriggerWildGuEncounterClosed() => OnWildGuEncounterClosed?.Invoke();
        public static void TriggerTerrainHazardTriggered(Vector2Int pos, string name, int hpDamage, float seaDamage) => OnTerrainHazardTriggered?.Invoke(pos, name, hpDamage, seaDamage);
        public static void TriggerWildBeastAmbushed(Vector2Int pos, string beastName) => OnWildBeastAmbushed?.Invoke(pos, beastName);
        public static void TriggerMountainChanged(SouthernMountainID target, string name) => OnMountainChanged?.Invoke(target, name);
        public static void TriggerTransitModalToggled(bool isOpen, bool isRegional)       => OnTransitModalToggled?.Invoke(isOpen, isRegional);
        public static void TriggerRoadAmbushOccurred(ArenaEncounterType enc, GridCoord pos) => OnRoadAmbushOccurred?.Invoke(enc, pos);

        // =========================================================================
        // 5. LIFECYCLE MANAGEMENT & LEAK PREVENTION
        // =========================================================================

        /// <summary>
        /// Clears all multicast delegate invocation lists across all 66 static events.
        /// </summary>
        public static void ClearAllSubscriptions()
        {
            // --- 1. Command / Intent Events (38 events) ---
            OnMoveRequested = null;
            OnAdvanceSolarTermRequested = null;
            OnFeedGuRequested = null;
            OnRefineGuRequested = null;
            OnOpenCauldronRequested = null;
            OnCloseCauldronRequested = null;
            OnInitiateRefinementRequested = null;
            OnCauldronPulseHeatRequested = null;
            OnCauldronAdjustWillFocusRequested = null;
            OnCauldronSuppressWillRequested = null;
            OnCauldronCrushStoneRequested = null;
            OnConsumeStoneRequested = null;
            OnNurtureSubstageRequested = null;
            OnBreakthroughMajorRankRequested = null;
            OnMeditateRequested = null;
            OnWashApertureWallRequested = null;
            OnBatterApertureWallRequested = null;
            OnOpenBreakthroughMinigameRequested = null;
            OnReleaseEssenceWaveRequested = null;
            OnBreakthroughCrushStoneRequested = null;
            OnBreakthroughUseRelicGuRequested = null;
            OnCloseBreakthroughRequested = null;
            OnDeductActionPointsRequested = null;
            OnTamperCrimeEvidenceRequested = null;
            OnPOIActionRequested = null;
            OnInspectTileRequested = null;
            OnInteractWithNPCRequested = null;
            OnNPCSparRequested = null;
            OnNPCBarterRequested = null;
            OnNPCExtortRequested = null;
            OnNPCAmbushRequested = null;
            OnCloseNPCDialogueRequested = null;
            OnSaveGameRequested = null;
            OnLoadGameRequested = null;
            OnDeleteSaveRequested = null;
            OnRegionTransitRequested = null;
            OnMountainTransitRequested = null;
            OnOpenTransitModalRequested = null;
            OnCloseTransitModalRequested = null;
            OnShowOnWorkNoticeRequested = null;
            OnOpenWildGuEncounterRequested = null;
            OnSetEncounterNeedleRequested = null;
            OnAdjustEncounterNeedleRequested = null;
            OnEncounterPulseEssenceRequested = null;
            OnEncounterInstantSubjugateRequested = null;
            OnCloseWildGuEncounterRequested = null;

            OnPanRequested = null;
            OnZoomRequested = null;
            OnWorldClickRequested = null;
            OnEscapeRequested = null;
            EscapeHandled = false;
            OnLedgerToggleRequested = null;
            OnRecenterCameraRequested = null;

            OnCombatMoveRequested = null;
            OnCombatDashRequested = null;
            OnCombatTriggerGuSlotRequested = null;
            OnCombatTriggerKillerMoveRequested = null;
            OnCombatAttackRequested = null;
            OnCombatSecondaryAttackRequested = null;
            OnEnterCombatRequested = null;
            OnExitCombatRequested = null;

            OnPauseToggled = null;
            OnUIModalActive = null;
            OnBattleStageStateChanged = null;
            OnLedgerToggled = null;
            OnPOIModalToggled = null;
            OnCauldronModalToggled = null;
            OnCultivatorIdentityChanged = null;
            OnBeginJourneyRequested = null;
            OnReturnToTitleRequested = null;

            // --- 2. State Changed Events (28 events) ---
            OnCombatStarted = null;
            OnCombatConcluded = null;
            OnVictimExecuted = null;
            OnCrimeScenePresent = null;

            OnActionPointsChanged = null;
            OnSolarTermAdvanced = null;
            OnPrimevalSeaChanged = null;
            OnPrimevalStonesChanged = null;
            OnCultivatorCultivationChanged = null;
            OnApertureWallStateChanged = null;
            OnHumanPathDaoMarksChanged = null;
            OnLifespanChanged = null;
            OnGuFed = null;
            OnGuStarvationChanged = null;
            OnGuInventoryUpdated = null;
            OnRegionChanged = null;
            OnPlayerMoved = null;
            OnTileStepped = null;
            OnFogOfWarUpdated = null;
            OnTargetTileSelected = null;
            OnPOIArrived = null;
            OnTileInspected = null;
            OnTileCultivatorsUpdated = null;
            OnCultivatorsWandered = null;
            OnCultivatorMapTokensUpdated = null;
            OnNPCDialogueOpened = null;
            OnNPCDialogueFeedback = null;
            OnNPCDialogueClosed = null;
            OnFactionReputationChanged = null;
            OnCrimeSceneUpdated = null;
            OnCauldronOpened = null;
            OnCauldronClosed = null;
            OnCauldronStateUpdated = null;
            OnCauldronSuccess = null;
            OnCauldronFailed = null;
            OnBreakthroughSessionOpened = null;
            OnBreakthroughSessionClosed = null;
            OnBreakthroughStateUpdated = null;
            OnBreakthroughWaveImpact = null;
            OnBreakthroughResolved = null;
            OnGameSaved = null;
            OnGameLoaded = null;
            OnSaveDeleted = null;
            OnWildGuEncounterOpened = null;
            OnWildGuEncounterStateUpdated = null;
            OnWildGuEncounterResolved = null;
            OnWildGuEncounterClosed = null;
            OnTerrainHazardTriggered = null;
            OnWildBeastAmbushed = null;
            OnMountainChanged = null;
            OnTransitModalToggled = null;
            OnRoadAmbushOccurred = null;
            OnPlaySoundRequested = null;
            OnModalOpened = null;
            OnModalClosed = null;
        }
    }
}
