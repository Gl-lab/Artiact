using Artiact.Contracts.Models.Api;
using Artiact.SmartProxy.Models;

namespace Artiact.SmartProxy.Services;

public sealed partial class MockScenarioStore
{
    internal MockScenarioStore(BasicMiningDefinition basic, BasicMiningDefinition progression)
    {
        ValidateDefinition(basic);
        ValidateDefinition(progression);
        _definitions = new(StringComparer.Ordinal)
        {
            ["basic-mining"] = Clone(basic), ["mining-progression"] = Clone(progression)
        };
        _definition = _definitions["basic-mining"];
    }

    internal static void ValidateDefinition(BasicMiningDefinition definition)
    {
        var resources = definition.Resources?.Data;
        var maps = definition.Maps?.Data;
        var items = definition.Items?.Data;
        var character = definition.Character;
        if (resources is null || maps is null || items is null || definition.Monsters?.Data is null ||
            character is null || character.Inventory is null || character.MiningLevel < 1 ||
            character.MiningXp < 0 || character.MiningMaxXp <= 0 || character.MiningXp >= character.MiningMaxXp ||
            character.InventoryMaxItems < 0 ||
            resources.Any(r => r is null || string.IsNullOrWhiteSpace(r.Code) || r.Level < 1) ||
            resources.Select(r => r.Code).Distinct(StringComparer.Ordinal).Count() != resources.Count ||
            maps.Any(m => m is null) || maps.Select(m => (m.X, m.Y)).Distinct().Count() != maps.Count ||
            items.Any(i => i is null || string.IsNullOrWhiteSpace(i.Code)) ||
            items.Select(i => i.Code).Distinct(StringComparer.Ordinal).Count() != items.Count ||
            character.Inventory.Any(i => i is null || i.Slot < 1 || i.Quantity < 0 || i.Quantity > 0 && string.IsNullOrWhiteSpace(i.Code)) ||
            character.Inventory.Select(i => i.Slot).Distinct().Count() != character.Inventory.Count ||
            character.Inventory.Sum(i => (long)i.Quantity) > character.InventoryMaxItems)
            throw new InvalidOperationException("Invalid mining scenario definition.");
        var resourceCodes = resources.Select(r => r.Code).ToHashSet(StringComparer.Ordinal);
        var itemCodes = items.Select(i => i.Code).ToHashSet(StringComparer.Ordinal);
        if (maps.Any(m => m.Content?.Type == "resource" && !resourceCodes.Contains(m.Content.Code)) ||
            resources.Any(r => r.Drops is null || r.Drops.Count == 0 || r.Drops.Any(d => d is null || !itemCodes.Contains(d.Code))) ||
            !maps.Any(m => m.X == character.X && m.Y == character.Y))
            throw new InvalidOperationException("Invalid mining scenario references.");
    }

    internal static (int Level, int Xp) AwardXp(int level, int xp, int award)
    {
        int total = checked(xp + award);
        return (checked(level + total / 10), total % 10);
    }

    private StoreResult<ActionResponse> ProgressionMove(int x, int y)
    {
        var map = (_definition.Maps.Data ?? throw new InvalidOperationException("Validated maps are missing.")).FirstOrDefault(m => m.X == x && m.Y == y);
        if (map is null) return StoreResult<ActionResponse>.Failure("destination_not_found", 422);
        if (_character!.X == x && _character.Y == y) return StoreResult<ActionResponse>.Failure("invalid_transition", 409);
        Character candidate = Clone(_character);
        candidate.X = x;
        candidate.Y = y;
        return CommitProgression(candidate, "move", 7, null, new Destination
        {
            Name = map.Name, Skin = map.Skin, X = map.X, Y = map.Y, Content = Clone(map.Content)
        });
    }

    private StoreResult<ActionResponse> ProgressionGather()
    {
        var map = (_definition.Maps.Data ?? throw new InvalidOperationException("Validated maps are missing.")).FirstOrDefault(m => m.X == _character!.X && m.Y == _character.Y);
        var resource = map?.Content?.Type == "resource"
            ? (_definition.Resources.Data ?? throw new InvalidOperationException("Validated resources are missing.")).FirstOrDefault(r => r.Code == map.Content.Code && r.Skill == "mining") : null;
        if (resource is null) return StoreResult<ActionResponse>.Failure("gathering_not_available", 422);
        if (_character!.MiningLevel < resource.Level) return StoreResult<ActionResponse>.Failure("insufficient_mining_level", 422);
        string ore = resource.Drops![0].Code;
        var candidate = Clone(_character);
        var inventory = candidate.Inventory ?? throw new InvalidOperationException("Validated scenario inventory is missing.");
        Inventory? slot = inventory.FirstOrDefault(i => i.Code == ore)
            ?? inventory.FirstOrDefault(i => i.Quantity == 0);
        if (inventory.Sum(i => (long)i.Quantity) >= candidate.InventoryMaxItems || slot is null)
            return StoreResult<ActionResponse>.Failure("inventory_full", 422);
        try
        {
            (candidate.MiningLevel, candidate.MiningXp) = AwardXp(candidate.MiningLevel, candidate.MiningXp, 6);
            candidate.MiningMaxXp = 10;
            slot.Code = ore;
            slot.Quantity = checked(slot.Quantity + 1);
            return CommitProgression(candidate, "gathering", 5, ore, null);
        }
        catch (OverflowException) { return StoreResult<ActionResponse>.Failure("invalid_transition", 409); }
    }

    private StoreResult<ActionResponse> CommitProgression(Character candidate, string action, int seconds, string? ore, Destination? destination)
    {
        DateTime completed;
        try { completed = _virtualTime.AddSeconds(seconds); }
        catch (ArgumentOutOfRangeException) { return StoreResult<ActionResponse>.Failure("invalid_transition", 409); }
        var trace = new TraceEntry(_trace.Count + 1L, _generation, action, "MockHero", Format(_virtualTime),
            Format(completed), seconds, _character!.X, _character.Y, candidate.X, candidate.Y,
            ore is null ? 0 : 6, ore, ore is null ? 0 : 1);
        var response = new ActionResponse
        {
            Data = new()
            {
                Character = Clone(candidate), Destination = destination!,
                Details = new() { Xp = ore is null ? 0 : 6, Items = ore is null ? [] : [new() { Code = ore, Quantity = 1 }] },
                Cooldown = new() { TotalSeconds = seconds, RemainingSeconds = 0, StartedAt = _virtualTime,
                    Expiration = completed, Reason = "mock_virtual_elapsed" }
            }
        };
        _character = candidate;
        _phase = action == "move" ? "Moved" : "Gathered";
        _virtualTime = completed;
        _trace.Add(trace);
        return StoreResult<ActionResponse>.Success(response);
    }
}
