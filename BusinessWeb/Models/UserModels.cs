using System.ComponentModel.DataAnnotations;

namespace BankMvc.Models
{
    // user details sent back by the part A service (the password hash is never sent)
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

    // data for the profile page, only Name, Email, Address and Phone can be changed
    public class ProfileViewModel
    {
        // shown on the page but not editable
        public string Username { get; set; } = "";
        public string Role { get; set; } = "";
        public bool HasPicture { get; set; }

        [Required(ErrorMessage = "Full name is required."), StringLength(100)]
        [Display(Name = "Full name")]
        public string Name { get; set; } = "";

        [Required(ErrorMessage = "Email is required."), EmailAddress, StringLength(100)]
        public string Email { get; set; } = "";

        [StringLength(200)]
        public string? Address { get; set; }

        [StringLength(30), Phone]
        public string? Phone { get; set; }
    }

    // filters the admin picks on the users page
    public class UserSearchQuery
    {
        // part of the username, name or email
        public string? Search { get; set; }

        public string? Role { get; set; }

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    // data from the admin's reset password form
    public class ResetPasswordViewModel
    {
        [Required(ErrorMessage = "New password is required.")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
        public string NewPassword { get; set; } = "";
    }

    // data from the sign in form
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Username is required.")]
        public string Username { get; set; } = "";

        [Required(ErrorMessage = "Password is required."), DataType(DataType.Password)]
        public string Password { get; set; } = "";
    }

    // data from the register form (same rules as the part A service)
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Username is required."), StringLength(50)]
        [RegularExpression(@"^[a-zA-Z0-9._-]+$", ErrorMessage = "Username can only contain letters, numbers, '.', '-' and '_'.")]
        public string Username { get; set; } = "";

        [Required(ErrorMessage = "Full name is required."), StringLength(100)]
        [Display(Name = "Full name")]
        public string Name { get; set; } = "";

        [Required(ErrorMessage = "Email is required."), EmailAddress, StringLength(100)]
        public string Email { get; set; } = "";

        [StringLength(200)]
        public string? Address { get; set; }

        [StringLength(30), Phone]
        public string? Phone { get; set; }

        [Required(ErrorMessage = "Password is required."), DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
        public string Password { get; set; } = "";

        [DataType(DataType.Password), Display(Name = "Confirm password")]
        [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = "";
    }

    // data from the change password form on the profile page
    public class ChangePasswordViewModel
    {
        [Required(ErrorMessage = "Current password is required."), DataType(DataType.Password)]
        [Display(Name = "Current password")]
        public string CurrentPassword { get; set; } = "";

        [Required(ErrorMessage = "New password is required."), DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
        [Display(Name = "New password")]
        public string NewPassword { get; set; } = "";

        [DataType(DataType.Password), Display(Name = "Confirm new password")]
        [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; } = "";
    }
}
