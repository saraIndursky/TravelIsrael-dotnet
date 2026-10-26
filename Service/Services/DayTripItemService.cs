using AutoMapper;
using Repository.Entities;
using Repository.Interfaces;
using Service.Dto.DayTripItem;
using Service.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Service.Services
{
    public class DayTripItemService : IDayTripItemService
    {
        private readonly IDayTripItemRepository repository;
        private readonly IPlaceRepository repositoryPlace;
        private readonly IRouteRepository repositoryRoute;
        private readonly IMapper mapper;
        public DayTripItemService(IDayTripItemRepository repository, IPlaceRepository repositoryPlace, IRouteRepository repositoryRoute, IMapper mapper)
        {
            this.repository = repository;
            this.repositoryPlace = repositoryPlace;
            this.repositoryRoute = repositoryRoute;
            this.mapper = mapper;
        }

        public async Task<List<DayTripItem>> GetByDayTripIdAsync(int dayTripId)
        {
            return await repository.GetByDayTripIdAsync(dayTripId);
        }

        public async Task<DayTripItemDto> GetByIdAsync(int id)
        {
            var dayTripItem = await repository.GetByIdAsync(id);
            if (dayTripItem == null)
                throw new KeyNotFoundException($"Day trip item with id {id} not found");

            return mapper.Map<DayTripItemDto>(dayTripItem);
        }

        public async Task<List<DayTripItemDto>> UpdateOrderInDayTripAsync(int dayTripItemId, int newOrder)
        {
            if (newOrder <= 0)
                throw new ArgumentException("Order must be greater than 0");

            var dayTripItem = await repository.GetByIdAsync(dayTripItemId);
            if (dayTripItem == null)
                throw new KeyNotFoundException($"Day trip with id {dayTripItemId} not found");

            var items = await repository.GetByDayTripIdAsync(dayTripItem.DayTripId);

            int oldOrder = dayTripItem.OrderInTrip;

            if (oldOrder == newOrder)
                return mapper.Map<List<DayTripItemDto>>(items);

            if (newOrder > items.Count)
                newOrder = items.Count;

            // Move down
            if (newOrder > oldOrder)
            {
                var affectedItems = items
                    .Where(i => i.OrderInTrip > oldOrder && i.OrderInTrip <= newOrder);

                foreach (var item in affectedItems)
                {
                    item.OrderInTrip--;
                    await repository.UpdateAsync(item);
                }
            }
            // Move up
            else
            {
                var affectedItems = items
                    .Where(i => i.OrderInTrip >= newOrder && i.OrderInTrip < oldOrder);

                foreach (var item in affectedItems)
                {
                    item.OrderInTrip++;
                    await repository.UpdateAsync(item);
                }
            }

            // Update the item itself
            dayTripItem.OrderInTrip = newOrder;
            await repository.UpdateAsync(dayTripItem);

            var updatedItems = await repository.GetByDayTripIdAsync(dayTripItem.DayTripId);

            return mapper.Map<List<DayTripItemDto>>(updatedItems);
        }

        public async Task<bool> DeleteDayTripItemAsync(int id)
        {
            var dayTripItem = await repository.GetByIdAsync(id);
            if (dayTripItem == null)
                throw new KeyNotFoundException($"Route place with id {id} not found");

            await repository.DeleteAsync(id);

            // Reorder the remaining items
            await ReorderDayTripItemsAsync(dayTripItem.DayTripId);

            return true;
        }

        private async Task ReorderDayTripItemsAsync(int dayTripId)
        {
            var dayTripItems = await repository.GetByDayTripIdAsync(dayTripId);

            for (int i = 0; i < dayTripItems.Count; i++)
            {
                dayTripItems[i].OrderInTrip = i + 1;
                await repository.UpdateAsync(dayTripItems[i]);
            }
        }

        // Adds a list of places to a trip in order
        private async Task AddDayTripItemsAsync(int dayTripId, List<DayTripItemToAdd> items)
        {
            var currentItems = await repository.GetByDayTripIdAsync(dayTripId);

            int order = currentItems.Count + 1;
            foreach (var item in items)
            {
                var estimatedDuration = 0.0;
                if (item.ItemType == ItemType.Place)
                {
                    var itemPlace = await repositoryPlace.GetByIdAsync((int)item.PlaceId);
                    estimatedDuration = itemPlace.AverageStayMinutes ?? 0;
                }
                else if (item.ItemType == ItemType.Route)
                {
                    var itemRoute = await repositoryRoute.GetByIdAsync((int)item.RouteId);
                    estimatedDuration = itemRoute.DurationMinutes;
                }

                var rp = new DayTripItem
                {
                    DayTripId = dayTripId,
                    ItemType = item.ItemType,
                    RouteId = item.RouteId,
                    PlaceId = item.PlaceId,
                    OrderInTrip = order++,
                    EstimatedDuration = estimatedDuration,
                    Mode = item.Mode
                };

                await repository.AddAsync(rp);
            }
        }

        // Replaces a trip's places with a new list, deleting the old ones
        public async Task<List<DayTripItemDto>> ReplaceDayTripItemsAsync(int dayTripId, List<DayTripItemToAdd> items)
        {
            if (items == null || !items.Any())
                throw new InvalidOperationException("No items provided");

            var distinctItems = items.GroupBy(i => new { i.ItemType, i.PlaceId, i.RouteId }).Select(g => g.First()).ToList();
            var placeIds = distinctItems.Where(i => i.ItemType == ItemType.Place && i.PlaceId.HasValue).Select(i => i.PlaceId.Value).Distinct().ToList();
            var places = await repositoryPlace.GetByIdsAsync(placeIds);
            var routeIds = distinctItems.Where(i => i.ItemType == ItemType.Route && i.RouteId.HasValue).Select(i => i.RouteId.Value).Distinct().ToList();
            var routes = await repositoryRoute.GetByIdsAsync(routeIds);
            if (places.Count != placeIds.Count || routes.Count != routeIds.Count)
                throw new InvalidOperationException("One or more items not found");

            if (distinctItems.Any(i =>
            (i.ItemType == ItemType.Place && !i.PlaceId.HasValue) ||
            (i.ItemType == ItemType.Route && !i.RouteId.HasValue)))
                throw new InvalidOperationException("Invalid item data");

            await repository.DeleteByDayTripIdAsync(dayTripId);

            await AddDayTripItemsAsync(dayTripId, items);

            var itemsNew = await repository.GetByDayTripIdAsync(dayTripId);
            return mapper.Map<List<DayTripItemDto>>(itemsNew);
        }

        public async Task<DayTripItemDto> AddItemToDayTripAsync(int dayTripId, DayTripItemToAdd item)
        {
            var existingDayTrip = await repository.GetByDayTripIdAsync(dayTripId);

            var estimatedDuration = 0.0;

            if (item.ItemType == ItemType.Place)
            {
                var place = await repositoryPlace.GetByIdAsync((int)item.PlaceId);
                if (place == null)
                    throw new InvalidOperationException("Place not found");

                if (existingDayTrip.Any(rp => rp.PlaceId == item.PlaceId))
                    throw new InvalidOperationException("Place already exists in route");

                estimatedDuration = place.AverageStayMinutes ?? 0;
            }
            else if (item.ItemType == ItemType.Route)
            {
                var route = await repositoryRoute.GetByIdAsync((int)item.RouteId);
                if (route == null)
                    throw new InvalidOperationException("Route not found");

                if (existingDayTrip.Any(rp => rp.RouteId == item.RouteId))
                    throw new InvalidOperationException("Route already exists in route");

                estimatedDuration = route.DurationMinutes;
            }

            var dayTripItem = new DayTripItem
            {
                DayTripId = dayTripId,
                ItemType = item.ItemType,
                RouteId = item.RouteId,
                PlaceId = item.PlaceId,
                OrderInTrip = existingDayTrip.Count() + 1,
                EstimatedDuration = estimatedDuration,
                Mode = item.Mode
            };

            var created = await repository.AddAsync(dayTripItem);
            return mapper.Map<DayTripItemDto>(created);
        }
    }
}