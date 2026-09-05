using System.Globalization;
using System.Text.Json;
using Artiact.Contracts.Models.Api;
using Artiact.SmartProxy.Models;

namespace Artiact.SmartProxy.Services;

public sealed class MockScenarioStore : IMockScenarioStore
{
    private static readonly DateTime Epoch = new( 2000, 1, 1, 0, 0, 0, DateTimeKind.Utc );
    private readonly object _sync = new();
    private readonly List<TraceEntry> _trace = [];
    private long _generation;
    private Character? _character;
    private DateTime _virtualTime = Epoch;
    private string _phase = "Uninitialized";
    private readonly BasicMiningDefinition _definition;

    public MockScenarioStore( IWebHostEnvironment environment )
    {
        string path = Path.Combine( environment.ContentRootPath, "BasicMiningScenario.json" );
        _definition = JsonSerializer.Deserialize<BasicMiningDefinition>(
            File.ReadAllText( path ),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true } )
            ?? throw new InvalidOperationException( "Basic mining scenario is missing." );
    }

    public ResetSummary Reset( string scenario )
    {
        lock ( _sync )
        {
            _generation++;
            _character = null;
            _virtualTime = Epoch;
            _phase = "Ready";
            _trace.Clear();
            return new ResetSummary( scenario, _generation, Format( Epoch ), 0 );
        }
    }

    public StoreResult<Character> GetCharacter( string name )
    {
        lock ( _sync )
        {
            if ( _phase == "Uninitialized" ) return StoreResult<Character>.Failure( "scenario_not_initialized", StatusCodes.Status409Conflict );
            if ( !IsMockHero( name ) ) return StoreResult<Character>.Failure( "character_not_found", StatusCodes.Status404NotFound );
            _character ??= Clone( _definition.Character );
            return StoreResult<Character>.Success( Clone( _character ) );
        }
    }

    public StoreResult<ActionResponse> Move( string name, string body )
    {
        lock ( _sync )
        {
            if ( _phase == "Uninitialized" ) return StoreResult<ActionResponse>.Failure( "scenario_not_initialized", StatusCodes.Status409Conflict );
            if ( !IsMockHero( name ) ) return StoreResult<ActionResponse>.Failure( "character_not_found", StatusCodes.Status404NotFound );
            if ( _character == null ) return StoreResult<ActionResponse>.Failure( "character_not_initialized", StatusCodes.Status409Conflict );
            if ( !TryReadMove( body, out int x, out int y ) ) return StoreResult<ActionResponse>.Failure( "invalid_move_request", StatusCodes.Status400BadRequest );
            if ( _phase != "Ready" ) return StoreResult<ActionResponse>.Failure( "invalid_transition", StatusCodes.Status409Conflict );
            if ( x != 2 || y != 0 ) return StoreResult<ActionResponse>.Failure( "destination_not_found", StatusCodes.Status422UnprocessableEntity );

            DateTime started = _virtualTime;
            int fromX = _character.X;
            int fromY = _character.Y;
            _virtualTime = _virtualTime.AddSeconds( 7 );
            _character.X = 2;
            _character.Y = 0;
            _phase = "Moved";
            _trace.Add( new TraceEntry( 1, _generation, "move", "MockHero", Format( started ), Format( _virtualTime ), 7, fromX, fromY, 2, 0, 0, null, 0 ) );

            return StoreResult<ActionResponse>.Success( new ActionResponse
            {
                Data = new ActionData
                {
                    Cooldown = new Cooldown
                    {
                        TotalSeconds = 7, RemainingSeconds = 0, StartedAt = started,
                        Expiration = _virtualTime, Reason = "mock_virtual_elapsed"
                    },
                    Destination = new Destination
                    {
                        Name = "Copper Rocks", Skin = "rocks", X = 2, Y = 0,
                        Content = new Artiact.Contracts.Models.Api.Content { Type = "resource", Code = "copper_rocks" }
                    },
                    Details = new Details { Xp = 0, Items = [] },
                    Character = Clone( _character )
                }
            } );
        }
    }

    public StoreResult<ActionResponse> Gather( string name )
    {
        lock ( _sync )
        {
            if ( _phase == "Uninitialized" ) return StoreResult<ActionResponse>.Failure( "scenario_not_initialized", StatusCodes.Status409Conflict );
            if ( !IsMockHero( name ) ) return StoreResult<ActionResponse>.Failure( "character_not_found", StatusCodes.Status404NotFound );
            if ( _character == null ) return StoreResult<ActionResponse>.Failure( "character_not_initialized", StatusCodes.Status409Conflict );
            if ( _character.X != 2 || _character.Y != 0 ) return StoreResult<ActionResponse>.Failure( "gathering_not_available", StatusCodes.Status422UnprocessableEntity );
            if ( _phase != "Moved" ) return StoreResult<ActionResponse>.Failure( "invalid_transition", StatusCodes.Status409Conflict );

            DateTime started = _virtualTime;
            _virtualTime = _virtualTime.AddSeconds( 5 );
            _character.MiningXp += 6;
            Inventory slot = _character.Inventory.First( item => item.Code == "copper_ore" || item.Quantity == 0 );
            slot.Code = "copper_ore";
            slot.Quantity += 1;
            _phase = "Gathered";
            _trace.Add( new TraceEntry( _trace.Count + 1, _generation, "gathering", "MockHero", Format( started ), Format( _virtualTime ), 5, 2, 0, 2, 0, 6, "copper_ore", 1 ) );

            return StoreResult<ActionResponse>.Success( new ActionResponse
            {
                Data = new ActionData
                {
                    Cooldown = new Cooldown
                    {
                        TotalSeconds = 5, RemainingSeconds = 0, StartedAt = started,
                        Expiration = _virtualTime, Reason = "mock_virtual_elapsed"
                    },
                    Destination = null!,
                    Details = new Details { Xp = 6, Items = [ new Item { Code = "copper_ore", Quantity = 1 } ] },
                    Character = Clone( _character )
                }
            } );
        }
    }

    public StoreResult<StateSummary> GetState( string name )
    {
        lock ( _sync )
        {
            if ( _phase == "Uninitialized" ) return StoreResult<StateSummary>.Failure( "scenario_not_initialized", StatusCodes.Status409Conflict );
            if ( !IsMockHero( name ) ) return StoreResult<StateSummary>.Failure( "character_not_found", StatusCodes.Status404NotFound );
            if ( _character == null ) return StoreResult<StateSummary>.Failure( "character_not_initialized", StatusCodes.Status409Conflict );
            return StoreResult<StateSummary>.Success( new StateSummary( "basic-mining", _generation, _phase, Format( _virtualTime ), Clone( _character ) ) );
        }
    }

    public StoreResult<Map> GetMaps() => ReadCatalog( _definition.Maps );
    public StoreResult<ResourceResponse> GetResources() => ReadCatalog( _definition.Resources );
    public StoreResult<ItemsResponse> GetItems() => ReadCatalog( _definition.Items );
    public StoreResult<MonstersResponse> GetMonsters() => ReadCatalog( _definition.Monsters );

    public StoreResult<IReadOnlyList<TraceEntry>> GetTrace()
    {
        lock ( _sync )
        {
            return _phase == "Uninitialized"
                ? StoreResult<IReadOnlyList<TraceEntry>>.Failure( "scenario_not_initialized", StatusCodes.Status409Conflict )
                : StoreResult<IReadOnlyList<TraceEntry>>.Success( _trace.ToArray() );
        }
    }

    private StoreResult<T> ReadCatalog<T>( T value ) where T : class
    {
        lock ( _sync )
        {
            return _phase == "Uninitialized"
                ? StoreResult<T>.Failure( "scenario_not_initialized", StatusCodes.Status409Conflict )
                : StoreResult<T>.Success( Clone( value ) );
        }
    }

    private static bool IsMockHero( string name ) => String.Equals( name, "MockHero", StringComparison.OrdinalIgnoreCase );
    private static bool TryReadMove( string body, out int x, out int y )
    {
        x = 0;
        y = 0;
        try
        {
            using JsonDocument document = JsonDocument.Parse( body );
            if ( document.RootElement.ValueKind != JsonValueKind.Object ) return false;
            JsonProperty[] properties = document.RootElement.EnumerateObject().ToArray();
            if ( properties.Length != 2 || properties.Count( property => property.Name == "x" ) != 1 ||
                 properties.Count( property => property.Name == "y" ) != 1 ) return false;
            JsonElement xValue = properties.Single( property => property.Name == "x" ).Value;
            JsonElement yValue = properties.Single( property => property.Name == "y" ).Value;
            return xValue.ValueKind == JsonValueKind.Number && yValue.ValueKind == JsonValueKind.Number &&
                   xValue.TryGetInt32( out x ) && yValue.TryGetInt32( out y );
        }
        catch ( JsonException )
        {
            return false;
        }
    }
    private static T Clone<T>( T value ) => JsonSerializer.Deserialize<T>( JsonSerializer.Serialize( value ) )!;
    private static string Format( DateTime value ) => value.ToString( "O", CultureInfo.InvariantCulture );
}
