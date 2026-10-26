using Repository.Entities;
using Service.Dto.RoutePoint;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Service.Interface
{
    public interface IRoutePointService
    {
        Task<List<RoutePointDto>> GetByRouteIdAsync(int routeId);
        Task<RoutePointDto> GetByIdAsync(int id);
        Task<RoutePointDto> AddPointToRouteAsync(int routeId, RoutePointCreateDto placeId);
        Task<RoutePointDto> UpdateOrderInRouteAsync(int routePlaceId, int newOrder);
        Task<bool> DeleteRoutePointAsync(int id);
        Task<List<RoutePointDto>> ReplaceRoutePointsAsync(int routeId, List<RoutePointCreateDto> placeIds);
    }
}
