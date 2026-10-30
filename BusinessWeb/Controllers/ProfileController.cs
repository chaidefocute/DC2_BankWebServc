using System.Security.Claims;
using BankMvc.Models;
using BankMvc.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankMvc.Controllers
{
    // any signed in user can see and edit their OWN profile (the user id always comes from the login cookie)
    [Authorize]
    public class ProfileController : Controller
    {
        // the client used to talk to the part A service
        private readonly BankApiClient _api;

        public ProfileController(BankApiClient api)
        {
            _api = api;
        }

        // helper method to get the logged in user's id from the login cookie
        private bool TryGetUserId(out int userId)
        {
            return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
        }

        // page that shows the profile details and picture
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

        // method that saves the edited details
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

                // e.g. "Email is already taken." or the service is down
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

        // method that uploads a new profile picture
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
                // e.g. "Only .jpg, .jpeg and .png files are allowed."
                TempData["Error"] = result.Details.Count > 0 ? string.Join(" ", result.Details) : result.Message;

            return RedirectToAction(nameof(Index));
        }

        // the picture itself, the browser loads it from here (the part A service is never called from the browser)
        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]   // so a new upload shows straight away
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
