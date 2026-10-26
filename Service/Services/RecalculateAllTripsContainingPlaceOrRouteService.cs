using AutoMapper;
using Repository.Entities;
using Repository.Interfaces;
using Service.Dto.DayTrip;
using Service.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Service.Services
{
    public class RecalculateAllTripsContainingPlaceOrRouteService : IRecalculateAllTripsContainingPlaceOrRouteService
    {
        private readonly IDayTripItemRepository repositoryDayTripItem;
        private readonly IDayTripRepository repositoryDayTrip; 
        private readonly IDayTripCalculatedFieldsService dayTripCalculatedFieldsService;
        public RecalculateAllTripsContainingPlaceOrRouteService(IDayTripItemRepository repositoryDayTripItem, IDayTripRepository repositoryDayTrip, IDayTripCalculatedFieldsService dayTripCalculatedFieldsService)
        {
            this.repositoryDayTripItem = repositoryDayTripItem;
            this.repositoryDayTrip = repositoryDayTrip;
            this.dayTripCalculatedFieldsService = dayTripCalculatedFieldsService;
        }

        public async Task RecalculateAllTripsContainingRouteAsync(int routeId)
        {
            // 1. Get all DayTripIds that contain this place or route
            var tripIds =  (await repositoryDayTripItem
                .GetByRouteIdAsync(routeId))
                .Select(dti => dti.DayTripId)
                .Distinct()
                .ToList();

            // 2. Recalculate each trip
            foreach (var id in tripIds)
            {
                await RecalculateDayTripAsync(id);
            }
        }
        public async Task RecalculateAllTripsContainingPlaceAsync(int placeId)
        {
            var tripIds = (await repositoryDayTripItem
                .GetByPlaceIdAsync(placeId))
                .Select(dti => dti.DayTripId)
                .Distinct()
                .ToList();

            foreach (var id in tripIds)
            {
                await RecalculateDayTripAsync(id);
            }
        }
        private async Task RecalculateDayTripAsync(int dayTripId)
        {
            var dayTrip = await repositoryDayTrip.GetByIdAsync(dayTripId);

            if (dayTrip == null)
                throw new KeyNotFoundException("DayTrip not found");

            var computed = await dayTripCalculatedFieldsService.CalculateComputedFieldsAsync(dayTrip);

            ApplyComputedFields(dayTrip, computed);

            await repositoryDayTrip.UpdateAsync(dayTrip);
        }

        private void ApplyComputedFields(DayTrip dayTrip, ComputedDayTripFields computed)
        {
            dayTrip.RegionId = computed.RegionId;
            dayTrip.TotalDurationHours = computed.TotalDurationHours;
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
    }
}
