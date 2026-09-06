using Artiact.Services.Strategy;
using System.Collections.Immutable;

namespace Artiact.Services.Operation;

public enum ExecutionMode { Inspect, OneShot, Legacy }
public sealed class ExecutionSettings
{
    public string Mode { get; set; } = "Inspect";
    public bool AllowActions { get; set; }
    public bool LiveActionsApproved { get; set; }
    public string ExpectedApiVersion { get; set; } = "8.2.3";
    public int FreshnessSeconds { get; set; } = 30;
    public ExecutionMode Validate(ApiSettings api)
    {
        if (!Enum.TryParse<ExecutionMode>(Mode, true, out var mode) || !Enum.IsDefined(mode) ||
            FreshnessSeconds is < 1 or > 300 || string.IsNullOrWhiteSpace(ExpectedApiVersion)) throw new ArgumentException("Invalid execution settings.");
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
        var result = new PortfolioPolicy(Skills.ToImmutableArray(), CombatTarget, Monster, Equipment, CombatValue,
            EquipmentValue, MoveSeconds, GatherSeconds, FightSeconds, RestSeconds, EquipmentSeconds);
        result.Validate(); return result;
    }
}
