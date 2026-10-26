using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Repository.Entities;
using Service.Dto.Region;
using Service.Interface;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ProjectTrips.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class RegionController : ControllerBase
    {
        private readonly IRegionService service;
        public RegionController(IRegionService service)
        {
            this.service = service;
        }

        // ===== Helper =====
        private int CurrentUserId
        {
            get
            {
                var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!int.TryParse(claim, out var id))
                    throw new UnauthorizedAccessException("User ID claim missing or invalid.");
                return id;
            }
        }

        // POST api/Region
        [HttpPost]
        public async Task<ActionResult<RegionDto>> PostAsync([FromBody] RegionCreateUpdateDto dto)
        {
            try
            {
                var createdRegion = await service.AddAsync(dto);
                return CreatedAtRoute(
                    "GetRegionById",
                    new { id = createdRegion.Id },
                    createdRegion
                );
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // GET api/Region
        [HttpGet]
        public async Task<ActionResult<List<RegionDto>>> GetAsync()
        {
            var regions = await service.GetAllAsync();
            return Ok(regions);
        }

        // GET api/Region/5
        [HttpGet("{id}", Name = "GetRegionById")]
        public async Task<ActionResult<RegionDto>> GetByIdAsync(int id)
        {
            try
            {
                var region = await service.GetByIdAsync(id);
                return Ok(region);
            }
            catch (KeyNotFoundException)
            {
                return NotFound($"Region with id {id} not found.");
            }
        }

        // PUT api/Region/5
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateAsync(int id, [FromBody] RegionCreateUpdateDto dto)
        {
            try
            {
                await service.UpdateAsync(CurrentUserId, id, dto);
                return Ok("Region updated successfully.");
            }
            catch (KeyNotFoundException)
            {
                return NotFound($"Region with id {id} not found.");
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // DELETE api/Region/5
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteAsync(int id)
        {
            try
            {
                await service.DeleteAsync(CurrentUserId, id);
                return Ok(new { message = "Region deactivated successfully" });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message); // 403
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, "Unexpected error");
            }
        }
    }
}
