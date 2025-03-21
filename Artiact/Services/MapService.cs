using Artiact.Contracts.Client;
using Artiact.Contracts.Models;
using Artiact.Contracts.Models.Api;

namespace Artiact.Services;

public class MapService : IMapService
{
    private readonly IGameClient _client;

    public MapService( IGameClient client )
    {
        _client = client;
    }

    public async Task<MapPoint?> GetByContentCode( ContentCode contentCode )
    {
        List<MapPlace> map = await _client.GetMap();
        MapPlace? place = map.FirstOrDefault( d => d.Content?.Code == contentCode.ToString() );
        if ( place is null )
        {
            return null;
        }

        return new MapPoint
        {
            X = place.X,
            Y = place.Y
        };
    }

    public async Task<MapPoint?> GetWorkshopBySkillCode( ContentCode skillCode )
    {
        List<MapPlace> map = await _client.GetMap();
        MapPlace? place = map.FirstOrDefault( d =>
            d.Content?.Type == ContentType.Workshop.ToString() && d.Content?.Code == skillCode.ToString() );
        if ( place is null )
        {
            return null;
        }

        return new MapPoint
        {
            X = place.X,
            Y = place.Y
        };
    }
}