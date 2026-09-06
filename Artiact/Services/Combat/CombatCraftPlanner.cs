using System.Collections.Immutable;
using System.Text.Json;
using Artiact.Client;
using Artiact.Contracts.Models;
using Artiact.Contracts.Models.Api;

namespace Artiact.Services.Combat;

public sealed class CombatCraftPlanner(GameClient client, CombatCatalog catalog)
{
    public async Task<CombatCraftPlan?> CreateAsync(string targetCode, CombatObservation state,
        CombatDestination destination, ICharacterService characters, CancellationToken token)
    {
        var maps = await catalog.ReadPagesAsync("maps", token);
        var items = await catalog.ReadPagesAsync("items", token);
        try
        {
            var usable = maps.Where(x => x.GetProperty("layer").GetString() == state.Layer &&
                x.GetProperty("access").GetProperty("type").GetString() == "standard" &&
                Empty(x.GetProperty("access"), "conditions") && Empty(x.GetProperty("interactions"), "transition")).ToArray();
            if (usable.Select(x => x.GetProperty("map_id").GetInt32()).Distinct().Count() != usable.Length) return null;
            var targetRaw = items.Single(x => x.GetProperty("code").GetString() == targetCode);
            var currentRaw = items.Single(x => x.GetProperty("code").GetString() == state.Weapon);
            if (!CombatCatalog.TryWeapon(targetRaw, state.Level, out int targetAttack) ||
                !CombatCatalog.TryWeapon(currentRaw, state.Level, out int currentAttack)) return null;
            var projected = state.Stats with { Attack = checked(state.Stats.Attack - currentAttack + targetAttack), Hp = state.MaxHp };
            var prediction = CombatPrediction.Evaluate(projected, destination.Monster);
            var baseline = CombatPrediction.Evaluate(state.Stats with { Hp = state.MaxHp }, destination.Monster);
            if (prediction.Viability != CombatViability.Safe || baseline.Viability == CombatViability.Unknown ||
                baseline.Viability == CombatViability.Safe && prediction.MaximumLoss >= baseline.MaximumLoss) return null;
            var adapter = new MapAdapter(usable);
            var finder = new WearCraftTargetFinder(client, new CraftTargetEvaluator(), new CraftChainBuilder(client),
                new TargetLootingResolver(client, adapter));
            var target = await finder.FindTargetAsync(targetCode, state.Inventory.Select(x => new Item { Code = x.Key, Quantity = x.Value }).ToList(), characters);
            if (target is null || target.Steps.Count == 0 || target.LootPrerequisite?.MonsterCode != destination.MonsterCode) return null;
            var commands = ImmutableArray.CreateBuilder<CombatCraftCommand>();
            foreach (var step in target.Steps)
            {
                token.ThrowIfCancellationRequested();
                var recipe = step.Item.Craft!;
                if (client.LastCharacterPayload!.Value.GetProperty(recipe.Skill + "_level").GetInt32() < recipe.Level) return null;
                var workshop = usable.Where(x => x.GetProperty("interactions").TryGetProperty("content", out var content) &&
                    content.ValueKind == JsonValueKind.Object && content.GetProperty("type").GetString() == "workshop" &&
                    content.GetProperty("code").GetString() == recipe.Skill)
                    .OrderBy(x => x.GetProperty("map_id").GetInt32()).FirstOrDefault();
                if (workshop.ValueKind == JsonValueKind.Undefined) return null;
                var required = step.RequiredItems.GroupBy(x => x.Code, StringComparer.Ordinal)
                    .ToImmutableDictionary(g => g.Key, g => checked(g.Sum(x => x.Quantity)), StringComparer.Ordinal);
                commands.Add(new(step.Item.Code, step.Quantity, checked(recipe.Quantity * step.Quantity), required,
                    workshop.GetProperty("map_id").GetInt32(), state.Layer));
            }
            return new(commands.ToImmutable(), target.LootPrerequisite.ItemCode, target.LootPrerequisite.RequiredQuantity,
                new(targetCode, projected));
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or FormatException or OverflowException or ArgumentException)
        { return null; }
    }

    private static bool Empty(JsonElement element, string name) => !element.TryGetProperty(name, out var value) ||
        value.ValueKind == JsonValueKind.Null || value.ValueKind == JsonValueKind.Array && value.GetArrayLength() == 0;

    private sealed class MapAdapter(JsonElement[] maps) : IMapService
    {
        public Task<MapPoint?> GetByContentCode(ContentCode code)
        {
            var map = maps.Where(x => x.GetProperty("interactions").TryGetProperty("content", out var content) &&
                content.ValueKind == JsonValueKind.Object && content.GetProperty("code").GetString() == code.ToString())
                .OrderBy(x => x.GetProperty("map_id").GetInt32()).FirstOrDefault();
            return Task.FromResult(map.ValueKind == JsonValueKind.Undefined ? null : new MapPoint
                { X = map.GetProperty("x").GetInt32(), Y = map.GetProperty("y").GetInt32() });
        }
        public Task<MapPoint?> GetWorkshopBySkillCode(ContentCode code) => GetByContentCode(code);
    }
}
