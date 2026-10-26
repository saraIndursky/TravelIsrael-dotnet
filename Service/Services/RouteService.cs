using AutoMapper;
using Repository.Entities;
using Repository.Interfaces;
using Service.Dto.Rating;
using Service.Dto.Route;
using Service.Dto.RoutePoint;
using Service.Interface;
using Service.Services;
using System.Drawing;
using System.Threading.Tasks;

namespace Service.Services
{
    public class RouteService : IRouteService
    {
        private readonly IRouteRepository repository;
        private readonly ITypeRepository repositoryType;
        private readonly IRegionRepository repositoryRegion;
        private readonly IUserRepository repositoryUser;
        private readonly IRoutePointService routePointService;
        private readonly IRecalculateAllTripsContainingPlaceOrRouteService recalculateAllTripsContainingPlaceOrRouteService;
        private readonly IMapper mapper;

        const double MIN_REAL_TEMP = -10;
        const double MAX_REAL_TEMP = 50;
        const double MAX_REAL_WIND = 200;

        public RouteService(IRouteRepository repository, ITypeRepository repositoryType, IRegionRepository repositoryRegion, IUserRepository repositoryUser,IRoutePointService routePointService, IRecalculateAllTripsContainingPlaceOrRouteService recalculateAllTripsContainingPlaceOrRouteService, IMapper mapper)
        {
            this.repository = repository;
            this.repositoryType = repositoryType;
            this.repositoryRegion = repositoryRegion;
            this.repositoryUser = repositoryUser;
            this.routePointService = routePointService;
            this.recalculateAllTripsContainingPlaceOrRouteService = recalculateAllTripsContainingPlaceOrRouteService;
            this.mapper = mapper;
        }

        public async Task<RouteDto> AddAsync(RouteCreateDto dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            if (string.IsNullOrWhiteSpace(dto.Name))
                throw new ArgumentException("Route name is required");

            if (string.IsNullOrWhiteSpace(dto.Description))
                throw new ArgumentException("Route description is required");

            var routeType = await repositoryType.GetByIdRouteTypeAsync(dto.TypeId);
            if (routeType == null)
                throw new InvalidOperationException("Route type is not exists");

            var routeRegion = await repositoryRegion.GetByIdAsync(dto.RegionId ?? 0);
            if (routeRegion == null)
                throw new InvalidOperationException("Region is not exists");

            if (dto.DurationMinutes <= 0)
                throw new InvalidOperationException("Duration minutes must be bigger than zero");

            if (dto.LengthKm <= 0)
                throw new InvalidOperationException("Length km must be bigger than zero");
            
            if (dto.StartLongitude == 0)
                throw new ArgumentException("Route start longitude is required");

            if (dto.StartLatitude == 0)
                throw new ArgumentException("Route start latitude is required");
            
            if (dto.EndLongitude == 0)
                throw new ArgumentException("Route end longitude is required");

            if (dto.EndLatitude == 0)
                throw new ArgumentException("Route end latitude is required");

            if (dto.Price < 0)
                throw new InvalidOperationException("Price cannot be less than zero");

            if (dto.ClosingTime < dto.OpeningTime)
                throw new ArgumentException("Route closing time can not be earlier than opening time");

            var existingUser = await repositoryUser.GetByIdAsync(dto.CreatedByUserId);
            if (existingUser == null)
                throw new InvalidOperationException("User is not exists");

            if (dto.MinTemperature.HasValue &&
                (dto.MinTemperature < MIN_REAL_TEMP || dto.MinTemperature > MAX_REAL_TEMP))
                throw new ArgumentException("MinTemperature out of realistic range");

            if (dto.MaxTemperature.HasValue &&
                (dto.MaxTemperature < MIN_REAL_TEMP || dto.MaxTemperature > MAX_REAL_TEMP))
                throw new ArgumentException("MaxTemperature out of realistic range");

            if (dto.MinTemperature > dto.MaxTemperature)
                throw new ArgumentException("MinTemperature cannot be greater than MaxTemperature");

            if (dto.MaxRainProbability.HasValue &&
                (dto.MaxRainProbability < 0 || dto.MaxRainProbability > 100))
                throw new ArgumentException("Rain probability must be between 0 and 100");

            if (dto.MaxHumidity.HasValue &&
                (dto.MaxHumidity < 0 || dto.MaxHumidity > 100))
                throw new ArgumentException("Humidity must be between 0 and 100");

            if (dto.MaxCloudCoverage.HasValue &&
                (dto.MaxCloudCoverage < 0 || dto.MaxCloudCoverage > 100))
                throw new ArgumentException("Cloud coverage must be between 0 and 100");

            if (dto.MaxWindSpeed < 0 || dto.MaxWindSpeed > MAX_REAL_WIND)
                throw new ArgumentException("Wind speed out of realistic range");

            var route = mapper.Map<Route>(dto);

            // Approval status by user role
            if (existingUser.Role == Role.Admin)
            {
                // An admin chooses whether to submit for approval or keep a draft
                route.ApprovalStatus = dto.SendForApproval ? ApprovalStatus.Approved : ApprovalStatus.Draft;
                route.ApprovedAt = dto.SendForApproval ? DateOnly.FromDateTime(DateTime.UtcNow) : null;
                route.RejectReason = null;
            }
            else
            {
                // A regular user gets a draft or Pending, as requested
                route.ApprovalStatus = dto.SendForApproval ? ApprovalStatus.Pending : ApprovalStatus.Draft;
                route.ApprovedAt = null;
                route.RejectReason = null;
            }

            route.Difficulty = CalculateDifficulty(dto.LengthKm, dto.DurationMinutes);
            route.AllowRain = dto.AllowRain;
            route.HasCommonWeather = dto.MinTemperature <= dto.MaxTemperature;
            route.CreatedAt = DateOnly.FromDateTime(DateTime.Today);
            route.AverageRating = 0;
            route.RatingsCount = 0;

            route = await repository.AddAsync(route);

            // Add the route points, if any were sent
            if (dto.Points != null && dto.Points.Any())
            {
                await routePointService.ReplaceRoutePointsAsync(route.Id, dto.Points);
            }

            return mapper.Map<RouteDto>(await repository.GetByIdAsync(route.Id));
        }
        public async Task<List<RouteDto>> GetAllAsync()
        {
            return mapper.Map<List<RouteDto>>(await repository.GetAllAsync());
        }

