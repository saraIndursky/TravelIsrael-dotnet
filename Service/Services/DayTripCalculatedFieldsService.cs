using AutoMapper;
using Repository.Entities;
using Repository.Interfaces;
using Service.Dto.DayTrip;
using Service.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Service.Services
{
    public class DayTripCalculatedFieldsService : IDayTripCalculatedFieldsService
    {
        private readonly IRegionRepository repositoryRegion;

        const double MIN_REAL_TEMP = -10;
        const double MAX_REAL_TEMP = 50;
        const double MAX_REAL_WIND = 200;

        public DayTripCalculatedFieldsService(IRegionRepository repositoryRegion)
        {
            this.repositoryRegion = repositoryRegion;
        }
        public async Task<ComputedDayTripFields> CalculateComputedFieldsAsync(DayTrip dayTrip)
        {
            if (dayTrip == null)
                throw new ArgumentNullException(nameof(dayTrip));

            var items = dayTrip.DayTripItems.OrderBy(i => i.OrderInTrip).ToList();

            var places = items.Where(i => i.ItemType == ItemType.Place && i.PlaceId.HasValue).Select(i => i.Place).Where(p => p != null).Distinct().ToList();
            var routes = items.Where(i => i.ItemType == ItemType.Route && i.RouteId.HasValue).Select(i => i.Route).Where(r => r != null).Distinct().ToList();

            // ---------------- PRICE ----------------
            double totalPrice = places.Sum(p => p.Price) + routes.Sum(r => r.Price);

            // ---------------- ACCESSIBILITY ----------------
            // Collect the accessibility values
            var allAccessibility = items.Select(i =>
                i.ItemType == ItemType.Place ?
                    i.Place.Accessibility :
                    i.Route.Accessibility
                ).ToList();

            var accessibility = CalculateAccessibility(allAccessibility);

            // ---------------- REGION ----------------
            var regionId = await CalculateCommonRegionAsync(places, routes);

            // ---------------- LENGTH ----------------
            double length = CalculateTotalDistance(items) + Math.Round(routes.Sum(r => r.LengthKm), 2);

            // ---------------- DURATION ----------------
            double totalMinutes = CalculateTotalTripDuration(items);

            // ---------------- ESTIMATED END ----------------
            int bufferMinutes = (int)Math.Ceiling(totalMinutes * 0.10);

            var estimatedEnd = dayTrip.StartTime.AddMinutes(totalMinutes + bufferMinutes);

            // ---------------- VALIDATION ----------------
            var scheduleResult = new ScheduleResult
            {
                EstimatedEndTime = estimatedEnd
            };

            // First and last stops by order
            var firstItem = items.FirstOrDefault();
            var lastItem = items.LastOrDefault();

            if (firstItem != null)
            {
                var firstOpening = firstItem.ItemType == ItemType.Place ?
                    firstItem.Place.OpeningTime :
                    firstItem.Route.OpeningTime;

                if (dayTrip.StartTime < firstOpening)
                {
                    scheduleResult.Warnings.Add(
                        "שעת ההתחלה מוקדמת משעת הפתיחה של היעד הראשון.");
                }
            }
            if (lastItem != null)
            {
                var lastClosing = lastItem.ItemType == ItemType.Place ?
                    lastItem.Place.ClosingTime :
                    lastItem.Route.ClosingTime;

                if (estimatedEnd > lastClosing)
                {
                    scheduleResult.Warnings.Add(
                        "הטיול מסתיים לאחר שעת הסגירה של היעד האחרון.");
                }
            }
            // ---------------- DIFFICULTY ----------------
            var allDifficulty = items.Select(i =>
                i.ItemType == ItemType.Route ?
                    i.Route.Difficulty :
                    Difficulty.Easy
            ).ToList();

            var maxDifficultyRoute = allDifficulty.Max(d => ((int)d));

            Difficulty difficulty = CalculateDifficulty(length, totalMinutes, maxDifficultyRoute);

            // ---------------- WEATHER ----------------
            var minTemperaturePlace = places.Any() ? places.Max(p => p.MinTemperature) : MIN_REAL_TEMP;
            var minTemperatureRoute = routes.Any() ? routes.Max(r => r.MinTemperature) : MIN_REAL_TEMP;
            var minTemperature = minTemperaturePlace > minTemperatureRoute ? minTemperaturePlace : minTemperatureRoute;

            var maxTemperaturePlace = places.Any() ? places.Min(p => p.MaxTemperature) : MAX_REAL_TEMP;
            var maxTemperatureRoute = routes.Any() ? routes.Min(r => r.MaxTemperature) : MAX_REAL_TEMP;
            var maxTemperature = maxTemperaturePlace < maxTemperatureRoute ? maxTemperaturePlace : maxTemperatureRoute;

            var maxWindSpeedPlace = places.Any() ? places.Min(p => p.MaxWindSpeed) : MAX_REAL_WIND;
            var maxWindSpeedRoute = routes.Any() ? routes.Min(r => r.MaxWindSpeed) : MAX_REAL_WIND;
            var maxWindSpeed = maxWindSpeedPlace < maxWindSpeedRoute ? maxWindSpeedPlace : maxWindSpeedRoute;

            var maxRainProbabilityPlace = places.Any() ? places.Min(p => p.MaxRainProbability ?? 100) : 100;
            var maxRainProbabilityRoute = routes.Any() ? routes.Min(r => r.MaxRainProbability ?? 100) : 100;
            var maxRainProbability = maxRainProbabilityPlace < maxRainProbabilityRoute ? maxRainProbabilityPlace : maxRainProbabilityRoute;

            var maxHumidityPlace = places.Any() ? places.Min(p => p.MaxHumidity ?? 100) : 100;
            var maxHumidityRoute = routes.Any() ? routes.Min(r => r.MaxHumidity ?? 100) : 100;
            var maxHumidity = maxHumidityPlace < maxHumidityRoute ? maxHumidityPlace : maxHumidityRoute;

            var maxCloudCoveragePlace = places.Any() ? places.Min(p => p.MaxCloudCoverage ?? 100) : 100;
            var maxCloudCoverageRoute = routes.Any() ? routes.Min(r => r.MaxCloudCoverage ?? 100) : 100;
            var maxCloudCoverage = maxCloudCoveragePlace < maxCloudCoverageRoute ? maxCloudCoveragePlace : maxCloudCoverageRoute;

            var allowRain = places.All(p => p.AllowRain) && routes.All(r => r.AllowRain);

            bool hasCommonTemperature = minTemperature <= maxTemperature;

            // -------- RETURN --------
            return new ComputedDayTripFields
            {
                RegionId = regionId,
                TotalDurationHours = Math.Round(totalMinutes / 60.0, 2),
                TotalLengthKM = length,
                Price = totalPrice,
                Accessibility = accessibility,
                EndTime = estimatedEnd,
                Difficulty = difficulty,
                MinTemperature = minTemperature,
                MaxTemperature = maxTemperature,
                MaxWindSpeed = maxWindSpeed,
                MaxRainProbability = maxRainProbability,
                MaxHumidity = maxHumidity,
                MaxCloudCoverage = maxCloudCoverage,
                AllowRain = allowRain,
                HasCommonWeather = hasCommonTemperature,
                ScheduleResult = scheduleResult
            };
        }

        private Accessibility CalculateAccessibility(List<Accessibility> values)
        {
            if (values.All(a => a == Accessibility.FullyAccessible))
                return Accessibility.FullyAccessible;

            if (values.Any(a => a == Accessibility.NotAccessible))
                return Accessibility.NotAccessible;

            if (values.Any(a => a == Accessibility.WheelchairAccessible))
                return Accessibility.WheelchairAccessible;

            if (values.Any(a => a == Accessibility.Low))
                return Accessibility.Low;

            return Accessibility.Partial;
        }

        private Difficulty CalculateDifficulty(double lengthKm, double durationMinutes, int maxRouteDifficulty)
        {
            // Combined score
            double score = 0;

            // Weights: 30% distance, 30% time, 40% route difficulty
            score += (lengthKm / 10.0) * 0.3;        // 10 km = 1 point
            score += (durationMinutes / 120.0) * 0.3;      // 2 hours = 1 point
            score += (maxRouteDifficulty / 5.0) * 0.4;  // route difficulty from 1 to 5

            // Convert the score to difficulty levels
            if (score < 1.5) return Difficulty.Easy;
            if (score < 2.5) return Difficulty.EasyMedium;
            if (score < 3.5) return Difficulty.Medium;
            if (score < 4.5) return Difficulty.MediumHard;
            return Difficulty.Hard;
        }

        private async Task<int?> CalculateCommonRegionAsync(List<Place> places, List<Route> routes)
        {
            if ((places == null || !places.Any()) && (routes == null || !routes.Any()))
                return null;

            var regionIds = places
                .Select(p => p.RegionId)
                .Concat(routes
                    .Where(r => r.RegionId.HasValue)
                    .Select(r => r.RegionId.Value)
                )
                .Distinct()
                .ToList();

            // Collect all ancestors of each region
            var allPaths = new List<List<int>>();

            foreach (var regionId in regionIds)
            {
                var path = new List<int>();
                var currentId = regionId;

                while (currentId != 0)
                {
                    path.Add(currentId);

                    var region = await repositoryRegion.GetByIdAsync(currentId);
                    if (region?.ParentRegionId == null)
                        break;

                    currentId = region.ParentRegionId.Value;
                }

                allPaths.Add(path);
            }

            // Intersect all the lists
            var common = allPaths
                .Skip(1)
                .Aggregate(
                    new HashSet<int>(allPaths.First()),
                    (h, e) => { h.IntersectWith(e); return h; }
                );

            if (!common.Any())
                return null;

            // Pick the deepest (most specific) region
            foreach (var id in allPaths.First())
            {
                if (common.Contains(id))
                    return id;
            }

            return null;
        }

        private double CalculateTotalDistance(List<DayTripItem> items)
        {
            double total = 0;

            for (int i = 0; i < items.Count - 1; i++)
            {
                var lat1 = items[i].ItemType == ItemType.Place ? items[i].Place.Latitude : items[i].Route.EndLatitude;
                var lon1 = items[i].ItemType == ItemType.Place ? items[i].Place.Longitude : items[i].Route.EndLongitude;
                var lat2 = items[i + 1].ItemType == ItemType.Place ? items[i + 1].Place.Latitude : items[i + 1].Route.StartLatitude;
                var lon2 = items[i + 1].ItemType == ItemType.Place ? items[i + 1].Place.Longitude : items[i + 1].Route.StartLongitude;
                total += HaversineDistance(lat1, lon1, lat2, lon2);
            }

            return Math.Round(total, 2);
        }

        private double HaversineDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371; // Earth radius in km

            var dLat = DegreesToRadians(lat2 - lat1);
            var dLon = DegreesToRadians(lon2 - lon1);

            //a=sin2(2Δlat​)+cos(lat1)⋅cos(lat2)⋅sin2(2Δlon​)
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(DegreesToRadians(lat1)) *
                    Math.Cos(DegreesToRadians(lat2)) *
                    Math.Sin(dLon / 2) *
                    Math.Sin(dLon / 2);

            //c=2⋅arctan2(a^0.5,(1−a)^0.5)
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return R * c;
        }

        private double DegreesToRadians(double deg)
        {
            return deg * (Math.PI / 180);
        }

        public double CalculateTotalTripDuration(List<DayTripItem> items)
        {
            if (items == null || items.Count == 0)
                return 0;

            double stayMinutes = items.Sum(p => p.EstimatedDuration);

            double travelMinutes = 0;

            for (int i = 0; i < items.Count - 1; i++)
            {
                var lat1 = items[i].ItemType == ItemType.Place ? items[i].Place.Latitude : items[i].Route.EndLatitude;
                var lon1 = items[i].ItemType == ItemType.Place ? items[i].Place.Longitude : items[i].Route.EndLongitude;
                var lat2 = items[i + 1].ItemType == ItemType.Place ? items[i + 1].Place.Latitude : items[i + 1].Route.StartLatitude;
                var lon2 = items[i + 1].ItemType == ItemType.Place ? items[i + 1].Place.Longitude : items[i + 1].Route.StartLongitude;

                double distance = HaversineDistance(lat1, lon1, lat2, lon2);

                double speed = GetSpeedKmPerHour(items[i].Mode);

                travelMinutes += (distance / speed) * 60;
            }

            return stayMinutes + (int)Math.Round(travelMinutes);
        }
        private double GetSpeedKmPerHour(TravelMode? mode)
        {
            return mode switch
            {
                TravelMode.Walking => 5.0,
                TravelMode.Bike => 15.0,
                TravelMode.Car => 50.0,
                _ => 5.0
            };
        }
    }
}