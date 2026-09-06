using Artiact.Contracts.Client;
using Artiact.Contracts.Models.Api;
using Artiact.Models;

namespace Artiact.Services;

public sealed class MiningDestinationResolver(IGameClient gameClient)
{
    public async Task<(MiningDestination? Destination, GoalDecisionReason? Reason)> ResolveAsync(Character character)
    {
        // Loading failures belong to the client/worker recovery boundary, not catalog validation.
        var resources = await gameClient.GetResources();
        var maps = await gameClient.GetMap();
        return Rank(character, resources, maps);
    }

    public static (MiningDestination? Destination, GoalDecisionReason? Reason) Rank(
        Character character, IReadOnlyList<ResourceDatum>? resources, IReadOnlyList<MapPlace>? maps)
    {
        if (resources is null || maps is null)
            return (null, GoalDecisionReason.InvalidMiningCatalog);

        HashSet<string> codes = new(StringComparer.Ordinal);
        foreach (var resource in resources)
        {
            if (resource is null || string.IsNullOrWhiteSpace(resource.Code) ||
                resource.Level < 1 || !codes.Add(resource.Code))
                return (null, GoalDecisionReason.InvalidMiningCatalog);
        }

        HashSet<(int X, int Y)> coordinates = new();
        foreach (var map in maps)
        {
            if (map is null || !coordinates.Add((map.X, map.Y)))
                return (null, GoalDecisionReason.InvalidMiningCatalog);
        }

        var destinations = from resource in resources
                           where string.Equals(resource.Skill, "mining", StringComparison.OrdinalIgnoreCase)
                                 && resource.Level <= Math.Max(1, character.MiningLevel)
                                 && (long)character.MiningLevel < (long)resource.Level + 10
                           join map in maps.Where(map => map.Content?.Type == "resource" &&
                               !string.IsNullOrWhiteSpace(map.Content.Code))
                               on resource.Code equals map.Content.Code
                           select new MiningDestination(resource.Code, resource.Level, map.X, map.Y);

        var selected = destinations.OrderByDescending(destination => destination.ResourceLevel)
            .ThenBy(destination => Math.Abs((long)destination.X - character.X) +
                                   Math.Abs((long)destination.Y - character.Y))
            .ThenBy(destination => destination.ResourceCode, StringComparer.Ordinal)
            .ThenBy(destination => destination.X)
            .ThenBy(destination => destination.Y)
            .FirstOrDefault();
        return selected is null ? (null, GoalDecisionReason.NoMiningDestination) : (selected, null);
    }
}
