using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Repository.Entities;
using Service.Dto;
using Service.Dto.DayTrip;
using Service.Dto.DayTripItem;
using Service.Dto.Rating;
using Service.Interface;
using Service.Services;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ProjectTrips.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    public class DayTripController : ControllerBase
    {
        private readonly IDayTripService service;
        private readonly IGetBestTripsService serviceGetBestTrips;
        public DayTripController(IDayTripService service, IGetBestTripsService serviceGetBestTrips)
        {
            this.service = service;
            this.serviceGetBestTrips = serviceGetBestTrips;
        }

        // ====== Helper ======
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

        // ================= DayTrip =================

        [HttpPost]
        [Authorize]
        public async Task<ActionResult<CalculateComputedFieldsResult>> CreateAsync([FromForm] DayTripCreateDto dto)
        {
            try
            {
                if (dto.FileImage == null || dto.FileImage.Length == 0)
                    return BadRequest("Image file is required");

                var folderPath = Path.Combine(Environment.CurrentDirectory, "Images/");

                if (!Directory.Exists(folderPath))
                    Directory.CreateDirectory(folderPath);

                var fileName = dto.FileImage.FileName;
                var fullPath = Path.Combine(folderPath, fileName);

                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await dto.FileImage.CopyToAsync(stream);
                    stream.Close();
                }

                dto.ImageUrl = fileName;

                dto.CreatedByUserId = CurrentUserId;
                var result = await service.AddAsync(dto);
                return CreatedAtRoute(
                    "GetDayTripById",
                    new { id = result.DayTrip.Id },
                    result
                );
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<List<DayTripDetaileDto>>> GetAllAsync()
        {
            return Ok(await service.GetAllAsync());
        }

        [HttpGet("top-three")]
        [AllowAnonymous] 
        public async Task<ActionResult<List<DayTripDto>>> GetTopThreeOrderByNumberOfViewsAsync()
        {
            return Ok(await service.GetTopThreeOrderByNumberOfViewsAsync());
        }

        [HttpPost("filtered")]
        [AllowAnonymous]
        public async Task<ActionResult<List<DayTripDto>>> GetFilteredTrips([FromBody] TripFilterDto filter)
        {
            var result = await service.GetFilteredTripsAsync(filter);
            return Ok(result);
        }

        [HttpGet("{id}", Name = "GetDayTripById")]
        [AllowAnonymous]
        public async Task<ActionResult<DayTripDetaileDto>> GetByIdAsync(int id)
        {
            try
            {
                return Ok(await service.GetByIdAsync(id));
            }
            catch (KeyNotFoundException)
            {
                return NotFound($"Day trip with id {id} not found.");
            }
        }

        [HttpPut("{id}")]
        [Authorize]
        public async Task<ActionResult<CalculateComputedFieldsResult>> UpdateAsync(int id, [FromForm] DayTripUpdateDto dto)
        {
            try
            {
                if (dto.FileImage == null || dto.FileImage.Length == 0)
                    return BadRequest("Image file is required");

                var folderPath = Path.Combine(Environment.CurrentDirectory, "Images/");

                if (!Directory.Exists(folderPath))
                    Directory.CreateDirectory(folderPath);

                var fileName = dto.FileImage.FileName;
                var fullPath = Path.Combine(folderPath, fileName);

                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await dto.FileImage.CopyToAsync(stream);
                    stream.Close();
                }

                dto.ImageUrl = fileName;

                var result = await service.UpdateAsync(CurrentUserId, id, dto);
                return Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return NotFound($"Day trip with id {id} not found.");
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("{id}/rating")]
        [Authorize]
        public async Task<ActionResult<DayTripDetaileDto>> UpdateRatingAsync(int id, [FromBody] UpdateRatingDto dto)
        {
            try
            {
                return Ok(await service.UpdateRatingAsync(id, dto));
            }
            catch (KeyNotFoundException)
            {
                return NotFound($"Day trip with id {id} not found.");
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("{id}/approval")]
        [Authorize]
        public async Task<ActionResult<DayTripDetaileDto>> SetApprovalAsync(int id, [FromQuery] ApprovalStatus approvalStatus)
        {
            try
            {
                return Ok(await service.SetApprovalStatusAsync(CurrentUserId, id, approvalStatus));
            }
            catch (KeyNotFoundException)
            {
                return NotFound($"Day trip with id {id} not found.");
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("{dayTripId}/approval/with-items")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ApproveDayTripWithItemsAsync(int dayTripId, [FromBody] ApproveRequest request)
        {
            Console.WriteLine(HttpContext.Request.QueryString);
            Console.WriteLine(HttpContext.Request.ContentType);
            try
            {
                await service.ApproveDayTripWithItemsAsync(CurrentUserId, dayTripId, request);
                return Ok(await service.GetByIdAsync(dayTripId));
            }
            catch (KeyNotFoundException)
            {
                return NotFound($"Day trip with id {dayTripId} not found.");
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteAsync(int id)
        {
            try
            {
                await service.DeleteAsync(CurrentUserId, id);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // ================= DayTrip Items =================

        [HttpGet("{dayTripId}/items")]
        [AllowAnonymous]
        public async Task<ActionResult<List<DayTripItemDto>>> GetDayTripItemsAsync(int dayTripId)
        {
            try
            {
                return Ok(await service.GetDayTripItemsAsync(dayTripId));
            }
            catch (KeyNotFoundException)
            {
                return NotFound("Day trip not found.");
            }
        }

        [HttpPost("{dayTripId}/item")]
        [Authorize]
        public async Task<ActionResult<CalculateComputedFieldsResult>> AddItemToDayTripAsync(int dayTripId, [FromBody] DayTripItemToAdd item)
        {
            try
            {
                var result = await service.AddItemToDayTripAsync(CurrentUserId, dayTripId, item);
                return Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return NotFound("Day trip not found.");
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("items/{dayTripItemId}/order")]
        [Authorize]
        public async Task<ActionResult<CalculateComputedFieldsResult>> UpdateOrderAsync(int dayTripItemId, [FromQuery] int newOrder)
        {
            try
            {
                var result = await service.UpdateOrderInDayTripAsync(CurrentUserId, dayTripItemId, newOrder);
                return Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return NotFound("Item not found.");
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("items/{dayTripItemId}")]
        [Authorize]
        public async Task<ActionResult<CalculateComputedFieldsResult>> DeleteDayTripItemAsync(int dayTripItemId)
        {
            try
            {
                var result = await service.DeleteDayTripItemAsync(CurrentUserId, dayTripItemId);
                return Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return NotFound("Item not found.");
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPut("{dayTripId}/items")]
        [Authorize]
        public async Task<IActionResult> ReplaceDayTripItemsAsync(int dayTripId, [FromBody] List<DayTripItemToAdd> items)
        {
            try
            {
                var result = await service.ReplaceDayTripItemsAsync(CurrentUserId, dayTripId, items);
                return Ok(result);
            }
            catch (KeyNotFoundException)
            {
                return NotFound("Day trip not found.");
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }


        // Returns the five best-scoring trips that match the user's request
        [HttpPost("recommend")]
        public async Task<ActionResult<List<RecommendedTripDto>>> GetBestTrips([FromBody] TripSearchRequestDto request)
        {
            var result = await serviceGetBestTrips.GetBestTripsAsync(request);
            return Ok(result);
        }
    }
}