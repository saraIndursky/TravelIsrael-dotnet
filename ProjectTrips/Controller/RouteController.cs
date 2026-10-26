using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Repository.Entities;
using Service.Dto;
using Service.Dto.Rating;
using Service.Dto.Route;
using Service.Dto.RoutePoint;
using Service.Interface;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ProjectTrips.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    public class RouteController : ControllerBase
    {
        private readonly IRouteService service;
        public RouteController(IRouteService service)
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

        // ================= ROUTE =================

        // Create a route (signed-in users)
        [HttpPost]
        [Authorize]
        public async Task<ActionResult<RouteDto>> CreateAsync([FromBody] RouteCreateDto dto)
        {
            try
            {
                dto.CreatedByUserId = CurrentUserId;
                var createdRoute = await service.AddAsync(dto);
                return CreatedAtRoute(
                    "GetRouteById",
                    new { id = createdRoute.Id },
                    createdRoute
                );
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // Get all routes (signed-in users)
        [HttpGet]
        [Authorize]
        public async Task<ActionResult<List<RouteDto>>> GetAllAsync()
        {
            return Ok(await service.GetAllAsync());
        }

        // Get a route by ID (open to everyone)
        [HttpGet("{id}", Name = "GetRouteById")]
        [AllowAnonymous]
        public async Task<ActionResult<RouteDto>> GetByIdAsync(int id)
        {
            try
            {
                return Ok(await service.GetByIdAsync(id));
            }
            catch (KeyNotFoundException)
            {
                return NotFound($"Route with id {id} not found.");
            }
        }

        // Update a route (signed-in users)
        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> UpdateAsync(int id, [FromBody] RouteUpdateDto dto)
        {
            try
            {
                await service.UpdateAsync(CurrentUserId, id, dto);
                return Ok("Route updated successfully.");
            }
            catch (KeyNotFoundException)
            {
                return NotFound($"Route with id {id} not found.");
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // Update a route rating (signed-in users)
        [HttpPut("{id}/rating")]
        [Authorize]
        public async Task<IActionResult> UpdateRatingAsync(int id, [FromBody] UpdateRatingDto dto)
        {
            try
            {
                await service.UpdateRatingAsync(CurrentUserId, id, dto);
                return Ok("Route updated successfully.");
            }
            catch (KeyNotFoundException)
            {
                return NotFound($"Route with id {id} not found.");
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // Approve a route (admin only)
        [HttpPut("{id}/approval")]
        [Authorize]
        public async Task<ActionResult<RouteDto>> SetApprovalAsync(int id, [FromBody] ApproveRequest request)
        {
            try
            {
                var route = await service.SetApprovalStatusAsync(CurrentUserId, id, request.ApprovalStatus, request.RejectReason);
                return Ok(route);
            }
            catch (KeyNotFoundException)
            {
                return NotFound($"Route with id {id} not found.");
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // Delete a route (signed-in users)
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteAsync(int id)
        {
            try
            {
                await service.DeleteAsync(CurrentUserId, id);
                return Ok(new { message = "Route type deactivated successfully" });
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

        // ================= ROUTE POINTS =================

        // Get route points (open to everyone)
        [HttpGet("{routeId}/points")]
        [AllowAnonymous]
        public async Task<ActionResult<List<RoutePointDto>>> GetRoutePointsAsync(int routeId)
        {
            try
            {
                return Ok(await service.GetRoutePointsAsync(routeId));
            }
            catch (KeyNotFoundException)
            {
                return NotFound("Route not found.");
            }
        }

        // Add a point to a route (signed-in users)
        [HttpPost("{routeId}/points")]
        [Authorize]
        public async Task<IActionResult> AddPointToRouteAsync(int routeId, [FromBody] RoutePointCreateDto point)
        {
            try
            {
                var result = await service.AddPointToRouteAsync(CurrentUserId, routeId, point);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // Update a point's order (signed-in users)
        [HttpPut("points/{routePointId}/order")]
        [Authorize]
        public async Task<IActionResult> UpdateOrderAsync(int routePointId, [FromQuery] int newOrder)
        {
            try
            {
                await service.UpdateOrderInRouteAsync(CurrentUserId, routePointId, newOrder);
                return Ok("Order updated successfully.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // Delete a point (signed-in users)
        [HttpDelete("points/{routePointId}")]
        [Authorize]
        public async Task<IActionResult> DeleteRoutePointAsync(int routePointId)
        {
            try
            {
               await service.DeleteRoutePointAsync(CurrentUserId, routePointId);
                return Ok("Point removed from route.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // Replace all points (signed-in users)
        [HttpPut("{routeId}/points")]
        [Authorize]
        public async Task<IActionResult> ReplaceRoutePointsAsync(int routeId, [FromBody] List<RoutePointCreateDto> points)
        {
            try
            {
                await service.ReplaceRoutePointsAsync(CurrentUserId, routeId, points);
                return Ok("Route points replaced successfully.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}