using System.ComponentModel.DataAnnotations;

namespace DataWebAPI.Models
{
    public class CreateUserRequest
    {
        [Required, StringLength(50)]
        [RegularExpression(@"^[a-zA-Z0-9._-]+$", ErrorMessage = "Username can only contain letters, numbers, '.', '-' and '_'.")]
        public string Username { get; set; } = "";

        [Required, StringLength(100)]
        public string Name { get; set; } = "";

        [Required, EmailAddress, StringLength(100)]
        public string Email { get; set; } = "";

        [Required, StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
        public string Password { get; set; } = "";

        [StringLength(200)]
        public string? Address { get; set; }

        [StringLength(30)]
        public string? Phone { get; set; }

        [RegularExpression("^(Customer|Admin)$", ErrorMessage = "Role must be Customer or Admin.")]
        public string Role { get; set; } = UserRoles.Customer;
    }

    // username, password and role can't be changed here
    public class UpdateUserRequest
    {
        [Required, StringLength(100)]
        public string Name { get; set; } = "";

        [Required, EmailAddress, StringLength(100)]
        public string Email { get; set; } = "";

        [StringLength(200)]
        public string? Address { get; set; }

        [StringLength(30)]
        public string? Phone { get; set; }
    }

    public class LoginRequest
    {
        [Required]
        public string Username { get; set; } = "";

        [Required]
        public string Password { get; set; } = "";
    }

    public class ResetPasswordRequest
    {
        [Required, StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
        public string NewPassword { get; set; } = "";
    }

    public class UserQuery
    {
        // matches part of the username, name, email or account number
        public string? Search { get; set; }

        [RegularExpression("^(Customer|Admin)$", ErrorMessage = "Role must be Customer or Admin.")]
        public string? Role { get; set; }

        [Range(1, int.MaxValue)]
        public int Page { get; set; } = 1;

        [Range(1, 100)]
        public int PageSize { get; set; } = 10;
    }

    // the password hash is never sent back
    public class UserDto
    {
        public int UserId { get; set; }
        public string Username { get; set; } = "";
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? PicturePath { get; set; }
        public string Role { get; set; } = "";
        public bool IsActive { get; set; }
    }
}
