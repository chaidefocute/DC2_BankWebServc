using System.Security.Claims;
using BankMvc.Models;
using BankMvc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankMvc.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminUsersController : Controller
    {
        private readonly BankApiClient _api;

        public AdminUsersController(BankApiClient api)
        {
            _api = api;
        }

        public async Task<IActionResult> Index(UserSearchQuery query)
        {
            // fix the page & role in case the browser sends wrong values, always 10 users per page
            query.Page = Math.Max(1, query.Page);
            query.PageSize = 10;
            if (query.Role != "Customer" && query.Role != "Admin")
                query.Role = null;

            var result = await _api.SearchUsersAsync(query);

            return View(new AdminUsersViewModel
            {
                Query = query,
                Result = result.Ok ? result.Data : null,
                Error = result.Ok ? null : result.Message
            });
        }

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

        [HttpGet]
        public IActionResult Create() => View(new CreateUserViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateUserViewModel m)
        {
            if (!ModelState.IsValid)
                return View(m);

            var result = await _api.CreateUserAsync(m, m.Role);
            if (!result.Ok || result.Data == null)
            {
                ModelState.AddModelError("", result.Message);
                foreach (var d in result.Details)
                    ModelState.AddModelError("", d);
                return View(m);
            }

            await _api.LogAdminActionAsync(User, "CREATE_USER", "User", result.Data.UserId.ToString(), $"Created {m.Role} {m.Username}");
            TempData["Success"] = "The user has been created.";
            return RedirectToAction(nameof(Details), new { id = result.Data.UserId });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var result = await _api.GetUserAsync(id);
            if (!result.Ok || result.Data == null)
            {
                TempData["Error"] = result.Message;
                return RedirectToAction(nameof(Index));
            }

            var u = result.Data;
            ViewData["UserId"] = id;
            return View(new ProfileViewModel
            {
                Username = u.Username,
                Role = u.Role,
                Name = u.Name,
                Email = u.Email,
                Address = u.Address,
                Phone = u.Phone
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ProfileViewModel m)
        {
            if (ModelState.IsValid)
            {
                var result = await _api.UpdateUserAsync(id, m);
                if (result.Ok)
                {
                    await _api.LogAdminActionAsync(User, "EDIT_USER", "User", id.ToString(), "User details edited by admin");
                    TempData["Success"] = "The user has been updated.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                ModelState.AddModelError("", result.Message);
                foreach (var d in result.Details)
                    ModelState.AddModelError("", d);
            }

            ViewData["UserId"] = id;
            return View(m);
        }

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
                // the new password itself is never logged
                await _api.LogAdminActionAsync(User, "RESET_PASSWORD", "User", id.ToString(), "Password reset by admin");
                TempData["Success"] = "The password has been reset.";
            }
            else
            {
                TempData["Error"] = result.Message;
            }

            return RedirectToAction(nameof(Details), new { id });
        }

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

    public class AdminUsersViewModel
    {
        public UserSearchQuery Query { get; set; } = new();

        // null if something went wrong
        public PagedResult<UserDto>? Result { get; set; }

        public string? Error { get; set; }
    }
}
