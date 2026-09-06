using System.Collections.Immutable;
using System.Text.Json;

namespace Artiact.Services.Strategy;

public sealed record SkillMilestone(string Skill, int Target, decimal Value);
public sealed record PortfolioPolicy(ImmutableArray<SkillMilestone> Skills, int CombatTarget, string Monster,
    string Equipment, decimal CombatValue = 10, decimal EquipmentValue = 100,
    decimal MoveSeconds = 7, decimal GatherSeconds = 5, decimal FightSeconds = 8,
    decimal RestSeconds = 6, decimal EquipmentSeconds = 3)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string Identity => JsonSerializer.Serialize(this);
    public void Validate()
    {
        if (Skills.IsDefaultOrEmpty || Skills.Any(x => x.Target <= 0 || x.Value is <= 0 or > 1_000_000 ||
                string.IsNullOrWhiteSpace(x.Skill) || !x.Skill.All(c => c is >= 'a' and <= 'z')) ||
            Skills.Select(x => x.Skill).Distinct(StringComparer.Ordinal).Count() != Skills.Length || CombatTarget <= 0 ||
            string.IsNullOrWhiteSpace(Monster) || string.IsNullOrWhiteSpace(Equipment) ||
            new[] { CombatValue, EquipmentValue, MoveSeconds, GatherSeconds, FightSeconds, RestSeconds, EquipmentSeconds }
                .Any(x => x is <= 0 or > 1_000_000)) throw new ArgumentException("Invalid portfolio policy.");
    }
}
