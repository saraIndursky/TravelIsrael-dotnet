using AutoMapper;
using Repository.Entities;
using Repository.Interfaces;
using Service.Dto.Region;
using Service.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Service.Services
{
    public class RegionService : IRegionService
    {
        private readonly IRegionRepository repository;
        private readonly IUserRepository repositoryUser;
        private readonly IMapper mapper;
        public RegionService(IRegionRepository repository, IUserRepository repositoryUser, IMapper mapper)
        {
            this.repository = repository;
            this.repositoryUser = repositoryUser;
            this.mapper = mapper;
        }
        public async Task<RegionDto> AddAsync(RegionCreateUpdateDto dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            if (string.IsNullOrWhiteSpace(dto.RegionName))
                throw new ArgumentException("Region name is required");

            if (await repository.ExistsByNameAsync(dto.RegionName))
                throw new InvalidOperationException("Region name already exists");

            // Check that the parent exists
            if (dto.ParentRegionId.HasValue)
            {
                var parent = await repository.GetByIdAsync(dto.ParentRegionId.Value);
                if (parent == null)
                    throw new InvalidOperationException("Parent region does not exist");

                // Check for a parent loop
                if (await CreatesHierarchyLoopAsync(0, dto.ParentRegionId.Value))
                    throw new InvalidOperationException("Hierarchy loop detected");
            }

            var region = mapper.Map<Region>(dto);
            return mapper.Map<Region, RegionDto>(await repository.AddAsync(region));
        }
        public async Task<List<RegionDto>> GetAllAsync()
        {
            var regions = await repository.GetAllAsync();
            return mapper.Map<List<RegionDto>>(regions);
        }

        public async Task<RegionDto> GetByIdAsync(int id)
        {
            var region = await repository.GetByIdAsync(id);
            if (region == null)
                throw new KeyNotFoundException($"Region with id {id} not found.");
            return mapper.Map<RegionDto>(region);
        }

        public async Task<RegionDto> UpdateAsync(int adminId, int id, RegionCreateUpdateDto dto)
        {
            var admin = await repositoryUser.GetByIdAsync(adminId);
            if (admin == null || admin.Role != Role.Admin)
                throw new UnauthorizedAccessException("Only admins can update region");

            var region = await repository.GetByIdAsync(id);
            if (region == null)
                throw new KeyNotFoundException("Region not found");

            // Check whether the region has trips or other content
            if (
                region.DayTrips != null && region.DayTrips.Any())
                throw new InvalidOperationException("Cannot update region with active content.");

            if (!string.IsNullOrWhiteSpace(dto.RegionName))
            {
                if (await repository.ExistsByNameAsync(dto.RegionName))
                    throw new InvalidOperationException("Region name already exists");

                region.RegionName = dto.RegionName;
            }

            // Check the parent
            if (dto.ParentRegionId.HasValue)
            {
                if (await CreatesHierarchyLoopAsync(id, dto.ParentRegionId.Value))
                    throw new InvalidOperationException("Hierarchy loop detected");

                if (dto.ParentRegionId.Value == id)
                    throw new InvalidOperationException("Region cannot be parent of itself");

                var parent = await repository.GetByIdAsync(dto.ParentRegionId.Value);
                if (parent == null)
                    throw new InvalidOperationException("Parent region does not exist");

                region.ParentRegionId = dto.ParentRegionId;
            }
            else
            {
                region.ParentRegionId = null;
            }

            await repository.UpdateAsync(region);

            return mapper.Map<RegionDto>(await repository.GetByIdAsync(id));
        }
        public async Task<bool> DeleteAsync(int adminId, int id)
        {
            // Check that the caller exists and is an admin
            var admin = await repositoryUser.GetByIdAsync(adminId);
            if (admin == null || admin.Role != Role.Admin)
                throw new UnauthorizedAccessException("Only admins can delete region.");

            var region = await repository.GetByIdAsync(id);
            if (region == null)
                throw new KeyNotFoundException($"Region with id {id} not found.");

            // Check whether the region has trips or other content
            if ((region.DayTrips != null && region.DayTrips.Any()) ||
                (region.Routes != null && region.Routes.Any()) ||
                (region.Places != null && region.Places.Any()))
                throw new InvalidOperationException("Cannot delete region with active content.");

            // Check for sub-regions
            if (await repository.HasChildrenAsync(id))
                throw new InvalidOperationException("Cannot delete region with sub regions.");

            return await repository.DeleteAsync(id);
        }
        private async Task<bool> CreatesHierarchyLoopAsync(int regionId, int newParentId)
        {
            var parent = await repository.GetByIdAsync(newParentId);

            while (parent != null)
            {
                if (parent.Id == regionId)
                    return true;

                if (!parent.ParentRegionId.HasValue)
                    break;

                parent = await repository.GetByIdAsync(parent.ParentRegionId.Value);
            }

            return false;
        }
    }
}
