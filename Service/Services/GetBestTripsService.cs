using AutoMapper;
using Repository.Entities;
using Repository.Interfaces;
using Service.Dto;
using Service.Dto.DayTrip;
using Service.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Service.Services
{
    public class GetBestTripsService : IGetBestTripsService
    {
        private readonly IDayTripRepository repository;
        private readonly IRegionRepository repositoryRegion;
        private readonly IMapper mapper;
        private Dictionary<int, Region> _regionDict;

        public GetBestTripsService(IDayTripRepository repository, IRegionRepository repositoryRegion, IMapper mapper)
        {
            this.repository = repository;
            this.repositoryRegion = repositoryRegion;
            this.mapper = mapper;
        }

        public async Task<List<RecommendedTripDto>> GetBestTripsAsync(TripSearchRequestDto request)
        {
            var trips = await repository.GetFilteredTripsAsync();

            if (_regionDict == null)
            {
                var regions = await repositoryRegion.GetAllAsync();
                _regionDict = regions.ToDictionary(r => r.Id);
            }

            var scoredTrips = trips
                .Select(t => new
                {
                    Trip = t,
                    Score = CalculateScore(t, request),
                    Match = CalculateMatchPercentage(t, request)

                })
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.Trip.NumberOfViews) // Tie-breaker: prefer the more popular trip
                .ThenBy(x => x.Trip.Id)
                .Take(request.Count)
                .Select(x => new RecommendedTripDto
                {
                    trip = mapper.Map<DayTripDetaileDto>(x.Trip),
                    MatchPercentage = x.Match
                })
                .ToList();

            return scoredTrips.OrderByDescending(x => x.MatchPercentage).ToList();
        }

        private int CalculateMatchPercentage(DayTrip trip, TripSearchRequestDto request)
        {
            double score = 0;
            double total = 0;

            // Region
            if (request.RegionId.HasValue && trip.Region != null)
            {
                total += 3;

                int distance = GetDistanceBetweenRegions(trip.Region.Id, request.RegionId.Value);

                if (distance == 0) score += 3;
                else if (distance == 1) score += 2.5;
                else if (distance == 2) score += 2;
                else if (distance <= 4) score += 1;
            }

            // Trip type
            if (request.TypeId.HasValue)
            {
                total += 2;
                if (trip.TypeId == request.TypeId)
                    score += 2;
            }

            // Price
            if (request.MaxPrice.HasValue)
            {
                total += 2;

                if (trip.Price <= request.MaxPrice)
                    score += 2;
                else
                {
                    double diff = trip.Price - request.MaxPrice.Value;
                    score += Math.Max(0, 2 - diff / 50); // gradual decrease
                }
            }

            // Duration
            if (request.AvailableHours.HasValue)
            {
                total += 2;

                double diff = Math.Abs(trip.TotalDurationHours - request.AvailableHours.Value);
                score += Math.Max(0, 2 - diff / 2);
            }

            // Difficulty
            if (request.Difficulty.HasValue)
            {
                total += 1.5;

                if (trip.Difficulty == request.Difficulty)
                    score += 1.5;
            }

            // Accessibility
            if (request.Accessibility.HasValue)
            {
                total += 1.5;

                if (trip.Accessibility == request.Accessibility)
                    score += 1.5;
            }

            // Weather
            if (request.IsRainyDay.HasValue)
            {
                total += 1;

                if (request.IsRainyDay.Value && !trip.AllowRain)
                    score += 0; // not suitable at all
                else
                    score += 1;
            }

            if (total == 0)
                return 100;

            var percentage = (score / total) * 100;

            return (int)Math.Round(Math.Min(100, Math.Max(0, percentage)));
        }

        private double CalculateScore(DayTrip trip, TripSearchRequestDto request)
        {
            double score = 0;

            score += ScoreRating(trip); // up to 20, boosted by rating confidence
            score += ScorePrice(trip, request); // up to 20
            score += ScoreDuration(trip, request); // up to 25
            score += ScoreType(trip, request); // 30 on a match, 0 otherwise
            score += ScorePopularity(trip); // up to 15
            score += ScoreWeather(trip, request); // up to 10, or -50 in extreme weather
            score += ScoreRegion(trip, request); // up to 40

            if (trip.CreatedAt > DateOnly.FromDateTime(DateTime.Now.AddMonths(-1)))
            {
                score += 5; // small boost for trips from the last month
            }

            return score; // maximum is about 245
        }
        private double ScoreRating(DayTrip trip)
        {
            if (!trip.AverageRating.HasValue) return 0;

            double ratingScore = trip.AverageRating.Value * 20;

            // Confidence grows with the number of ratings
            double confidence = Math.Min(trip.RatingsCount ?? 0, 50) / 50.0;

            return ratingScore * (0.5 + 0.5 * confidence);
        }

        private double ScorePrice(DayTrip trip, TripSearchRequestDto request)
        {
            if (!request.MaxPrice.HasValue) return 0;

            double diff = trip.Price - request.MaxPrice.Value;

            if (diff <= 0)
                return 20;

            return Math.Max(0, 20 - diff * 2); // gradual decrease
        }

        private double ScoreDuration(DayTrip trip, TripSearchRequestDto request)
        {
            if (!request.AvailableHours.HasValue) return 0;

            double diff = Math.Abs(trip.TotalDurationHours - request.AvailableHours.Value);

            return Math.Max(0, 25 - diff * 5);
        }

        private double ScoreType(DayTrip trip, TripSearchRequestDto request)
        {
            if (!request.TypeId.HasValue) return 0;

            return trip.TypeId == request.TypeId ? 30 : 0;
        }

        private double ScorePopularity(DayTrip trip)
        {
            return Math.Min(trip.NumberOfViews / 100.0, 15);
        }

        private double ScoreWeather(DayTrip trip, TripSearchRequestDto request)
        {
            if (!request.IsRainyDay.HasValue) return 0;

            if (request.IsRainyDay.Value && !trip.AllowRain)
                return -50; // almost disqualifies the trip

            if (trip.HasCommonWeather)
                return 10;

            return 0;
        }

        // Returns the full ancestor chain of a region
        private List<int> GetAncestors(int regionId)
        {
            var result = new List<int>();

            if (!_regionDict.ContainsKey(regionId))
                return result;

            var current = _regionDict[regionId];

            while (current != null)
            {
                result.Add(current.Id);

                if (current.ParentRegionId == null)
                    break;

                if (!_regionDict.ContainsKey(current.ParentRegionId.Value))
                    break;

                current = _regionDict[current.ParentRegionId.Value];
            }

            return result;
        }

        // Distance between two regions in the region tree
        private int GetDistanceBetweenRegions(int regionAId, int regionBId)
        {
            var ancestorsA = GetAncestors(regionAId);
            var ancestorsB = GetAncestors(regionBId);

            for (int i = 0; i < ancestorsA.Count; i++)
            {
                for (int j = 0; j < ancestorsB.Count; j++)
                {
                    if (ancestorsA[i] == ancestorsB[j])
                    {
                        return i + j;
                    }
                }
            }

            return int.MaxValue;
        }

        private double ScoreRegion(DayTrip trip, TripSearchRequestDto request)
        {
            if (!request.RegionId.HasValue || trip.Region == null)
                return 0;

            int distance = GetDistanceBetweenRegions(trip.Region.Id, request.RegionId.Value);

            if (distance == 0)
                return 40; // same region

            if (distance == 1)
                return 35; // parent or child

            if (distance == 2)
                return 30; // siblings (e.g. Bnei Brak and Ramat Gan)

            if (distance <= 4)
                return 20; // wider area (e.g. Gush Dan)

            return 5; // far away
        }
    }
}