using Artiact.Models;
using Artiact.Client;

namespace Artiact.Services;

public class MapService
{
    private readonly IGameClient _client;

    private Dictionary<ContentType, List<ContentCode>> contentCodeByType =
        new()
        {
            { ContentType.Resource, new List<ContentCode> { ContentCode.SalmonFishingSpot, ContentCode.GoldRocks } },
            {
                ContentType.Monster,
                new List<ContentCode>
                {
                    ContentCode.GoblinWolfrider, ContentCode.Orc, ContentCode.Ogre, ContentCode.Pig, ContentCode.Cyclops
                }
            },
            { ContentType.Workshop, new List<ContentCode> { ContentCode.Woodcutting } }
        };

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
            Y = place.Y,
        };
    }
}