        public async Task<RouteDto> GetByIdAsync(int id)
        {
            var route = await repository.GetByIdAsync(id);
            if (route == null)
                throw new KeyNotFoundException($"Route with id {id} not found");

            return mapper.Map<RouteDto>(route);
        }

        public async Task<RouteDto> UpdateAsync(int userId, int id, RouteUpdateDto dto)
        {
            var route = await repository.GetByIdAsync(id);
            if (route == null)
                throw new KeyNotFoundException($"Route with id {id} not found");

            var user = await repositoryUser.GetByIdAsync(userId);
            if (user == null)
                throw new UnauthorizedAccessException("user is not exist");

            if (route.CreatedByUserId != userId && user.Role != Role.Admin)
                throw new UnauthorizedAccessException("Only admins or creator can update place image");

            if (!string.IsNullOrWhiteSpace(dto.Name))
                route.Name = dto.Name;

            if (!string.IsNullOrWhiteSpace(dto.Description))
                route.Description = dto.Description;

            if (dto.LengthKm > 0)
                route.LengthKm = dto.LengthKm;

            if (dto.StartLongitude != 0)
                route.StartLongitude = dto.StartLongitude;

            if (dto.StartLatitude != 0)
                route.StartLatitude = dto.StartLatitude;

            if (dto.EndLongitude != 0) 
                route.EndLongitude = dto.EndLongitude;

            if (dto.EndLatitude != 0) 
                route.EndLatitude = dto.EndLatitude;

            var routeType = await repositoryType.GetByIdRouteTypeAsync(dto.TypeId);
            if (routeType != null)
                route.TypeId = dto.TypeId;

            var existingRegion = await repositoryRegion.GetByIdAsync(dto.RegionId ?? 0);
                route.RegionId = existingRegion != null? dto.RegionId : null;

            if (dto.DurationMinutes > 0)
                route.DurationMinutes = dto.DurationMinutes;

            if (dto.Price >= 0)
                route.Price = dto.Price;

            if (dto.ClosingTime >= dto.OpeningTime)
            {
                route.OpeningTime = dto.OpeningTime;
                route.ClosingTime = dto.ClosingTime;
            }

            if (dto.MinTemperature.HasValue &&
                dto.MinTemperature > MIN_REAL_TEMP && dto.MinTemperature < MAX_REAL_TEMP)
                route.MinTemperature = dto.MinTemperature;

            if (dto.MaxTemperature.HasValue &&
                dto.MaxTemperature > MIN_REAL_TEMP && dto.MaxTemperature < MAX_REAL_TEMP)
                route.MaxTemperature = dto.MaxTemperature;

            if (dto.MinTemperature > dto.MaxTemperature)
                throw new ArgumentException("MinTemperature cannot be greater than MaxTemperature");

            if (dto.MaxWindSpeed.HasValue && dto.MaxWindSpeed >= 0 && dto.MaxWindSpeed < MAX_REAL_WIND)
                route.MaxWindSpeed = dto.MaxWindSpeed;

            if (dto.MaxRainProbability.HasValue && dto.MaxRainProbability >= 0 && dto.MaxRainProbability <= 100)
                route.MaxRainProbability = dto.MaxRainProbability;

            if (dto.MaxHumidity.HasValue && dto.MaxHumidity >= 0 && dto.MaxHumidity <= 100)
                route.MaxHumidity = dto.MaxHumidity;

            if (dto.MaxCloudCoverage.HasValue && dto.MaxCloudCoverage >= 0 && dto.MaxCloudCoverage <= 100)
                route.MaxCloudCoverage = dto.MaxCloudCoverage;

            route.AllowRain = dto.AllowRain;
            route.HasCommonWeather = dto.MinTemperature <= dto.MaxTemperature;
            route.Difficulty = CalculateDifficulty(dto.LengthKm, dto.DurationMinutes);

            await repository.UpdateAsync(route);

            // --- Replace the points if they were sent ---
            if (dto.Points != null && dto.Points.Any())
            {
                await routePointService.ReplaceRoutePointsAsync(route.Id, dto.Points); // also recalculates the order
            }

            // --- Recalculate every trip that contains this route ---
            await recalculateAllTripsContainingPlaceOrRouteService.RecalculateAllTripsContainingRouteAsync(id);

            if (user.Role != Role.Admin)
            {
                // Only the creating or updating user
                if (route.ApprovalStatus != ApprovalStatus.Draft)
                {
                    route.ApprovalStatus = ApprovalStatus.Pending;
                    route.ApprovedAt = null;
                    route.RejectReason = null;
                }
            }

            return mapper.Map<RouteDto>(await repository.GetByIdAsync(id));
        }
        public async Task<RouteDto> UpdateRatingAsync(int userId, int id, UpdateRatingDto dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            var user = await repositoryUser.GetByIdAsync(userId);
            if (user == null)
                throw new UnauthorizedAccessException("user is not exist");

            var route = await repository.GetByIdAsync(id);
            if (route == null)
                throw new KeyNotFoundException($"Route with id {id} not found");

            if (dto.Rating < 0 || dto.Rating > 5)
                throw new ArgumentException("Rating can not be negative or greater than five ");

            route.AverageRating = (route.AverageRating * route.RatingsCount + dto.Rating) / ++route.RatingsCount;
            await repository.UpdateAsync(route);

            return mapper.Map<RouteDto>(await repository.GetByIdAsync(id));
        }

