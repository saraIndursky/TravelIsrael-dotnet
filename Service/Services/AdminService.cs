using AutoMapper;
using Repository.Entities;
using Repository.Interfaces;
using Service.Dto;
using Service.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Service.Services
{
    public class AdminService : IAdminService
    {
        private readonly IDayTripRepository repositoryDayTrip;
        private readonly IPlaceRepository repositoryPlace;
        private readonly IRouteRepository repositoryRoute;
        private readonly IUserRepository repositoryUser;
        private readonly IMapper mapper;
        public AdminService(IDayTripRepository repositoryDayTrip, IPlaceRepository repositoryPlace, IRouteRepository repositoryRoute, IUserRepository repositoryUser, IMapper mapper)
        {
            this.repositoryDayTrip = repositoryDayTrip;
            this.repositoryPlace = repositoryPlace;
            this.repositoryRoute = repositoryRoute;
            this.repositoryUser = repositoryUser;
            this.mapper = mapper;
        }

        public async Task<AdminStatsDto> GetStatsForAdminAsync(int adminId)
        {
            var user = await repositoryUser.GetByIdAsync(adminId);
            if (user == null || user.Role != Role.Admin)
            {
                throw new UnauthorizedAccessException("User is not authorized to access admin stats.");
            }

            return new AdminStatsDto
            {
                PendingTrips = await repositoryDayTrip.GetPendingTripsAsync(),
                totalTrips = await repositoryDayTrip.GetTotalTripsAsync(),
                places = await repositoryPlace.GetTotalPlacesAsync() + await repositoryRoute.GetTotalRoutesAsync(),
                users = await repositoryUser.GetTotalUsersAsync()
            };
        }

        public async Task<List<AdminAllTripsDto>> GetAllTripsForAdminAsync(int adminId)
        {
            var user = await repositoryUser.GetByIdAsync(adminId);
            if (user == null || user.Role != Role.Admin)
            {
                throw new UnauthorizedAccessException("User is not authorized to access admin stats.");
            }

            var trips = await repositoryDayTrip.GetAllTripsForAdminAsync();

            return trips.Select(trip => new AdminAllTripsDto
            {
                Id = trip.Id,
                Name = trip.Name,
                Creator = trip.User.FirstName + " " + trip.User.LastName,
                Email = trip.User.Email,
                Region = trip.Region != null ? trip.Region.RegionName : null,
                Status = trip.ApprovalStatus.ToString().ToLower(),
                RejectReason = trip.RejectReason,
                Type = trip.Type.TypeName,
                Stops = trip.DayTripItems.Count,
                Rating = trip.AverageRating ?? 0,
                Saves = GenerateFakeSaves(trip.Id), // TODO: replace with a real saves count
                Views = trip.NumberOfViews,
                Popularity = GetPopularity(trip.NumberOfViews, trip.ApprovalStatus)
            }).ToList();
        }
        private string GetPopularity(int views, ApprovalStatus approvalStatus)
        {
            if (approvalStatus != ApprovalStatus.Approved) return null;
            if (views > 1500) return "high";
            if (views > 500) return "medium";
            return "low";
        }

        private int GenerateFakeSaves(int tripId)
        {
            var random = new Random(tripId); // seeded by ID so the value is stable
            return random.Next(0, 200);
        }

        public async Task<List<AdminPendingTripsDto>> GetPendingTripsForAdminAsync(int adminId)
        {
            var user = await repositoryUser.GetByIdAsync(adminId);
            if (user == null || user.Role != Role.Admin)
            {
                throw new UnauthorizedAccessException("User is not authorized to access admin stats.");
            }

            var trips = await repositoryDayTrip.GetPendingTripsForAdminAsync();

            return trips.Select(trip => new AdminPendingTripsDto
            {
                Id = trip.Id,
                Name = trip.Name,
                Creator = trip.User.FirstName + " " + trip.User.LastName,
                Email = trip.User.Email,
                Region = trip.Region != null ? trip.Region.RegionName : "לא ידוע",
                Difficulty = TranslateDifficulty(trip.Difficulty),
                Type = trip.Type.TypeName,
                Stops = trip.DayTripItems.Count,
                SubmittedDate = trip.SubmittedDate
            }).ToList();
        }

        private string TranslateDifficulty(Difficulty value)
        {
            return value switch
            {
                Difficulty.Easy => "קל",
                Difficulty.EasyMedium => "קל-בינוני",
                Difficulty.Medium => "בינוני",
                Difficulty.MediumHard => "בינוני-קשה",
                Difficulty.Hard => "קשה",
                _ => value.ToString()
            };
        }

        public async Task<List<AdminPlacesAndRoutesDto>> GetAllPlacesAndRoutesForAdminAsync(int adminId)
        {
            var user = await repositoryUser.GetByIdAsync(adminId);
            if (user == null || user.Role != Role.Admin)
            {
                throw new UnauthorizedAccessException("User is not authorized to access admin stats.");
            }

            var places = await repositoryPlace.GetAllPlacesForAdminAsync();
            var routes = await repositoryRoute.GetAllRoutesForAdminAsync();

            var allplaces = places.Select(place => new AdminPlacesAndRoutesDto
            {
                Id = place.Id,
                Name = place.Name,
                ItemType = "place",
                Creator = place.User.FirstName + " " + place.User.LastName,
                Email = place.User.Email,
                Image = fromStringToByte(place.Images.FirstOrDefault(i => i.IsMain)?.ImageUrl),
                Region = place.Region != null ? place.Region.RegionName : null,
                Status = place.ApprovalStatus.ToString().ToLower(),
                RejectReason = place.RejectReason,
                Type = place.Type.TypeName,
                Rating = place.AverageRating ?? 0,
                Saves = place.DayTripItems.Count,
                Views = place.NumberOfViews,
                Popularity = GetPopularity(place.NumberOfViews, place.ApprovalStatus),
                CreateAt = place.CreatedAt,
            })
            .Concat(
                routes.Select(route => new AdminPlacesAndRoutesDto
                {
                    Id = route.Id,
                    Name = route.Name,
                    ItemType = "route",
                    Creator = route.User.FirstName + " " + route.User.LastName,
                    Email = route.User.Email,
                    Image = fromStringToByte(route.Images.FirstOrDefault(i => i.IsMain)?.ImageUrl),
                    Region = route.Region != null ? route.Region.RegionName : null,
                    Status = route.ApprovalStatus.ToString().ToLower(),
                    RejectReason = route.RejectReason,
                    Type = route.Type.TypeName,
                    Rating = route.AverageRating ?? 0,
                    Saves = route.DayTripItems.Count,
                    Views = route.NumberOfViews,
                    Popularity = GetPopularity(route.NumberOfViews, route.ApprovalStatus),
                    CreateAt = route.CreatedAt,
                })
            );

            return allplaces.OrderByDescending(x => x.CreateAt).ToList();
        }

        public byte[] fromStringToByte(string? fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return null;

            var path = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Images",
                fileName
            );

            if (!File.Exists(path))
                return null;

            return File.ReadAllBytes(path);
        }
    }
}
