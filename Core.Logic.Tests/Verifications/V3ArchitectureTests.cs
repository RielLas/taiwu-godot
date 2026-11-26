#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using TaiwuGu.Director;
using TaiwuGu.Gu;
using TaiwuGu.Meta;
using TaiwuGu.Tags;
using TaiwuGu.World;
using Xunit;

namespace Core.Logic.Tests.Verifications;

public sealed class V3ArchitectureTests
{
    [Fact]
    public void UnifiedPredicateEvaluator_BlocksStarvingDetoxGu_AllowsFedDetoxGu()
    {
        var reg = new DaoTagRegistry();
        var miasmaRule = new PredicateRule(
            AllOf: new[]
            {
                new PredicateClause(PredicateOp.AnyFedGuHasTag, reg.Detox, 1),
                new PredicateClause(PredicateOp.HungerBelow, reg.Detox, 60)
            },
            AnyOf: Array.Empty<PredicateClause>());

        var terrain = new TerrainRuleProfile(reg.MiasmaDense, "Miasma_Dense", miasmaRule, true, "gate_requires_fed_detox_gu");

        var starvingCtx = new WorldQueryCtx(
            daoMarks: Array.Empty<DaoTagWeight>(),
            activeGu: new[] { new GuSnapshot(1, HungerPercent: 85, PenaltyThresholdPercent: 60, IsDormant: false, DaoTagVector.Single(reg.Detox, 2)) },
            primevalStones: 100, rank: 2, bountyHeat: 0);

        var fedCtx = new WorldQueryCtx(
            daoMarks: Array.Empty<DaoTagWeight>(),
            activeGu: new[] { new GuSnapshot(1, HungerPercent: 15, PenaltyThresholdPercent: 60, IsDormant: false, DaoTagVector.Single(reg.Detox, 2)) },
            primevalStones: 100, rank: 2, bountyHeat: 0);

        Assert.False(terrain.IsPassable(in starvingCtx));
        Assert.True(terrain.IsPassable(in fedCtx));
    }

    [Fact]
    public void SynthesisResolver_MatchesBlueprintAndComputesDynamicFrictionBacklash()
    {
        var reg = new DaoTagRegistry();
        var blueprints = new[]
        {
            new SynthesisBlueprint("bp_detox", "gu_jade_detox_r2", new[] { new DaoTagWeight(reg.Wood, 3), new DaoTagWeight(reg.Detox, 2) }, 2, 0.90f)
        };

        var cleanInputs = new[] { new DaoTagWeight(reg.Wood, 3), new DaoTagWeight(reg.Detox, 2) };
        var ok = SynthesisResolver.Resolve(cleanInputs, totalRank: 3, blueprints, reg, new Random(42));
        Assert.True(ok.MatchedBlueprint);
        Assert.True(ok.Succeeded);
        Assert.Equal("gu_jade_detox_r2", ok.OutputGuId);

        // Conflicting Water + Fire + Detox + Poison without blueprint => dynamic friction backlash, never rejected
        var chaoticInputs = new[] { new DaoTagWeight(reg.Water, 3), new DaoTagWeight(reg.Fire, 3), new DaoTagWeight(reg.Detox, 2), new DaoTagWeight(reg.Poison, 2) };
        var backlash = SynthesisResolver.Resolve(chaoticInputs, totalRank: 3, blueprints, reg, new Random(42));
        Assert.False(backlash.MatchedBlueprint);
        Assert.True(backlash.FrictionScore > 0.8f);
        Assert.True(backlash.BacklashSeaCost > 0 && backlash.BacklashHpCost > 0);
    }

    [Fact]
    public void MetabolismEngine_CannibalizeAndHibernateBehaviorsWorkFromDataProfiles()
    {
        var profiles = new Dictionary<string, MetabolismProfile>
        {
            ["hib"] = new("hib", 0.30f, 0.05f, 0.60f, StarveBehavior.Hibernate, 1),
            ["can"] = new("can", 0.30f, 0.05f, 0.60f, StarveBehavior.CannibalizeAperture, 4)
        };

        var roster = new[]
        {
            new GuMetabolismState { GuId = "gu_predator", ProfileId = "can", HungerRatio = 0.55f, Rank = 2 },
            new GuMetabolismState { GuId = "gu_prey", ProfileId = "hib", HungerRatio = 0.55f, Rank = 1 }
        };

        MetabolismEngine.TickMonth(roster, profiles, out _, out _, out int cannibalized, out _);
        Assert.Equal(1, cannibalized);
        Assert.True(roster[1].IsDestroyed);
        Assert.True(roster[0].HungerRatio < 0.5f);
    }

