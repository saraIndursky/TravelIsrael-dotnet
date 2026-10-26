using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Repository.Entities;
using Service.Dto.Image;
using Service.Interface;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ProjectTrips.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    public class ImageController : ControllerBase
    {
        private readonly IImageService service;
        public ImageController(IImageService service)
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

        // ==================== POST ====================
        [HttpPost]
        [Authorize]
        public async Task<ActionResult<ImageDto>> PostAsync([FromForm] ImageCreateDto dto)
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
                    dto.FileImage.CopyTo(stream);
                    stream.Close();
                }

                // The creator ID is read from the token
                dto.CreatedByUserId = CurrentUserId;

                var createdImage = await service.AddAsync(dto);
                return CreatedAtRoute(
                    "GetImageById",
                    new { id = createdImage.Id },
                    createdImage
                );
            }
            catch (ArgumentNullException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
        }

        // ==================== GET ====================
        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<List<ImageDto>>> GetAsync([FromQuery] ItemType? itemType, [FromQuery] int? placeId, [FromQuery] int? routeId)
        {
            var images = await service.GetAllAsync(itemType, placeId, routeId);
            return Ok(images);
        }

        [HttpGet("{id}", Name = "GetImageById")]
        [AllowAnonymous]
        public async Task<ActionResult<ImageDto>> GetByIdAsync(int id)
        {
            try
            {
                var image = await service.GetByIdAsync(id);
                return Ok(image);
            }
            catch (KeyNotFoundException)
            {
                return NotFound($"Image with id {id} not found.");
            }
        }

        // GET BY PLACE
        [HttpGet("place/{placeId}")]
        [AllowAnonymous]
        public async Task<ActionResult<List<ImageDto>>> GetByPlaceAsync(int placeId)
        {
            try
            {
                var images = await service.GetByPlaceIdAsync(placeId);
                return Ok(images);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }

        // GET BY ROUTE
        [HttpGet("route/{routeId}")]
        [AllowAnonymous]
        public async Task<ActionResult<List<ImageDto>>> GetByRouteAsync(int routeId)
        {
            try
            {
                var images = await service.GetByRouteIdAsync(routeId);
                return Ok(images);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }

        // GET MAIN PLACE
        [HttpGet("place/{placeId}/main")]
        [AllowAnonymous]
        public async Task<ActionResult<ImageDto?>> GetMainPlaceAsync(int placeId)
        {
            var image = await service.GetMainByPlaceIdAsync(placeId);
            if (image == null)
                return NotFound("Main image not found.");

            return Ok(image);
        }

        // GET MAIN ROUTE
        [HttpGet("route/{routeId}/main")]
        [AllowAnonymous]
        public async Task<ActionResult<ImageDto?>> GetMainRouteAsync(int routeId)
        {
            var image = await service.GetMainByRouteIdAsync(routeId);
            if (image == null)
                return NotFound("Main image not found.");

            return Ok(image);
        }

        // ==================== PUT ====================
        [HttpPut("{imageId}/updateIsMain")]
        [Authorize] // Only an admin or the image creator can update
        public async Task<IActionResult> UpdateIsMainAsync(int imageId, [FromBody] UpdateIsMainImageDto dto)
        {
            try
            {
                await service.UpdateIsMainAsync(CurrentUserId, imageId, dto);
                return Ok("Image  updated successfully.");
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // ==================== DELETE ====================
        [HttpDelete("{imageId}/delete")]
        [Authorize] // Only an admin or the image creator can delete
        public async Task<IActionResult> DeleteAsync(int imageId)
        {
            try
            {
                await service.DeleteAsync(CurrentUserId, imageId);
                return Ok(new { message = "Image deleted successfully" });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception)
            {
                return StatusCode(500, "Unexpected error");
            }
        }
    }
}