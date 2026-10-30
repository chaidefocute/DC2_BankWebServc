using DataWebAPI.Models;

namespace DataWebAPI.Services
{
    public interface IUserService
    {
        Task<UserDto> CreateUserAsync(CreateUserRequest req);

        Task<UserDto> GetUserAsync(int userId);
        Task<UserDto> GetUserByUsernameAsync(string username);
        Task<UserDto> GetUserByEmailAsync(string email);

        Task UpdateUserAsync(int userId, UpdateUserRequest req);

        Task DeactivateUserAsync(int userId);

        Task<PagedResult<UserDto>> SearchUsersAsync(UserQuery q);

        Task ResetPasswordAsync(int userId, ResetPasswordRequest req);

        // returns null if the username or password is wrong
        Task<UserDto?> LoginAsync(LoginRequest req);

        Task<UserDto> SavePictureAsync(int userId, IFormFile file);

        Task<string> GetPictureFileAsync(int userId);
    }
}
