using System.Collections.Immutable;
using Artiact.Client;
using Artiact.Services.Combat;

namespace Artiact.Services.Strategy;

public sealed class StrategySessionFactory(GameClient client, CombatCatalog catalog,
    ICharacterService characters, IMiningCooldownDelay cooldown, Artiact.Services.Operation.ApiCompatibility? compatibility = null)
{
    public StrategySession Create(PortfolioPolicy policy, StrategyLimits? limits = null)
    {
        policy.Validate();
        var port = new StrategyActionPort(client, characters);
        var strategies = policy.Skills.Select(x => (IProgressionStrategy)new GatheringStrategy(x, policy, port))
            .Concat([new CombatMilestoneStrategy(policy, port), new EquipmentStrategy(policy, port)]);
        return new(new HttpStrategyObserver(client, catalog, characters, policy.Identity, compatibility), strategies, cooldown, limits);
    }
}

public sealed class HttpStrategyObserver(GameClient client, CombatCatalog catalog,
    ICharacterService characters, string policy, Artiact.Services.Operation.ApiCompatibility? compatibility = null) : IStrategyObserver
{
    public async Task<StrategyObservation> ObserveAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var operation = client.BeginOperation(token);
        var started = compatibility?.Now;
        if (compatibility is not null) await compatibility.CheckAsync(token);
        var catalogs = ImmutableDictionary.CreateBuilder<string, ImmutableArray<System.Text.Json.JsonElement>>(StringComparer.Ordinal);
        foreach (string name in new[] { "maps", "resources", "items", "monsters" })
            catalogs[name] = (await catalog.ReadPagesAsync(name, token)).ToImmutableArray();
        characters.SaveCharacter(await client.GetCharacter());
        token.ThrowIfCancellationRequested();
        var result = new StrategyObservation(client.LastCharacterPayload!.Value, catalogs.ToImmutable(), policy);
        if (compatibility is not null) compatibility.Observed(started!.Value, result.Fingerprint);
        return result;
    }
}
