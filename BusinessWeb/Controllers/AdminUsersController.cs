using System.Security.Claims;
using BankMvc.Models;
using BankMvc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankMvc.Controllers
{
    // only administrators can manage users, customers get blocked on the server
    [Authorize(Roles = "Admin")]
    public class AdminUsersController : Controller
    {
        // the client used to talk to the part A service
        private readonly BankApiClient _api;

        public AdminUsersController(BankApiClient api)
        {
            _api = api;
        }

        // page that lists all users, with search, role filter and paging
        // everything comes from the query string, ex: ?Search=smith&Role=Customer&Page=2
        public async Task<IActionResult> Index(UserSearchQuery query)
        {
            // fix the page & role in case the browser sends wrong values, always 10 users per page
            query.Page = Math.Max(1, query.Page);
            query.PageSize = 10;
            if (query.Role != "Customer" && query.Role != "Admin")
                query.Role = null;

            var result = await _api.SearchUsersAsync(query);

            // if the service is down or gave an error, show a clear message instead of crashing
            return View(new AdminUsersViewModel
            {
                Query = query,
                Result = result.Ok ? result.Data : null,
                Error = result.Ok ? null : result.Message
            });
        }

        // page that shows one user, with the reset password and deactivate buttons
        public async Task<IActionResult> Details(int id)
        {
            var result = await _api.GetUserAsync(id);
            if (!result.Ok || result.Data == null)
            {
                TempData["Error"] = result.Message;
                return RedirectToAction(nameof(Index));
            }

            return View(result.Data);
        }

        // method that sets a new password for the user
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(int id, ResetPasswordViewModel m)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage;
                return RedirectToAction(nameof(Details), new { id });
            }

            var result = await _api.ResetPasswordAsync(id, m.NewPassword);
            if (result.Ok)
            {
                // record who did it, the new password itself is never logged
                await _api.LogAdminActionAsync(User, "RESET_PASSWORD", "User", id.ToString(), "Password reset by admin");
                TempData["Success"] = "The password has been reset.";
            }
            else
            {
                TempData["Error"] = result.Message;
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // method that deactivates the user (they are signed out on their next page and can't sign in again)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deactivate(int id)
        {
            // stop admins locking themselves out
            if (User.FindFirstValue(ClaimTypes.NameIdentifier) == id.ToString())
            {
                TempData["Error"] = "You can't deactivate your own account.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var result = await _api.DeactivateUserAsync(id);
            if (result.Ok)
            {
                await _api.LogAdminActionAsync(User, "DEACTIVATE_USER", "User", id.ToString(), "User deactivated by admin");
                TempData["Success"] = "The user has been deactivated.";
            }
            else
            {
                TempData["Error"] = result.Message;
            }

            return RedirectToAction(nameof(Details), new { id });
        }
    }

    // data for the admin users page
    public class AdminUsersViewModel
    {
        // the filters currently picked, so the form keeps its values
        public UserSearchQuery Query { get; set; } = new();

        // the page of users (null if something went wrong)
        public PagedResult<UserDto>? Result { get; set; }

        public string? Error { get; set; }
    }
}
