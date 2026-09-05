using Artiact.Contracts.Models.Api;
using Artiact.SmartProxy.Models;

namespace Artiact.SmartProxy.Services;

public interface IMockScenarioStore
{
    ResetSummary Reset( string scenario );
    StoreResult<Character> GetCharacter( string name );
    StoreResult<ActionResponse> Move( string name, string body );
    StoreResult<ActionResponse> Gather( string name );
    StoreResult<StateSummary> GetState( string name );
    StoreResult<Map> GetMaps();
    StoreResult<ResourceResponse> GetResources();
    StoreResult<ItemsResponse> GetItems();
    StoreResult<MonstersResponse> GetMonsters();
    StoreResult<IReadOnlyList<TraceEntry>> GetTrace();
}
