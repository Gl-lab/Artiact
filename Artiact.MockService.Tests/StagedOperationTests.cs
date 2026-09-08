using System.Diagnostics;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json;
using Artiact;
using Artiact.Client;
using Artiact.Contracts.Client;
using Artiact.Services;
using Artiact.Services.Combat;
using Artiact.Services.Operation;
using Artiact.Services.Strategy;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Artiact.MockService.Tests;

public class StagedOperationTests(Xunit.Abstractions.ITestOutputHelper output)
{
    private static string SkillSnapshot(System.Text.Json.JsonElement character) => System.Text.Json.JsonSerializer.Serialize(character.EnumerateObject()
        .Where(x => x.Name is "level" or "xp" || x.Name.EndsWith("_level", StringComparison.Ordinal) ||
            x.Name.EndsWith("_xp", StringComparison.Ordinal) && !x.Name.EndsWith("_max_xp", StringComparison.Ordinal)).ToDictionary(x => x.Name, x => x.Value));
    [Theory]
    [InlineData("recovery-training", 20, 112)]
    [InlineData("consumable-production", 9, 49)]
    [InlineData("consumable-bank", 3, 13)]
    public async Task RecoveryComparisonsReplayUsefulHpAgainstBothBaselines(string scenario, int actions, long seconds)
    {
        async Task<(int Actions, long Seconds, int Hp, string Stock, int Moves, int Switches)> Execute(string strategy)
        {
            await using var h = new Harness(new(), miningOnly: true); await h.Reset(scenario);
            var policy = strategy == "auto" ? RecoveryProfile() : FoodPolicy(scenario == "recovery-training");
            var saved = new Checkpoint();
            var limits = new StrategyLimits(NoProgress: 60);
            var run = strategy == "lowest" ? h.LowestSession(policy, limits) : h.Factory.Create(policy, limits, saved, "recovery-compare");
            StrategyDecision? result = null; int moves = 0; var candidates = new List<string?>();
            for (int i = 0; i < 100; i++)
            {
                result = await run.TickAsync();
                if (result.Command is not null) candidates.Add(result.Candidate);
                if (result.Command?.StartsWith("Move:", StringComparison.Ordinal) == true) moves++;
                if (run.State!.Character.GetProperty("hp").GetInt32() == 20 || result.Status is StrategyStatus.Blocked or StrategyStatus.Stopped or StrategyStatus.Completed) break;
            }
            var inventory = CharacterObservation.Read(run.State!.Character)!.Inventory;
            Assert.Equal(1, inventory.GetValueOrDefault("protected"));
            int switches = candidates.Zip(candidates.Skip(1)).Count(x => x.First != x.Second);
            output.WriteLine($"METRIC {scenario}/{strategy}: actions={result!.Attempts}, cooldown={result.CooldownSeconds}, moves={moves}, switches={switches}, hp={run.State.Character.GetProperty("hp")}");
            output.WriteLine($"SKILLS {scenario}/{strategy}: {SkillSnapshot(run.State.Character)}");
            return (result.Attempts, result.CooldownSeconds, run.State.Character.GetProperty("hp").GetInt32(),
                System.Text.Json.JsonSerializer.Serialize(inventory.OrderBy(x => x.Key)) + System.Text.Json.JsonSerializer.Serialize(run.State.Bank?.Items.OrderBy(x => x.Key)), moves, switches);
        }
        var auto = await Execute("auto"); var fixedRun = await Execute("fixed"); var lowest = await Execute("lowest");
        Assert.Equal((actions, seconds, 20), (auto.Actions, auto.Seconds, auto.Hp));
        Assert.Equal((scenario == "consumable-bank" ? 5 : actions, scenario == "consumable-bank" ? 19L : seconds, 20), (fixedRun.Actions, fixedRun.Seconds, fixedRun.Hp));
        Assert.Equal(4, lowest.Hp);
        Assert.True(auto.Moves <= fixedRun.Moves); Assert.True(auto.Switches <= fixedRun.Switches);
        Assert.Equal(auto, await Execute("auto")); Assert.Equal(fixedRun, await Execute("fixed")); Assert.Equal(lowest, await Execute("lowest"));
    }
    private static PortfolioPolicy RecoveryProfile(bool craft = true, bool rest = false) => new PortfolioSettings
    {
        AutonomousGoals = true,
        BankRetain = FoodPolicy().Bank!.Retain.ToDictionary(x => x.Key, x => x.Value),
        Recovery = new(AllowUse: true, AllowCraft: craft, AllowRest: rest, AllowBankWithdrawal: true)
    }.Policy();

