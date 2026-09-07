using System.Collections.Immutable;
using System.Text.Json;

namespace Artiact.Services.Strategy;

public sealed record SkillMilestone(string Skill, int Target, decimal Value);
public sealed record PortfolioPolicy(ImmutableArray<SkillMilestone> Skills, int CombatTarget, string Monster,
    string Equipment, decimal CombatValue = 10, decimal EquipmentValue = 100,
    decimal MoveSeconds = 7, decimal GatherSeconds = 5, decimal FightSeconds = 8,
    decimal RestSeconds = 6, decimal EquipmentSeconds = 3, BankPolicy? Bank = null, ImmutableArray<ItemMilestone> Items = default)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool CombatEnabled => CombatTarget > 0;
    [System.Text.Json.Serialization.JsonIgnore]
    public string Identity => JsonSerializer.Serialize(this with { Items = Items.IsDefault ? [] : Items });
    public void Validate()
    {
        if (Bank is not null && (Bank.Retain.IsEmpty || Bank.Retain.Any(x => string.IsNullOrWhiteSpace(x.Key) || x.Value < 0)))
            throw new ArgumentException("Invalid bank policy.");
        if (!Items.IsDefault && (Items.Any(x => string.IsNullOrWhiteSpace(x.Code) || x.Quantity is <= 0 or > 10000 || x.Value is <= 0 or > 1_000_000) ||
            Items.Select(x => x.Code).Distinct(StringComparer.Ordinal).Count() != Items.Length)) throw new ArgumentException("Invalid item goals.");
        if (Skills.IsDefault || Skills.IsEmpty && Items.IsDefaultOrEmpty || Skills.Any(x => x.Target <= 0 || x.Value is <= 0 or > 1_000_000 ||
                string.IsNullOrWhiteSpace(x.Skill) || !x.Skill.All(c => c is >= 'a' and <= 'z')) ||
            Skills.Select(x => x.Skill).Distinct(StringComparer.Ordinal).Count() != Skills.Length || CombatTarget < 0 ||
            (CombatEnabled ? string.IsNullOrWhiteSpace(Monster) : !string.IsNullOrEmpty(Monster) || !string.IsNullOrEmpty(Equipment)) ||
            new[] { CombatValue, EquipmentValue, MoveSeconds, GatherSeconds, FightSeconds, RestSeconds, EquipmentSeconds }
                .Any(x => x is <= 0 or > 1_000_000)) throw new ArgumentException("Invalid portfolio policy.");
    }
}
public sealed record BankPolicy(ImmutableDictionary<string, int> Retain);
public sealed record ItemMilestone(string Code, int Quantity, decimal Value = 30);