        public async Task<RouteDto> SetApprovalStatusAsync(int userId, int id, ApprovalStatus newStatus, string? rejectReason = null)
        {
            var user = await repositoryUser.GetByIdAsync(userId);
            if (user == null)
                throw new UnauthorizedAccessException("User not found");

            var route = await repository.GetByIdAsync(id);
            if (route == null)
                throw new KeyNotFoundException($"Route with id {id} not found");

            // The user wants to move the route from draft to Pending
            if (newStatus == ApprovalStatus.Pending)
            {
                if (route.ApprovalStatus != ApprovalStatus.Draft)
                    throw new InvalidOperationException("Only Draft routes can be submitted for approval");

                if (route.CreatedByUserId != userId)
                    throw new UnauthorizedAccessException("Only the creator can submit for approval");

                route.ApprovalStatus = ApprovalStatus.Pending;
                route.ApprovedAt = null;
                route.RejectReason = null;
            }
            // The user wants to approve or reject
            else if (newStatus == ApprovalStatus.Approved || newStatus == ApprovalStatus.Rejected)
            {
                if (user.Role != Role.Admin)
                    throw new UnauthorizedAccessException("Only admins can approve or reject");

                if (route.ApprovalStatus != ApprovalStatus.Pending)
                    throw new InvalidOperationException("Only Pending routes can be approved or rejected");

                route.ApprovalStatus = newStatus;

                if (newStatus == ApprovalStatus.Approved)
                {
                    route.ApprovedAt = DateOnly.FromDateTime(DateTime.UtcNow);
                    route.RejectReason = null;
                }
                else if (newStatus == ApprovalStatus.Rejected)
                {
                    route.ApprovedAt = null;
                    route.RejectReason = rejectReason;
                }
            }
            else
            {
                throw new InvalidOperationException("Invalid status transition");
            }

            await repository.UpdateAsync(route);

            return mapper.Map<RouteDto>(await repository.GetByIdAsync(id));
        }

