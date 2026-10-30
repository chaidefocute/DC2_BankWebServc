using System.Security.Claims;
using BankMvc.Models;
using BankMvc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankMvc.Controllers
{
    // the user id always comes from the login cookie, so users can only see their own profile
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly BankApiClient _api;

        public ProfileController(BankApiClient api)
        {
            _api = api;
        }

        private bool TryGetUserId(out int userId)
        {
            return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!TryGetUserId(out var userId))
                return Challenge();

            var result = await _api.GetUserAsync(userId);
            if (!result.Ok || result.Data == null)
            {
                ViewData["Error"] = result.Message;
                return View(new ProfileViewModel { Username = User.Identity?.Name ?? "" });
            }

            var u = result.Data;
            return View(new ProfileViewModel
            {
                Username = u.Username,
                Role = u.Role,
                HasPicture = u.PicturePath != null,
                Name = u.Name,
                Email = u.Email,
                Address = u.Address,
                Phone = u.Phone
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(ProfileViewModel m)
        {
            if (!TryGetUserId(out var userId))
                return Challenge();

            if (ModelState.IsValid)
            {
                var result = await _api.UpdateUserAsync(userId, m);
                if (result.Ok)
                {
                    TempData["Success"] = "Your profile has been updated.";
                    return RedirectToAction(nameof(Index));
                }

                ModelState.AddModelError("", result.Message);
                foreach (var d in result.Details)
                    ModelState.AddModelError("", d);
            }

            // the read-only parts aren't posted by the form, so get them again to show the page
            var user = await _api.GetUserAsync(userId);
            m.Username = user.Data?.Username ?? User.Identity?.Name ?? "";
            m.Role = user.Data?.Role ?? "";
            m.HasPicture = user.Data?.PicturePath != null;
            return View(m);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadPicture(IFormFile? picture)
        {
            if (!TryGetUserId(out var userId))
                return Challenge();

            if (picture == null || picture.Length == 0)
            {
                TempData["Error"] = "Choose a picture to upload.";
                return RedirectToAction(nameof(Index));
            }

            var result = await _api.UploadPictureAsync(userId, picture);
            if (result.Ok)
                TempData["Success"] = "Your profile picture has been updated.";
            else
                TempData["Error"] = result.Details.Count > 0 ? string.Join(" ", result.Details) : result.Message;

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel m)
        {
            if (!TryGetUserId(out var userId))
                return Challenge();

            if (!ModelState.IsValid)
            {
                TempData["Error"] = ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage;
                return RedirectToAction(nameof(Index));
            }

            // check the current password by trying to sign in with it
            var check = await _api.LoginAsync(User.Identity?.Name ?? "", m.CurrentPassword);
            if (!check.Ok)
            {
                TempData["Error"] = check.Message;
                return RedirectToAction(nameof(Index));
            }

            var result = await _api.ResetPasswordAsync(userId, m.NewPassword);
            if (result.Ok)
                TempData["Success"] = "Your password has been changed.";
            else
                TempData["Error"] = result.Message;

            return RedirectToAction(nameof(Index));
        }

        // the browser loads the picture from here, it never calls the part A service itself
        [HttpGet]
        public async Task<IActionResult> Picture()
        {
            if (!TryGetUserId(out var userId))
                return Challenge();

            var picture = await _api.GetPictureAsync(userId);
            if (picture == null)
                return NotFound();

            return File(picture.Value.Data, picture.Value.ContentType);
        }
    }
}
