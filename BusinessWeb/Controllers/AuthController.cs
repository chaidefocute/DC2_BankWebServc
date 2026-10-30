using System.Security.Claims;
using BankMvc.Models;
using BankMvc.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace BankMvc.Controllers
{
    // sign in, register and sign out (open to everyone)
    public class AuthController : Controller
    {
        // the client used to talk to the part A service
        private readonly BankApiClient _api;

        public AuthController(BankApiClient api)
        {
            _api = api;
        }

        // method to show the sign in form
        [HttpGet]
        public IActionResult Login() => View(new LoginViewModel());

        // method to check the username + password and sign the user in
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel m)
        {
            if (!ModelState.IsValid)
                return View(m);

            // the service checks the password hash, we never see it
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

        // method to show the register form
        [HttpGet]
        public IActionResult Register() => View(new RegisterViewModel());

        // method to create a new customer and sign them in
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel m)
        {
            if (!ModelState.IsValid)
                return View(m);

            var result = await _api.RegisterAsync(m);
            if (!result.Ok || result.Data == null)
            {
                // e.g. "Username is already taken." plus any field messages from the service
                ModelState.AddModelError("", result.Message);
                foreach (var detail in result.Details)
                    ModelState.AddModelError("", detail);
                return View(m);
            }

            await SignInAsync(result.Data);
            TempData["Success"] = $"Welcome, {result.Data.Name}! Your profile has been created.";
            return RedirectToAction("Index", "Home");
        }

        // method to sign out (POST only, see the form in _Layout)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            TempData["Success"] = "You have been signed out.";
            return RedirectToAction("Index", "Home");
        }

        // helper method to save who the user is in the login cookie
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