        public async Task<bool> DeleteAsync(int userId, int id)
        {
            var user = await repositoryUser.GetByIdAsync(userId);
            if (user == null)
                throw new UnauthorizedAccessException("User not exists");

            var route = await repository.GetByIdAsync(id);
            if (route == null)
                throw new KeyNotFoundException($"Route with id {id} not found");

            if (route.CreatedByUserId != userId && user.Role != Role.Admin)
                throw new UnauthorizedAccessException("Only admins or creator can update place image");

            // Delete all points first
            var routePoints = await routePointService.GetByRouteIdAsync(route.Id);
            foreach (var rp in routePoints)
                await routePointService.DeleteRoutePointAsync(rp.Id);

            return await repository.DeleteAsync(id);
        }
        
        public async Task<RoutePointDto> AddPointToRouteAsync(int userId, int routeId, RoutePointCreateDto point)
        {
            var route = await repository.GetByIdAsync(routeId);
            if (route == null)
                throw new KeyNotFoundException("Route not found");

            var user = await repositoryUser.GetByIdAsync(userId);
            if (user == null)
                throw new UnauthorizedAccessException("User not exists");

            if (route.CreatedByUserId != userId && user.Role != Role.Admin)
                throw new UnauthorizedAccessException("Only admin or creator can modify route");

            return await routePointService.AddPointToRouteAsync(routeId, point);
        }

        public async Task<List<RoutePointDto>> ReplaceRoutePointsAsync(int userId, int routeId, List<RoutePointCreateDto> newPoints)
        {
            var route = await repository.GetByIdAsync(routeId);
            if (route == null)
                throw new KeyNotFoundException("Route not found");

            var user = await repositoryUser.GetByIdAsync(userId);
            if (user == null)
                throw new UnauthorizedAccessException("User not exists");

            if (route.CreatedByUserId != userId && user.Role != Role.Admin)
                throw new UnauthorizedAccessException("Only admin or creator can modify route");

            return await routePointService.ReplaceRoutePointsAsync(routeId, newPoints);
        }
        public async Task<List<RoutePointDto>> GetRoutePointsAsync(int routeId)
        {
            var route = await repository.GetByIdAsync(routeId);
            if (route == null)
                throw new KeyNotFoundException("Route not found");

            return await routePointService.GetByRouteIdAsync(routeId);
        }
        public async Task<RoutePointDto> UpdateOrderInRouteAsync(int userId, int routePointId, int newOrder)
        {
            var routePoint = await routePointService.GetByIdAsync(routePointId);

            if (routePoint == null)
                throw new KeyNotFoundException("Route point not found");

            var route = await repository.GetByIdAsync(routePoint.RouteId);
            if (route == null)
                throw new KeyNotFoundException("Route not found");

            var user = await repositoryUser.GetByIdAsync(userId);
            if (user == null)
                throw new UnauthorizedAccessException("User not exists");

            if (route.CreatedByUserId != userId && user.Role != Role.Admin)
                throw new UnauthorizedAccessException("Only admin or creator can modify route");

            return await routePointService.UpdateOrderInRouteAsync(routePointId, newOrder);
        }
        public async Task<bool> DeleteRoutePointAsync(int userId, int routePointId)
        {
            var routePoint = await routePointService.GetByIdAsync(routePointId);
            if (routePoint == null)
                throw new KeyNotFoundException("Route point not found");

            var route = await repository.GetByIdAsync(routePoint.RouteId);
            if (route == null)
                throw new KeyNotFoundException("Route not found");

            var user = await repositoryUser.GetByIdAsync(userId);
            if (user == null)
                throw new UnauthorizedAccessException("User not exists");

            if (route.CreatedByUserId != userId && user.Role != Role.Admin)
                throw new UnauthorizedAccessException("Only admin or creator can modify route");

            return await routePointService.DeleteRoutePointAsync(routePointId);
        }

        private Difficulty CalculateDifficulty(double lengthKm, double durationMinutes)
        {
            if (lengthKm > 10 || durationMinutes > 360)
                return Difficulty.Hard;

            if (lengthKm > 6 || durationMinutes > 240)
                return Difficulty.MediumHard;

            if (lengthKm >= 3 && durationMinutes > 120)
                return Difficulty.Medium;

            if (lengthKm <= 3 && durationMinutes < 60)
                return Difficulty.Easy;

            return Difficulty.EasyMedium;
        }
    }
}

