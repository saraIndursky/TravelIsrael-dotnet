using AutoMapper;
using Repository.Entities;
using Repository.Interfaces;
using Service.Dto.Rating;
using Service.Dto.Route;
using Service.Dto.RoutePoint;
using Service.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Service.Interface
{
    public interface IRouteService
    {
        Task<List<RouteDto>> GetAllAsync();
        Task<RouteDto> GetByIdAsync(int id);
        Task<RouteDto> AddAsync(RouteCreateDto dto);
        // Updates route details, optionally including its list of places
        Task<RouteDto> UpdateAsync(int userId, int id, RouteUpdateDto dto);
        Task<RouteDto> UpdateRatingAsync(int userId, int id, UpdateRatingDto dto);
        Task<RouteDto> SetApprovalStatusAsync(int userId, int id, ApprovalStatus newStatus, string? rejectReason = null);
        Task<bool> DeleteAsync(int userId, int id);
        Task<RoutePointDto> AddPointToRouteAsync(int userId, int routeId, RoutePointCreateDto pointId);
        Task<List<RoutePointDto>> ReplaceRoutePointsAsync(int userId, int routeId, List<RoutePointCreateDto> newPointIds);
        Task<List<RoutePointDto>> GetRoutePointsAsync(int routeId);
        Task<RoutePointDto> UpdateOrderInRouteAsync(int userId, int routePointId, int newOrder);
        Task<bool> DeleteRoutePointAsync(int userId, int routePointId);
    }
}
