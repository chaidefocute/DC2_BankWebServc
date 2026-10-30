using System.Security.Claims;
using BankMvc.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews()
    .AddJsonOptions(o => o.JsonSerializerOptions.IncludeFields = true);

builder.Services.AddSingleton<BankMvc.Services.BankApiClient>();

// login cookie: holds the user id, username and role after signing in
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Auth/Login";               // not signed in -> sign in page (then back to the page they wanted)
        o.AccessDeniedPath = "/Home/Status/403";   // signed in but wrong role -> access denied page
        o.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        o.SlidingExpiration = true;                // the 30 minutes restart while the user is active
        o.Cookie.HttpOnly = true;                  // javascript can't read the cookie

        // on every request, check the user hasn't been deactivated since they signed in
        // otherwise a deactivated user could keep using the site until the cookie runs out
        o.Events.OnValidatePrincipal = async ctx =>
        {
            if (!int.TryParse(ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return;

            var api = ctx.HttpContext.RequestServices.GetRequiredService<BankApiClient>();
            var user = await api.GetUserAsync(userId);

            // only sign out when the service says the user is inactive (if the service is down, the pages show that instead)
            if (user.Ok && user.Data?.IsActive == false)
            {
                ctx.RejectPrincipal();
                await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });

var app = builder.Build();

// resilience: never show the developer exception page / stack trace to the user, even in Development
// any unhandled exception -> friendly /Home/Error page (the real error is written to the log)
app.UseExceptionHandler("/Home/Error");

// 403 / 404 etc. with no page of their own -> friendly /Home/Status/{code} page
app.UseStatusCodePagesWithReExecute("/Home/Status/{0}");

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
