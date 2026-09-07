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
        var strategies = policy.Skills.Select(x => (IProgressionStrategy)new GatheringStrategy(x, policy, port)).ToList();
        if (policy.CombatEnabled) strategies.Add(new CombatMilestoneStrategy(policy, port));
        if (!string.IsNullOrWhiteSpace(policy.Equipment)) strategies.Add(new EquipmentStrategy(policy, port));
        if (!policy.Items.IsDefaultOrEmpty) strategies.AddRange(policy.Items.Select(x => new ItemProductionStrategy(x, policy, port)));
        return new(new HttpStrategyObserver(client, catalog, characters, policy.Identity, compatibility, policy), strategies, cooldown, limits,
            checkpoints: checkpoints, identity: identity);
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
        foreach (string name in profile is { CombatEnabled: false } ? new[] { "maps", "resources" } : new[] { "maps", "resources", "items", "monsters" })
            catalogs[name] = (await catalog.ReadPagesAsync(name, token)).ToImmutableArray();
        if (profile is not null && !profile.Items.IsDefaultOrEmpty && !catalogs.ContainsKey("items"))
            catalogs["items"] = (await catalog.ReadPagesAsync("items", token)).ToImmutableArray();
        characters.SaveCharacter(await client.GetCharacter());
        token.ThrowIfCancellationRequested();
        var bank = profile?.Bank is null ? null : await client.GetBank();
        var result = new StrategyObservation(client.LastCharacterPayload!.Value, catalogs.ToImmutable(), policy, bank);
        if (compatibility is not null) compatibility.Observed(started!.Value, result.Fingerprint);
        return result;
    }
}
