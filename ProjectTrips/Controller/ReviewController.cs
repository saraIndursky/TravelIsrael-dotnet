using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Service.Dto.Review;
using Service.Interface;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ProjectTrips.Controller
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReviewController : ControllerBase
    {
        private readonly IReviewService reviewService;

        public ReviewController(IReviewService reviewService)
        {
            this.reviewService = reviewService;
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

        // Create a review (signed-in users)
        [HttpPost]
        [Authorize]
        [EnableRateLimiting("reviews")]
        public async Task<ActionResult> CreateReviewAsync([FromBody] ReviewCreateDto dto)
        {
            try
            {
                dto.UserId = CurrentUserId;
                var review = await reviewService.CreateReviewAsync(dto);
                return Ok(review);
            }
            catch (ArgumentNullException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        // ===== Reading reviews is open to everyone =====
        [HttpGet("place/{placeId}")]
        [AllowAnonymous]
        public async Task<ActionResult> GetReviewsForPlaceAsync(int placeId)
        {
            try
            {
                var reviews = await reviewService.GetReviewsForPlaceAsync(placeId);
                return Ok(reviews);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("route/{routeId}")]
        [AllowAnonymous]
        public async Task<ActionResult> GetReviewsForRouteAsync(int routeId)
        {
            try
            {
                var reviews = await reviewService.GetReviewsForRouteAsync(routeId);
                return Ok(reviews);
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("daytrip/{dayTripId}")]
        [AllowAnonymous]
        public async Task<ActionResult> GetReviewsForDayTripAsync(int dayTripId)
        {
            try
            {
                var reviews = await reviewService.GetReviewsForDayTripAsync(dayTripId);
                return Ok(reviews);
            }
            catch (Exception ex) 
            { 
                return StatusCode(500, ex.Message); 
            }
        }

        // Delete a review (signed-in users)
        [HttpDelete("{reviewId}")]
        [Authorize]
        public async Task<IActionResult> DeleteReviewAsync(int reviewId)
        {
            try
            {
                await reviewService.DeleteReviewAsync(CurrentUserId, reviewId);
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
            catch (Exception ex) 
            { 
                return StatusCode(500, ex.Message); 
            }
        }
    }
}

