using System.Collections.Immutable;

namespace Artiact.Services.Combat;

public sealed record CombatCraftCommand(string Code, int Quantity, int OutputQuantity,
    ImmutableDictionary<string, int> Required, int WorkshopMapId, string Layer);
public sealed record CombatCraftPlan(ImmutableArray<CombatCraftCommand> Steps, string LootCode,
    int LootQuantity, CombatGear Gear);
