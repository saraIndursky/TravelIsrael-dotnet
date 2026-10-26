using AutoMapper;
using Repository.Entities;
using Repository.Interfaces;
using Repository.Repositories;
using Service.Dto.Type;
using Service.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Service.Services
{
    public class TypeService : ITypeService
    {
        private readonly ITypeRepository repository;
        private readonly IUserRepository repositoryUser;
        private readonly IMapper mapper;
        public TypeService(ITypeRepository repository, IUserRepository repositoryUser, IMapper mapper)
        {
            this.repository = repository;
            this.repositoryUser = repositoryUser;
            this.mapper = mapper;
        }
        public async Task<List<TypeDto>> GetAllTypesAsync()
        {
            return mapper.Map<List<TypeDto>>(await repository.GetAllAsync());
        }
        public async Task<List<TypeDto>> GetAllDayTripTypesAsync()
        {
            return mapper.Map<List<TypeDto>>(await repository.GetAllDayTripTypeAsync());
        }
        public async Task<List<TypeDto>> GetAllPlaceTypesAsync()
        {
            return mapper.Map<List<TypeDto>>(await repository.GetAllPlaceTypeAsync());
        }
        public async Task<List<TypeDto>> GetAllRouteTypesAsync()
        {
            return mapper.Map<List<TypeDto>>(await repository.GetAllRouteTypeAsync());
        }
        public async Task<TypeDto> GetTypeByIdAsync(int id)
        {
            var type = await repository.GetByIdAsync(id);
            if (type == null)
                throw new KeyNotFoundException($"Type with id {id} not found.");
            return mapper.Map<TypeDto>(type);
        }

        public async Task<TypeDto> CreateTypeAsync(TypeCreateUpdateDto type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            if (string.IsNullOrWhiteSpace(type.TypeName))
                throw new ArgumentException("Type name is required");

            var allTypes = await repository.GetAllAsync();
            var existingType = allTypes.FirstOrDefault(u => u.ContentType == type.ContentType && u.TypeName == type.TypeName);
            if (existingType != null)
                throw new InvalidOperationException("Type name already exists");

            var newType = mapper.Map<Repository.Entities.Type>(type);
            return mapper.Map<TypeDto>(await repository.AddAsync(newType));
        }

        public async Task<TypeDto> UpdateTypeAsync(int adminId, int id, TypeCreateUpdateDto type)
        {
            var admin = await repositoryUser.GetByIdAsync(adminId);
            if (admin == null || admin.Role != Role.Admin)
                throw new UnauthorizedAccessException("Only admins can delete route type.");

            var exisitingType = await repository.GetByIdAsync(id);
            if (exisitingType == null)
                throw new KeyNotFoundException("Route type not found");

            // Check whether there are trips or other content of this type
            if (exisitingType.Routes != null && exisitingType.Routes.Any() ||
                exisitingType.Places != null && exisitingType.Places.Any() ||
                exisitingType.DayTrips != null && exisitingType.DayTrips.Any())
                throw new InvalidOperationException("Cannot update Type with active content.");

            if (!string.IsNullOrWhiteSpace(type.TypeName))
                exisitingType.TypeName = type.TypeName;

            return mapper.Map<TypeDto>(await repository.UpdateAsync(exisitingType));
        }

        public async Task<bool> DeleteTypeAsync(int adminId, int id)
        {
            // Check that the caller exists and is an admin
            var admin = await repositoryUser.GetByIdAsync(adminId);
            if (admin == null || admin.Role != Role.Admin)
                throw new UnauthorizedAccessException("Only admins can delete type.");

            var type = await repository.GetByIdAsync(id);
            if (type == null)
                throw new KeyNotFoundException($"Type with id {id} not found.");

            // Check whether there are trips or other content of this type
            if (type.Routes != null && type.Routes.Any() ||
                type.Places != null && type.Places.Any() ||
                type.DayTrips != null && type.DayTrips.Any())
                throw new InvalidOperationException("Cannot delete type with active content.");

            return await repository.DeleteAsync(id);
        }
    }
}