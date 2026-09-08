using Artiact.Services.Strategy;
using System.Collections.Immutable;

namespace Artiact.Services.Operation;

public enum ExecutionMode { Inspect, OneShot, Legacy, Bounded }
public sealed class ExecutionSettings
{
    public string Mode { get; set; } = "Inspect";
    public bool AllowActions { get; set; }
    public bool LiveActionsApproved { get; set; }
    public string ExpectedApiVersion { get; set; } = "8.2.3";
    public int FreshnessSeconds { get; set; } = 30;
    public string RunId { get; set; } = "";
    public string RunDirectory { get; set; } = "";
    public int MaxActions { get; set; } = 100;
    public int MaxDecisions { get; set; } = 200;
    public int MaxNoProgress { get; set; } = 10;
    public int MaxSeconds { get; set; } = 3600;
    public ExecutionMode Validate(ApiSettings api)
    {
        if (!Enum.TryParse<ExecutionMode>(Mode, true, out var mode) || !Enum.IsDefined(mode) ||
            FreshnessSeconds is < 1 or > 300 || string.IsNullOrWhiteSpace(ExpectedApiVersion)) throw new ArgumentException("Invalid execution settings.");
        if (mode == ExecutionMode.Bounded && (string.IsNullOrWhiteSpace(RunId) || string.IsNullOrWhiteSpace(RunDirectory) ||
            MaxActions <= 0 || MaxDecisions <= 0 || MaxNoProgress <= 0 || MaxNoProgress > MaxDecisions || MaxSeconds is <= 0 or > 86400)) throw new ArgumentException("Invalid bounded run settings.");
        if (!Uri.TryCreate(api.BaseUrl, UriKind.Absolute, out var uri) || uri.UserInfo.Length != 0 || uri.Query.Length != 0 ||
            uri.Fragment.Length != 0 || uri.AbsolutePath != "/" ||
            !(uri.IsLoopback && uri.Scheme == "http" || uri.Scheme == "https" && uri.Host == "api.artifactsmmo.com" && uri.Port == 443))
            throw new ArgumentException("Unsupported API origin.");
        if (mode != ExecutionMode.Inspect && (!AllowActions || !uri.IsLoopback && !LiveActionsApproved))
            throw new ArgumentException("Action execution requires explicit opt-in.");
        if (string.IsNullOrWhiteSpace(api.Character) || string.IsNullOrWhiteSpace(api.Username) || string.IsNullOrWhiteSpace(api.Password))
            throw new ArgumentException("API configuration required.");
        return mode;
    }
}

public sealed class PortfolioSettings
{
    public bool AutonomousGoals { get; set; }
    public NeedsPolicy? Needs { get; set; }
    public RecoveryPolicy? Recovery { get; set; }
    public CombatDiscoveryPolicy? CombatDiscovery { get; set; }
    public AutonomousCombatPolicy? AutonomousCombat { get; set; }
    public SkillPreparationPolicy? Preparation { get; set; }
    public ConsumablePolicy? Consumable { get; set; }
    public bool CapacityAwareProduction { get; set; }
    public Dictionary<string, int> ProductionReserves { get; set; } = [];
    public Dictionary<string, int>? BankRetain { get; set; }
    public ItemMilestone[] Items { get; set; } = [];
    public bool PrepareEquipment { get; set; }
    public bool MeasuredSelection { get; set; }
    public bool FullPathSelection { get; set; }
    public decimal UnknownMultiplier { get; set; } = 2;
    public decimal SwitchRatio { get; set; } = 1.1m;
    public string[] MonsterAlternatives { get; set; } = [];
    public SkillMilestone[] Skills { get; set; } = [];
    public int CombatTarget { get; set; }
    public string Monster { get; set; } = "";
    public string Equipment { get; set; } = "";
    public decimal CombatValue { get; set; } = 10;
    public decimal EquipmentValue { get; set; } = 100;
    public decimal MoveSeconds { get; set; } = 7;
    public decimal GatherSeconds { get; set; } = 5;
    public decimal FightSeconds { get; set; } = 8;
    public decimal RestSeconds { get; set; } = 6;
    public decimal EquipmentSeconds { get; set; } = 3;
    public PortfolioPolicy Policy()
    {
        if (Needs is not null && AutonomousGoals) throw new ArgumentException("Choose needs or general development explicitly.");
        var result = new PortfolioPolicy(Skills.ToImmutableArray(), CombatTarget, Monster, Equipment, CombatValue,
            EquipmentValue, MoveSeconds, GatherSeconds, FightSeconds, RestSeconds, EquipmentSeconds,
            BankRetain is null ? null : new(BankRetain.ToImmutableDictionary(StringComparer.Ordinal)), Items.ToImmutableArray(), PrepareEquipment,
            MeasuredSelection || FullPathSelection || AutonomousGoals || Needs is not null ? new(UnknownMultiplier, SwitchRatio, FullPathSelection || AutonomousGoals || Needs is not null) : null, MonsterAlternatives.ToImmutableArray(), Preparation,
            CapacityAwareProduction ? new(ProductionReserves.ToImmutableDictionary(StringComparer.Ordinal)) : null, Consumable, AutonomousCombat, AutonomousGoals || Needs is not null, Recovery, CombatDiscovery, Needs);
        result.Validate(); return result;
    }
}
