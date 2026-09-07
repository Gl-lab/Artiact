using System.Collections.Immutable;
using System.Text.Json;

namespace Artiact.Services.Strategy;

public sealed record SkillMilestone(string Skill, int Target, decimal Value);
public sealed record PortfolioPolicy(ImmutableArray<SkillMilestone> Skills, int CombatTarget, string Monster,
    string Equipment, decimal CombatValue = 10, decimal EquipmentValue = 100,
    decimal MoveSeconds = 7, decimal GatherSeconds = 5, decimal FightSeconds = 8,
    decimal RestSeconds = 6, decimal EquipmentSeconds = 3, BankPolicy? Bank = null, ImmutableArray<ItemMilestone> Items = default, bool PrepareEquipment = false,
    MeasurementPolicy? Measurement = null, ImmutableArray<string> Monsters = default,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] SkillPreparationPolicy? Preparation = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] ProductionPolicy? Production = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] ConsumablePolicy? Consumable = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] AutonomousCombatPolicy? AutonomousCombat = null)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool CombatEnabled => CombatTarget > 0;
    [System.Text.Json.Serialization.JsonIgnore]
    public string Identity => JsonSerializer.Serialize(this with { Items = Items.IsDefault ? [] : Items, Monsters = Monsters.IsDefault ? [] : Monsters });
    public void Validate()
    {
        if (Measurement?.FullPaths == true && (!string.IsNullOrEmpty(Equipment) || Skills.Length > 8 || !Items.IsDefault && Items.Length > 32))
            throw new ArgumentException("Full-path selection supports bounded skill/item goals and autonomous equipment preparation.");
        if (AutonomousCombat is { } auto && (!CombatEnabled || PrepareEquipment || Equipment.Length != 0 || !Monsters.IsDefaultOrEmpty ||
            auto.Stages.IsDefaultOrEmpty || auto.Stages.Length > 10 || auto.Stages[^1].Target != CombatTarget ||
            auto.Stages.Select(x => x.Target).Where((x, i) => x <= (i == 0 ? 1 : auto.Stages[i - 1].Target)).Any() ||
            auto.Stages.Any(x => x.Monsters.IsDefaultOrEmpty || x.Monsters.Length > 10 || x.Monsters.Any(string.IsNullOrWhiteSpace) || x.Monsters.Distinct().Count() != x.Monsters.Length) ||
            auto.Equipment.IsDefault || auto.Equipment.Length > 20 || auto.Equipment.Any(string.IsNullOrWhiteSpace) || auto.Equipment.Distinct().Count() != auto.Equipment.Length))
            throw new ArgumentException("Invalid autonomous combat policy.");
        if (Consumable is { } food && (!(food.ParentItem == "combat" && AutonomousCombat is not null) && (Items.IsDefaultOrEmpty || !Items.Any(x => x.Code == food.ParentItem)) ||
                string.IsNullOrWhiteSpace(food.Code) || food.Code == food.ParentItem || food.HpBelowPercent is < 1 or > 100 ||
                food.Reserve < 0 || food.MinimumStock <= food.Reserve || food.TargetStock < food.MinimumStock || food.TargetStock > 10000 ||
                food.MaxUsed <= 0 || food.MaxMaterialUnits <= 0 || food.UseSeconds <= 0 || food.PreparationSeconds < 0))
            throw new ArgumentException("Invalid consumable policy.");
        if (Production is not null && (Bank is null || Production.Reserved is null || Production.Reserved.Any(x => string.IsNullOrWhiteSpace(x.Key) || x.Value < 0)))
            throw new ArgumentException("Invalid production reserve policy.");
        if (Preparation is not null && (Preparation.MaxIngredientUnits is < 1 or > 1000 || Preparation.MaxDepth is < 1 or > 16))
            throw new ArgumentException("Invalid skill preparation policy.");
        if (PrepareEquipment && (!CombatEnabled || string.IsNullOrWhiteSpace(Equipment))) throw new ArgumentException("Invalid preparation policy.");
        if (Measurement is not null && (Measurement.UnknownMultiplier is < 1 or > 10 || Measurement.SwitchRatio is < 1 or > 10)) throw new ArgumentException("Invalid measurement policy.");
        if (!Monsters.IsDefaultOrEmpty && (!CombatEnabled || PrepareEquipment || Monsters.Any(string.IsNullOrWhiteSpace) ||
            Monsters.Distinct(StringComparer.Ordinal).Count() != Monsters.Length || Monsters.Contains(Monster))) throw new ArgumentException("Invalid monster alternatives.");
        if (Bank is not null && (Bank.Retain.IsEmpty || Bank.Retain.Any(x => string.IsNullOrWhiteSpace(x.Key) || x.Value < 0)))
            throw new ArgumentException("Invalid bank policy.");
        if (!Items.IsDefault && (Items.Any(x => string.IsNullOrWhiteSpace(x.Code) || x.Quantity is <= 0 or > 10000 || x.Value is <= 0 or > 1_000_000) ||
            Items.Select(x => x.Code).Distinct(StringComparer.Ordinal).Count() != Items.Length)) throw new ArgumentException("Invalid item goals.");
        if (Skills.IsDefault || Skills.IsEmpty && Items.IsDefaultOrEmpty && !CombatEnabled || Skills.Any(x => x.Target <= 0 || x.Value is <= 0 or > 1_000_000 ||
                string.IsNullOrWhiteSpace(x.Skill) || !x.Skill.All(c => c is >= 'a' and <= 'z')) ||
            Skills.Select(x => x.Skill).Distinct(StringComparer.Ordinal).Count() != Skills.Length || CombatTarget < 0 ||
            (CombatEnabled ? string.IsNullOrWhiteSpace(Monster) : !string.IsNullOrEmpty(Monster) || !string.IsNullOrEmpty(Equipment)) ||
            new[] { CombatValue, EquipmentValue, MoveSeconds, GatherSeconds, FightSeconds, RestSeconds, EquipmentSeconds }
                .Any(x => x is <= 0 or > 1_000_000)) throw new ArgumentException("Invalid portfolio policy.");
    }
}
public sealed record BankPolicy(ImmutableDictionary<string, int> Retain);
public sealed record ItemMilestone(string Code, int Quantity, decimal Value = 30);
public sealed record SkillPreparationPolicy(int MaxIngredientUnits = 100, int MaxDepth = 12);
public sealed record ProductionPolicy(ImmutableDictionary<string, int> Reserved);
public sealed record ConsumablePolicy(string ParentItem, string Code, int HpBelowPercent = 50,
    int MinimumStock = 1, int TargetStock = 2, int Reserve = 0, int MaxUsed = 10, int MaxMaterialUnits = 100,
    bool AllowRest = false, decimal UseSeconds = 3, decimal PreparationSeconds = 30);
public sealed record CombatStage(int Target, ImmutableArray<string> Monsters);
public sealed record AutonomousCombatPolicy(ImmutableArray<CombatStage> Stages, ImmutableArray<string> Equipment);
