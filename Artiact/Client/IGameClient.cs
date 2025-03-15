using Artiact.Models;
using Artiact.Models.Api;

namespace Artiact.Client;

public interface IGameClient
{
    public Task<Character> GetCharacter();
    public Task<ActionResponse> Move( MapPoint target );
    public Task<ActionResponse> Gathering();
    public Task<ActionResponse> Fight();
    public Task<ActionResponse> Rest();
    public Task<ActionResponse> Crafting( Item item );
    public Task<ActionResponse> EquipItem( Inventory inventory );
    public Task<ActionResponse> UnequipItem( Inventory inventory );
    public Task<ActionResponse> UseItem( Item item );
    public Task<ActionResponse> Recycling( Item item );
    public Task<ActionResponse> DeleteItem( Item item );


    Task<List<MapPlace>> GetMap();
    Task<List<ResourceDatum>> GetResources();
    Task<List<ItemDatum>> GetItems();
    public Task<List<MonsterDatum>> GetMonsters();
}