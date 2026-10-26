using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Repository.Entities;
using Repository.Interfaces;
using Service.Dto;
using Service.Dto.User;
using Service.Interface;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ProjectTrips.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly IUserService service;
        public UserController(IUserService service)
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

        // ================= USERS =================

        // Get all users (admin only)
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<List<UserDto>>> GetAsync()
        {
            var users = await service.GetAllAsync();
            return Ok(users);
        }

        // Get a user by ID (admin only)
        [HttpGet("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<UserDto>> GetAsync(int id)
        {
            try
            {
                var user = await service.GetByIdAsync(id);
                return Ok(user);
            }
            catch (KeyNotFoundException)
            {
                return NotFound($"User with id {id} not found.");
            }
        }

        // Update the profile (the user only)
        [HttpPut("profile")]
        [Authorize]
        public async Task<IActionResult> UpdateProfileAsync([FromBody] UpdateProfileDto dto)
        {
            try
            {
                await service.UpdateProfileAsync(CurrentUserId, dto);
                return Ok("Profile updated successfully.");
            }
            catch (KeyNotFoundException)
            {
                return NotFound($"User with id {CurrentUserId} not found.");
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // Change the password (the user only)
        [HttpPut("password")]
        [Authorize]
        public async Task<IActionResult> ChangePasswordAsync([FromBody] ChangePasswordDto dto)
        {
            try
            {
                await service.ChangePasswordAsync(CurrentUserId, dto);
                return Ok("Password changed successfully.");
            }
            catch (KeyNotFoundException)
            {
                return NotFound($"User with id {CurrentUserId} not found.");
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // Change a role (admin only)
        [HttpPut("role/{targetId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ChangeRoleAsync(int targetId, [FromBody] ChangeRoleDto dto)
        {
            try
            {
                await service.ChangeRoleAsync(CurrentUserId, targetId, dto);
                return Ok("Role updated successfully.");
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // Activate a user (admin only)
        [HttpPut("reactivate/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ReactivateAsync(int id)
        {
            try
            {
                await service.ReactivateUserAsync(CurrentUserId, id);
                return Ok(new { message = "User reactivated successfully" });
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
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, "Unexpected error");
            }
        }

        // Delete a user (admin only)
        [HttpDelete("delete/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteAsync(int id)
        {
            try
            {
                await service.DeleteUserAsync(CurrentUserId, id);
                return Ok(new { message = "User deactivated successfully" });
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