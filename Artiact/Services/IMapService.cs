using Artiact.Contracts.Models;

namespace Artiact.Services;

public interface IMapService
{
    Task<MapPoint?> GetByContentCode( ContentCode contentCode );
    Task<MapPoint?> GetWorkshopBySkillCode( ContentCode skillCode );
}