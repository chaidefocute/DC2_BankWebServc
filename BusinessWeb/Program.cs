using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews()
    .AddJsonOptions(o => o.JsonSerializerOptions.IncludeFields = true);

builder.Services.AddSingleton<BankMvc.Services.BankApiClient>();

// login cookie: holds the user id, username and role after signing in
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Auth/Login";               // not signed in -> sign in page
        o.AccessDeniedPath = "/Home/Status/403";   // signed in but wrong role -> access denied page
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