    [Fact]
    public async Task RecoveryNeedTrainsBothSkillsProducesFoodAndStopsSupplyAtFullHp()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("recovery-training");
        var policy = RecoveryProfile(); var saved = new Checkpoint(); var commands = new List<string?>();
        StrategySession? run = null; StrategyDecision? decision = null;
        for (int i = 0; i < 60; i++)
        {
            run = h.Factory.Create(policy, new(NoProgress: 60), saved, "recovery-training");
            decision = await run.TickAsync(); commands.Add(decision.Command);
            Assert.True(decision.Status == StrategyStatus.Selected, decision.Reason + ":" + string.Join(',', decision.Candidates.Select(x => x.Rejection)));
            if (run.State!.Character.GetProperty("hp").GetInt32() == 20) break;
        }
        Assert.Equal(20, run!.State!.Character.GetProperty("hp").GetInt32());
        Assert.Equal(3, run.State.Character.GetProperty("cooking_level").GetInt32());
        Assert.Equal(4, run.State.Character.GetProperty("fishing_level").GetInt32());
        var inventory = CharacterObservation.Read(run.State.Character)!.Inventory;
        Assert.Equal(1, inventory.GetValueOrDefault("protected")); Assert.Equal(2, inventory.GetValueOrDefault("baitfish"));
        Assert.Equal(2, inventory.GetValueOrDefault("snack")); Assert.Equal(0, inventory.GetValueOrDefault("meal"));
        Assert.Equal(20, decision!.Attempts); Assert.Equal(112, decision.CooldownSeconds);
        Assert.Equal(2, run.State.Context.Used["recovery:use"]); Assert.Equal(4, run.State.Context.Used["recovery:materials"]);
        Assert.DoesNotContain("Fight", commands); Assert.DoesNotContain(commands, x => x?.StartsWith("Craft:tool", StringComparison.Ordinal) == true);
        var next = await h.Factory.Create(policy, new(NoProgress: 60), saved, "recovery-training").InspectAsync();
        Assert.DoesNotContain(next.Candidates, x => x.Category == "recovery");
    }

    [Theory]
    [InlineData("consumable-bank", false, 3, 13)]
    [InlineData("consumable-production", true, 9, 49)]
    [InlineData("consumable-capacity", true, 9, 49)]
    public async Task RecoveryUsesBankOrOneFiniteProductionChainWithoutRefill(string scenario, bool craft, int actions, int seconds)
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset(scenario);
        var policy = RecoveryProfile(craft); var saved = new Checkpoint(); var commands = new List<string?>();
        StrategySession? run = null; StrategyDecision? result = null;
        for (int i = 0; i < 30; i++)
        {
            run = h.Factory.Create(policy, new(NoProgress: 60), saved, "recovery"); result = await run.TickAsync(); commands.Add(result.Command);
            Assert.True(result.Status == StrategyStatus.Selected, result.Reason + ":" + string.Join(',', result.Candidates.Select(x => x.Rejection)));
            if (run.State!.Character.GetProperty("hp").GetInt32() == 20) break;
        }
        Assert.Equal(20, run!.State!.Character.GetProperty("hp").GetInt32());
        Assert.Equal(actions, result!.Attempts); Assert.Equal(seconds, result.CooldownSeconds);
        Assert.Equal(0, result.NoProgress);
        Assert.Equal(2, run.State.Context.Used["recovery:use"]);
        Assert.Equal(craft ? 2 : 0, run.State.Context.Used.GetValueOrDefault("recovery:materials"));
        var inventory = CharacterObservation.Read(run.State.Character)!.Inventory;
        Assert.Equal(0, inventory.GetValueOrDefault("meal")); Assert.Equal(0, inventory.GetValueOrDefault("snack"));
        Assert.Equal(1, inventory.GetValueOrDefault("protected"));
        Assert.Equal(craft ? 0 : 2, run.State.Bank!.Items.GetValueOrDefault("meal"));
        Assert.DoesNotContain("Fight", commands); Assert.DoesNotContain(commands, x => x?.StartsWith("Craft:snack", StringComparison.Ordinal) == true);
        var inspected = await h.Factory.Create(policy).InspectAsync(); Assert.DoesNotContain(inspected.Candidates, x => x.Category == "recovery");
    }

    [Fact]
    public async Task RecoveryLostUseReconcilesHpAndChargesWithoutAnotherPost()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("consumable-bank");
        var policy = RecoveryProfile(false); var saved = new Checkpoint();
        await h.Factory.Create(policy, checkpoints: saved, identity: "lost-recovery").TickAsync();
        await h.Factory.Create(policy, checkpoints: saved, identity: "lost-recovery").TickAsync();
        h.Handler.Corruption = "loss";
        Assert.Equal(StrategyStatus.UnknownOutcome, (await h.Factory.Create(policy, checkpoints: saved, identity: "lost-recovery").TickAsync()).Status);
        h.Handler.Corruption = null;
        var run = h.Factory.Create(policy, checkpoints: saved, identity: "lost-recovery");
        Assert.Equal(StrategyStatus.Reconciled, (await run.TickAsync()).Status);
        Assert.Equal(3, h.Handler.Actions); Assert.Equal(20, run.State!.Character.GetProperty("hp").GetInt32());
        Assert.Equal(2, run.State.Context.Used["recovery:use"]);
    }

    [Fact]
    public async Task RecoveryCancellationStopsSupplyAndDoesNotRefundAlreadyChargedWork()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("consumable-production");
        var policy = RecoveryProfile(); var saved = new Checkpoint();
        for (int i = 0; i < 4; i++) await h.Factory.Create(policy, checkpoints: saved, identity: "cancel-recovery").TickAsync();
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Equal(StrategyStatus.Cancelled, (await h.Factory.Create(policy, checkpoints: saved, identity: "cancel-recovery").TickAsync(cancellation.Token)).Status);
        Assert.Equal(StrategyStatus.Cancelled, (await h.Factory.Create(policy, checkpoints: saved, identity: "cancel-recovery").TickAsync()).Status);
        Assert.Equal(4, h.Handler.Actions);
    }

    [Fact]
    public async Task RecoveryProductionNeedsNoImplicitBankPermission()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("consumable-production");
        var policy = new PortfolioSettings { AutonomousGoals = true, Recovery = new(AllowUse: true, AllowCraft: true) }.Policy();
        var saved = new Checkpoint(); StrategySession? run = null;
        for (int i = 0; i < 9; i++)
        {
            run = h.Factory.Create(policy, new(NoProgress: 60), saved, "recovery-no-bank");
            Assert.Equal(StrategyStatus.Selected, (await run.TickAsync()).Status);
        }
        Assert.Equal(20, run!.State!.Character.GetProperty("hp").GetInt32()); Assert.Null(run.State.Bank);
        Assert.DoesNotContain(h.Handler.Reads, x => x.StartsWith("/my/bank", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RecoveryRestMeasuresHpWithoutManufacturingOrSpendingFood()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("consumable-production");
        var policy = RecoveryProfile(false, true); var saved = new Checkpoint();
        var run = h.Factory.Create(policy, checkpoints: saved, identity: "recovery-rest");
        Assert.Equal("Rest", (await run.TickAsync()).Command);
        Assert.Equal(20, run.State!.Character.GetProperty("hp").GetInt32());
        Assert.Empty(run.State.Context.Used);
        var entry = Assert.Single(saved.Load()!.Journal);
        Assert.Equal(16, entry.Facts!.HpDelta); Assert.Equal(16, entry.Facts.UsefulProgress);
        Assert.Equal(20, Assert.Single(saved.Load()!.Autonomous!.History).ObservedHp);
    }

    [Fact]
    public async Task RecoveryMaterialAllowanceRejectsWholePathBeforeStartingSupply()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("consumable-production");
        var policy = RecoveryProfile() with { Recovery = RecoveryProfile().Recovery! with { MaxMaterialUnits = 1 } };
        var result = await h.Factory.Create(policy).InspectAsync();
        Assert.Contains(result.Candidates, x => x.Rejection == "RecoveryMaterialBudgetExhausted" && x.Command is null);
        Assert.Equal(0, h.Handler.Actions);
    }

    [Theory]
    [InlineData(false, StrategyStatus.Selected)]
    [InlineData(true, StrategyStatus.Blocked)]
    public async Task RecoveryUseSchemaIsRequiredOnlyWhenUseIsEnabled(bool recovery, StrategyStatus status)
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("consumable-bank"); h.Handler.Corruption = "food-use-schema";
        var policy = recovery ? RecoveryProfile(false) : new PortfolioSettings { AutonomousGoals = true }.Policy();
        Assert.Equal(status, (await h.Factory.Create(policy).InspectAsync()).Status); Assert.Equal(0, h.Handler.Actions);
    }

    [Fact]
    public async Task RecoveryInvalidUseResultDoesNotCompleteParentOrReplay()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("consumable-bank");
        var policy = RecoveryProfile(false); var saved = new Checkpoint();
        for (int i = 0; i < 2; i++) await h.Factory.Create(policy, checkpoints: saved, identity: "invalid-recovery").TickAsync();
        h.Handler.Corruption = "food-result";
        Assert.Equal("InvalidPostcondition", (await h.Factory.Create(policy, checkpoints: saved, identity: "invalid-recovery").TickAsync()).Reason);
        Assert.Empty(saved.Load()!.Autonomous!.History);
        Assert.Equal("InvalidPostcondition", (await h.Factory.Create(policy, checkpoints: saved, identity: "invalid-recovery").TickAsync()).Reason);
        Assert.Equal(3, h.Handler.Actions);
    }
    [Fact]
    public async Task FullPathRejectsAnUnreachableLaterWorkshopBeforeGathering()
    {
        await using var h = new Harness(new()); await h.Reset("item-production");
        h.Handler.Corruption = "future-workshop";
        var policy = new PortfolioPolicy([], 0, "", "", Items: [new("tool", 1)], Measurement: new(FullPaths: true));
        var result = await h.Factory.Create(policy).TickAsync();
        Assert.Equal(StrategyStatus.Blocked, result.Status); Assert.Equal(0, h.Handler.Actions);
        Assert.Equal("UnsupportedFullPathEstimate", Assert.Single(result.Candidates).Rejection);
    }
    [Theory]
    [InlineData("item-production", false, false, 6, 32)]
    [InlineData("capacity-training", false, false, 9, 50)]
    [InlineData("consumable-production", true, false, 23, 127)]
    [InlineData("autonomous-weapon", false, true, 20, 120)]
    [InlineData("autonomous-shield", true, true, 38, 221)]
    public async Task FullPathModeCompletesProductionTrainingFoodAndCombatWithVerifiedFacts(string scenario, bool food, bool combat, int actions, int seconds)
    {
        foreach (bool full in new[] { false, true })
        {
            await using var h = new Harness(new()); await h.Reset(scenario);
            var policy = combat ? AutonomousPolicy(scenario == "autonomous-weapon" ? "water_blade" : "ward") : food ? FoodPolicy() :
                new PortfolioPolicy([], 0, "", "", Items: [new("tool", 1)], Preparation: new(),
                    Bank: new(ImmutableDictionary<string, int>.Empty.Add("bar", 0).Add("ore", 0).Add("tool", 0)), Production: new(ImmutableDictionary<string, int>.Empty));
            if (combat && food) policy = policy with { Consumable = new("combat", "meal", HpBelowPercent: 100, MaxUsed: 8, PreparationSeconds: 0),
                AutonomousCombat = policy.AutonomousCombat! with { Equipment = ["ward", "water_blade"] } };
            policy = policy with { Measurement = full ? new(FullPaths: true) : null };
            var saved = new Checkpoint(); var decisions = new List<StrategyDecision>(); StrategySession? run = null;
            for (int i = 0; i < 95; i++)
            {
                run = h.Factory.Create(policy, new(NoProgress: 60), saved, "full-route");
                var result = await run.TickAsync(); decisions.Add(result);
                if (result.Status != StrategyStatus.Selected) break;
            }
            Assert.True(decisions[^1].Status == StrategyStatus.Completed, System.Text.Json.JsonSerializer.Serialize(decisions[^1]));
            Assert.Equal(actions, decisions[^1].Attempts); Assert.Equal(seconds, decisions[^1].CooldownSeconds);
            Assert.All(saved.Load()!.Journal, x => { Assert.NotNull(x.Facts); Assert.NotNull(x.Facts!.WaitSeconds); if (full) Assert.NotNull(x.MeasurementContext); });
            Assert.All(saved.Load()!.Journal.Where(x => x.Command.StartsWith("Deposit:") || x.Command.StartsWith("Withdraw:")), x => Assert.All(x.Facts!.StockDelta!, y => Assert.Equal(0, y.Value)));
            var consumed = RunPerformance.From(saved.Load()!).ConsumedInputs;
            var expectedInputs = scenario switch
            {
                "item-production" => new Dictionary<string, long> { ["ore"] = 2, ["bar"] = 1 },
                "capacity-training" => new Dictionary<string, long> { ["ore"] = 2, ["bar"] = 1 },
                "consumable-production" => new Dictionary<string, long> { ["ore"] = 2, ["bar"] = 1, ["fish"] = 4, ["meal"] = 2 },
                "autonomous-weapon" => new Dictionary<string, long> { ["ore"] = 2, ["feather"] = 2 },
                _ => new Dictionary<string, long> { ["fish"] = 6, ["meal"] = 4, ["feather"] = 2 }
            };
            Assert.Equal(expectedInputs.OrderBy(x => x.Key), consumed.OrderBy(x => x.Key));
            if (food) Assert.True(saved.Load()!.Journal.Sum(x => x.Facts!.Inputs.GetValueOrDefault("meal")) > 0);
            if (combat) Assert.Equal(20, saved.Load()!.Journal.Sum(x => x.Facts!.Skills.GetValueOrDefault("combat")?.Xp ?? 0));
            if (full) Assert.Contains(decisions.SelectMany(x => x.Candidates), x => x.Path is not null && x.Path.Context is not null);
            if (full && combat && food) Assert.Contains(decisions, d => d.Candidates.Count(c => c.CombatRoute?.Equipment is not null && c.Path is not null) == 2);
        }
    }
    [Fact]
    public async Task AutonomousComparesPreparationCostAcrossSupportedGearAndOpponents()
    {
        await using var h = new Harness(new()); await h.Reset("autonomous-shield");
        var policy = AutonomousPolicy("ward") with { AutonomousCombat = new([new(2, ["dummy"]), new(3, ["guardian", "missing", "missing2"])], ["water_blade", "ward"]) };
        var run = h.Factory.Create(policy);
        for (int i = 0; i < 4; i++) await run.TickAsync();
        var result = await run.TickAsync();
        Assert.Equal("combat:guardian:shield:ward", result.Candidate);
        var routes = result.Candidates.Where(x => x.CombatRoute?.Equipment is not null).ToArray();
        Assert.Equal(2, routes.Length);
        Assert.True(routes.Single(x => x.CombatRoute!.Equipment == "ward").TotalSeconds < routes.Single(x => x.CombatRoute!.Equipment == "water_blade").TotalSeconds);
        Assert.Contains(result.Candidates, x => x.Rejection == "UnsupportedOrZeroXpOpponent:missing");
    }
    [Fact]
    public async Task AutonomousCombatUsesBoundedFoodAndStopsRefillWithParent()
    {
        await using var h = new Harness(new()); await h.Reset("autonomous-shield");
        var policy = AutonomousPolicy("ward") with { Consumable = new("combat", "meal", HpBelowPercent: 100, MaxUsed: 8, PreparationSeconds: 0) };
        var saved = new Checkpoint(); var decisions = new List<StrategyDecision>(); StrategySession? run = null;
        for (int i = 0; i < 90; i++)
        {
            run = h.Factory.Create(policy, new(NoProgress: 60), saved, "combat-food");
            var result = await run.TickAsync(); decisions.Add(result);
            if (result.Status != StrategyStatus.Selected) break;
        }
        Assert.True(decisions[^1].Status == StrategyStatus.Completed, System.Text.Json.JsonSerializer.Serialize(decisions));
        Assert.Equal(4, decisions.Count(x => x.Command == "Fight"));
        Assert.DoesNotContain(decisions, x => x.Command == "Rest");
        Assert.Equal(4, saved.Load()!.Journal.Sum(x => x.Charges?.GetValueOrDefault("use:meal") ?? 0));
        Assert.Equal(6, saved.Load()!.Journal.Sum(x => x.Charges?.GetValueOrDefault("materials:meal") ?? 0));
        var state = CharacterObservation.Read(run!.State!.Character)!;
        Assert.Equal(2, state.Inventory.GetValueOrDefault("meal") + (run.State.Bank?.Items.GetValueOrDefault("meal") ?? 0));
        int actions = h.Handler.Actions;
        await h.Factory.Create(policy, new(NoProgress: 60), saved, "combat-food").TickAsync(); Assert.Equal(actions, h.Handler.Actions);
    }

    [Theory]
    [InlineData("combat-effects")]
    [InlineData("combat-access")]
    [InlineData("combat-gear-effect")]
    [InlineData("combat-recipe")]
    public async Task AutonomousUnsupportedSecondStageCannotDispatchFight(string corruption)
    {
        await using var h = new Harness(new()); await h.Reset("autonomous-shield");
        var run = h.Factory.Create(AutonomousPolicy("ward"));
        for (int i = 0; i < 4; i++) await run.TickAsync();
        h.Handler.Corruption = corruption;
        Assert.Equal(StrategyStatus.Blocked, (await run.TickAsync()).Status);
        Assert.Equal(4, h.Handler.Actions);
    }

    [Theory]
    [InlineData("loss", StrategyStatus.UnknownOutcome)]
    [InlineData("combat-equip-result", StrategyStatus.Blocked)]
    public async Task AutonomousEquipmentResponseIsVerifiedOrReconciledWithoutReplay(string corruption, StrategyStatus status)
    {
        await using var h = new Harness(new()); await h.Reset("autonomous-shield");
        var saved = new Checkpoint(); var policy = AutonomousPolicy("ward");
        var run = h.Factory.Create(policy, checkpoints: saved, identity: "gear-response");
        for (int i = 0; i < 6; i++) await run.TickAsync();
        h.Handler.Corruption = corruption; Assert.Equal(status, (await run.TickAsync()).Status);
        h.Handler.Corruption = null;
        var after = await h.Factory.Create(policy, checkpoints: saved, identity: "gear-response").TickAsync();
        Assert.Equal(status == StrategyStatus.UnknownOutcome ? StrategyStatus.Reconciled : StrategyStatus.Blocked, after.Status);
        Assert.Equal(7, h.Handler.Actions);
    }
    [Theory]
    [InlineData("combat-unknown-result")]
    [InlineData("combat-defeat")]
    public async Task AutonomousUnverifiedFightStopsAndNeverReplays(string corruption)
    {
        await using var h = new Harness(new()); await h.Reset("autonomous-shield");
        var policy = AutonomousPolicy("ward"); var saved = new Checkpoint();
        var run = h.Factory.Create(policy, checkpoints: saved, identity: "fight-boundary");
        await run.TickAsync(); h.Handler.Corruption = corruption;
        Assert.Equal(corruption == "combat-defeat" ? StrategyStatus.Blocked : StrategyStatus.UnknownOutcome, (await run.TickAsync()).Status);
        h.Handler.Corruption = null;
        await h.Factory.Create(policy, checkpoints: saved, identity: "fight-boundary").TickAsync();
        Assert.Equal(2, h.Handler.Actions);
    }
    private static PortfolioPolicy AutonomousPolicy(string gear) => new([], 3, "dummy", "", Preparation: new(),
        Bank: new(ImmutableDictionary<string, int>.Empty.Add("feather", 0).Add("meal", 0)),
        Production: new(ImmutableDictionary<string, int>.Empty),
        AutonomousCombat: new([new(2, ["dummy"]), new(3, ["guardian"])], [gear]));

    private static PortfolioPolicy CombatDiscoveryProfile() => new PortfolioSettings { AutonomousGoals = true,
        CombatDiscovery = new(AllowFight: true, AllowEquip: true, AllowCraft: true, AllowBankWithdrawal: true),
        Recovery = new(AllowRest: true), BankRetain = new() { ["protected"] = 1, ["feather"] = 0 } }.Policy();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CombatDiscoveryRequiresFightSchemaOnlyWhenPermitted(bool allowed)
    {
        await using var h = new Harness(new()); await h.Reset("combat-discovery-shield"); h.Handler.Corruption = "route";
        var policy = CombatDiscoveryProfile() with { CombatDiscovery = new(AllowFight: allowed) };
        var result = await h.Factory.Create(policy).InspectAsync();
        Assert.Equal(allowed, result.Reason == "ObservationFailed"); Assert.Equal(0, h.Handler.Actions);
    }

    [Fact]
    public async Task CombatCompletionCanBeArchivedWithoutCombatLevelField()
    {
        await using var h = new Harness(new()); await h.Reset("combat-discovery-shield");
        var saved = new Checkpoint(); var policy = CombatDiscoveryProfile();
        for (int i = 0; i < 13; i++) await h.Factory.Create(policy, new(NoProgress: 30, Actions: 12), saved, "archive-combat").TickAsync();
        Assert.Equal("AutonomousBudgetExhausted", saved.Load()!.Terminal!.Reason);
        string directory = Path.Combine(Path.GetTempPath(), "artiact-combat-archive-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var store = new FileRunCheckpointStore(directory, "researcher"); store.Save(saved.Load()!);
            Assert.True(File.Exists(store.ArchiveCompleted(FileRunCheckpointStore.IdentityDigest("archive-combat"))));
            Assert.Null(store.Load());
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData("fight")]
    [InlineData("equip")]
    [InlineData("craft")]
    [InlineData("materials")]
    [InlineData("cycle")]
    [InlineData("effect")]
    [InlineData("monster")]
    public async Task CombatDiscoveryRejectsForbiddenOrUnsupportedPreparation(string failure)
    {
        await using var h = new Harness(new()); await h.Reset("combat-discovery-shield");
        var policy = CombatDiscoveryProfile(); var run = h.Factory.Create(AutonomousPolicy("ward"));
        for (int i = 0; i < 4; i++) await run.TickAsync();
        var observed = run.State!; var character = JsonNode.Parse(observed.Character.GetRawText())!; character["hp"] = 20;
        var items = observed.Catalogs["items"].Select(x => JsonNode.Parse(x.GetRawText())!).ToArray();
        var monsters = observed.Catalogs["monsters"].Select(x => JsonNode.Parse(x.GetRawText())!).ToArray();
        var ward = items.Single(x => x["code"]!.GetValue<string>() == "ward");
        if (failure == "cycle") ward["craft"]!["items"]![0]!["code"] = "ward";
        if (failure == "effect") ward["effects"]![0]!["code"] = "healing";
        if (failure == "monster") monsters.Single(x => x["code"]!.GetValue<string>() == "guardian")["type"] = "boss";
        policy = policy with { CombatDiscovery = policy.CombatDiscovery! with {
            AllowFight = failure != "fight", AllowEquip = failure != "equip", AllowCraft = failure != "craft", MaxMaterialUnits = failure == "materials" ? 1 : 100 } };
        var state = new StrategyObservation(System.Text.Json.JsonSerializer.SerializeToElement(character), observed.Catalogs
            .SetItem("items", items.Select(x => System.Text.Json.JsonSerializer.SerializeToElement(x)).ToImmutableArray())
            .SetItem("monsters", monsters.Select(x => System.Text.Json.JsonSerializer.SerializeToElement(x)).ToImmutableArray()), observed.Policy, observed.Bank);
        var result = new CombatGoalDiscovery(policy, new(null!, null!)).EvaluateAll(state).ToArray();
        Assert.DoesNotContain(result, x => x.CombatRoute?.Equipment == "ward" && (failure != "monster" || x.CombatRoute.Monster == "guardian") && x.Command is not null && x.Rejection is null);
        if (failure == "fight") Assert.All(result, x => Assert.Null(x.Command));
        if (failure == "effect") Assert.Contains(result, x => x.Rejection == "UnsupportedDiscoveryEquipment:ward");
        if (failure is "materials" or "craft") Assert.Contains(result, x => x.Rejection == (failure == "materials" ? "CombatMaterialBudgetExhausted" : "CombatProductionNotAllowed"));
        Assert.Equal(4, h.Handler.Actions);
    }

    [Fact]
    public async Task CombatDiscoveryReadyEquipmentAvoidsCraftAndTraining()
    {
        await using var h = new Harness(new()); await h.Reset("combat-discovery-weapon");
        var run = h.Factory.Create(AutonomousPolicy("water_blade")); for (int i = 0; i < 4; i++) await run.TickAsync();
        var observed = run.State!; var character = JsonNode.Parse(observed.Character.GetRawText())!; character["hp"] = 20;
        character["inventory"]!.AsArray().Add(new JsonObject { ["slot"] = 3, ["code"] = "water_blade", ["quantity"] = 1 });
        var policy = CombatDiscoveryProfile() with { CombatDiscovery = new(AllowFight: true, AllowEquip: true) };
        var result = new CombatGoalDiscovery(policy, new(null!, null!)).EvaluateAll(new(System.Text.Json.JsonSerializer.SerializeToElement(character), observed.Catalogs, observed.Policy));
        var gear = Assert.Single(result, x => x.CombatRoute?.Monster == "guardian" && x.CombatRoute.Equipment == "water_blade");
        Assert.Null(gear.Rejection); Assert.Null(gear.Prerequisite); Assert.StartsWith("Unequip:", gear.Command!.Id);
    }

    [Theory]
    [InlineData("combat-discovery-shield", "ward", 12, 78)]
    [InlineData("combat-discovery-weapon", "water_blade", 20, 120)]
    public async Task CombatDiscoveryComparisonReplaysAllThreePolicies(string scenario, string gear, int actions, int seconds)
    {
        async Task<(int Actions, long Seconds, int Moves, bool Achieved, string State, int Switches)> Execute(string mode)
        {
            await using var h = new Harness(new()); await h.Reset(scenario);
            var policy = mode == "fixed" ? AutonomousPolicy(gear) : CombatDiscoveryProfile();
            var saved = new Checkpoint(); var limits = new StrategyLimits(NoProgress: 30, Actions: actions);
            var run = mode == "lowest" ? h.LowestSession(policy, limits) : h.Factory.Create(policy, limits, saved, "comparison");
            StrategyDecision? result = null; int moves = 0; bool achieved = false; var candidates = new List<string?>();
            for (int i = 0; i < 40; i++)
            {
                result = await run.TickAsync(); if (result.Command?.StartsWith("Move:", StringComparison.Ordinal) == true) moves++;
                if (result.Command is not null) candidates.Add(result.Candidate);
                achieved = run.State?.Character.GetProperty("level").GetInt32() == 3;
                if (achieved || result.Status != StrategyStatus.Selected) break;
            }
            if (achieved)
            {
                var stock = CharacterObservation.Read(run.State!.Character)!.Inventory;
                Assert.Equal(2, stock["feather"]); Assert.Equal(1, stock["protected"]);
                Assert.Equal(gear, run.State.Character.GetProperty(gear == "ward" ? "shield_slot" : "weapon_slot").GetString());
            }
            int switches = candidates.Zip(candidates.Skip(1)).Count(x => x.First != x.Second);
            output.WriteLine($"METRIC {scenario}/{mode}: actions={result!.Attempts}, cooldown={result.CooldownSeconds}, moves={moves}, switches={switches}, combat={run.State!.Character.GetProperty("level")}");
            output.WriteLine($"SKILLS {scenario}/{mode}: {SkillSnapshot(run.State.Character)}");
            return (result.Attempts, result.CooldownSeconds, moves, achieved, run.State.Character.GetRawText(), switches);
        }
        var auto = await Execute("auto"); var fixedRun = await Execute("fixed"); var lowest = await Execute("lowest");
        Assert.Equal((actions, (long)seconds, true), (auto.Actions, auto.Seconds, auto.Achieved));
        Assert.Equal((fixedRun.Actions, fixedRun.Seconds, fixedRun.Moves), (auto.Actions, auto.Seconds, auto.Moves));
        Assert.False(lowest.Achieved);
        Assert.Equal(auto, await Execute("auto")); Assert.Equal(fixedRun, await Execute("fixed")); Assert.Equal(lowest, await Execute("lowest"));
    }

    [Theory]
    [InlineData("Fight")]
    [InlineData("Equip:shield:ward")]
    public async Task DiscoveredCombatLostActionReconcilesWithoutReplay(string desired)
    {
        await using var h = new Harness(new()); await h.Reset("combat-discovery-shield");
        var policy = CombatDiscoveryProfile(); var saved = new Checkpoint();
        for (int i = 0; i < 20; i++)
        {
            // Inspect the current observation; the durable active parent remains in the saved context.
            var preview = h.Factory.Create(policy, new(NoProgress: 30), saved, "lost-combat");
            if (saved.Load()?.Latest is { } latest)
            {
                var state = latest.Restore();
                var command = new CombatGoalDiscovery(policy, new(null!, null!)).EvaluateAll(new(state.Character, state.Catalogs, state.Policy, state.Bank,
                    state.Context with { Autonomous = saved.Load()!.Autonomous })).Where(x => x.Command is not null && x.Rejection is null)
                    .Any(x => x.Command!.Id == desired);
                if (command) h.Handler.Corruption = "loss";
            }
            var decision = await preview.TickAsync();
            if (decision.Status == StrategyStatus.UnknownOutcome)
            {
                int count = h.Handler.Actions; h.Handler.Corruption = null;
                Assert.Equal(desired, saved.Load()!.PendingCommand);
                Assert.Equal(StrategyStatus.Reconciled, (await h.Factory.Create(policy, new(NoProgress: 30), saved, "lost-combat").TickAsync()).Status);
                Assert.Equal(count, h.Handler.Actions); return;
            }
            Assert.Equal(StrategyStatus.Selected, decision.Status);
        }
        Assert.Fail("Expected action was never dispatched");
    }

    [Fact]
    public async Task CombatRecoveryUsesFoodAtDeficitAboveStandaloneThreshold()
    {
        await using var h = new Harness(new()); await h.Reset("combat-discovery-shield");
        var initial = CombatDiscoveryProfile(); var saved = new Checkpoint();
        var policy = initial with { Recovery = new(AllowUse: true, AllowCraft: true, AllowBankWithdrawal: true) };
        bool used = false;
        for (int i = 0; i < 30; i++)
        {
            var run = h.Factory.Create(policy, new(NoProgress: 60), saved, "food-combat"); var result = await run.TickAsync();
            Assert.True(result.Status == StrategyStatus.Selected, result.Reason + ":" + string.Join(',', result.Candidates.Select(x => x.Rejection)));
            if (result.Command?.StartsWith("Use:", StringComparison.Ordinal) == true)
            {
                used = true; Assert.Equal(20, run.State!.Character.GetProperty("hp").GetInt32());
                Assert.Equal("combat", saved.Load()!.Autonomous!.Active!.Skill);
                Assert.True(result.NoProgress > 0); Assert.Equal(1, run.State.Context.Used["recovery:use"]);
                Assert.Equal(0, saved.Load()!.Journal[^1].Facts!.UsefulProgress); break;
            }
            Assert.NotEqual("Rest", result.Command);
        }
        Assert.True(used);
    }

    [Fact]
    public async Task CombatParentLostUseRetainsAllowancesAndDoesNotReplay()
    {
        await using var h = new Harness(new()); await h.Reset("combat-discovery-shield");
        var policy = CombatDiscoveryProfile() with { Recovery = new(AllowUse: true, AllowCraft: true, AllowBankWithdrawal: true) };
        var saved = new Checkpoint();
        for (int i = 0; i < 30; i++)
        {
            if (saved.Load()?.Latest is { } last && CharacterObservation.Read(last.Character)!.Inventory.GetValueOrDefault("meal") > 0)
                h.Handler.Corruption = "loss";
            var result = await h.Factory.Create(policy, new(NoProgress: 60), saved, "unknown-combat-use").TickAsync();
            if (result.Status == StrategyStatus.UnknownOutcome)
            {
                Assert.StartsWith("Use:", saved.Load()!.PendingCommand); int actions = h.Handler.Actions;
                h.Handler.Corruption = null;
                var resumed = h.Factory.Create(policy, new(NoProgress: 60), saved, "unknown-combat-use");
                Assert.Equal(StrategyStatus.Reconciled, (await resumed.TickAsync()).Status);
                Assert.Equal(actions, h.Handler.Actions); Assert.Equal(20, resumed.State!.Character.GetProperty("hp").GetInt32());
                Assert.Equal(1, resumed.State.Context.Used["recovery:use"]);
                Assert.Equal("combat", saved.Load()!.Autonomous!.Active!.Skill);
                Assert.Null(saved.Load()!.Journal[^1].Facts); return;
            }
            Assert.Equal(StrategyStatus.Selected, result.Status);
        }
        Assert.Fail("Expected unknown Use outcome");
    }

    [Theory]
    [InlineData("combat-discovery-shield", "ward", 12, 78)]
    [InlineData("combat-discovery-weapon", "water_blade", 20, 120)]
    public async Task DiscoveredCombatPreparesUsefulSlotWithoutManualGoals(string scenario, string gear, int actions, int seconds)
    {
        await using var h = new Harness(new()); await h.Reset(scenario);
        var policy = CombatDiscoveryProfile(); var saved = new Checkpoint();
        var decisions = new List<StrategyDecision>(); StrategySession? run = null;
        for (int i = 0; i < 40; i++)
        {
            run = h.Factory.Create(policy, new(NoProgress: 30, Actions: actions), saved, "combat-discovery");
            var decision = await run.TickAsync(); decisions.Add(decision);
            if (run.State?.Character.GetProperty("level").GetInt32() >= 3 || decision.Status != StrategyStatus.Selected) break;
        }
        Assert.True(run!.State?.Character.GetProperty("level").GetInt32() == 3, System.Text.Json.JsonSerializer.Serialize(decisions));
        Assert.Equal(actions, h.Handler.Actions); Assert.Equal(seconds, decisions[^1].CooldownSeconds);
        Assert.Equal(gear, run.State.Character.GetProperty(gear == "ward" ? "shield_slot" : "weapon_slot").GetString());
        Assert.Equal(2, CharacterObservation.Read(run.State.Character)!.Inventory["feather"]);
        Assert.Equal(1, CharacterObservation.Read(run.State.Character)!.Inventory["protected"]);
        Assert.Equal(2, saved.Load()!.Autonomous!.History.Count(x => x.Goal.Skill == "combat" && x.Outcome == "Completed"));
        Assert.Equal(gear == "ward" ? 2 : 4, run.State.Context.Used["combat:materials"]);
    }

    [Theory]
    [InlineData("autonomous-shield", "ward", 12, 78)]
    [InlineData("autonomous-weapon", "water_blade", 20, 120)]
    public async Task AutonomousTwoStagesPrepareRequiredSlotAndSurviveEveryTickRestart(string scenario, string gear, int actions, int seconds)
    {
        await using var h = new Harness(new()); await h.Reset(scenario);
        var policy = AutonomousPolicy(gear); var saved = new Checkpoint();
        var decisions = new List<StrategyDecision>(); StrategySession? run = null;
        for (int i = 0; i < 40; i++)
        {
            run = h.Factory.Create(policy, new(NoProgress: 30), saved, "autonomous");
            var decision = await run.TickAsync(); decisions.Add(decision);
            if (decision.Status != StrategyStatus.Selected) break;
        }
        Assert.True(decisions[^1].Status == StrategyStatus.Completed, System.Text.Json.JsonSerializer.Serialize(decisions));
        Assert.Equal(actions, h.Handler.Actions); Assert.Equal(seconds, decisions[^1].CooldownSeconds);
        Assert.Equal(3, run!.State!.Character.GetProperty("level").GetInt32());
        var stock = CharacterObservation.Read(run.State.Character)!.Inventory;
        Assert.Equal(2, stock["feather"]); Assert.Equal(1, stock["protected"]);
        Assert.Contains(decisions.SelectMany(x => x.Candidates), x => x.CombatRoute?.Equipment == gear && x.CombatRoute.PreparationSeconds > 0);
        Assert.Equal(4, decisions.Count(x => x.Command == "Fight"));
        Assert.Equal(gear == "ward" ? 8 : 10, run.State.Character.GetProperty("hp").GetInt32());
        Assert.Equal(StrategyStatus.Completed, (await h.Factory.Create(policy, new(NoProgress: 30), saved, "autonomous").TickAsync()).Status);
        Assert.Equal(actions, h.Handler.Actions);
    }
    [Theory]
    [InlineData("food-effect", 0, "UnsupportedConsumableEffectOrCondition")]
    [InlineData("food-use-schema", 0, null)]
    [InlineData("food-result", 8, null)]
    public async Task UnsupportedOrUnverifiedUseStopsWithoutReplay(string corruption, int before, string? rejection)
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("consumable-production");
        var run = h.Factory.Create(FoodPolicy(), new(NoProgress: 60));
        for (int i = 0; i < before; i++) await run.TickAsync();
        h.Handler.Corruption = corruption;
        var result = await run.TickAsync();
        Assert.Equal(StrategyStatus.Blocked, result.Status);
        if (rejection is not null) Assert.Equal(rejection, Assert.Single(result.Candidates).Rejection);
        Assert.Equal(before + (corruption == "food-result" ? 1 : 0), h.Handler.Actions);
        await run.TickAsync();
        Assert.Equal(before + (corruption == "food-result" ? 1 : 0), h.Handler.Actions);
    }

    [Fact]
    public async Task ConsumableCannotSpendAnotherGoalReservedStock()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("consumable-bank");
        var policy = FoodPolicy() with { Items = [new("tool", 1), new("meal", 4)] };
        var result = await h.Factory.Create(policy).TickAsync();
        Assert.Equal(StrategyStatus.Blocked, result.Status);
        Assert.Contains(result.Candidates, x => x.Rejection == "ConsumableReserveConflict");
        Assert.Equal(0, h.Handler.Actions);
    }

    [Fact]
    public async Task LostUseReplyReconcilesExactStockHpAndChargedAllowance()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("consumable-production");
        var policy = FoodPolicy(); var saved = new Checkpoint();
        var run = h.Factory.Create(policy, new(NoProgress: 60), saved, "use-loss");
        for (int i = 0; i < 8; i++) Assert.Equal(StrategyStatus.Selected, (await run.TickAsync()).Status);
        h.Handler.Corruption = "loss";
        Assert.Equal(StrategyStatus.UnknownOutcome, (await run.TickAsync()).Status);
        h.Handler.Corruption = null;
        var resumed = h.Factory.Create(policy, new(NoProgress: 60), saved, "use-loss");
        Assert.Equal(StrategyStatus.Reconciled, (await resumed.TickAsync()).Status);
        Assert.Equal(9, h.Handler.Actions);
        Assert.Equal(20, resumed.State!.Character.GetProperty("hp").GetInt32());
        Assert.Equal(2, resumed.State.Context.Used["use:meal"]);
        Assert.Equal(0, CharacterObservation.Read(resumed.State.Character)!.Inventory.GetValueOrDefault("meal"));
    }

    [Fact]
    public async Task FullHpAndCompletedParentDoNotUseOrReplenishFood()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("consumable-full");
        var saved = new Checkpoint(); var policy = FoodPolicy();
        var run = h.Factory.Create(policy, new(NoProgress: 60), saved, "full-hp");
        for (int i = 0; i < 7; i++) await run.TickAsync();
        Assert.Equal(StrategyStatus.Completed, saved.Load()!.Terminal!.Status);
        Assert.Equal(6, h.Handler.Actions);
        Assert.DoesNotContain(saved.Load()!.Journal, x => x.Command.StartsWith("Use:", StringComparison.Ordinal) || x.Command.StartsWith("Craft:meal", StringComparison.Ordinal));
        await h.Reset("consumable-production");
        var completed = policy with { Items = [new("protected", 1)], Consumable = policy.Consumable! with { ParentItem = "protected" } };
        int before = h.Handler.Actions;
        Assert.Equal(StrategyStatus.Completed, (await h.Factory.Create(completed).TickAsync()).Status);
        Assert.Equal(before, h.Handler.Actions);
    }

    [Theory]
    [InlineData(1, 100, 60, "ResourceBudgetExhausted", 7)]
    [InlineData(100, 10, 5, "BudgetExhausted", 5)]
    public async Task SupplyCannotResetMaterialOrNoProgressBudget(int materials, int uses, int noProgress, string reason, int actions)
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("consumable-production");
        var policy = FoodPolicy(); policy = policy with { Consumable = policy.Consumable! with { MaxMaterialUnits = materials, MaxUsed = uses } };
        var run = h.Factory.Create(policy, new(NoProgress: noProgress));
        StrategyDecision? last = null;
        for (int i = 0; i < 30; i++) { last = await run.TickAsync(); if (last.Status == StrategyStatus.Blocked) break; }
        Assert.Equal(reason, last!.Reason); Assert.Equal(actions, h.Handler.Actions);
    }

    [Fact]
    public async Task OptionalRestUsesRemainingStockConservativelyAfterUseCap()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("consumable-bank");
        var policy = FoodPolicy(); policy = policy with { Consumable = policy.Consumable! with { MaxUsed = 1, AllowRest = true, Reserve = 1, MinimumStock = 2, TargetStock = 3 } };
        var run = h.Factory.Create(policy, new(NoProgress: 60));
        var commands = new List<string?>();
        for (int i = 0; i < 25; i++) { var decision = await run.TickAsync(); commands.Add(decision.Command); if (decision.Status == StrategyStatus.Completed) break; }
        Assert.Contains("Rest", commands);
        Assert.Equal(1, run.State!.Context.Used["use:meal"]);
        Assert.Equal(20, run.State.Character.GetProperty("hp").GetInt32());
        Assert.Equal(3, run.State.Bank!.Items.GetValueOrDefault("meal") + CharacterObservation.Read(run.State.Character)!.Inventory.GetValueOrDefault("meal"));
    }

    [Fact]
    public async Task SharedCommandRestoresTheSelectedParentCandidate()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("capacity-production");
        var policy = CapacityPolicy(new("bar", 1), new("tool", 1));
        var saved = new Checkpoint();
        h.Handler.Corruption = "loss";
        Assert.Equal(StrategyStatus.UnknownOutcome, (await h.Factory.Create(policy, checkpoints: saved, identity: "shared").TickAsync()).Status);
        h.Handler.Corruption = null;
        Assert.Equal(StrategyStatus.Reconciled, (await h.Factory.Create(policy, checkpoints: saved, identity: "shared").TickAsync()).Status);
        Assert.Equal(1, h.Handler.Actions);
    }

    private static PortfolioPolicy FoodPolicy(bool training = false) => CapacityPolicy(new ItemMilestone("tool", 1)) with
    {
        Bank = new(CapacityPolicy(new ItemMilestone("tool", 1)).Bank!.Retain.Add("meal", 0).Add("snack", 0).Add("fish", 0).Add("baitfish", 0)),
        Preparation = training ? new() : null,
        Consumable = new("tool", "meal", HpBelowPercent: 100)
    };

    [Theory]
    [InlineData("consumable-production", false, 23, 127)]
    [InlineData("consumable-capacity", false, 25, 137)]
    [InlineData("consumable-bank", false, 11, 51)]
    public async Task HealingSupplyUseAndRefillSurviveReconstruction(string scenario, bool training, int actions, int seconds)
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset(scenario);
        var policy = FoodPolicy(training); var saved = new Checkpoint();
        StrategyDecision? last = null; StrategySession? run = null;
        for (int i = 0; i < 80; i++)
        {
            run = h.Factory.Create(policy, new(NoProgress: 60), saved, "food");
            last = await run.TickAsync();
            if (last.Status is StrategyStatus.Completed or StrategyStatus.Blocked) break;
        }
        Assert.True(last!.Status == StrategyStatus.Completed, last.Reason + ":" + string.Join(',', last.Candidates.Select(x => x.Rejection)));
        Assert.Equal(actions, last.Attempts); Assert.Equal(seconds, last.CooldownSeconds);
        Assert.Equal(20, run!.State!.Character.GetProperty("hp").GetInt32());
        var inventory = CharacterObservation.Read(run.State.Character)!.Inventory;
        Assert.Equal(1, inventory["tool"]); Assert.Equal(1, inventory["protected"]);
        Assert.Equal(2, inventory.GetValueOrDefault("meal") + run.State.Bank!.Items.GetValueOrDefault("meal"));
        Assert.Equal(2, run.State.Context.Used["use:meal"]);
        Assert.Equal(scenario == "consumable-bank" ? 0 : 4, run.State.Context.Used.GetValueOrDefault("materials:meal"));
        Assert.Equal(StrategyStatus.Completed, (await h.Factory.Create(policy, new(NoProgress: 60), saved, "food").TickAsync()).Status);
        Assert.Equal(actions, h.Handler.Actions);
    }

    [Fact]
    public async Task FoodSupplyTrainsCookingAndFishingBeforeMealRecipe()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("consumable-training");
        var run = h.Factory.Create(FoodPolicy(true), new(NoProgress: 60));
        StrategyDecision? last = null;
        for (int i = 0; i < 80; i++) { last = await run.TickAsync(); if (last.Status is StrategyStatus.Completed or StrategyStatus.Blocked) break; }
        Assert.True(last!.Status == StrategyStatus.Completed, last.Reason + ":" + string.Join(',', last.Candidates.Select(x => x.Rejection)));
        Assert.True(run.State!.Character.GetProperty("cooking_level").GetInt32() >= 2);
        Assert.True(run.State.Character.GetProperty("fishing_level").GetInt32() >= 2);
        Assert.Equal(20, run.State.Character.GetProperty("hp").GetInt32());
        Assert.Equal(2, run.State.Context.Used["use:meal"]);
    }

    [Theory]
    [InlineData("capacity-production", 9, 5)]
    [InlineData("capacity-production", 4, 5)]
    [InlineData("item-production-bank", 1, 1)]
    public async Task CapacityBankAndCraftRepliesReconcileWithoutRepeatedPost(string scenario, int beforeAction, int quantity)
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset(scenario);
        var policy = CapacityPolicy(new ItemMilestone("tool", quantity));
        var saved = new Checkpoint(); var run = h.Factory.Create(policy, checkpoints: saved, identity: "capacity-loss");
        for (int i = 0; i < beforeAction; i++) Assert.Equal(StrategyStatus.Selected, (await run.TickAsync()).Status);
        h.Handler.Corruption = "loss";
        Assert.Equal(StrategyStatus.UnknownOutcome, (await run.TickAsync()).Status);
        h.Handler.Corruption = null;
        Assert.Equal(StrategyStatus.Reconciled, (await h.Factory.Create(policy, checkpoints: saved, identity: "capacity-loss").TickAsync()).Status);
        Assert.Equal(beforeAction + 1, h.Handler.Actions);
    }

    [Theory]
    [InlineData("capacity-too-small", "MinimalRecipeExceedsCapacity", 0)]
    [InlineData("bank-full", "BankFull", 8)]
    public async Task ImpossibleCapacityOrFullBankStopsWithoutShuttling(string corruption, string reason, int actions)
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("capacity-production");
        h.Handler.Corruption = corruption;
        var run = h.Factory.Create(CapacityPolicy(new ItemMilestone("tool", 5)));
        StrategyDecision? last = null;
        for (int i = 0; i < 20; i++) { last = await run.TickAsync(); if (last.Status == StrategyStatus.Blocked) break; }
        Assert.Equal(StrategyStatus.Blocked, last!.Status);
        Assert.Equal(reason, Assert.Single(last.Candidates).Rejection);
        Assert.Equal(actions, h.Handler.Actions);
    }

    private static PortfolioPolicy CapacityPolicy(params ItemMilestone[] goals) => new([], 0, "", "", Items: goals.ToImmutableArray(),
        Bank: new(System.Collections.Immutable.ImmutableDictionary<string, int>.Empty.Add("ore", 0).Add("bar", 0).Add("tool", 0).Add("protected", 1)),
        Production: new(System.Collections.Immutable.ImmutableDictionary<string, int>.Empty.Add("protected", 1)));

    [Fact]
    public async Task SharedIngredientGoalRetainsItsQuotaWhileAnotherGoalCrafts()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("capacity-production");
        var run = h.Factory.Create(CapacityPolicy(new("bar", 1), new("tool", 1)));
        StrategyDecision? last = null;
        for (int i = 0; i < 30; i++) { last = await run.TickAsync(); if (last.Status is StrategyStatus.Completed or StrategyStatus.Blocked) break; }
        Assert.Equal(StrategyStatus.Completed, last!.Status);
        var inventory = CharacterObservation.Read(run.State!.Character)!.Inventory;
        Assert.Equal(1, inventory.GetValueOrDefault("bar") + run.State.Bank!.Items.GetValueOrDefault("bar"));
        Assert.Equal(1, inventory.GetValueOrDefault("tool") + run.State.Bank.Items.GetValueOrDefault("tool"));
        Assert.Equal(1, inventory["protected"]);
        Assert.Equal(14, last.Attempts); Assert.Equal(77, last.CooldownSeconds);
    }

    [Fact]
    public async Task SkillPreparationAndLargerOrderShareSmallInventory()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("capacity-training");
        var run = h.Factory.Create(CapacityPolicy(new ItemMilestone("tool", 3)) with { Preparation = new() });
        StrategyDecision? last = null;
        for (int i = 0; i < 50; i++) { last = await run.TickAsync(); if (last.Status is StrategyStatus.Completed or StrategyStatus.Blocked) break; }
        Assert.Equal(StrategyStatus.Completed, last!.Status);
        var inventory = CharacterObservation.Read(run.State!.Character)!.Inventory;
        Assert.Equal(3, inventory.GetValueOrDefault("tool") + run.State.Bank!.Items.GetValueOrDefault("tool"));
        Assert.Equal(1, inventory["protected"]);
        Assert.Equal(2, run.State.Character.GetProperty("weaponcrafting_level").GetInt32());
    }

    [Fact]
    public async Task OrderLargerThanInventoryCompletesThroughBankAndNestedBatches()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("capacity-production");
        var retain = System.Collections.Immutable.ImmutableDictionary<string, int>.Empty.Add("ore", 0).Add("bar", 0).Add("tool", 0).Add("protected", 1);
        var policy = new PortfolioPolicy([], 0, "", "", Items: [new("tool", 5)], Bank: new(retain),
            Production: new(System.Collections.Immutable.ImmutableDictionary<string, int>.Empty.Add("protected", 1)));
        var run = h.Factory.Create(policy);
        StrategyDecision? last = null;
        for (int i = 0; i < 60; i++)
        {
            last = await run.TickAsync();
            if (last.Status is StrategyStatus.Completed or StrategyStatus.Blocked) break;
        }
        Assert.Equal(StrategyStatus.Completed, last!.Status);
        Assert.Equal(5, run.State!.Bank!.Items.GetValueOrDefault("tool") + CharacterObservation.Read(run.State.Character)!.Inventory.GetValueOrDefault("tool"));
        Assert.Equal(1, CharacterObservation.Read(run.State.Character)!.Inventory["protected"]);
        Assert.Equal(42, last.Attempts); Assert.Equal(228, last.CooldownSeconds);
    }

    [Fact]
    public async Task InaccessibleRequiredResourceDoesNotStartPointlessTraining()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("resource-preparation");
        h.Handler.Corruption = "training-no-access";
        var policy = new PortfolioPolicy([], 0, "", "", Items: [new("tool", 1)], Preparation: new());
        var result = await h.Factory.Create(policy).TickAsync();
        Assert.Equal("NoSupportedResourcePrerequisite", Assert.Single(result.Candidates).Rejection);
        Assert.Equal(0, h.Handler.Actions);
    }

    [Theory]
    [InlineData("training-cycle", 12, "RecipeCycle")]
    [InlineData(null, 1, "PrerequisiteCycleOrDepth")]
    public async Task CyclicOrTooDeepTrainingStopsBeforeAction(string? corruption, int depth, string rejection)
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("skill-preparation");
        h.Handler.Corruption = corruption;
        var policy = new PortfolioPolicy([], 0, "", "", Items: [new("tool", 1)], Preparation: new(MaxDepth: depth));
        var result = await h.Factory.Create(policy).TickAsync();
        Assert.Equal(StrategyStatus.Blocked, result.Status);
        Assert.Equal(rejection, Assert.Single(result.Candidates).Rejection);
        Assert.Equal(0, h.Handler.Actions);
    }

    [Fact]
    public async Task TrainingMockRejectsUnmetSkillWithoutMutatingStockOrTrace()
    {
        await using var factory = new MockServiceFactory();
        using var client = new HttpClient(factory.Server.CreateHandler()) { BaseAddress = new("http://localhost") };
        using var reset = await client.PostAsync("/__mock/reset", new StringContent("{\"scenario\":\"skill-preparation\"}", Encoding.UTF8, "application/json"));
        reset.EnsureSuccessStatusCode();
        await client.GetStringAsync("/characters/researcher");
        using var move = await client.PostAsync("/my/researcher/action/move", new StringContent("{\"map_id\":3}", Encoding.UTF8, "application/json"));
        move.EnsureSuccessStatusCode();
        string before = await client.GetStringAsync("/__mock/state/researcher"), trace = await client.GetStringAsync("/__mock/trace");
        using var craft = await client.PostAsync("/my/researcher/action/crafting", new StringContent("{\"code\":\"tool\",\"quantity\":1}", Encoding.UTF8, "application/json"));
        Assert.Equal(422, (int)craft.StatusCode);
        Assert.Equal(before, await client.GetStringAsync("/__mock/state/researcher"));
        Assert.Equal(trace, await client.GetStringAsync("/__mock/trace"));
    }

    [Fact]
    public async Task MissingTrainingSkillSchemaBlocksBeforeDispatch()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("skill-preparation");
        h.Handler.Corruption = "training-schema";
        var policy = new PortfolioPolicy([], 0, "", "", Items: [new("tool", 1)], Preparation: new());
        Assert.Equal(StrategyStatus.Blocked, (await h.Factory.Create(policy).TickAsync()).Status);
        Assert.Equal(0, h.Handler.Actions);
    }

    [Fact]
    public async Task TrainingMaterialCeilingRejectsBeforeSpendingStock()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("item-production-bank");
        h.Handler.Corruption = "training-requirement";
        var policy = new PortfolioPolicy([], 0, "", "", Bank: BankPolicy.Bank, Items: [new("tool", 1)], Preparation: new(MaxIngredientUnits: 1));
        var result = await h.Factory.Create(policy).TickAsync();
        Assert.Equal(StrategyStatus.Blocked, result.Status);
        Assert.Equal("NoSupportedTrainingRecipe:weaponcrafting", Assert.Single(result.Candidates).Rejection);
        Assert.Equal(0, h.Handler.Actions);
    }

    [Fact]
    public async Task TrainingWithoutXpStopsWithoutAnotherCraft()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("skill-preparation");
        var policy = new PortfolioPolicy([], 0, "", "", Items: [new("tool", 1)], Preparation: new());
        var run = h.Factory.Create(policy);
        for (int i = 0; i < 3; i++) await run.TickAsync();
        h.Handler.Corruption = "training-no-xp";
        Assert.Equal("InvalidPostcondition", (await run.TickAsync()).Reason);
        Assert.Equal(StrategyStatus.Blocked, (await run.TickAsync()).Status);
        Assert.Equal(4, h.Handler.Actions);
    }

    [Fact]
    public async Task TrainingAndFinalGoalShareActionBudget()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("skill-preparation");
        var policy = new PortfolioPolicy([], 0, "", "", Items: [new("tool", 1)], Preparation: new());
        var run = h.Factory.Create(policy, new(Actions: 4));
        for (int i = 0; i < 4; i++) await run.TickAsync();
        Assert.Equal("BudgetExhausted", (await run.TickAsync()).Reason);
        Assert.Equal(4, h.Handler.Actions);
    }

    [Theory]
    [InlineData("skill-preparation", 9, 50, 2, 1)]
    [InlineData("resource-preparation", 7, 40, 1, 1)]
    public async Task ItemGoalTrainsMissingSkillAndCompletesWithoutConfigurationChanges(string scenario, int actions, int seconds, int level, int xp)
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset(scenario);
        var policy = new PortfolioPolicy([], 0, "", "", Items: [new("tool", 1)], Preparation: new());
        var saved = new Checkpoint();
        var run = h.Factory.Create(policy, checkpoints: saved, identity: "preparation");
        var decisions = new List<StrategyDecision>();
        for (int i = 0; i <= actions; i++) decisions.Add(await run.TickAsync());
        Assert.Equal(StrategyStatus.Completed, decisions[^1].Status);
        Assert.Equal(actions, h.Handler.Actions); Assert.Equal(seconds, decisions[^1].CooldownSeconds);
        Assert.Equal(level, run.State!.Character.GetProperty("weaponcrafting_level").GetInt32());
        Assert.Equal(xp, run.State.Character.GetProperty("weaponcrafting_xp").GetInt32());
        var inventory = CharacterObservation.Read(run.State.Character)!.Inventory;
        Assert.Equal(1, inventory["tool"]); Assert.Equal(1, inventory["protected"]);
        Assert.Equal(scenario == "skill-preparation" ? 1 : 0, inventory.GetValueOrDefault("bar"));
        Assert.Equal(scenario == "resource-preparation" ? 2 : 0, inventory.GetValueOrDefault("ore"));
        Assert.Equal(3, inventory.Count);
        string?[] expected = scenario == "skill-preparation"
            ? ["Move:4", "Gather:mining", "Move:3", "Craft:bar:1", "Move:4", "Gather:mining", "Move:3", "Craft:bar:1", "Craft:tool:1", null]
            : ["Move:4", "Gather:mining", "Gather:mining", "Move:5", "Gather:mining", "Move:3", "Craft:tool:1", null];
        Assert.Equal(expected, decisions.Select(x => x.Command));
        Assert.Equal(StrategyStatus.Completed, (await h.Factory.Create(policy, checkpoints: saved, identity: "preparation").TickAsync()).Status);
        Assert.Equal(actions, h.Handler.Actions);
        Assert.Contains(decisions, x => x.Candidates.Any(c => c.Prerequisite?.Parent == "item:tool"));
    }

    [Fact]
    public async Task TrainingReplyLossReconcilesWithoutRepeatingCraft()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("skill-preparation");
        var policy = new PortfolioPolicy([], 0, "", "", Items: [new("tool", 1)], Preparation: new());
        var saved = new Checkpoint(); var run = h.Factory.Create(policy, checkpoints: saved, identity: "training-loss");
        for (int i = 0; i < 3; i++) Assert.Equal(StrategyStatus.Selected, (await run.TickAsync()).Status);
        h.Handler.Corruption = "loss";
        Assert.Equal(StrategyStatus.UnknownOutcome, (await run.TickAsync()).Status);
        h.Handler.Corruption = null;
        Assert.Equal(StrategyStatus.Reconciled, (await h.Factory.Create(policy, checkpoints: saved, identity: "training-loss").TickAsync()).Status);
        Assert.Equal(4, h.Handler.Actions);
    }

    [Fact]
    public async Task MissingCraftSkillSelectsTrainingInsteadOfBlockedRecipe()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("item-production-bank");
        h.Handler.Corruption = "training-requirement";
        var policy = new PortfolioPolicy([], 0, "", "", Bank: BankPolicy.Bank, Items: [new("tool", 1)], Preparation: new());
        var run = h.Factory.Create(policy);
        var decision = await run.InspectAsync();
        Assert.Equal(StrategyStatus.Selected, decision.Status);
        // Available bank materials are for the training recipe; preparation is explained before withdrawal.
        var candidate = Assert.Single(decision.Candidates);
        var json = System.Text.Json.JsonSerializer.SerializeToElement(candidate);
        Assert.True(json.TryGetProperty("Prerequisite", out var prerequisite));
        Assert.Equal("weaponcrafting", prerequisite.GetProperty("Skill").GetString());
        Assert.Equal(2, prerequisite.GetProperty("Target").GetInt32());
        Assert.Equal(0, h.Handler.Actions);
    }

    [Fact]
    public async Task MeasuredResourceAlternativesRetainFullWorldPreflightAndReply()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset();
        h.Handler.Corruption = "resource-alternatives";
        var policy = new PortfolioPolicy([new("mining", 2, 30)], 0, "", "", Measurement: new());
        var run = h.Factory.Create(policy);
        var inspected = await run.InspectAsync();
        Assert.Equal(2, inspected.Candidates.Length); Assert.Equal(0, h.Handler.Actions);
        Assert.All(inspected.Candidates, x => Assert.Equal("Assumed", x.EstimateSource));
        Assert.Equal(StrategyStatus.Selected, (await run.TickAsync()).Status);
        Assert.Equal(StrategyStatus.Selected, (await run.TickAsync()).Status);
        Assert.Equal(2, h.Handler.Actions);
    }
    [Fact]
    public async Task UnsafePreparationNeverUsesFutureWeaponToAuthorizeFight()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("combat-preparation");
        h.Handler.Corruption = "unsafe-preparation";
        Assert.Equal(StrategyStatus.Blocked, (await h.Factory.Create(new([], 3, "dummy", "crafted_blade", PrepareEquipment: true)).TickAsync()).Status);
        Assert.Equal(0, h.Handler.Actions);
    }
    [Fact]
    public async Task PreparationUsesOneBudgetAcrossLootAndCraft()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("combat-preparation");
        var run = h.Factory.Create(new([], 3, "dummy", "crafted_blade", PrepareEquipment: true), new(Actions: 2));
        await run.TickAsync(); await run.TickAsync();
        Assert.Equal("BudgetExhausted", (await run.TickAsync()).Reason); Assert.Equal(2, h.Handler.Actions);
    }
    [Fact]
    public async Task CombatPreparationObtainsTwoLeavesCraftsEquipsAndCompletes()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("combat-preparation");
        var run = h.Factory.Create(new([], 3, "dummy", "crafted_blade", PrepareEquipment: true));
        StrategyDecision? last = null;
        for (int i = 0; i < 14; i++) last = await run.TickAsync();
        Assert.Equal(StrategyStatus.Completed, last!.Status); Assert.Equal(13, h.Handler.Actions); Assert.Equal(81, last.CooldownSeconds);
        var state = CombatObservation.Read(run.State!.Character)!;
        Assert.Equal(3, state.Level); Assert.Equal(17, state.Stats.Hp); Assert.Equal("crafted_blade", state.Weapon);
        Assert.Equal(3, state.Inventory["feather"]); Assert.Equal(3, state.Inventory["shard"]); Assert.Equal(1, state.Inventory["quick_blade"]);
    }
    [Theory]
    [InlineData("item-production-bank", 1)]
    [InlineData("item-production", 4)]
    public async Task LostProductionOrWithdrawalReplyReconcilesAfterRestart(string scenario, int beforeAction)
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset(scenario);
        var policy = new PortfolioPolicy([], 0, "", "", Bank: BankPolicy.Bank, Items: [new("tool", 1)]);
        var store = new Checkpoint(); var run = h.Factory.Create(policy, checkpoints: store, identity: "production");
        for (int i = 0; i < beforeAction; i++) Assert.Equal(StrategyStatus.Selected, (await run.TickAsync()).Status);
        h.Handler.Corruption = "loss"; Assert.Equal(StrategyStatus.UnknownOutcome, (await run.TickAsync()).Status);
        h.Handler.Corruption = null;
        Assert.Equal(StrategyStatus.Reconciled, (await h.Factory.Create(policy, checkpoints: store, identity: "production").TickAsync()).Status);
        Assert.Equal(beforeAction + 1, h.Handler.Actions);
    }
    [Theory]
    [InlineData("item-production", 6, 32)]
    [InlineData("item-production-bank", 5, 25)]
    public async Task ItemGoalProducesNestedRecipeFromGatheredOrBankStock(string scenario, int actions, int seconds)
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset(scenario);
        var policy = new PortfolioPolicy([], 0, "", "", Bank: BankPolicy.Bank, Items: [new("tool", 1)]);
        var run = h.Factory.Create(policy);
        StrategyDecision? last = null;
        for (int i = 0; i <= actions; i++) last = await run.TickAsync();
        Assert.Equal(StrategyStatus.Completed, last!.Status); Assert.Equal(actions, h.Handler.Actions); Assert.Equal(seconds, last.CooldownSeconds);
        var state = CharacterObservation.Read(run.State!.Character)!;
        Assert.Equal(1, state.Inventory["tool"]); Assert.Equal(1, state.Inventory["protected"]); Assert.Equal(2, state.Inventory.Count);
        Assert.Empty(run.State.Bank!.Items);
    }
    private static PortfolioPolicy BankPolicy => new([new("mining", 4, 30)], 0, "", "", Bank: new(System.Collections.Immutable.ImmutableDictionary<string, int>.Empty.Add("ore", 0)));
    private sealed class Checkpoint : IRunCheckpointStore
    {
        private RunCheckpoint? _saved;
        public RunCheckpoint? Load() => _saved;
        public void Save(RunCheckpoint value) => _saved = value;
    }
    [Fact]
    public async Task IncompleteBankPagesBlockBeforeAction()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("gathering-bank");
        h.Handler.Corruption = "bank-pages";
        Assert.Equal(StrategyStatus.Blocked, (await h.Factory.Create(BankPolicy).TickAsync()).Status);
        Assert.Equal(0, h.Handler.Actions);
    }
    [Fact]
    public async Task LostDepositReplyAfterRestartReconcilesBothInventoriesWithoutPost()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("gathering-bank");
        var store = new Checkpoint(); var run = h.Factory.Create(BankPolicy, checkpoints: store, identity: "bank-test");
        for (int i = 0; i < 4; i++) Assert.Equal(StrategyStatus.Selected, (await run.TickAsync()).Status);
        h.Handler.Corruption = "loss";
        Assert.Equal(StrategyStatus.UnknownOutcome, (await run.TickAsync()).Status);
        h.Handler.Corruption = null;
        var resumed = h.Factory.Create(BankPolicy, checkpoints: store, identity: "bank-test");
        Assert.Equal(StrategyStatus.Reconciled, (await resumed.TickAsync()).Status);
        Assert.Equal(5, h.Handler.Actions); Assert.Equal(2, resumed.State!.Bank!.Items["ore"]);
    }

    [Theory]
    [InlineData("bank-full", "BankFull")]
    [InlineData("bank-access", "NoSupportedBank")]
    [InlineData("protected-only", "NoDepositableStock")]
    public async Task ImpossibleBankRemediationStopsWithoutDeposit(string corruption, string reason)
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("gathering-bank");
        h.Handler.Corruption = corruption;
        var policy = corruption == "protected-only" ? BankPolicy with { Bank = new(System.Collections.Immutable.ImmutableDictionary<string, int>.Empty.Add("ore", 2)) } : BankPolicy;
        var run = h.Factory.Create(policy);
        for (int i = 0; i < 3; i++) await run.TickAsync();
        var blocked = await run.TickAsync();
        Assert.Equal(StrategyStatus.Blocked, blocked.Status); Assert.Equal(reason, Assert.Single(blocked.Candidates).Rejection);
        Assert.Equal(3, h.Handler.Actions);
    }
    [Fact]
    public async Task VerifiedRealClientCheckpointRoundTripsOnDisk()
    {
        await using var h = new Harness(new(), miningOnly: true); await h.Reset("gathering-bank");
        var memory = new Checkpoint();
        var run = h.Factory.Create(BankPolicy, checkpoints: memory, identity: "disk");
        await run.TickAsync();
        string directory = Path.Combine(Path.GetTempPath(), "artiact-disk-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var disk = new FileRunCheckpointStore(directory, "hero");
            disk.Save(memory.Load()!);
            Assert.NotNull(disk.Load()!.Verified);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task GatheringBankConservesProtectedStockAcrossTwoDeposits()
    {
        await using var h = new Harness(new(), miningOnly: true);
        await h.Reset("gathering-bank");
        var policy = BankPolicy;
        var store = new Checkpoint();
        var run = h.Factory.Create(policy, checkpoints: store, identity: "bank-completion");
        var decisions = new List<StrategyDecision>();
        for (int i = 0; i < 14; i++) decisions.Add(await run.TickAsync());
        Assert.Equal(StrategyStatus.Completed, decisions[^1].Status);
        Assert.Equal(13, h.Handler.Actions);
        Assert.Equal(71, decisions[^1].CooldownSeconds);
        Assert.Equal(new string?[] { "Move:4", "Gather:mining", "Gather:mining", "Move:6", "Deposit:ore:2", "Move:4",
            "Gather:mining", "Gather:mining", "Move:6", "Deposit:ore:2", "Move:4", "Gather:mining", "Gather:mining", null }, decisions.Select(x => x.Command));
        Assert.Equal(4, run.State!.Bank!.Items["ore"]);
        var character = CharacterObservation.Read(run.State.Character)!;
        Assert.Equal(2, character.Inventory["ore"]); Assert.Equal(1, character.Inventory["protected"]);
        Assert.Equal(4, run.State.Character.GetProperty("mining_level").GetInt32());
        Assert.Equal(0, run.State.Character.GetProperty("mining_xp").GetInt32());
        var resumed = h.Factory.Create(policy, checkpoints: store, identity: "bank-completion");
        Assert.Equal(StrategyStatus.Completed, (await resumed.TickAsync(new CancellationToken(true))).Status);
        Assert.Equal(StrategyStatus.Completed, (await resumed.TickAsync()).Status);
        Assert.Equal(13, h.Handler.Actions);
        Assert.Equal(1, store.Load()!.Initial!.Character.GetProperty("mining_level").GetInt32());
        Assert.Equal(4, store.Load()!.Latest!.Bank!.Items["ore"]);
    }
    [Fact]
    public async Task BoundedMiningCompletesAndRestartDoesNotDispatchAgain()
    {
        string directory = Path.Combine(Path.GetTempPath(), "artiact-bounded-" + Guid.NewGuid().ToString("N"));
        var settings = new ExecutionSettings { Mode = "Bounded", AllowActions = true, RunId = "mining-test", RunDirectory = directory };
        try
        {
            await using (var h = new Harness(settings, miningOnly: true))
            {
                await h.Reset(); h.Handler.MiningOnly = true;
                var result = await h.Runner.RunAsync(CancellationToken.None);
                Assert.True(result!.Status == StrategyStatus.Completed, System.Text.Json.JsonSerializer.Serialize(result)); Assert.Equal(3, h.Handler.Actions);
            }
            await using (var h = new Harness(settings, miningOnly: true))
            {
                Assert.Equal(StrategyStatus.Completed, (await h.Runner.RunAsync(CancellationToken.None))!.Status);
                Assert.Equal(0, h.Handler.Actions);
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task BoundedSecondExecutorDoesNotDispatch()
    {
        string directory = Path.Combine(Path.GetTempPath(), "artiact-owner-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var lease = new FileRunCheckpointStore(directory, "http://localhost/researcher");
            await using var h = new Harness(new() { Mode = "Bounded", AllowActions = true, RunId = "test", RunDirectory = directory }, miningOnly: true);
            Assert.Null(await h.Runner.RunAsync(CancellationToken.None)); Assert.Equal(0, h.Handler.Actions);
        }
        finally { Directory.Delete(directory, true); }
    }
    [Theory]
    [InlineData("Inspect", 0)]
    [InlineData("OneShot", 1)]
    public async Task MiningOnlyIgnoresCombatModelAndCombatCatalogs(string mode, int actions)
    {
        await using var h = new Harness(new() { Mode = mode, AllowActions = true }, miningOnly: true);
        await h.Reset(); h.Handler.MiningOnly = true;
        var result = await h.Runner.RunAsync(CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal(StrategyStatus.Selected, result.Status);
        Assert.Equal("skill:mining", result.Candidate);
        Assert.Single(result.Candidates);
        Assert.Equal(actions, h.Handler.Actions);
        Assert.DoesNotContain("/items", h.Handler.Reads);
        Assert.DoesNotContain("/monsters", h.Handler.Reads);
    }

    [Fact]
    public async Task MiningOnlyMovesThenGathersWithoutCombatNormalization()
    {
        await using var h = new Harness(new() { Mode = "OneShot", AllowActions = true }, miningOnly: true);
        await h.Reset(); h.Handler.MiningOnly = true;
        var run = h.Factory.Create(new([new("mining", 2, 30)], 0, "", ""));
        Assert.Equal("Move:4", (await run.TickAsync()).Command);
        Assert.Equal("Gather:mining", (await run.TickAsync()).Command);
        Assert.Equal("Gather:mining", (await run.TickAsync()).Command);
        Assert.Equal(StrategyStatus.Completed, (await run.TickAsync()).Status);
        Assert.Equal(3, h.Handler.Actions);
        Assert.Equal(2, run.State!.Character.GetProperty("mining_level").GetInt32());
    }

    [Theory]
    [InlineData("missing-skill")]
    [InlineData("inventory")]
    [InlineData("access")]
    [InlineData("stale")]
    [InlineData("skill-schema")]
    public async Task MiningOnlyInvalidInputsNeverDispatch(string corruption)
    {
        await using var h = new Harness(new() { Mode = "OneShot", AllowActions = true }, miningOnly: true);
        await h.Reset(); h.Handler.MiningOnly = true; h.Handler.Corruption = corruption;
        Assert.Equal(StrategyStatus.Blocked, (await h.Runner.RunAsync(CancellationToken.None))!.Status);
        Assert.Equal(0, h.Handler.Actions);
    }

    [Theory]
    [InlineData("Inspect", false, 0)]
    [InlineData("OneShot", true, 1)]
    public async Task StagedModesHaveExplicitActionBudgetAndExpiringReadiness(string mode, bool allow, int actions)
    {
        await using var h = new Harness(new() { Mode = mode, AllowActions = allow }); await h.Reset();
        var result = await h.Runner.RunAsync(CancellationToken.None);
        Assert.NotNull(result); Assert.Equal(StrategyStatus.Selected, result.Status);
        Assert.Equal(actions, h.Handler.Actions); Assert.True(h.State.Snapshot(30).Ready);
        h.Clock.Advance(31); Assert.False(h.State.Snapshot(30).Ready); Assert.Equal("StaleObservation", h.State.Snapshot(30).State);
    }
    [Theory]
    [InlineData("nonsense", true, "http://localhost")]
    [InlineData("OneShot", false, "http://localhost")]
    [InlineData("OneShot", true, "https://api.artifactsmmo.com")]
    [InlineData("OneShot", true, "https://other.invalid")]
    [InlineData("Inspect", false, "http://api.artifactsmmo.com")]
    public async Task InvalidModeConsentOrOriginDispatchesNothing(string mode, bool allow, string url)
    {
        await using var h = new Harness(new() { Mode = mode, AllowActions = allow }, url);
        Assert.Null(await h.Runner.RunAsync(CancellationToken.None)); Assert.Equal(0, h.Handler.Actions);
        Assert.Equal("ConfigurationRequiredOrInvalid", h.State.Snapshot(30).State); Assert.False(h.State.Snapshot(30).Ready);
    }
    [Theory]
    [InlineData("version")]
    [InlineData("schema")]
    [InlineData("route")]
    [InlineData("malformed")]
    [InlineData("stale")]
    public async Task DriftAndStaleObservationBlockBeforeDispatch(string corruption)
    {
        await using var h = new Harness(new() { Mode = "OneShot", AllowActions = true }); await h.Reset();
        h.Handler.Corruption = corruption;
        var result = await h.Runner.RunAsync(CancellationToken.None);
        Assert.Equal(StrategyStatus.Blocked, result!.Status); Assert.Equal(0, h.Handler.Actions); Assert.False(h.State.Snapshot(30).Ready);
        Assert.Equal(corruption == "stale" ? "StaleObservation" : "ApiContractUnavailableOrDrift", h.State.Snapshot(30).State);
    }
    [Fact]
    public async Task OneShotLostResponseOnlyReconcilesReadOnly()
    {
        await using var h = new Harness(new() { Mode = "OneShot", AllowActions = true }); await h.Reset();
        h.Handler.Corruption = "loss";
        var result = await h.Runner.RunAsync(CancellationToken.None);
        Assert.Equal(StrategyStatus.Reconciled, result!.Status); Assert.Equal(1, h.Handler.Actions);
    }
    [Fact]
    public async Task OneShotCancelledDuringAuthNeverDispatchesAction()
    {
        await using var h = new Harness(new() { Mode = "OneShot", AllowActions = true }); await h.Reset();
        using var cancel = new CancellationTokenSource(); h.Handler.Cancel = cancel;
        var result = await h.Runner.RunAsync(cancel.Token);
        Assert.Equal(StrategyStatus.Cancelled, result!.Status); Assert.Equal(0, h.Handler.Actions); Assert.False(h.State.Snapshot(30).Ready);
    }
    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 9, 6, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(int seconds) => _now = _now.AddSeconds(seconds);
    }
    private sealed class PanelExecution(Harness harness, ApiSettings api, PortfolioSettings portfolio) : IOperatorExecution
    {
        public Task<StrategyDecision?> ExecuteAsync(ExecutionSettings settings, CancellationToken token) =>
            new StagedExecution(settings, api, portfolio, harness.Factory, harness.State).RunAsync(token);
    }

    [Theory]
    [InlineData("item-production")]
    [InlineData("skill-preparation")]
    [InlineData("resource-preparation")]
    public async Task NeedsOrderBuildsSupportedChainAndStopsWithoutGeneralDevelopment(string scenario)
    {
        string directory = Path.Combine(Path.GetTempPath(), "artiact-needs-flow-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using var h = new Harness(new()); await h.Reset(scenario);
            var orders = new NeedOrderStore(directory, "researcher"); orders.Put(new("tool-order", "tool", 1), 0);
            var portfolio = new PortfolioSettings { Needs = new(directory, AllowCraft: true) };
            var settings = new ExecutionSettings { Mode = "Bounded", AllowActions = true, RunDirectory = directory, RunId = "needs", MaxActions = 100, MaxDecisions = 120, MaxNoProgress = 40, MaxSeconds = 5000 };
            var api = new ApiSettings { BaseUrl = "http://localhost", Character = "researcher", Username = "mock", Password = "mock" };
            using var store = new FileRunCheckpointStore(directory, "http://localhost/researcher");
            string identity = StagedExecution.RunIdentity(settings, api, portfolio.Policy());
            var limits = new StrategyLimits(120, 40, 100, 5000);
            var inspected = await h.Factory.Create(portfolio.Policy(), limits).InspectAsync();
            Assert.True(inspected.Status == StrategyStatus.Selected, JsonSerializer.Serialize(inspected));
            Assert.Equal(0, h.Handler.Actions);
            Assert.Contains(inspected.Candidates, x => x.Need?.Id == "tool-order");
            StrategyDecision? result = null;
            for (int i = 0; i < 100; i++)
            {
                result = await h.Factory.Create(portfolio.Policy(), limits, store, identity).TickAsync();
                if (result.Status != StrategyStatus.Selected) break;
            }
            Assert.Equal("NoActiveSupportedNeeds", result!.Reason);
            Assert.Equal("Completed", Assert.Single(orders.Read().Orders).Status);
            int actions = h.Handler.Actions;
            Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(await h.Factory.Create(portfolio.Policy(), limits, store, identity).TickAsync()));
            Assert.Equal(actions, h.Handler.Actions);
            Assert.True(FileRunCheckpointStore.CanArchive(store.Load()!));
            var completed = store.Load()!;
            Assert.False(FileRunCheckpointStore.CanArchive(completed with { PendingCommand = "Gather:mining" }));
            Assert.False(FileRunCheckpointStore.CanArchive(completed with { Journal = [] }));
            store.ArchiveCompleted(FileRunCheckpointStore.IdentityDigest(identity));
            Assert.Equal("Completed", Assert.Single(orders.Read().Orders).Status);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NeedsCancellationAfterSentCommandReconcilesBeforeStopping(bool lostReply)
    {
        string directory = Path.Combine(Path.GetTempPath(), "artiact-needs-cancel-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using var h = new Harness(new()); await h.Reset("skill-preparation");
            var orders = new NeedOrderStore(directory, "researcher"); orders.Put(new("order", "tool", 1), 0);
            var policy = new PortfolioSettings { Needs = new(directory, AllowCraft: true) }.Policy();
            var saved = new Checkpoint(); var limits = new StrategyLimits(120, 40, 100, 5000);
            if (lostReply) h.Handler.Corruption = "loss";
            var first = await h.Factory.Create(policy, limits, saved, "cancel").TickAsync();
            Assert.Equal(lostReply ? StrategyStatus.UnknownOutcome : StrategyStatus.Selected, first.Status);
            orders.Put(new("order", "tool", 1, Revision: 2, Status: "Cancelled"), 1);
            int actions = h.Handler.Actions;
            var next = await h.Factory.Create(policy, limits, saved, "cancel").TickAsync();
            if (lostReply)
            {
                Assert.Equal(StrategyStatus.Reconciled, next.Status);
                next = await h.Factory.Create(policy, limits, saved, "cancel").TickAsync();
            }
            Assert.Equal("NoActiveSupportedNeeds", next.Reason);
            Assert.Equal(actions, h.Handler.Actions);
            Assert.Equal(first.Attempts, next.Attempts);
            Assert.Equal("Cancelled", Assert.Single(orders.Read().Orders).Status);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task NeedsEmptyInspectThenOrderAndForbiddenFullChainNeverStartsPartialPreparation()
    {
        string directory = Path.Combine(Path.GetTempPath(), "artiact-needs-inspect-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using var h = new Harness(new()); await h.Reset("skill-preparation");
            var policy = new PortfolioSettings { Needs = new(directory) }.Policy();
            var run = h.Factory.Create(policy);
            Assert.Equal("NoActiveSupportedNeeds", (await run.InspectAsync()).Reason);
            Assert.False(Directory.Exists(directory));
            new NeedOrderStore(directory, "researcher").Put(new("order", "tool", 1), 0);
            var denied = await run.InspectAsync();
            Assert.Equal(StrategyStatus.Blocked, denied.Status);
            Assert.All(denied.Candidates, x => Assert.Null(x.Command));
            Assert.Equal(0, h.Handler.Actions);
            var allowed = await h.Factory.Create(policy with { Needs = new(directory, AllowCraft: true) }, new(120, 40, 100, 5000)).InspectAsync();
            Assert.Equal(StrategyStatus.Selected, allowed.Status);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(40, true)]
    public async Task NeedsNoProgressBoundaryAgreesWithActualTicksAndRestart(int maximum, bool feasible)
    {
        string directory = Path.Combine(Path.GetTempPath(), "artiact-needs-bound-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using var h = new Harness(new()); await h.Reset("item-production");
            new NeedOrderStore(directory, "researcher").Put(new("order", "tool", 1), 0);
            var policy = new PortfolioSettings { Needs = new(directory, AllowCraft: true) }.Policy();
            var limits = new StrategyLimits(120, maximum, 100, 5000); var saved = new Checkpoint();
            var inspected = await h.Factory.Create(policy, limits).InspectAsync();
            Assert.Equal(feasible ? StrategyStatus.Selected : StrategyStatus.Blocked, inspected.Status);
            if (!feasible) Assert.Contains(inspected.Candidates, x => x.Rejection == "EstimatedNoProgressInsufficient");
            StrategyDecision? result = null;
            for (int i = 0; i < 100; i++)
            {
                result = await h.Factory.Create(policy, limits, saved, "boundary").TickAsync();
                if (result.Status != StrategyStatus.Selected) break;
            }
            Assert.Equal(feasible ? "NoActiveSupportedNeeds" : "NoFeasibleCandidate", result!.Reason);
            Assert.Equal(feasible ? 6 : 0, h.Handler.Actions);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData("consumable-bank", false, true)]
    [InlineData("consumable-bank", false, false)]
    [InlineData("consumable-production", true, false)]
    [InlineData("recovery-training", true, false)]
    public async Task NeedsHpComparesAllowedAlternativesAndStopsAtSatisfiedParent(string scenario, bool craft, bool rest)
    {
        string directory = Path.Combine(Path.GetTempPath(), "artiact-needs-hp-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using var h = new Harness(new()); await h.Reset(scenario);
            var policy = RecoveryProfile(craft, rest) with { Needs = new(directory) };
            var limits = new StrategyLimits(120, 60, 100, 5000); var saved = new Checkpoint();
            var inspect = await h.Factory.Create(policy, limits).InspectAsync();
            Assert.True(inspect.Status == StrategyStatus.Selected, JsonSerializer.Serialize(inspect));
            Assert.All(inspect.Candidates.Where(x => x.Command is not null), x => Assert.Equal("ObservedHp", x.Need?.Source));
            StrategyDecision? result = null; StrategySession? run = null;
            for (int i = 0; i < 100; i++)
            {
                run = h.Factory.Create(policy, limits, saved, "hp-parent"); result = await run.TickAsync();
                if (result.Status != StrategyStatus.Selected) break;
            }
            Assert.True(result!.Reason == "NoActiveSupportedNeeds", JsonSerializer.Serialize(result));
            Assert.Equal(20, run!.State!.Character.GetProperty("hp").GetInt32());
            Assert.Equal(1, CharacterObservation.Read(run.State.Character)!.Inventory["protected"]);
            Assert.Equal(0, result.NoProgress);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData("cycle")]
    [InlineData("unsupported-skill")]
    [InlineData("capacity")]
    [InlineData("random-yield")]
    [InlineData("missing-resource")]
    public async Task NeedsFullChainRefusalsCannotBeBypassedByTinySlice(string corruption)
    {
        string directory = Path.Combine(Path.GetTempPath(), "artiact-needs-invalid-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using var h = new Harness(new()); await h.Reset("item-production");
            new NeedOrderStore(directory, "researcher").Put(new("order", "tool", 1), 0);
            var policy = new PortfolioSettings { Needs = new(directory, AllowCraft: true) }.Policy();
            var candidates = await h.InspectNeeds(policy, state =>
            {
                var raw = JsonNode.Parse(state.Character.GetRawText())!;
                if (corruption == "capacity") raw["inventory_max_items"] = 1;
                var items = state.Catalogs["items"].Select(x => JsonNode.Parse(x.GetRawText())!).ToArray();
                var tool = items.Single(x => x["code"]!.GetValue<string>() == "tool");
                if (corruption == "cycle") tool["craft"]!["items"] = JsonNode.Parse("""[{"code":"tool","quantity":1}]""");
                if (corruption == "unsupported-skill") tool["craft"]!["skill"] = "alchemy";
                var resources = state.Catalogs["resources"].Select(x => JsonNode.Parse(x.GetRawText())!).ToArray();
                if (corruption == "random-yield") foreach (var resource in resources) resource["drops"]![0]!["rate"] = 2;
                return new StrategyObservation(JsonSerializer.SerializeToElement(raw), state.Catalogs
                    .SetItem("items", items.Select(x => JsonSerializer.SerializeToElement(x)).ToImmutableArray())
                    .SetItem("resources", corruption == "missing-resource" ? [] : resources.Select(x => JsonSerializer.SerializeToElement(x)).ToImmutableArray()),
                    state.Policy, state.Bank, state.Context with { RemainingActions = 2, RemainingSeconds = 5000, RemainingDecisions = 120, MaxNoProgress = 40 }, state.Orders);
            });
            Assert.All(candidates, x => { Assert.Null(x.Command); Assert.NotNull(x.Rejection); });
            Assert.Equal(0, h.Handler.Actions);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task NeedsFreshSatisfiedHpStopsExistingPreparationWithoutMoreActions()
    {
        string directory = Path.Combine(Path.GetTempPath(), "artiact-needs-external-hp-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using var h = new Harness(new()); await h.Reset("recovery-training");
            var policy = RecoveryProfile() with { Needs = new(directory) }; var saved = new Checkpoint();
            var limits = new StrategyLimits(120, 60, 100, 5000);
            Assert.Equal(StrategyStatus.Selected, (await h.Factory.Create(policy, limits, saved, "external-hp").TickAsync()).Status);
            int actions = h.Handler.Actions; h.Handler.ObservedFullHp = true;
            var end = await h.Factory.Create(policy, limits, saved, "external-hp").TickAsync();
            Assert.Equal("NoActiveSupportedNeeds", end.Reason); Assert.Equal(actions, h.Handler.Actions);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public async Task NeedsExistingRootCountsOnlyAboveProtectedFloor(int floor, bool satisfied)
    {
        string directory = Path.Combine(Path.GetTempPath(), "artiact-needs-root-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using var h = new Harness(new()); await h.Reset("item-production");
            new NeedOrderStore(directory, "researcher").Put(new("root", "tool", 1), 0);
            var policy = new PortfolioSettings { Needs = new(directory, AllowCraft: true, Reserved: ImmutableDictionary<string, int>.Empty.Add("tool", floor)) }.Policy();
            var candidates = await h.InspectNeeds(policy, state =>
            {
                var raw = JsonNode.Parse(state.Character.GetRawText())!;
                raw["inventory"]!.AsArray().Add(JsonNode.Parse("""{"slot":2,"code":"tool","quantity":1}"""));
                return state.WithCharacter(JsonSerializer.SerializeToElement(raw));
            });
            Assert.Equal(satisfied, candidates.Any(x => x.Rejection == "NoActiveSupportedNeeds"));
            Assert.Equal(0, h.Handler.Actions);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NeedsBankPermissionControlsAlternativesAndPreservesProtectedStock(bool withdrawal)
    {
        string directory = Path.Combine(Path.GetTempPath(), "artiact-needs-bank-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using var h = new Harness(new()); await h.Reset("item-production-bank");
            new NeedOrderStore(directory, "researcher").Put(new("order", "tool", 1), 0);
            var policy = new PortfolioSettings { Needs = new(directory, AllowCraft: true, AllowBankWithdrawal: withdrawal,
                Reserved: ImmutableDictionary<string, int>.Empty.Add("protected", 1)), BankRetain = new() { ["ore"] = 0 } }.Policy();
            var limits = new StrategyLimits(120, 40, 100, 5000); var saved = new Checkpoint();
            var inspect = await h.Factory.Create(policy, limits).InspectAsync();
            Assert.Equal(withdrawal ? 2 : 1, inspect.Candidates.Length);
            Assert.All(inspect.Candidates, x => Assert.Equal(1, x.Value));
            StrategyDecision? result = null; StrategySession? run = null; var commands = new List<string?>();
            for (int i = 0; i < 100; i++)
            {
                run = h.Factory.Create(policy, limits, saved, "bank-order"); result = await run.TickAsync(); commands.Add(result.Command);
                if (result.Status != StrategyStatus.Selected) break;
            }
            Assert.Equal("NoActiveSupportedNeeds", result!.Reason);
            Assert.Equal(1, CharacterObservation.Read(run!.State!.Character)!.Inventory["protected"]);
            if (!withdrawal)
            {
                Assert.DoesNotContain(commands, x => x?.StartsWith("Withdraw:", StringComparison.Ordinal) == true);
                Assert.Equal(2, run.State.Bank!.Items["ore"]);
            }
            Assert.Equal(1, CharacterObservation.Read(run.State.Character)!.Inventory["tool"]);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData("craft")]
    [InlineData("withdraw")]
    [InlineData("skill")]
    public async Task NeedsCompatibilityRejectsMissingRequiredContract(string removed)
    {
        await using var factory = new MockServiceFactory(); using var client = factory.CreateClient();
        using var reset = await client.PostAsync("/__mock/reset", new StringContent("""{"scenario":"item-production"}""", Encoding.UTF8, "application/json"));
        reset.EnsureSuccessStatusCode();
        var schema = JsonNode.Parse(await client.GetStringAsync("/openapi.json"))!;
        var policy = new PortfolioSettings { Needs = new(Path.GetTempPath(), AllowCraft: true, AllowBankWithdrawal: true), BankRetain = new() { ["ore"] = 0 } }.Policy();
        Assert.True(ApiCompatibility.Compatible(JsonSerializer.SerializeToElement(schema), "8.2.3", policy));
        if (removed == "skill") schema["components"]!["schemas"]!["CharacterSchema"]!["properties"]!.AsObject().Remove("weaponcrafting_xp");
        else schema["paths"]!.AsObject().Remove(removed == "craft" ? "/my/{name}/action/crafting" : "/my/{name}/action/bank/withdraw/item");
        Assert.False(ApiCompatibility.Compatible(JsonSerializer.SerializeToElement(schema), "8.2.3", policy));
    }

    [Fact]
    public async Task NeedsBudgetSlicePreservesAbsoluteOrderForNextRun()
    {
        string directory = Path.Combine(Path.GetTempPath(), "artiact-needs-slice-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using var h = new Harness(new()); await h.Reset("item-production");
            var orders = new NeedOrderStore(directory, "researcher"); orders.Put(new("order", "tool", 1), 0);
            var portfolio = new PortfolioSettings { Needs = new(directory, AllowCraft: true) };
            var api = new ApiSettings { BaseUrl = "http://localhost", Character = "researcher", Username = "mock", Password = "mock" };
            var settings = new ExecutionSettings { RunId = "slice", MaxActions = 2, MaxDecisions = 120, MaxNoProgress = 40, MaxSeconds = 5000 };
            using var store = new FileRunCheckpointStore(directory, "http://localhost/researcher");
            string identity = StagedExecution.RunIdentity(settings, api, portfolio.Policy());
            var limits = new StrategyLimits(120, 40, 2, 5000);
            var inspected = await h.Factory.Create(portfolio.Policy(), limits).InspectAsync();
            var evidence = Assert.Single(inspected.Candidates).Need!;
            Assert.True(evidence.FullActions > evidence.SliceActions);
            Assert.NotEqual("ParentSatisfied", evidence.SliceResult);
            await h.Factory.Create(portfolio.Policy(), limits, store, identity).TickAsync();
            await h.Factory.Create(portfolio.Policy(), limits, store, identity).TickAsync();
            var end = await h.Factory.Create(portfolio.Policy(), limits, store, identity).TickAsync();
            Assert.Equal("AutonomousBudgetExhausted", end.Reason);
            Assert.Equal("Active", Assert.Single(orders.Read().Orders).Status);
            store.ArchiveCompleted(FileRunCheckpointStore.IdentityDigest(identity));
            var next = new Checkpoint(); StrategyDecision? result = null;
            for (int i = 0; i < 100; i++)
            {
                result = await h.Factory.Create(portfolio.Policy(), new(120, 40, 100, 5000), next, "next-slice").TickAsync();
                if (result.Status != StrategyStatus.Selected) break;
            }
            Assert.Equal("NoActiveSupportedNeeds", result!.Reason);
            Assert.Equal(6, h.Handler.Actions);
            Assert.Equal("Completed", Assert.Single(orders.Read().Orders).Status);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task ScheduleArchivesTwoBoundedRunsAndRestartCannotRefundSeriesBudget()
    {
        string directory = Path.Combine(Path.GetTempPath(), "artiact-schedule-flow-" + Guid.NewGuid().ToString("N"));
        var settings = new ExecutionSettings { AllowActions = true, RunDirectory = directory, MaxActions = 6, MaxDecisions = 40, MaxNoProgress = 10, MaxSeconds = 300 };
        var api = new ApiSettings { BaseUrl = "http://localhost", Character = "researcher", Username = "mock", Password = "mock" };
        var portfolio = new PortfolioSettings { AutonomousGoals = true };
        try
        {
            await using var h = new Harness(settings); await h.Reset("discovery-new");
            var schedule = new ScheduleSettings { Enabled = true, SeriesId = "flow", FirstRunUtc = h.Clock.GetUtcNow(), ExpiresUtc = h.Clock.GetUtcNow().AddHours(2),
                IntervalSeconds = 60, MaxRuns = 2, MaxTotalActions = 12, MaxTotalDecisions = 80, MaxTotalSeconds = 600 };
            ScheduleRunner Runner() => new(schedule, settings, api, portfolio, h.State,
                new ScheduledRun(new PanelExecution(h, api, portfolio), api, portfolio), h.Clock);
            var runner = Runner(); await runner.TickAsync();
            Assert.Equal("Waiting", runner.Snapshot().Status);
            Assert.InRange(h.Handler.Actions, 1, 6);
            int first = h.Handler.Actions;
            h.Clock.Advance(60); runner = Runner(); await runner.TickAsync();
            Assert.Equal("Completed", runner.Snapshot().Status);
            Assert.InRange(h.Handler.Actions - first, 1, 6);
            Assert.Equal(12, runner.Snapshot().ReservedActions);
            Assert.Equal(2, runner.Snapshot().Notifications.Count(x => x.Kind == "RunCompleted"));
            using (var store = new FileRunCheckpointStore(directory, "http://localhost/researcher")) Assert.Null(store.Load());
            int total = h.Handler.Actions;
            h.Clock.Advance(60); await Runner().TickAsync(); Assert.Equal(total, h.Handler.Actions);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OperatorCycleUsesRealClientAndPreservesTerminalOnRestart(bool stopInFlight)
    {
        string directory = Path.Combine(Path.GetTempPath(), "artiact-panel-cycle-" + Guid.NewGuid().ToString("N"));
        var settings = new ExecutionSettings { AllowActions = true, RunDirectory = directory };
        var api = new ApiSettings { BaseUrl = "http://localhost", Character = "researcher", Username = "mock", Password = "mock" };
        var portfolio = new PortfolioSettings { Skills = [new("mining", 2, 30)] };
        var request = new OperatorRunRequest("panel-cycle", 10, 300, 40, 10);
        try
        {
            await using var h = new Harness(settings, miningOnly: true);
            await h.Reset(); h.Handler.MiningOnly = true;
            var coordinator = new OperatorCoordinator(settings, api, portfolio, h.State, new PanelExecution(h, api, portfolio));
            var inspected = await coordinator.InspectAsync(request);
            Assert.True(inspected.Accepted, inspected.Reason); Assert.Equal(0, h.Handler.Actions);
            if (stopInFlight)
            {
                h.Handler.ActionReached = new(TaskCreationOptions.RunContinuationsAsynchronously);
                h.Handler.ActionRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
            }
            Assert.True((await coordinator.StartRunAsync(inspected.Receipt!)).Accepted);
            Assert.True((await coordinator.StartRunAsync(inspected.Receipt!)).Accepted);
            if (stopInFlight)
            {
                await h.Handler.ActionReached!.Task.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.Equal("StopRequested", (await coordinator.RequestStopAsync()).Reason);
                h.Handler.ActionRelease!.TrySetResult();
            }
            await coordinator.WaitForIdleAsync().WaitAsync(TimeSpan.FromSeconds(10));
            var expected = stopInFlight ? StrategyStatus.Cancelled : StrategyStatus.Completed;
            using (var store = new FileRunCheckpointStore(directory, "http://localhost/researcher"))
            {
                var saved = store.Load()!;
                Assert.Equal(expected, saved.Terminal!.Status);
                Assert.Null(saved.PendingCommand);
                Assert.All(saved.Journal, x => Assert.Equal("Verified", x.Status));
                Assert.Equal(stopInFlight ? 1 : 3, saved.Attempts);
            }
            int actions = h.Handler.Actions;
            var restarted = new StagedExecution(new ExecutionSettings { Mode = "Bounded", AllowActions = true, RunDirectory = directory,
                RunId = request.RunId, MaxActions = request.MaxActions, MaxSeconds = request.MaxSeconds, MaxDecisions = request.MaxDecisions, MaxNoProgress = request.MaxNoProgress },
                api, portfolio, h.Factory, h.State);
            Assert.Equal(expected, (await restarted.RunAsync(CancellationToken.None))!.Status);
            Assert.Equal(actions, h.Handler.Actions);
            using var reader = new FileRunCheckpointStore(directory, "http://localhost/researcher");
            string digest = FileRunCheckpointStore.IdentityDigest(reader.Load()!.Identity);
            if (stopInFlight) Assert.Throws<IOException>(() => reader.ArchiveCompleted(digest));
            else Assert.True(File.Exists(reader.ArchiveCompleted(digest)));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    private sealed class Harness : IAsyncDisposable
    {
        private readonly MockServiceFactory _factory = new();
        private readonly HttpClient _transport;
        public readonly Clock Clock = new();
        public readonly Handler Handler;
        public readonly OperationState State;
        public readonly StagedExecution Runner;
        public readonly StrategySessionFactory Factory;
        private readonly GameClient _client;
        private readonly CombatCatalog _catalog;
        private readonly CharacterService _characters = new();
        public StrategySession LowestSession(PortfolioPolicy policy, StrategyLimits limits)
        {
            var gathering = policy with { Consumable = null, Items = [], Skills = [new("mining", 2, 1), new("fishing", 2, 1)], Measurement = new(FullPaths: true) };
            return new(new HttpStrategyObserver(_client, _catalog, _characters, gathering.Identity, profile: gathering),
                [new AutonomousExecutionFlowTests.Lowest(gathering, new(_client, _characters))], new NoDelay(), limits, selection: gathering.Measurement);
        }
        public async Task<StrategyCandidate[]> InspectNeeds(PortfolioPolicy policy, Func<StrategyObservation, StrategyObservation> transform)
        {
            var state = await new HttpStrategyObserver(_client, _catalog, _characters, policy.Identity, profile: policy).ObserveAsync(CancellationToken.None);
            return new NeedsGoalDiscovery(policy, new(_client, _characters)).EvaluateAll(transform(state)).ToArray();
        }
        public Harness(ExecutionSettings execution, string origin = "http://localhost", bool miningOnly = false)
        {
            State = new(Clock); Handler = new(_factory.Server.CreateHandler(), Clock);
            _transport = new(Handler);
            var api = new ApiSettings { BaseUrl = "http://localhost", Character = "researcher", Username = "mock", Password = "mock" };
            var http = new GameHttpClient(new ClientFactory(_transport), api, Clock, (_, _) => Task.CompletedTask);
            var client = new GameClient(http, api, NullLogger<IGameClient>.Instance, new EmptyCache(), new ActivitySource("Staged"));
            _client = client; _catalog = new CombatCatalog(http);
            var factory = new StrategySessionFactory(client, _catalog, _characters, new NoDelay(), new ApiCompatibility(http, execution, State, Clock));
            Factory = factory;
            api.BaseUrl = origin;
            Runner = new(execution, api, miningOnly ? new() { Skills = [new("mining", 2, 30)] } : new() { Skills = [new("mining", 2, 30), new("woodcutting", 2, 20)], CombatTarget = 2, Monster = "dummy", Equipment = "quick_blade" }, factory, State);
        }
        public async Task Reset(string scenario = "strategy-portfolio")
        {
            using var response = await _transport.PostAsync("/__mock/reset", new StringContent(System.Text.Json.JsonSerializer.Serialize(new { scenario }), Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();
        }
        public async ValueTask DisposeAsync() { _transport.Dispose(); await _factory.DisposeAsync(); }
    }
    private sealed class Handler(HttpMessageHandler inner, Clock clock) : DelegatingHandler(inner)
    {
        public int Actions;
        public bool MiningOnly;
        public bool ObservedFullHp;
        public List<string> Reads = [];
        public string? Corruption;
        public CancellationTokenSource? Cancel;
        public TaskCompletionSource? ActionReached, ActionRelease;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var response = await base.SendAsync(request, token);
            string path = request.RequestUri!.AbsolutePath;
            if (ObservedFullHp && path.StartsWith("/characters/", StringComparison.Ordinal))
            {
                var body = JsonNode.Parse(await response.Content.ReadAsStringAsync(token))!;
                body["data"]!["hp"] = body["data"]!["max_hp"]!.GetValue<int>();
                response.Content = new StringContent(body.ToJsonString());
            }
            if (path == "/maps" && Corruption == "future-workshop")
            {
                var node = JsonNode.Parse(await response.Content.ReadAsStringAsync(token))!;
                node["data"]!.AsArray().Single(x => x!["map_id"]!.GetValue<int>() == 3)!["access"]!["type"] = "conditional";
                response.Content = new StringContent(node.ToJsonString());
            }
            if (path == "/monsters" && Corruption == "combat-effects" || path == "/maps" && Corruption == "combat-access" ||
                path == "/items" && Corruption is "combat-gear-effect" or "combat-recipe" || path.EndsWith("/action/equip", StringComparison.Ordinal) && Corruption == "combat-equip-result")
            {
                var node = JsonNode.Parse(await response.Content.ReadAsStringAsync(token))!;
                if (Corruption == "combat-effects") node["data"]!.AsArray().Single(x => x!["code"]!.GetValue<string>() == "guardian")!["effects"] = JsonNode.Parse("[{\"code\":\"poison\",\"value\":1}]");
                if (Corruption == "combat-access") node["data"]!.AsArray().Single(x => x!["map_id"]!.GetValue<int>() == 9)!["access"]!["type"] = "conditional";
                if (Corruption == "combat-gear-effect") node["data"]!.AsArray().Single(x => x!["code"]!.GetValue<string>() == "ward")!["effects"]![0]!["code"] = "unknown";
                if (Corruption == "combat-recipe") node["data"]!.AsArray().Single(x => x!["code"]!.GetValue<string>() == "ward")!["craft"]!["items"]![0]!["code"] = "missing_material";
                if (Corruption == "combat-equip-result") node["data"]!["character"]!["res_fire"] = 74;
                response.Content = new StringContent(node.ToJsonString());
            }
            if (path.EndsWith("/action/fight", StringComparison.Ordinal) && Corruption is "combat-unknown-result" or "combat-defeat")
            {
                var node = JsonNode.Parse(await response.Content.ReadAsStringAsync(token))!;
                node["data"]!["fight"]!["result"] = Corruption == "combat-defeat" ? "loss" : "unknown";
                response.Content = new StringContent(node.ToJsonString());
            }
            if (path == "/resources" && Corruption == "resource-alternatives")
            {
                var node = JsonNode.Parse(await response.Content!.ReadAsStringAsync(token))!;
                foreach (var resource in node["data"]!.AsArray()) resource!["skill"] = "mining";
                response.Content = new StringContent(node.ToJsonString());
            }
            if (path.StartsWith("/characters/", StringComparison.Ordinal) && Corruption == "unsafe-preparation")
            {
                var node = JsonNode.Parse(await response.Content!.ReadAsStringAsync(token))!;
                node["data"]!["attack_fire"] = 0; response.Content = new StringContent(node.ToJsonString());
            }
            if (path == "/my/bank" && Corruption == "bank-full")
                response.Content = new StringContent("{\"data\":{\"slots\":0}}");
            if (path == "/my/bank/items" && Corruption == "bank-pages")
                response.Content = new StringContent("{\"data\":[],\"page\":1,\"pages\":1,\"total\":1,\"size\":50}");
            if (path == "/maps" && Corruption == "bank-access")
            {
                var node = JsonNode.Parse(await response.Content!.ReadAsStringAsync(token))!;
                foreach (var map in node["data"]!.AsArray().Where(x => x!["map_id"]!.GetValue<int>() == 6)) map!["access"]!["type"] = "conditional";
                response.Content = new StringContent(node.ToJsonString());
            }
            if (request.Method == HttpMethod.Get) Reads.Add(path);
            if (path == "/items" && Corruption == "training-requirement")
            {
                var node = JsonNode.Parse(await response.Content.ReadAsStringAsync(token))!;
                foreach (var item in node["data"]!.AsArray())
                {
                    if (item!["code"]!.GetValue<string>() == "tool") item["craft"]!["level"] = 2;
                    if (item["code"]!.GetValue<string>() == "bar") item["level"] = 1;
                }
                response.Content = new StringContent(node.ToJsonString());
            }
            if (path.EndsWith("/action/crafting", StringComparison.Ordinal) && Corruption == "training-no-xp")
            {
                var node = JsonNode.Parse(await response.Content.ReadAsStringAsync(token))!;
                node["data"]!["character"]!["weaponcrafting_xp"] = 0;
                response.Content = new StringContent(node.ToJsonString());
            }
            if (path == "/openapi.json" && Corruption == "training-schema")
            {
                var node = JsonNode.Parse(await response.Content.ReadAsStringAsync(token))!;
                node["components"]!["schemas"]!["CharacterSchema"]!["properties"]!.AsObject().Remove("weaponcrafting_xp");
                response.Content = new StringContent(node.ToJsonString());
            }
            if (path == "/items" && Corruption == "training-cycle")
            {
                var node = JsonNode.Parse(await response.Content.ReadAsStringAsync(token))!;
                node["data"]!.AsArray().Single(x => x!["code"]!.GetValue<string>() == "bar")!["craft"]!["items"]![0]!["code"] = "tool";
                response.Content = new StringContent(node.ToJsonString());
            }
            if (path == "/maps" && Corruption == "training-no-access")
            {
                var node = JsonNode.Parse(await response.Content.ReadAsStringAsync(token))!;
                node["data"]!.AsArray().Single(x => x!["map_id"]!.GetValue<int>() == 5)!["access"]!["type"] = "conditional";
                response.Content = new StringContent(node.ToJsonString());
            }
            if (path.StartsWith("/characters/", StringComparison.Ordinal) && Corruption == "capacity-too-small")
            {
                var node = JsonNode.Parse(await response.Content.ReadAsStringAsync(token))!;
                node["data"]!["inventory_max_items"] = 2;
                response.Content = new StringContent(node.ToJsonString());
            }
            if (path == "/items" && Corruption == "food-effect")
            {
                var node = JsonNode.Parse(await response.Content.ReadAsStringAsync(token))!;
                node["data"]!.AsArray().Single(x => x!["code"]!.GetValue<string>() == "meal")!["effects"]![0]!["code"] = "gold";
                response.Content = new StringContent(node.ToJsonString());
            }
            if (path == "/openapi.json" && Corruption == "food-use-schema")
            {
                var node = JsonNode.Parse(await response.Content.ReadAsStringAsync(token))!;
                node["paths"]!.AsObject().Remove("/my/{name}/action/use");
                response.Content = new StringContent(node.ToJsonString());
            }
            if (path.EndsWith("/action/use", StringComparison.Ordinal) && Corruption == "food-result")
            {
                var node = JsonNode.Parse(await response.Content.ReadAsStringAsync(token))!;
                node["data"]!["character"]!["hp"] = 19;
                response.Content = new StringContent(node.ToJsonString());
            }
            if (MiningOnly && (path.StartsWith("/characters/", StringComparison.Ordinal) || path.Contains("/action/")))
            {
                var node = JsonNode.Parse(await response.Content!.ReadAsStringAsync(token))!;
                var character = path.Contains("/action/") ? node["data"]!["character"]! : node["data"]!;
                character["attack_water"] = 25;
                if (Corruption == "missing-skill") character.AsObject().Remove("mining_xp");
                if (Corruption == "inventory") character["inventory_max_items"] = character["inventory"]!.AsArray().Sum(x => x!["quantity"]!.GetValue<int>());
                response.Content = new StringContent(node.ToJsonString());
            }
            if (MiningOnly && path == "/openapi.json")
            {
                var node = JsonNode.Parse(await response.Content!.ReadAsStringAsync(token))!;
                foreach (var action in new[] { "fight", "rest", "equip", "unequip", "crafting" })
                    node["paths"]!.AsObject().Remove("/my/{name}/action/" + action);
                node["paths"]!.AsObject().Remove("/items"); node["paths"]!.AsObject().Remove("/monsters");
                node["components"]!["schemas"]!["MapLayer"] = JsonNode.Parse("{\"type\":\"string\",\"enum\":[\"overworld\",\"underground\",\"interior\"]}");
                foreach (var schema in new[] { "CharacterSchema", "MapSchema" })
                    node["components"]!["schemas"]![schema]!["properties"]!["layer"] = JsonNode.Parse("{\"$ref\":\"#/components/schemas/MapLayer\"}");
                if (Corruption == "skill-schema") node["components"]!["schemas"]!["CharacterSchema"]!["properties"]!.AsObject().Remove("mining_xp");
                response.Content = new StringContent(node.ToJsonString());
            }
            if (MiningOnly && path == "/maps" && Corruption == "access")
            {
                var node = JsonNode.Parse(await response.Content!.ReadAsStringAsync(token))!;
                foreach (var map in node["data"]!.AsArray()) map!["access"]!["type"] = "conditional";
                response.Content = new StringContent(node.ToJsonString());
            }
            if (path == "/token") Cancel?.Cancel();
            if (path.Contains("/action/"))
            {
                Actions++;
                if (ActionReached is not null) { ActionReached.TrySetResult(); await ActionRelease!.Task; }
                if (Corruption == "loss") { response.Dispose(); throw new HttpRequestException(); }
            }
            if (path.StartsWith("/characters/", StringComparison.Ordinal) && Corruption == "stale") clock.Advance(31);
            if (path == "/openapi.json" && Corruption is "version" or "schema" or "route" or "malformed")
            {
                var node = JsonNode.Parse(await response.Content!.ReadAsStringAsync(token))!;
                if (Corruption == "version") node["info"]!["version"] = "changed";
                if (Corruption == "schema") node["components"]!["schemas"]!["CharacterSchema"]!["properties"]!["hp"]!["type"] = "string";
                if (Corruption == "route") node["paths"]!.AsObject().Remove("/my/{name}/action/fight");
                response.Content = new StringContent(Corruption == "malformed" ? "{" : node.ToJsonString());
            }
            return response;
        }
    }
    private sealed class ClientFactory(HttpClient client) : IHttpClientFactory { public HttpClient CreateClient(string name) => client; }
    private sealed class EmptyCache : ICacheService
    {
        public Task<T?> GetFromCache<T>() where T : class => Task.FromResult<T?>(null);
        public Task SaveToCache<T>(T data) where T : class => Task.CompletedTask;
    }
    private sealed class NoDelay : IMiningCooldownDelay { public Task WaitAsync(int seconds, CancellationToken token) => Task.CompletedTask; }
}
