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
    [AllowAnonymous] // Login and sign-up are open to everyone
    public class AuthController : ControllerBase
    {
        private readonly IUserService service;
        private readonly TokenService tokenService;

        public AuthController(IUserService service, TokenService tokenService)
        {
            this.service = service;
            this.tokenService = tokenService;
        }

        // Register
        [HttpPost("register")]
        public async Task<ActionResult<AuthResponseDto>> RegisterAsync([FromBody] CreateUserDto dto)
        {
            try
            {
                var user = await service.AddAsync(dto);

                var accessToken = tokenService.CreateToken(user);
                var refreshToken = tokenService.GenerateRefreshToken();

                await service.SaveRefreshTokenAsync(user.Id, refreshToken);

                return Ok(new AuthResponseDto()
                {
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    User = new UserResponseDto()
                    {
                        Id = user.Id,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        Email = user.Email,
                        Role = user.Role.ToString(),
                    }
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("login")]
        public async Task<ActionResult<AuthResponseDto>> LoginAsync([FromBody] LoginDto dto)
        {
            try
            {
                var user = await service.LoginAsync(dto);

                var accessToken = tokenService.CreateToken(user);
                var refreshToken = tokenService.GenerateRefreshToken();

                await service.SaveRefreshTokenAsync(user.Id, refreshToken);

                return Ok(new AuthResponseDto()
                {
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    User = new UserResponseDto()
                    {
                        Id = user.Id,
                        FirstName = user.FirstName,
                        LastName = user.LastName,
                        Email = user.Email,
                        Role = user.Role.ToString(),
                    }
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpPost("refresh")]
        public async Task<ActionResult<AuthResponseDto>> RefreshAsync([FromBody] RefreshRequestDto dto)
        {
            var user = await service.GetUserByRefreshTokenAsync(dto.RefreshToken);

            if (user == null)
                return Unauthorized("Invalid refresh token");

            var newAccessToken = tokenService.CreateToken(user);
            var newRefreshToken = tokenService.GenerateRefreshToken();

            await service.SaveRefreshTokenAsync(user.Id, newRefreshToken);

            return Ok(new AuthResponseDto()
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken,
                User = new UserResponseDto()
                {
                    Id = user.Id,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email,
                    Role = user.Role.ToString(),
                }
            });
        }

        // ===== Helper =====
        private int? CurrentUserId
        {
            get
            {
                var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!int.TryParse(claim, out var id))
                    return null;
                return id;
            }
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<ActionResult<UserResponseDto>> GetAsync()
        {
            var userId = CurrentUserId;
            if (userId == null)
                return Unauthorized();

            try
            {
                var user = await service.GetByIdAsync(userId ?? 0);
                return Ok(new UserResponseDto()
                {
                    Id = user.Id,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email,
                    Role = user.Role.ToString(),
                });
            }
            catch (KeyNotFoundException)
            {
                return NotFound($"User with id {userId} not found.");
            }
        }


        [HttpGet("statistics")]
        [Authorize]
        public async Task<ActionResult<UserStatisticsDto>> GetUserStatisticsAsync()
        {
            var userId = CurrentUserId;
            if (userId == null)
                return Unauthorized();
            try
            {
                var userStatistics = await service.GetUserStatisticsAsync(userId ?? 0);
                return Ok(userStatistics);
            }
            catch (KeyNotFoundException)
            {
                return NotFound($"User with id {userId} not found.");
            }
        }
    }
}
