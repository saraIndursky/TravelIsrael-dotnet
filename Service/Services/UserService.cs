using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Service.Interface;
using Repository.Entities;
using Repository.Interfaces;
using AutoMapper;
using Service.Dto.User;
using Service.Dto;

namespace Service.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository repository;
        private readonly IDayTripRepository repositoryDayTrip;
        private readonly IMapper mapper;
        public UserService(IUserRepository repository, IDayTripRepository repositoryDayTrip, IMapper mapper)
        {
            this.repository = repository;
            this.repositoryDayTrip = repositoryDayTrip;
            this.mapper = mapper;
        }
        public async Task<UserDto> AddAsync(CreateUserDto dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            if (string.IsNullOrWhiteSpace(dto.FirstName))
                throw new ArgumentException("First name is required");

            if (string.IsNullOrWhiteSpace(dto.LastName))
                throw new ArgumentException("Last name is required");

            if (string.IsNullOrWhiteSpace(dto.Email))
                throw new ArgumentException("Email is required");

            if (!dto.Email.Contains("@"))
                throw new ArgumentException("Email format is invalid");

            if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 6)
                throw new ArgumentException("Password must be at least 6 characters");

            var existingUser = await repository.GetByEmailAsync(dto.Email);
            if (existingUser != null)
            {
                if (existingUser.IsActive)
                    throw new InvalidOperationException("Email already exists");

                // Inactive user: check that the password matches
                if (!BCrypt.Net.BCrypt.Verify(dto.Password, existingUser.Password))
                    throw new InvalidOperationException("Password does not match for existing inactive user");

                // Reactivate the user
                existingUser.IsActive = true;
                existingUser.FirstName = dto.FirstName;
                existingUser.LastName = dto.LastName;
                await repository.UpdateAsync(existingUser);
                return mapper.Map<UserDto>(existingUser);
            }

            var user = mapper.Map<User>(dto);
            user.Password = BCrypt.Net.BCrypt.HashPassword(dto.Password);
            user.Role = Role.User;
            user.CreatedAt = DateOnly.FromDateTime(DateTime.Today);
            user.IsActive = true;
            return mapper.Map<UserDto>(await repository.AddAsync(user));
        }

        public async Task<UserDto> LoginAsync(LoginDto dto)
        {
            var user = await repository.GetByEmailAsync(dto.Email);
            if (user == null)
                throw new UnauthorizedAccessException("Invalid email");

            if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.Password))
                throw new UnauthorizedAccessException("Invalid email or password");

            return mapper.Map<UserDto>(user);
        }

        public async Task<bool> SaveRefreshTokenAsync(int userId, string refreshToken)
        {
            var user = await repository.GetByIdAsync(userId);
            if (user != null)
            {
                user.RefreshToken = refreshToken;
                user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
                user.RefreshTokenCreated = DateTime.UtcNow;
                user.RefreshTokenRevoked = false;
                await repository.UpdateAsync(user);

                return true;
            }
            return false;
        }

        public async Task<UserDto> GetUserByRefreshTokenAsync(string refreshToken)
        {
            var user = await repository.GetByRefreshTokenAsync(refreshToken);

            if (user == null)
                return null;

            if (user.RefreshTokenRevoked)
                return null;

            if (user.RefreshTokenExpiryTime < DateTime.UtcNow)
                return null;

            return mapper.Map<UserDto>(user);
        }

        public async Task<List<UserDto>> GetAllAsync()
        {
            return mapper.Map<List<User>, List<UserDto>>(await repository.GetAllAsync());
        }

        public async Task<UserDto> GetByIdAsync(int userId)
        {
            var user = await repository.GetByIdAsync(userId);
            if (user == null)
                throw new KeyNotFoundException($"User with id {userId} not found.");
            return mapper.Map<UserDto>(user);
        }

        public async Task<UserDto> UpdateProfileAsync(int userId, UpdateProfileDto dto)
        {
            var user = await repository.GetByIdAsync(userId);
            if (user == null)
                throw new KeyNotFoundException("User not found");

            if (!string.IsNullOrWhiteSpace(dto.FirstName))
                user.FirstName = dto.FirstName;

            if (!string.IsNullOrWhiteSpace(dto.LastName))
                user.LastName = dto.LastName;

            if (!string.IsNullOrWhiteSpace(dto.Email) && !dto.Email.Contains("@"))
                throw new ArgumentException("Email format is invalid");

            var emailExists = await repository.ExistsByEmailAsync(userId, dto.Email);
            if (emailExists)
                throw new InvalidOperationException("Email already in use");

            if (!string.IsNullOrWhiteSpace(dto.Email))
                user.Email = dto.Email;

            await repository.UpdateAsync(user);

            return mapper.Map<UserDto>(await repository.GetByIdAsync(userId));
        }

        public async Task<UserDto> ChangePasswordAsync(int userId, ChangePasswordDto dto)
        {
            var user = await repository.GetByIdAsync(userId);
            if (user == null)
                throw new KeyNotFoundException("User not found");

            if (!BCrypt.Net.BCrypt.Verify(dto.CurrentPassword, user.Password))
                throw new UnauthorizedAccessException("Current password is incorrect");

            if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 6)
                throw new ArgumentException("Password must be at least 6 characters");

            if (dto.NewPassword == dto.CurrentPassword)
                throw new ArgumentException("New password must be different");

            user.Password = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            await repository.UpdateAsync(user);
            return mapper.Map<UserDto>(await repository.GetByIdAsync(userId));
        }

        public async Task<UserDto> ChangeRoleAsync(int adminId, int targetUserId, ChangeRoleDto dto)
        {
            var admin = await repository.GetByIdAsync(adminId);
            if (admin == null || admin.Role != Role.Admin)
                throw new UnauthorizedAccessException("Only admins can change roles");

            var user = await repository.GetByIdAsync(targetUserId);
            if (user == null)
                throw new KeyNotFoundException("User not found");

            if (!Enum.IsDefined(typeof(Role), dto.Role))
                throw new ArgumentException("Invalid role");

            user.Role = dto.Role;
            await repository.UpdateAsync(user);
            return mapper.Map<UserDto>(await repository.GetByIdAsync(targetUserId));
        }

        public async Task<bool> ReactivateUserAsync(int adminId, int userId)
        {
            // Check that the caller is an admin
            var admin = await repository.GetByIdAsync(adminId);
            if (admin == null || admin.Role != Role.Admin)
                throw new UnauthorizedAccessException("Only admins can reactivate users.");

            var user = await repository.GetByIdAsync(userId);
            if (user == null)
                throw new KeyNotFoundException($"User with id {userId} not found.");

            if (user.IsActive)
                throw new InvalidOperationException("User is already active.");

            // Mark the user as active
            user.IsActive = true;
            await repository.UpdateAsync(user);
            return true;
        }

        public async Task<bool> DeleteUserAsync(int adminId, int userId)
        {
            // Check that the caller exists and is an admin
            var admin = await repository.GetByIdAsync(adminId);
            if (admin == null || admin.Role != Role.Admin)
                throw new UnauthorizedAccessException("Only admins can delete users.");

            var user = await repository.GetByIdAsync(userId);
            if (user == null)
                throw new KeyNotFoundException($"User with id {userId} not found.");

            // Check whether the user is already inactive
            if (!user.IsActive)
                throw new InvalidOperationException("User is already inactive.");

            user.IsActive = false;
            await repository.UpdateAsync(user);
            return true;
        }

        public async Task<UserStatisticsDto> GetUserStatisticsAsync(int userId)
        {
            var createdAtUser = await repository.GetCreateAtByUserIdAsync(userId);

            var trips = await repositoryDayTrip.GetByUserIdAsync(userId);

            return new UserStatisticsDto
            {
                JoinDate = createdAtUser,
                TripsCreated = trips.Count(),
                TripsViews = trips.Sum(t => t.NumberOfViews)
            };
        }
    }
}