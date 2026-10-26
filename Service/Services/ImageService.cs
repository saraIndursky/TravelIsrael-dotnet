using AutoMapper;
using Repository.Entities;
using Repository.Interfaces;
using Service.Dto.Image;
using Service.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Image = Repository.Entities.Image;

namespace Service.Services
{
    public class ImageService : IImageService
    {
        private readonly IImageRepository repository;
        private readonly IPlaceRepository repositoryPlace;
        private readonly IRouteRepository repositoryRoute;
        private readonly IUserRepository repositoryUser;
        private readonly IMapper mapper;
        public ImageService(IImageRepository repository, IPlaceRepository repositoryPlace, IRouteRepository repositoryRoute, IUserRepository repositoryUser, IMapper mapper)
        {
            this.repository = repository;
            this.repositoryPlace = repositoryPlace;
            this.repositoryRoute = repositoryRoute;
            this.repositoryUser = repositoryUser;
            this.mapper = mapper;
        }
        public async Task<ImageDto> AddAsync(ImageCreateDto dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            var user = await repositoryUser.GetByIdAsync(dto.CreatedByUserId);
            if (user == null)
                throw new UnauthorizedAccessException("User does not exist");

            if (dto.ItemType == ItemType.Place)
            {
                if (dto.PlaceId == null)
                    throw new InvalidOperationException("PlaceId must be provided");

                var place = await repositoryPlace.GetByIdAsync(dto.PlaceId.Value);
                if (place == null)
                    throw new InvalidOperationException("Place does not exist");

                dto.RouteId = null;
            }
            else if (dto.ItemType == ItemType.Route)
            {
                if (dto.RouteId == null)
                    throw new InvalidOperationException("RouteId must be provided");

                var route = await repositoryRoute.GetByIdAsync(dto.RouteId.Value);
                if (route == null)
                    throw new InvalidOperationException("Route does not exist");

                dto.PlaceId = null;
            }
            else
            {
                throw new InvalidOperationException("Invalid ItemType");
            }

            if (dto.IsMain)
            {
                var allowed = await TryRemoveExistingMainAsync(user, dto.ItemType, dto.PlaceId, dto.RouteId);
                
                if (!allowed)
                    dto.IsMain = false;
            }

            var image = mapper.Map<Image>(dto);
            image.CreatedAt = DateOnly.FromDateTime(DateTime.Today);

            var createdImage = await repository.AddAsync(image);
            return mapper.Map<ImageDto>(createdImage);
        }
        public async Task<List<ImageDto>> GetAllAsync(ItemType? itemType = null, int? placeId = null, int? routeId = null)
        {
            var images = await repository.GetFilteredImagesAsync(itemType, placeId, routeId);

            return mapper.Map<List<ImageDto>>(images);
        }
        public async Task<List<ImageDto>> GetByPlaceIdAsync(int placeId)
        {
            var place = await repositoryPlace.GetByIdAsync(placeId);
            if (place == null)
                throw new KeyNotFoundException($"Place with id {placeId} not found.");

            var images = await repository.GetFilteredImagesAsync(ItemType.Place, placeId, null);

            return mapper.Map<List<ImageDto>>(images);
        }
        public async Task<List<ImageDto>> GetByRouteIdAsync(int routeId)
        {
            var route = await repositoryRoute.GetByIdAsync(routeId);
            if (route == null)
                throw new KeyNotFoundException($"Route with id {routeId} not found.");

            var images = await repository.GetFilteredImagesAsync(ItemType.Route, null, routeId);

            return mapper.Map<List<ImageDto>>(images);
        }
        public async Task<ImageDto> GetByIdAsync(int id)
        {
            var image = await repository.GetByIdAsync(id);
            if (image == null)
                throw new KeyNotFoundException($"Place image with id {id} not found");
            return mapper.Map<ImageDto>(image);
        }
        public async Task<ImageDto?> GetMainByPlaceIdAsync(int placeId)
        {
            var image = await repository.GetMainImageAsync(ItemType.Place, placeId, null);

            if (image == null)
                return null;

            return mapper.Map<ImageDto>(image);
        }
        public async Task<ImageDto?> GetMainByRouteIdAsync(int routeId)
        {
            var image = await repository.GetMainImageAsync(ItemType.Route, null, routeId);

            if (image == null)
                return null;

            return mapper.Map<ImageDto>(image);
        }
        public async Task<ImageDto> UpdateIsMainAsync(int userId, int imageId, UpdateIsMainImageDto dto)
        {
            var user = await repositoryUser.GetByIdAsync(userId);
            if (user == null)
                throw new UnauthorizedAccessException("User does not exist");

            var image = await repository.GetByIdAsync(imageId);
            if (image == null)
                throw new KeyNotFoundException($"Image with id {imageId} not found.");

            if (user.Role != Role.Admin && image.CreatedByUserId != user.Id)
                throw new UnauthorizedAccessException("Only admin or creator can update image");

            if (dto.IsMain)
            {
                var allowed = await TryRemoveExistingMainAsync(user, image.ItemType, image.PlaceId, image.RouteId, image.Id);

                if (!allowed)
                    throw new UnauthorizedAccessException("Only admin or creator of the main image can replace it");
            }

            image.IsMain = dto.IsMain;
            await repository.UpdateAsync(image);
            return mapper.Map<ImageDto>(await repository.GetByIdAsync(imageId));
        }
        public async Task<bool> DeleteAsync(int userId, int imageId)
        {
            // Check that the caller exists and is an admin
            var user = await repositoryUser.GetByIdAsync(userId);
            if (user == null)
                throw new UnauthorizedAccessException("User does not exist");

            var image = await repository.GetByIdAsync(imageId);
            if (image == null)
                throw new KeyNotFoundException($"Image with id {imageId} not found.");

            if (user.Role != Role.Admin && image.CreatedByUserId != user.Id)
                throw new UnauthorizedAccessException("Only admin or creator can delete image");

            return await repository.DeleteAsync(imageId);
        }

        private async Task<bool> TryRemoveExistingMainAsync(User user, ItemType itemType, int? placeId, int? routeId, int? currentImageId = null)
        {
            var existingMain = await repository.GetMainImageAsync(itemType, placeId, routeId);

            if (existingMain == null)
                return true;

            if (currentImageId != null && existingMain.Id == currentImageId)
                return true;

            var allowed = await CanModifyMainImage(user, existingMain);

            if (!allowed)
                return false;

            existingMain.IsMain = false;

            await repository.UpdateAsync(existingMain);

            return true;
        }

        private async Task<bool> CanModifyMainImage(User user, Image image)
        {
            if (user.Role == Role.Admin)
                return true;

            if (image.ItemType == ItemType.Place)
            {
                var place = await repositoryPlace.GetByIdAsync(image.PlaceId!.Value);
                return place != null && place.CreatedByUserId == user.Id;
            }

            if (image.ItemType == ItemType.Route)
            {
                var route = await repositoryRoute.GetByIdAsync(image.RouteId!.Value);
                return route != null && route.CreatedByUserId == user.Id;
            }

            return false;
        }
    }
}