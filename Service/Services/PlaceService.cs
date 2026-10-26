using AutoMapper;
using Repository.Entities;
using Repository.Interfaces;
using Service.Dto.Place;
using Service.Dto.Rating;
using Service.Interface;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Service.Services
{
    public class PlaceService : IPlaceService
    {
        private readonly IPlaceRepository repository;
        private readonly ITypeRepository repositoryType;
        private readonly IRegionRepository repositoryRegion;
        private readonly IUserRepository repositoryUser;
        private readonly IRecalculateAllTripsContainingPlaceOrRouteService recalculateAllTripsContainingPlaceOrRouteService;
        private readonly IMapper mapper;
        //const double MIN_REAL_TEMP = -100;
        //const double MAX_REAL_TEMP = 100;
        //const double MAX_REAL_WIND = 400;
        const double MIN_REAL_TEMP = -10;
        const double MAX_REAL_TEMP = 50;
        const double MAX_REAL_WIND = 200;
        public PlaceService(IPlaceRepository repository, ITypeRepository repositoryType, IRegionRepository repositoryRegion, IUserRepository repositoryUser, IRecalculateAllTripsContainingPlaceOrRouteService recalculateAllTripsContainingPlaceOrRouteService, IMapper mapper)
        {
            this.repository = repository;
            this.repositoryType = repositoryType;
            this.repositoryRegion = repositoryRegion;
            this.repositoryUser = repositoryUser;
            this.recalculateAllTripsContainingPlaceOrRouteService = recalculateAllTripsContainingPlaceOrRouteService;
            this.mapper = mapper;
        }
        public async Task<PlaceDto> AddAsync(PlaceCreateDto dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            if (string.IsNullOrWhiteSpace(dto.Name))
                throw new ArgumentException("Place name is required");

            if (!string.IsNullOrWhiteSpace(dto.Description))
                dto.Description = dto.Description;
            else
                dto.Description = "";

            // TODO: validate the location with Google Maps
            if (dto.Longitude == 0)
                throw new ArgumentException("Place longitude is required");

            if (dto.Latitude == 0)
                throw new ArgumentException("Place latitude is required");

            var existingPlaceType = await repositoryType.GetByIdPlaceTypeAsync(dto.TypeId);
            if (existingPlaceType == null)
                throw new InvalidOperationException("Place type is not exists");

            var existingRegion = await repositoryRegion.GetByIdAsync(dto.RegionId);
            if (existingRegion == null)
                throw new InvalidOperationException("Region is not exists");

            if (dto.Price < 0)
                throw new ArgumentException("Place price can not be negative");

            if (dto.ClosingTime < dto.OpeningTime)
                throw new ArgumentException("Place closing time can not be earlier than opening time");

            if (dto.AverageStayMinutes <= 0)
                throw new ArgumentException("Place average stay minutes can not be negative");

            var existingUser = await repositoryUser.GetByIdAsync(dto.CreatedByUserId);
            if (existingUser == null)
                throw new InvalidOperationException("User is not exists");

            // ===============================
            // Weather validations
            // ===============================
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
            // ===============================

            var place = mapper.Map<Place>(dto);

            // Approval status by user role
            if (existingUser.Role == Role.Admin)
            {
                // An admin chooses whether to submit for approval or keep a draft
                place.ApprovalStatus = dto.SendForApproval ? ApprovalStatus.Approved : ApprovalStatus.Draft;
                place.ApprovedAt = dto.SendForApproval ? DateOnly.FromDateTime(DateTime.UtcNow) : null;
                place.RejectReason = null;
            }
            else
            {
                // A regular user gets a draft or Pending, as requested
                place.ApprovalStatus = dto.SendForApproval ? ApprovalStatus.Pending : ApprovalStatus.Draft;
                place.ApprovedAt = null;
                place.RejectReason = null;
            }

            place.AllowRain = dto.AllowRain;
            place.HasCommonWeather = dto.MinTemperature <= dto.MaxTemperature;
            place.AverageRating = 0;
            place.RatingsCount = 0;
            place.CreatedAt = DateOnly.FromDateTime(DateTime.Today);
            var addPlace = await repository.AddAsync(place);

            return mapper.Map<PlaceDto>(addPlace);
        }
        public async Task<List<PlaceDto>> GetAllAsync()
        {
            return mapper.Map<List<Place>, List<PlaceDto>>(await repository.GetAllAsync());
        }
        public async Task<PlaceDto> GetByIdAsync(int id)
        {
            var place = await repository.GetByIdAsync(id);
            if (place == null)
                throw new KeyNotFoundException($"Place with id {id} not found.");
            return mapper.Map<PlaceDto>(place);
        }

        public async Task<PlaceDto> UpdateProfilePlaceAsync(int userId, int id, UpdateProfilePlaceDto dto)
        {
            var user = await repositoryUser.GetByIdAsync(userId);
            if (user == null)
                throw new UnauthorizedAccessException("User is not exist");

            var place = await repository.GetByIdAsync(id);
            if (place == null)
                throw new KeyNotFoundException($"Place with id {id} not found");

            if (user.Role != Role.Admin && user.Id != place.CreatedByUserId)
                throw new UnauthorizedAccessException("Only admins or who create it can update place");

            // Check whether the user has trips or other content
            if (place.Reviews != null && place.Reviews.Any())
                throw new InvalidOperationException("Cannot update place with active content");

            if (!string.IsNullOrWhiteSpace(dto.Name))
                place.Name = dto.Name;

            if (!string.IsNullOrWhiteSpace(dto.Description))
                place.Description = dto.Description;

            if (dto.Longitude != 0)
                place.Longitude = dto.Longitude;

            if (dto.Latitude != 0)
                place.Latitude = dto.Latitude;

            var existingPlaceType = repositoryType.GetByIdPlaceTypeAsync(dto.TypeId);
            if (existingPlaceType != null)
                place.TypeId = dto.TypeId;

            var existingRegion = await repositoryRegion.GetByIdAsync(dto.RegionId);
            if (existingRegion != null)
                place.RegionId = dto.RegionId;

            if (dto.Price >= 0)
                place.Price = dto.Price;

            if (dto.ClosingTime >= dto.OpeningTime)
            {
                place.OpeningTime = dto.OpeningTime;
                place.ClosingTime = dto.ClosingTime;
            }

            if (dto?.AverageStayMinutes >= 0)
                place.AverageStayMinutes = dto.AverageStayMinutes;

            if (dto.MinTemperature.HasValue &&
                dto.MinTemperature > MIN_REAL_TEMP && dto.MinTemperature < MAX_REAL_TEMP)
                place.MinTemperature = dto.MinTemperature;

            if (dto.MaxTemperature.HasValue &&
                dto.MaxTemperature > MIN_REAL_TEMP && dto.MaxTemperature < MAX_REAL_TEMP)
                place.MaxTemperature = dto.MaxTemperature;

            if (dto.MinTemperature > dto.MaxTemperature)
                throw new ArgumentException("MinTemperature cannot be greater than MaxTemperature");

            if (dto.MaxWindSpeed.HasValue && dto.MaxWindSpeed >= 0 && dto.MaxWindSpeed < MAX_REAL_WIND)
                place.MaxWindSpeed = dto.MaxWindSpeed;

            if (dto.MaxRainProbability.HasValue && dto.MaxRainProbability >= 0 && dto.MaxRainProbability <= 100)
                place.MaxRainProbability = dto.MaxRainProbability;

            if (dto.MaxHumidity.HasValue && dto.MaxHumidity >= 0 && dto.MaxHumidity <= 100)
                place.MaxHumidity = dto.MaxHumidity;

            if (dto.MaxCloudCoverage.HasValue && dto.MaxCloudCoverage >= 0 && dto.MaxCloudCoverage <= 100)
                place.MaxCloudCoverage = dto.MaxCloudCoverage;

            place.AllowRain = dto.AllowRain;
            place.HasCommonWeather  = dto.MinTemperature <= dto.MaxTemperature;

            await repository.UpdateAsync(place);

            // --- Recalculate every trip that contains this place ---
            await recalculateAllTripsContainingPlaceOrRouteService.RecalculateAllTripsContainingPlaceAsync(id);

            if (user.Role != Role.Admin)
            {
                // Only the creating or updating user
                if (place.ApprovalStatus != ApprovalStatus.Draft)
                {
                    place.ApprovalStatus = ApprovalStatus.Pending;
                    place.ApprovedAt = null;
                    place.RejectReason = null;
                }
            }
            return mapper.Map<PlaceDto>(await repository.GetByIdAsync(id));
        }
        public async Task<PlaceDto> UpdateRatingAsync(int userId, int id, UpdateRatingDto dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            var user = await repositoryUser.GetByIdAsync(userId);
            if (user == null)
                throw new UnauthorizedAccessException("User is not exist");

            var place = await repository.GetByIdAsync(id);
            if (place == null)
                throw new KeyNotFoundException($"Place with id {id} not found");
            
            if (dto.Rating < 0 || dto.Rating > 5)
                throw new ArgumentException("Rating can not be negative or greater than five ");

            place.AverageRating = (place.AverageRating * place.RatingsCount + dto.Rating) / ++place.RatingsCount;
            await repository.UpdateAsync(place);

            return mapper.Map<PlaceDto>(await repository.GetByIdAsync(id));
        }
        public async Task<PlaceDto> SetApprovalStatusAsync(int userId, int id, ApprovalStatus newStatus, string? rejectReason = null)
        {
            var user = await repositoryUser.GetByIdAsync(userId);
            if (user == null)
                throw new UnauthorizedAccessException("User not found");

            var place = await repository.GetByIdAsync(id);
            if (place == null)
                throw new KeyNotFoundException($"Place with id {id} not found");

            // The user wants to move the place from draft to Pending
            if (newStatus == ApprovalStatus.Pending)
            {
                if (place.ApprovalStatus != ApprovalStatus.Draft)
                    throw new InvalidOperationException("Only Draft places can be submitted for approval");

                if (place.CreatedByUserId != userId)
                    throw new UnauthorizedAccessException("Only the creator can submit for approval");

                place.ApprovalStatus = ApprovalStatus.Pending;
                place.ApprovedAt = null;
                place.RejectReason = null;
            }
            // The user wants to approve or reject
            else if (newStatus == ApprovalStatus.Approved || newStatus == ApprovalStatus.Rejected)
            {
                if (user.Role != Role.Admin)
                    throw new UnauthorizedAccessException("Only admins can approve or reject");

                if (place.ApprovalStatus != ApprovalStatus.Pending)
                    throw new InvalidOperationException("Only Pending places can be approved or rejected");

                place.ApprovalStatus = newStatus;

                if (newStatus == ApprovalStatus.Approved)
                {
                    place.ApprovedAt = DateOnly.FromDateTime(DateTime.UtcNow);
                    place.RejectReason = null;
                }
                else if (newStatus == ApprovalStatus.Rejected)
                {
                    place.ApprovedAt = null;
                    place.RejectReason = rejectReason;
                }
            }
            else
            {
                throw new InvalidOperationException("Invalid status transition");
            }

            await repository.UpdateAsync(place);

            return mapper.Map<PlaceDto>(await repository.GetByIdAsync(id));
        }
        public async Task<bool> DeleteAsync(int userId, int id)
        {
            // Check that the caller exists and is an admin
            var user = await repositoryUser.GetByIdAsync(userId);
            if (user == null)
                throw new UnauthorizedAccessException("User is not exist");

            var place = await repository.GetByIdAsync(id);
            if (place == null)
                throw new KeyNotFoundException($"Place with id {id} not found");

            if (user.Role != Role.Admin && user.Id != place.CreatedByUserId)
                throw new UnauthorizedAccessException("Only admins or who create it can delete place");

            // Check whether the user has trips or other content
            if (place.DayTripItems != null && place.DayTripItems.Any() ||
                place.Images != null && place.Images.Any() ||
                place.Reviews != null && place.Reviews.Any())
                throw new InvalidOperationException("Cannot delete place with active content");

            return await repository.DeleteAsync(id);
        }
    }
}