    [Fact]
    public void DataOnlyMockGu_CompilesIntoKillerMoveWithZeroAllocationsInHotLoop()
    {
        var reg = new DaoTagRegistry();
        ushort frostTag = reg.Register("MockFrostPath");
        reg.SetConflictWeight(frostTag, reg.Fire, 0.50f);

        ReadOnlySpan<ModifierNodeSpec> nodes = stackalloc ModifierNodeSpec[]
        {
            new(NodeRole.Delivery, ModifierOpCode.NovaAura, 0, 0, 1.2f),
            new(NodeRole.Payload, ModifierOpCode.ConvertDamageTag, frostTag, 0, 0.25f),
            new(NodeRole.CostMitigator, ModifierOpCode.ScaleSeaCost, 0, 0, 0.85f)
        };
        ReadOnlySpan<DaoTagWeight> tags = stackalloc DaoTagWeight[]
        {
            new(frostTag, 3),
            new(reg.Fire, 2)
        };

        // Warmup JIT
        var compiled = KillerMovePipelineCompiler.Compile(nodes, tags, reg);
        Assert.Equal(ModifierOpCode.NovaAura, compiled.Delivery);
        Assert.Equal(frostTag, compiled.DamageTagId);
        Assert.True(compiled.BacklashDamage > 0);

        long before = GC.GetTotalAllocatedBytes(precise: true);
        for (int tick = 0; tick < 600; tick++)
            _ = KillerMovePipelineCompiler.Compile(nodes, tags, reg);
        long after = GC.GetTotalAllocatedBytes(precise: true);

        Assert.Equal(0, after - before);
    }

    [Fact]
    public void WorldPressureDirector_SwapsBiomeDeckWithZeroEngineEdits()
    {
        var director = new WorldPressureDirector(initialTension: 30f);
        string npDeckJson = """{"schemaVersion":1,"deckId":"northern_plains_fixture","events":[{"eventId":"np_wolf","deckId":"northern_plains_fixture","title":"Wolves","vignette":"Steppe","minPressure":0,"tensionCost":5,"eligibility":{"allOf":[],"anyOf":[]},"choices":[{"label":"Fight","gate":{"allOf":[],"anyOf":[]},"successProb":1.0,"stoneDelta":10,"hpDelta":0,"seaDelta":0,"bountyDelta":0,"grantGuId":"","daoTagGain":0,"daoMarkDelta":1}]}]}""";
        director.LoadDeckFromJson(npDeckJson);

        var vec = new WorldPressureVector(PlayerNetWorth: 200, TotalDaoMarks: 10, BountyHeat: 5, AvgGuHunger: 20f);
        var ctx = new WorldQueryCtx(Array.Empty<DaoTagWeight>(), Array.Empty<GuSnapshot>(), 200, 1, 5);

        Assert.True(director.TrySelectEvent(in vec, in ctx, new Random(7), out var chosen));
        Assert.Equal("northern_plains_fixture", chosen.DeckId);
        Assert.Equal("np_wolf", chosen.EventId);
    }

    [Fact]
    public void LegacyArchive_AtomicSaveAndReload_CarriesOverToRun2()
    {
        string tmpFile = Path.Combine(Path.GetTempPath(), $"taiwu_legacy_{Guid.NewGuid():N}.json");
        try
        {
            var snap = new LegacyRunSnapshot(
                PeakRank: 3, TotalDaoMarks: 30, MonthsSurvived: 9, PeakNetWorth: 1800, ClearedTrial: true,
                DiscoveredBlueprints: new[] { "bp_jade_detox_gu" },
                RunDaoTagWeights: new[] { new DaoTagWeight(1, 12) });

            var arc = LegacyArchive.ExtractFromRun(null, snap);
            LegacyArchive.SaveAtomic(tmpFile, arc);

            var reloaded = LegacyArchive.LoadAtomic(tmpFile);
            Assert.Contains("bp_jade_detox_gu", reloaded.DiscoveredSynthesisBlueprints);
            Assert.Contains("caravan_heir", reloaded.UnlockedOriginIds);
            Assert.Contains("cicada_vessel", reloaded.UnlockedOriginIds);

            var run2Cfg = new NewRunMetaConfig();
            LegacyArchive.ApplyToNewRunConfig(reloaded, run2Cfg, requestedOrigin: "caravan_heir", spendFragments: 4);
            Assert.Equal("caravan_heir", run2Cfg.SelectedOriginId);
            Assert.True(run2Cfg.StartingBonusStones >= 180);
            Assert.Contains("bp_jade_detox_gu", run2Cfg.KnownBlueprintIds);
        }
        finally
        {
            if (File.Exists(tmpFile)) File.Delete(tmpFile);
            if (File.Exists(tmpFile + ".bak")) File.Delete(tmpFile + ".bak");
        }
    }
}
