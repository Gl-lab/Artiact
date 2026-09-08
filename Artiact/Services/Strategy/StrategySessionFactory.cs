using System.Collections.Immutable;
using Artiact.Client;
using Artiact.Services.Combat;

namespace Artiact.Services.Strategy;

public sealed class StrategySessionFactory(GameClient client, CombatCatalog catalog,
    ICharacterService characters, IMiningCooldownDelay cooldown, Artiact.Services.Operation.ApiCompatibility? compatibility = null)
{
    public StrategySession Create(PortfolioPolicy policy, StrategyLimits? limits = null,
        IRunCheckpointStore? checkpoints = null, string identity = "")
    {
        policy.Validate();
        var port = new StrategyActionPort(client, characters);
        var strategies = policy.Skills.Select(x => policy.Measurement is null ? (IProgressionStrategy)new GatheringStrategy(x, policy, port) : new ResourceAlternatives(x, policy, port)).ToList();
        if (policy.AutonomousCombat is not null)
        {
            IProgressionStrategy combat = new AutonomousCombatStrategy(policy, port);
            strategies.Add(policy.Consumable?.ParentItem == "combat" ? new ConsumableStrategy(combat, policy, port) : combat);
        }
        else if (policy.CombatEnabled) strategies.Add(new CombatMilestoneStrategy(policy, port));
        if (!policy.Monsters.IsDefaultOrEmpty) strategies.AddRange(policy.Monsters.Select(monster => new NamedStrategy("combat:" + monster,
            new CombatMilestoneStrategy(policy with { Monster = monster }, port))));
        if (!string.IsNullOrWhiteSpace(policy.Equipment)) strategies.Add(new EquipmentStrategy(policy, port));
        if (!policy.Items.IsDefaultOrEmpty)
            foreach (var goal in policy.Items)
            {
                IProgressionStrategy strategy = new SkillPrerequisiteStrategy(goal, policy, port);
                strategies.Add(policy.Consumable?.ParentItem == goal.Code ? new ConsumableStrategy(strategy, policy, port) : strategy);
            }
        if (policy.Measurement?.FullPaths == true) strategies = strategies.Select(x => (IProgressionStrategy)new FullPathStrategy(x, policy)).ToList();
        if (policy.Needs is not null) strategies.Add(new NeedsGoalDiscovery(policy, port));
        else if (policy.AutonomousGoals) strategies.Add(new AutonomousGoalDiscovery(policy, port, limits ?? new()));
        if (policy.Recovery is not null && policy.Needs is null) strategies.Add(new RecoveryGoalDiscovery(policy, port));
        if (policy.CombatDiscovery is not null) strategies.Add(new CombatGoalDiscovery(policy, port));
        var resources = new Dictionary<string, int>();
        if (policy.Consumable is { } food) { resources["use:" + food.Code] = food.MaxUsed; resources["materials:" + food.Code] = food.MaxMaterialUnits; }
        if (policy.Recovery is { } recovery) { resources["recovery:use"] = recovery.MaxUsed; resources["recovery:materials"] = recovery.MaxMaterialUnits; }
        if (policy.CombatDiscovery is { } combatPolicy) resources["combat:materials"] = combatPolicy.MaxMaterialUnits;
        return new(new HttpStrategyObserver(client, catalog, characters, policy.Identity, compatibility, policy), strategies, cooldown, limits,
            checkpoints: checkpoints, identity: identity, selection: policy.Measurement,
            resourceLimits: resources.Count == 0 ? null : resources,
            inspectOnly: policy.AutonomousGoals && checkpoints is null, autonomous: policy.AutonomousGoals,
            reconcileNeeds: policy.Needs is null ? null : state =>
            {
                var store = new NeedOrderStore(policy.Needs.Directory, state.Name);
                foreach (var order in state.Orders!.Orders.Where(x => x.Status == "Active" && NeedsGoalDiscovery.Satisfied(state, x, policy.Needs)))
                    store.Put(order with { Status = "Completed", Revision = checked(order.Revision + 1) }, order.Revision);
                return state.WithOrders(store.Read());
            });
    }
}

public sealed class HttpStrategyObserver(GameClient client, CombatCatalog catalog,
    ICharacterService characters, string policy, Artiact.Services.Operation.ApiCompatibility? compatibility = null,
    PortfolioPolicy? profile = null) : IStrategyObserver
{
    public async Task<StrategyObservation> ObserveAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var operation = client.BeginOperation(token);
        var started = compatibility?.Now;
        if (compatibility is not null) await compatibility.CheckAsync(token, profile);
        var catalogs = ImmutableDictionary.CreateBuilder<string, ImmutableArray<System.Text.Json.JsonElement>>(StringComparer.Ordinal);
        foreach (string name in profile is { CombatEnabled: false, CombatDiscovery: null } ? new[] { "maps", "resources" } : new[] { "maps", "resources", "items", "monsters" })
            catalogs[name] = (await catalog.ReadPagesAsync(name, token)).ToImmutableArray();
        if (profile is not null && (profile.AutonomousGoals || !profile.Items.IsDefaultOrEmpty) && !catalogs.ContainsKey("items"))
            catalogs["items"] = (await catalog.ReadPagesAsync("items", token)).ToImmutableArray();
        characters.SaveCharacter(await client.GetCharacter());
        token.ThrowIfCancellationRequested();
        var bank = profile?.Bank is null ? null : await client.GetBank();
        var result = new StrategyObservation(client.LastCharacterPayload!.Value, catalogs.ToImmutable(), policy, bank);
        if (profile?.Needs is { } needs) result = result.WithOrders(new NeedOrderStore(needs.Directory, result.Name).Read());
        if (compatibility is not null) compatibility.Observed(started!.Value, result.Fingerprint);
        return result;
    }
}
