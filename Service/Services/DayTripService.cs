using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Repository.Entities;
using Repository.Interfaces;
using Service.Dto;
using Service.Dto.DayTrip;
using Service.Dto.DayTripItem;
using Service.Dto.Rating;
using Service.Interface;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Service.Services
{
    public class DayTripService : IDayTripService
    {
        private readonly IDayTripRepository repository;
        private readonly ITypeRepository repositoryType;
        private readonly IUserRepository repositoryUser;
        private readonly IPlaceRepository repositoryPlace;
        private readonly IRouteRepository repositoryRoute;
        private readonly IDayTripItemService dayTripItemService;
        private readonly IDayTripCalculatedFieldsService dayTripCalculatedFieldsService;
        private readonly IMapper mapper;

        public DayTripService(IDayTripRepository repository, ITypeRepository repositoryType, IUserRepository repositoryUser, IPlaceRepository repositoryPlace, IRouteRepository repositoryRoute, IDayTripItemService dayTripItemService, IDayTripCalculatedFieldsService dayTripCalculatedFieldsService, IMapper mapper)
        {
            this.repository = repository;
            this.repositoryType = repositoryType;
            this.repositoryUser = repositoryUser;
            this.repositoryPlace = repositoryPlace;
            this.repositoryRoute = repositoryRoute;
            this.dayTripItemService = dayTripItemService;
            this.dayTripCalculatedFieldsService = dayTripCalculatedFieldsService;
            this.mapper = mapper;
        }
        public async Task<List<DayTripDetaileDto>> GetAllAsync()
        {
            var trips = await repository.GetAllAsync();
            return mapper.Map<List<DayTripDetaileDto>>(trips);
        }

        public async Task<List<DayTripDto>> GetTopThreeOrderByNumberOfViewsAsync()
        {
            var trips = await repository.GetTopThreeOrderByNumberOfViewsAsync();
            return mapper.Map<List<DayTripDto>>(trips);
        }
        public async Task<DayTripDetaileDto> GetByIdAsync(int id)
        {
            var trip = await repository.GetByIdAsync(id);
            if (trip == null)
                throw new KeyNotFoundException($"Day trip with id {id} not found");

            return mapper.Map<DayTripDetaileDto>(trip);
        }

        // Converted to async to ensure EF queries are awaited and avoid concurrent DbContext usage.
        public async Task<List<DayTripDto>> GetFilteredTripsAsync(TripFilterDto filter)
        {
            if (filter == null)
                throw new ArgumentNullException(nameof(filter));

            // Base query from repository (IQueryable tied to DbContext)
            var query = repository.GetFilteredDayTrips();

            // Apply filters
            if (!string.IsNullOrEmpty(filter.Search))
                query = query.Where(d => d.Name.Contains(filter.Search) || d.Description.Contains(filter.Search));

            if (filter.Type != null && filter.Type != 0)
                query = query.Where(d => d.TypeId == filter.Type);

            if (filter.Region != null && filter.Region != 0)
                query = query.Where(d => d.RegionId == filter.Region);

            if (filter.Difficulty != null)
                query = query.Where(d => d.Difficulty == filter.Difficulty);

            if (filter.Accessibility != null)
                query = query.Where(d => d.Accessibility == filter.Accessibility);

            if (filter.Duration != null && filter.Duration != 0)
                query = query.Where(d => d.TotalDurationHours <= filter.Duration);

            if (filter.LengthKM != null && filter.LengthKM != 0)
                query = query.Where(d => d.TotalLengthKM <= filter.LengthKM);

            if (filter.Price != null && filter.Price != 0)
                query = query.Where(d => d.Price <= filter.Price);

            if (filter.StopsCount != null && filter.StopsCount != 0)
                query = query.Where(d => d.DayTripItems.Count <= filter.StopsCount);

            query = filter.SortBy switch
            {
                "name" => filter.SortDirection == "desc"
                    ? query.OrderByDescending(t => t.Name)
                    : query.OrderBy(t => t.Name),
                "price" => filter.SortDirection == "desc"
                    ? query.OrderByDescending(t => t.Price)
                    : query.OrderBy(t => t.Price),
                "duration" => filter.SortDirection == "desc"
                    ? query.OrderByDescending(t => t.TotalDurationHours)
                    : query.OrderBy(t => t.TotalDurationHours),
                "lengthKM" => filter.SortDirection == "desc"
                    ? query.OrderByDescending(t => t.TotalLengthKM)
                    : query.OrderBy(t => t.TotalLengthKM),
                "numberOfViews" => filter.SortDirection == "desc"
                    ? query.OrderByDescending(t => t.NumberOfViews)
                    : query.OrderBy(t => t.NumberOfViews),
                _ => filter.SortDirection == "desc"
                    ? query.OrderByDescending(t => t.CreatedAt)
                    : query.OrderBy(t => t.CreatedAt),
            };

            // Pagination
            query = query.Skip(filter.Skip).Take(filter.Take);

            // IMPORTANT: enumerate asynchronously while DbContext is still valid and single-threaded for this request
            var list = await query.ToListAsync();
            return mapper.Map<List<DayTripDto>>(list);
        }

        public async Task<CalculateComputedFieldsResult> AddAsync(DayTripCreateDto dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            if (string.IsNullOrWhiteSpace(dto.Name))
                throw new ArgumentException("Day trip name is required");

            if (string.IsNullOrWhiteSpace(dto.Description))
                throw new ArgumentException("Day trip description is required");

            if (dto.Items == null || !dto.Items.Any())
                throw new ArgumentException("Day trip must contain at least one item");

            // --- Check the user ---
            var user = await repositoryUser.GetByIdAsync(dto.CreatedByUserId);
            if (user == null)
                throw new InvalidOperationException("User does not exist");

            var dayTripType = await repositoryType.GetByIdDayTripTypeAsync(dto.TypeId);
            if (dayTripType == null)
                throw new InvalidOperationException("Day trip type does not exist");

            await ValidateTripNotExistsAsync(0, dto.Items);

            var dayTrip = mapper.Map<DayTrip>(dto);

            // --- Approval status by user role ---
            if (user.Role == Role.Admin)
            {
                // An admin chooses whether to submit for approval or keep a draft
                dayTrip.ApprovalStatus = dto.SendForApproval ? ApprovalStatus.Approved : ApprovalStatus.Draft;
                dayTrip.ApprovedAt = dto.SendForApproval ? DateOnly.FromDateTime(DateTime.UtcNow) : null;
                dayTrip.RejectReason = null;
            }
            else
            {
                // A regular user gets a draft or Pending, as requested
                dayTrip.ApprovalStatus = dto.SendForApproval ? ApprovalStatus.Pending : ApprovalStatus.Draft;
                dayTrip.ApprovedAt = null;
                dayTrip.RejectReason = null;
                if(dayTrip.ApprovalStatus == ApprovalStatus.Pending)
                    dayTrip.SubmittedDate = DateOnly.FromDateTime(DateTime.Today);
            }

            dayTrip.TripHash = CalculateSignatureFromDto(dto.Items);
            dayTrip.CreatedAt = DateOnly.FromDateTime(DateTime.Today);
            dayTrip.AverageRating = 0;
            dayTrip.RatingsCount = 0;

            dayTrip = await repository.AddAsync(dayTrip);

            if (dto.Items.Any())
                await dayTripItemService.ReplaceDayTripItemsAsync(dayTrip.Id, dto.Items);

            // --- Computed fields ---
            return await RecalculateAndReturnAsync(dayTrip.Id);
        }

        public async Task<CalculateComputedFieldsResult> UpdateAsync(int userId, int id, DayTripUpdateDto dto)
        {
            var dayTrip = await repository.GetByIdAsync(id);
            if (dayTrip == null)
                throw new KeyNotFoundException($"Day trip with id {id} not found");

            var user = await repositoryUser.GetByIdAsync(userId);
            if (user == null)
                throw new UnauthorizedAccessException("user is not exist");

            if (dayTrip.CreatedByUserId != userId && user.Role != Role.Admin)
                throw new UnauthorizedAccessException("Only admins or creator can update day trip");

            if (!string.IsNullOrWhiteSpace(dto.Name))
                dayTrip.Name = dto.Name;

            if (!string.IsNullOrWhiteSpace(dto.Description))
                dayTrip.Description = dto.Description;

            if (dto.TypeId.HasValue)
            {
                var type = await repositoryType.GetByIdPlaceTypeAsync(dto.TypeId.Value);
                if (type != null)
                    dayTrip.TypeId = dto.TypeId.Value;
            }

            if (!string.IsNullOrWhiteSpace(dto.ImageUrl))
                dayTrip.ImageUrl = dto.ImageUrl;


            if (user.Role != Role.Admin && dayTrip.ApprovalStatus != ApprovalStatus.Draft)
            {
                dayTrip.ApprovalStatus = ApprovalStatus.Pending;
                dayTrip.ApprovedAt = null;
                dayTrip.RejectReason = null;
            }

            await repository.UpdateAsync(dayTrip);

            // --- Update the items if they were sent ---
            if (dto.Items != null && dto.Items.Any())
            {
                await ValidateTripNotExistsAsync(dayTrip.Id, dto.Items);
                await dayTripItemService.ReplaceDayTripItemsAsync(dayTrip.Id, dto.Items);
            }

            return await RecalculateAndReturnAsync(dayTrip.Id);
        }
        public async Task<DayTripDetaileDto> UpdateRatingAsync(int id, UpdateRatingDto dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            if (dto.Rating < 0 || dto.Rating > 5)
                throw new ArgumentException("Rating can not be negative or greater than five ");

            var dayTrip = await repository.GetByIdAsync(id);
            if (dayTrip == null)
                throw new KeyNotFoundException($"Day trip with id {id} not found");

            
            dayTrip.AverageRating = (dayTrip.AverageRating * dayTrip.RatingsCount + dto.Rating) / ++dayTrip.RatingsCount;
            await repository.UpdateAsync(dayTrip);

            return mapper.Map<DayTripDetaileDto>(dayTrip);
        }

        public async Task<DayTripDetaileDto> SetApprovalStatusAsync(int userId, int id, ApprovalStatus newStatus)
        {
            var user = await repositoryUser.GetByIdAsync(userId);
            if (user == null)
                throw new UnauthorizedAccessException("User not found");

            var dayTrip = await repository.GetByIdAsync(id);
            if (dayTrip == null)
                throw new KeyNotFoundException($"Day trip with id {id} not found");

            // The user wants to move the trip from draft to Pending
            if (newStatus == ApprovalStatus.Pending)
            {
                if (dayTrip.ApprovalStatus != ApprovalStatus.Draft)
                    throw new InvalidOperationException("Only Draft day trips can be submitted for approval");

                if (dayTrip.CreatedByUserId != userId)
                    throw new UnauthorizedAccessException("Only the creator can submit for approval");

                dayTrip.ApprovalStatus = ApprovalStatus.Pending;
                dayTrip.SubmittedDate = DateOnly.FromDateTime(DateTime.Today);
                dayTrip.ApprovedAt = null;
                dayTrip.RejectReason = null;
            }
            else
            {
                throw new InvalidOperationException("Invalid status transition");
            }

            await repository.UpdateAsync(dayTrip);
            return mapper.Map<DayTripDetaileDto>(dayTrip);
        }

        public async Task<DayTripDetaileDto> ApproveDayTripWithItemsAsync(int adminId, int dayTripId, ApproveRequest request)
        {
            var admin = await repositoryUser.GetByIdAsync(adminId);
            if (admin == null || admin.Role != Role.Admin)
                throw new UnauthorizedAccessException("Only admins can approve or reject day trips");

            var dayTrip = await repository.GetByIdAsync(dayTripId);
            if (dayTrip == null)
                throw new KeyNotFoundException($"Day trip with id {dayTripId} not found");

            // Collect all places and routes in the trip
            var items = await dayTripItemService.GetByDayTripIdAsync(dayTripId);

            bool canApprove = true;
            string explanation = "";

            foreach (var item in items)
            {
                if ((item.Place != null && (item.Place.ApprovalStatus == ApprovalStatus.Draft && item.Place.CreatedByUserId != dayTrip.CreatedByUserId)) ||
                    (item.Place != null && item.Place.ApprovalStatus == ApprovalStatus.Rejected))
                {
                    canApprove = false;
                    explanation = $"Place '{item.Place?.Name}' cannot be approved";
                    break;
                }

                if ((item.Route != null && (item.Route.ApprovalStatus == ApprovalStatus.Draft && item.Route.CreatedByUserId != dayTrip.CreatedByUserId)) ||
                    (item.Route != null && item.Route.ApprovalStatus == ApprovalStatus.Rejected))
                {
                    canApprove = false;
                    explanation = $"Route '{item.Route?.Name}' cannot be approved";
                    break;
                }
            }

            if (request.ApprovalStatus == ApprovalStatus.Approved && canApprove)
            {
                // Update the creator's places and routes that are waiting for approval
                foreach (var item in items)
                {
                    var place = item.Place;
                    var route = item.Route;

                    if (item.Place != null &&
                        (item.Place.ApprovalStatus == ApprovalStatus.Pending || 
                        (item.Place.ApprovalStatus == ApprovalStatus.Draft && 
                        item.Place.CreatedByUserId == dayTrip.CreatedByUserId)))
                    {
                        item.Place.ApprovalStatus = ApprovalStatus.Approved;
                        item.Place.ApprovedAt = DateOnly.FromDateTime(DateTime.UtcNow);
                        item.Place.RejectReason = null;
                        await repositoryPlace.UpdateAsync(item.Place);
                    }

                    if (item.Route != null &&
                        (item.Route.ApprovalStatus == ApprovalStatus.Pending || 
                        (item.Route.ApprovalStatus == ApprovalStatus.Draft && 
                        item.Route.CreatedByUserId == dayTrip.CreatedByUserId)))
                    {
                        item.Route.ApprovalStatus = ApprovalStatus.Approved;
                        item.Route.ApprovedAt = DateOnly.FromDateTime(DateTime.UtcNow);
                        item.Route.RejectReason = null;
                        await repositoryRoute.UpdateAsync(item.Route);
                    }
                }
                dayTrip.ApprovalStatus = ApprovalStatus.Approved;
                dayTrip.ApprovedAt = DateOnly.FromDateTime(DateTime.UtcNow);
                dayTrip.RejectReason = null;
            }
            else
            {
                dayTrip.ApprovalStatus = ApprovalStatus.Rejected;
                dayTrip.RejectReason = request.RejectReason ?? explanation;
                dayTrip.ApprovedAt = null;
            }

            await repository.UpdateAsync(dayTrip);
            return mapper.Map<DayTripDetaileDto>(dayTrip);
        }

        public async Task<bool> DeleteAsync(int userId, int id)
        {
            var user = await repositoryUser.GetByIdAsync(userId);
            if (user == null)
                throw new UnauthorizedAccessException("User not exists");

            var dayTrip = await repository.GetByIdAsync(id);
            if (dayTrip == null)
                throw new KeyNotFoundException($"Day trip with id {id} not found");

            if (dayTrip.CreatedByUserId != userId && user.Role != Role.Admin)
                throw new UnauthorizedAccessException("Only admins or creator can update place image");

            // Delete all places first
            var dayTripItems = await dayTripItemService.GetByDayTripIdAsync(dayTrip.Id);
            foreach (var item in dayTripItems)
                await dayTripItemService.DeleteDayTripItemAsync(item.Id);

            return await repository.DeleteAsync(id);
        }

        public async Task<CalculateComputedFieldsResult> AddItemToDayTripAsync(int userId, int dayTripId, DayTripItemToAdd item)
        {
            var dayTrip = await repository.GetByIdAsync(dayTripId);
            if (dayTrip == null)
                throw new KeyNotFoundException("Day trip not found");

            var user = await repositoryUser.GetByIdAsync(userId);
            if (user == null)
                throw new UnauthorizedAccessException("User not exists");

            if (user.Role != Role.Admin && dayTrip.CreatedByUserId != userId)
                throw new UnauthorizedAccessException("Only admin or creator can modify day trip");

            var items = await dayTripItemService.GetByDayTripIdAsync(dayTrip.Id);
            var signature = CalculateSignatureFromItems(items);
            await ValidateTripSignature(dayTrip.Id, signature);

            var result = await dayTripItemService.AddItemToDayTripAsync(dayTripId, item);

            if (user.Role != Role.Admin && dayTrip.ApprovalStatus != ApprovalStatus.Draft)
            {
                dayTrip.ApprovalStatus = ApprovalStatus.Pending;
                dayTrip.ApprovedAt = null;
                dayTrip.RejectReason = null;
            }

            return await RecalculateAndReturnAsync(dayTripId);
        }

        public async Task<CalculateComputedFieldsResult> ReplaceDayTripItemsAsync(int userId, int dayTripId, List<DayTripItemToAdd> newItems)
        {
            var dayTrip = await repository.GetByIdAsync(dayTripId);
            if (dayTrip == null)
                throw new KeyNotFoundException("Day trip not found");

            var user = await repositoryUser.GetByIdAsync(userId);
            if (user == null)
                throw new UnauthorizedAccessException("User not exists");

            if (dayTrip.CreatedByUserId != userId && user.Role != Role.Admin)
                throw new UnauthorizedAccessException("Only admin or creator can modify day trip");

            var items = await dayTripItemService.GetByDayTripIdAsync(dayTrip.Id);
            var signature = CalculateSignatureFromItems(items);
            await ValidateTripSignature(dayTrip.Id, signature);

            await dayTripItemService.ReplaceDayTripItemsAsync(dayTripId, newItems);

            if (user.Role != Role.Admin && dayTrip.ApprovalStatus != ApprovalStatus.Draft)
            {
                dayTrip.ApprovalStatus = ApprovalStatus.Pending;
                dayTrip.ApprovedAt = null;
                dayTrip.RejectReason = null;
            }

            return await RecalculateAndReturnAsync(dayTripId);
        }
        public async Task<List<DayTripItemDto>> GetDayTripItemsAsync(int dayTripId)
        {
            var dayTrip = await repository.GetByIdAsync(dayTripId);
            if (dayTrip == null)
                throw new KeyNotFoundException("Day trip not found");

            var items = await dayTripItemService.GetByDayTripIdAsync(dayTripId);
            return mapper.Map<List<DayTripItem>, List<DayTripItemDto>>(items);
        }
        public async Task<CalculateComputedFieldsResult> UpdateOrderInDayTripAsync(int userId, int dayTripItemId, int newOrder)
        {
            var dayTripItem = await dayTripItemService.GetByIdAsync(dayTripItemId);

            if (dayTripItem == null)
                throw new KeyNotFoundException("Day trip item not found");

            var dayTrip = await repository.GetByIdAsync(dayTripItem.DayTripId);
            if (dayTrip == null)
                throw new KeyNotFoundException("Day trip not found");

            var user = await repositoryUser.GetByIdAsync(userId);
            if (user == null)
                throw new UnauthorizedAccessException("User not exists");

            if (dayTrip.CreatedByUserId != userId && user.Role != Role.Admin)
                throw new UnauthorizedAccessException("Only admin or creator can modify day trip");

            var items = await dayTripItemService.GetByDayTripIdAsync(dayTrip.Id);
            var signature = CalculateSignatureFromItems(items);
            await ValidateTripSignature(dayTrip.Id, signature);

            await dayTripItemService.UpdateOrderInDayTripAsync(dayTripItemId, newOrder);

            return await  RecalculateAndReturnAsync(dayTrip.Id);
        }
        public async Task<CalculateComputedFieldsResult> DeleteDayTripItemAsync(int userId, int dayTripItemId)
        {
            var dayTripItem = await dayTripItemService.GetByIdAsync(dayTripItemId);
            if (dayTripItem == null)
                throw new KeyNotFoundException("Day trip item not found");

            var dayTrip = await repository.GetByIdAsync(dayTripItem.DayTripId);
            if (dayTrip == null)
                throw new KeyNotFoundException("Day trip not found");

            var user = await repositoryUser.GetByIdAsync(userId);
            if (user == null)
                throw new UnauthorizedAccessException("User not exists");

            if (dayTrip.CreatedByUserId != userId && user.Role != Role.Admin)
                throw new UnauthorizedAccessException("Only admin or creator can modify day trip");

            var items = await dayTripItemService.GetByDayTripIdAsync(dayTrip.Id);
            var signature = CalculateSignatureFromItems(items);
            await ValidateTripSignature(dayTrip.Id, signature);

            await dayTripItemService.DeleteDayTripItemAsync(dayTripItemId);

            if (user.Role != Role.Admin && dayTrip.ApprovalStatus != ApprovalStatus.Draft)
            {
                dayTrip.ApprovalStatus = ApprovalStatus.Pending;
                dayTrip.ApprovedAt = null;
                dayTrip.RejectReason = null;
            }

            return await RecalculateAndReturnAsync(dayTrip.Id);
        }

        private async Task<CalculateComputedFieldsResult> RecalculateAndReturnAsync(int dayTripId)
        {
            var dayTrip = await repository.GetByIdAsync(dayTripId);
            if (dayTrip == null)
                throw new KeyNotFoundException("DayTrip not found");

            var items = await dayTripItemService.GetByDayTripIdAsync(dayTripId);

            // --- Compute a new signature ---
            var signature = CalculateSignatureFromItems(items);
            await ValidateTripSignature(dayTripId, signature);
            dayTrip.TripHash = signature;

            var computed = await dayTripCalculatedFieldsService.CalculateComputedFieldsAsync(dayTrip);

            ApplyComputedFields(dayTrip, computed);

            await repository.UpdateAsync(dayTrip);

            return new CalculateComputedFieldsResult
            {
                DayTrip = mapper.Map<DayTripDetaileDto>(dayTrip),
                ScheduleResult = computed.ScheduleResult
            };
        }

        private void ApplyComputedFields(DayTrip dayTrip, ComputedDayTripFields computed)
        {
            dayTrip.RegionId = computed.RegionId;
            dayTrip.TotalDurationHours = computed.TotalDurationHours;
            dayTrip.TotalLengthKM = computed.TotalLengthKM;
            dayTrip.Price = computed.Price;
            dayTrip.Accessibility = computed.Accessibility;
            dayTrip.EndTime = computed.EndTime;
            dayTrip.Difficulty = computed.Difficulty;
            dayTrip.MinTemperature = computed.MinTemperature;
            dayTrip.MaxTemperature = computed.MaxTemperature;
            dayTrip.MaxWindSpeed = computed.MaxWindSpeed;
            dayTrip.MaxRainProbability = computed.MaxRainProbability;
            dayTrip.MaxHumidity = computed.MaxHumidity;
            dayTrip.MaxCloudCoverage = computed.MaxCloudCoverage;
            dayTrip.AllowRain = computed.AllowRain;
            dayTrip.HasCommonWeather = computed.HasCommonWeather;
        }

        private string CalculateSignatureFromDto(List<DayTripItemToAdd> items)
        {
            var signature = string.Join("-",
                items
                .Select(i =>
                    i.ItemType == ItemType.Place
                    ? $"P{i.PlaceId}"
                    : $"R{i.RouteId}"
                ));

            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(signature));

            return Convert.ToBase64String(bytes);
        }
        private async Task ValidateTripNotExistsAsync(int currentTripId, List<DayTripItemToAdd> items)
        {
            var signature = CalculateSignatureFromDto(items);

            var exists = await repository.ExistsByHashAsync(currentTripId, signature);

            if (exists)
                throw new InvalidOperationException("An identical day trip already exists");
        }
        private string CalculateSignatureFromItems(List<DayTripItem> items)
        {
            var signature = string.Join("-",
                items
                .OrderBy(i => i.OrderInTrip)
                .Select(i =>
                    i.ItemType == ItemType.Place
                    ? $"P{i.PlaceId}"
                    : $"R{i.RouteId}"
                ));

            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(signature));

            return Convert.ToBase64String(bytes);
        }
        private async Task ValidateTripSignature(int currentTripId, string signature)
        {
            var exists = await repository.ExistsByHashAsync(currentTripId, signature);
                
            if (exists)
                throw new InvalidOperationException("Another identical day trip already exists");
        }
    }
}