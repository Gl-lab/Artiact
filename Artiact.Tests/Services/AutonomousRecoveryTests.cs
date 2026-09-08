using Artiact.Services.Operation;
using Microsoft.Extensions.Configuration;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Artiact.Services;
using Artiact.Services.Strategy;

namespace Artiact.Tests.Services;

public class AutonomousRecoveryTests
{
    private sealed class Observer(StrategyObservation state) : IStrategyObserver
    {
        public Task<StrategyObservation> ObserveAsync(CancellationToken token) => Task.FromResult(state);
    }
    private sealed class Delay : IMiningCooldownDelay
    {
        public Task WaitAsync(int seconds, CancellationToken token) => throw new InvalidOperationException("Inspect cannot dispatch");
    }
    private static StrategyObservation State(int hp = 4, int maximum = 20, string effect = "heal", int quantity = 2, bool cycle = false)
    {
        return new(JsonSerializer.SerializeToElement(new { name = "hero", map_id = 1, layer = "overworld", hp, max_hp = maximum,
            inventory_max_items = 10, inventory = new[] { new { code = "food", quantity } }, cooldown_expiration = "2000-01-01T00:00:00Z" }),
            new Dictionary<string, ImmutableArray<JsonElement>>
            {
                ["items"] = [JsonSerializer.SerializeToElement(new { code = "food", type = "consumable", effects = new[] { new { code = effect, value = 8 } },
                    craft = cycle ? new { skill = "cooking", level = 1, quantity = 1, items = new[] { new { code = "food", quantity = 1 } } } : null })],
                ["maps"] = [JsonSerializer.SerializeToElement(new { map_id = 1, layer = "overworld", access = new { type = "standard" }, interactions = new { content = (object?)null } })],
                ["resources"] = []
            }, "recovery-test");
    }
    private static Task<StrategyDecision> Inspect(StrategyObservation state, RecoveryPolicy? recovery = null)
    {
        var policy = new PortfolioSettings { AutonomousGoals = true, Recovery = recovery ?? new(AllowUse: true) }.Policy();
        return new StrategySession(new Observer(state), [new RecoveryGoalDiscovery(policy, new(null!, null!))], new Delay(), selection: policy.Measurement).InspectAsync();
    }
    [Fact]
    public async Task ObservedDeficitChoosesReadyFoodWithoutSkillOrCombatFields()
    {
        var result = await Inspect(State());
        Assert.Equal(StrategyStatus.Selected, result.Status); Assert.Equal("Use:food:2", result.Command);
        var candidate = Assert.Single(result.Candidates);
        Assert.Equal(3.2m, candidate.Value); Assert.Equal(0, candidate.Discovery!.RecipeUtility);
        Assert.Equal("hp", candidate.Path!.Goal.Kind); Assert.Equal(20, candidate.Discovery.Target);
        Assert.Equal(2, candidate.Command!.Charges!["recovery:use"]);
    }
    [Theory]
    [InlineData(20)]
    [InlineData(10)]
    public void FullHealthOrThresholdCreatesNoRecoveryNeed(int hp)
    {
        var policy = new PortfolioSettings { AutonomousGoals = true, Recovery = new(AllowUse: true) }.Policy();
        Assert.Empty(new RecoveryGoalDiscovery(policy, new(null!, null!)).EvaluateAll(State(hp)));
    }
    [Fact]
    public async Task RestWinsEqualCostWithoutManufacturingFood()
    {
        var result = await Inspect(State(99, 100), new(HpBelowPercent: 100, AllowUse: true, AllowRest: true));
        Assert.Equal("Rest", result.Command);
    }
    [Theory]
    [InlineData(false, "heal", "RecoveryUseNotAllowed")]
    [InlineData(true, "attack_fire", "UnsupportedRecoveryEffect")]
    public async Task ForbiddenUseAndUnknownEffectsCannotDispatch(bool allowUse, string effect, string reason)
    {
        var result = await Inspect(State(effect: effect), new(AllowUse: allowUse));
        Assert.Equal(reason, Assert.Single(result.Candidates).Rejection); Assert.Null(result.Command);
    }
    [Fact]
    public async Task RecipeCycleRejectsProductionBeforeAnyAction()
    {
        var result = await Inspect(State(quantity: 0, cycle: true), new(AllowUse: true, AllowCraft: true));
        Assert.Contains("RecipeCycle", Assert.Single(result.Candidates).Rejection); Assert.Null(result.Command);
    }
    [Fact]
    public void RecoveryCannotExpandManualOrBankPermissions()
    {
        Assert.Throws<ArgumentException>(() => new PortfolioSettings { Skills = [new("mining", 2, 1)], Recovery = new() }.Policy());
        Assert.Throws<ArgumentException>(() => new PortfolioSettings { AutonomousGoals = true, Recovery = new(AllowBankWithdrawal: true) }.Policy());
        Assert.DoesNotContain("Recovery", new PortfolioSettings { AutonomousGoals = true }.Policy().Identity);
    }

    [Fact]
    public void CompetingFoodsShareOneIngredientWithoutReservingBothAlternatives()
    {
        var original = State(hp: 12, quantity: 0);
        var character = JsonNode.Parse(original.Character.GetRawText())!;
        character["inventory"] = JsonNode.Parse("""[{"code":"fish","quantity":1}]""");
        character["cooking_level"] = 1; character["cooking_xp"] = 0; character["cooking_max_xp"] = 10;
        JsonElement Food(string code) => JsonSerializer.SerializeToElement(new { code, type = "consumable", level = 1,
            effects = new[] { new { code = "heal", value = 8 } }, craft = new { skill = "cooking", level = 1, quantity = 1, items = new[] { new { code = "fish", quantity = 1 } } } });
        var map = JsonSerializer.SerializeToElement(new { map_id = 1, layer = "overworld", access = new { type = "standard" },
            interactions = new { content = new { type = "workshop", code = "cooking" } } });
        var state = new StrategyObservation(JsonSerializer.SerializeToElement(character), original.Catalogs.SetItem("items", [Food("a"), Food("b")]).SetItem("maps", [map]), original.Policy);
        var policy = new PortfolioSettings { AutonomousGoals = true, Recovery = new(HpBelowPercent: 100, AllowUse: true, AllowCraft: true) }.Policy();
        var candidates = new RecoveryGoalDiscovery(policy, new(null!, null!)).EvaluateAll(state).ToArray();
        Assert.Equal(2, candidates.Length);
        Assert.All(candidates, candidate => { Assert.Null(candidate.Rejection); Assert.Equal(1, candidate.Path!.Materials["fish"]); Assert.Equal(0, candidate.Discovery!.RecipeUtility); });
        var protectedPolicy = policy with { Recovery = policy.Recovery! with { Reserved = ImmutableDictionary<string, int>.Empty.Add("fish", 1) } };
        Assert.All(new RecoveryGoalDiscovery(protectedPolicy, new(null!, null!)).EvaluateAll(state), candidate => Assert.Null(candidate.Command));
    }
    [Fact]
    public void RecoveryPermissionsArePartOfStablePolicyIdentity()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AutonomousGoals"] = "true", ["Recovery:AllowUse"] = "true"
        }).Build();
        var settings = new PortfolioSettings(); configuration.Bind(settings);
        Assert.Contains("Recovery", settings.Policy().Identity);
    }
}
