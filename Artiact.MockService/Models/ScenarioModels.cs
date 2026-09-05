using System.Text.Json.Serialization;
using Artiact.Contracts.Models.Api;

namespace Artiact.SmartProxy.Models;

public sealed record ResetSummary(
    string Scenario,
    long Generation,
    [property: JsonPropertyName( "virtual_time" )] string VirtualTime,
    [property: JsonPropertyName( "trace_count" )] int TraceCount );

public sealed record TraceEntry(
    long Sequence,
    long Generation,
    string Action,
    string Character,
    [property: JsonPropertyName( "virtual_started_at" )] string VirtualStartedAt,
    [property: JsonPropertyName( "virtual_completed_at" )] string VirtualCompletedAt,
    [property: JsonPropertyName( "duration_seconds" )] int DurationSeconds,
    [property: JsonPropertyName( "from_x" )] int FromX,
    [property: JsonPropertyName( "from_y" )] int FromY,
    [property: JsonPropertyName( "to_x" )] int ToX,
    [property: JsonPropertyName( "to_y" )] int ToY,
    [property: JsonPropertyName( "mining_xp_delta" )] int MiningXpDelta,
    [property: JsonPropertyName( "item_code" )] string? ItemCode,
    [property: JsonPropertyName( "item_quantity_delta" )] int ItemQuantityDelta );

public sealed record StateSummary(
    string Scenario,
    long Generation,
    string Phase,
    [property: JsonPropertyName( "virtual_time" )] string VirtualTime,
    Character Character );

public sealed record StoreResult<T>( T? Value, string? Code, int Status ) where T : class
{
    public static StoreResult<T> Success( T value ) => new( value, null, StatusCodes.Status200OK );
    public static StoreResult<T> Failure( string code, int status ) => new( null, code, status );
}

public sealed class BasicMiningDefinition
{
    public required Character Character { get; init; }
    public required Map Maps { get; init; }
    public required ResourceResponse Resources { get; init; }
    public required ItemsResponse Items { get; init; }
    public required MonstersResponse Monsters { get; init; }
}
