
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using OnlineFoodOrderingSystem.Models;

var builder = WebApplication.CreateBuilder(args);

// =============================================
// 1. DATABASE HELPER
// =============================================
builder.Services.AddScoped<DatabaseHelper>();

// =============================================
// 2. MVC
// =============================================
builder.Services.AddControllersWithViews();

// =============================================
// 3. PASSWORD HASHING
// =============================================
builder.Services.AddScoped<
    IPasswordHasher<User>,
    PasswordHasher<User>>();

// =============================================
// 4. COOKIE AUTHENTICATION
// =============================================
builder.Services
    .AddAuthentication(
        CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";

        options.Cookie.Name = "OnlineFoodOrdering.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy =
            CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;

        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = true;
    });

// =============================================
// 5. AUTHORIZATION
// =============================================
builder.Services.AddAuthorization();

// =============================================
// 6. SESSION - SHOPPING CART
// =============================================
builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);

    options.Cookie.Name = "OnlineFoodOrdering.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// =============================================
// 7. BUILD APPLICATION
// =============================================
var app = builder.Build();

// =============================================
// 8. ERROR HANDLING
// =============================================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// =============================================
// 9. HTTP PIPELINE
// =============================================
app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

// Session must be before controllers access the cart.
app.UseSession();

// Authentication must be before authorization.
app.UseAuthentication();

app.UseAuthorization();

// =============================================
// 10. DEFAULT ROUTE
// =============================================
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// =============================================
// 11. START APPLICATION
// =============================================
app.Run();
