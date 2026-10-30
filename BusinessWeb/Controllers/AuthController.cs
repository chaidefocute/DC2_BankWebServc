using System.Security.Claims;
using BankMvc.Models;
using BankMvc.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace BankMvc.Controllers
{
    public class AuthController : Controller
    {
        private readonly BankApiClient _api;

        public AuthController(BankApiClient api)
        {
            _api = api;
        }

        [HttpGet]
        public IActionResult Login() => View(new LoginViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel m)
        {
            if (!ModelState.IsValid)
                return View(m);

            var result = await _api.LoginAsync(m.Username, m.Password);
            if (!result.Ok || result.Data == null)
            {
                ModelState.AddModelError("", result.Message);
                return View(m);
            }

            await SignInAsync(result.Data);

            // home sends admins to the dashboard and customers to their accounts
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult Register() => View(new RegisterViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel m)
        {
            if (!ModelState.IsValid)
                return View(m);

            var result = await _api.RegisterAsync(m);
            if (!result.Ok || result.Data == null)
            {
                ModelState.AddModelError("", result.Message);
                foreach (var detail in result.Details)
                    ModelState.AddModelError("", detail);
                return View(m);
            }

            await SignInAsync(result.Data);
            TempData["Success"] = $"Welcome, {result.Data.Name}! Your profile has been created.";
            return RedirectToAction("Index", "Home");
        }

        // POST only, so another site can't sign the user out with a link
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            TempData["Success"] = "You have been signed out.";
            return RedirectToAction("Index", "Home");
        }

        // saves who the user is in the login cookie
        // the other controllers read NameIdentifier (user id) and Role from here
        private Task SignInAsync(UserDto user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role)
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            return HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        }
    }
}
