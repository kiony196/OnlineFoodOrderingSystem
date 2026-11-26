
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using OnlineFoodOrderingSystem.Models;

namespace OnlineFoodOrderingSystem.Controllers
{
    public class AccountController : Controller
    {
        private readonly string _connectionString;
        private readonly IPasswordHasher<User> _passwordHasher;

        public AccountController(
            IConfiguration configuration,
            IPasswordHasher<User> passwordHasher)
        {
            _connectionString =
                configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "Connection string 'DefaultConnection' was not found.");

            _passwordHasher = passwordHasher;
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Register()
        {
            return View(new User());
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(
            User model,
            string confirmPassword)
        {
            if (model.Password != confirmPassword)
            {
                ModelState.AddModelError(
                    "Password", "Passwords do not match.");
            }

            if (!ModelState.IsValid)
                return View(model);

            model.Email = model.Email.Trim();

            try
            {
                await using var connection =
                    new SqlConnection(_connectionString);

                await connection.OpenAsync();

                const string checkSql = @"
                    SELECT COUNT(1)
                    FROM Users
                    WHERE Email = @Email;";

                await using (var checkCommand =
                    new SqlCommand(checkSql, connection))
                {
                    checkCommand.Parameters.AddWithValue(
                        "@Email", model.Email);

                    int exists = Convert.ToInt32(
                        await checkCommand.ExecuteScalarAsync());

                    if (exists > 0)
                    {
                        ModelState.AddModelError(
                            "Email",
                            "An account with this email already exists.");

                        return View(model);
                    }
                }

                // Never accept a role from the registration form.
                model.Role = "Customer";

                string passwordHash =
                    _passwordHasher.HashPassword(
                        model, model.Password);

                const string insertSql = @"
                    INSERT INTO Users
                        (FullName, Email, PasswordHash,
                         Phone, Address, Role)
                    VALUES
                        (@FullName, @Email, @PasswordHash,
                         @Phone, @Address, @Role);";

                await using var insertCommand =
                    new SqlCommand(insertSql, connection);

                insertCommand.Parameters.AddWithValue(
                    "@FullName", model.FullName.Trim());

                insertCommand.Parameters.AddWithValue(
                    "@Email", model.Email);

                insertCommand.Parameters.AddWithValue(
                    "@PasswordHash", passwordHash);

                insertCommand.Parameters.AddWithValue(
                    "@Phone", (object?)model.Phone ?? DBNull.Value);

                insertCommand.Parameters.AddWithValue(
                    "@Address", (object?)model.Address ?? DBNull.Value);

                insertCommand.Parameters.AddWithValue(
                    "@Role", "Customer");

                await insertCommand.ExecuteNonQueryAsync();

                TempData["Success"] =
                    "Registration successful. Please log in.";

                return RedirectToAction(nameof(Login));
            }
            catch (SqlException ex) when (
                ex.Number == 2601 || ex.Number == 2627)
            {
                ModelState.AddModelError(
                    "Email",
                    "An account with this email already exists.");

                return View(model);
            }
            catch (SqlException)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "A database error occurred. Please try again.");

                return View(model);
            }
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            string email,
            string password,
            string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrEmpty(password))
            {
                ModelState.AddModelError(
                    string.Empty, "Email and password are required.");

                return View();
            }

            try
            {
                User? user = null;

                await using var connection =
                    new SqlConnection(_connectionString);

                await connection.OpenAsync();

                const string sql = @"
                    SELECT
                        UserId,
                        FullName,
                        Email,
                        PasswordHash,
                        Phone,
                        Address,
                        Role,
                        CreatedAt,
                        IsActive
                    FROM Users
                    WHERE Email = @Email;";

                await using var command =
                    new SqlCommand(sql, connection);

                command.Parameters.AddWithValue(
                    "@Email", email.Trim());

                await using var reader =
                    await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    user = new User
                    {
                        UserId = reader.GetInt32(0),
                        FullName = reader.GetString(1),
                        Email = reader.GetString(2),
                        PasswordHash = reader.GetString(3),
                        Phone = reader.IsDBNull(4)
                            ? null : reader.GetString(4),
                        Address = reader.IsDBNull(5)
                            ? null : reader.GetString(5),
                        Role = reader.GetString(6),
                        CreatedAt = reader.GetDateTime(7),
                        IsActive = reader.GetBoolean(8)
                    };
                }

                // Use the same message for unknown email and wrong password.
                if (user == null)
                {
                    ModelState.AddModelError(
                        string.Empty, "Invalid email or password.");

                    return View();
                }

                var verification =
                    _passwordHasher.VerifyHashedPassword(
                        user, user.PasswordHash, password);

                if (verification == PasswordVerificationResult.Failed)
                {
                    ModelState.AddModelError(
                        string.Empty, "Invalid email or password.");

                    return View();
                }

                // Block inactive customer accounts.
                if (user.Role == "Customer" && !user.IsActive)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        "Your account has been deactivated. " +
                        "Please contact the administrator.");

                    return View();
                }

                // Upgrade older password hashes when necessary.
                if (verification ==
                    PasswordVerificationResult.SuccessRehashNeeded)
                {
                    string updatedHash =
                        _passwordHasher.HashPassword(user, password);

                    await using var updateConnection =
                        new SqlConnection(_connectionString);

                    await updateConnection.OpenAsync();

                    const string updateSql = @"
                        UPDATE Users
                        SET PasswordHash = @Hash
                        WHERE UserId = @UserId;";

                    await using var updateCommand =
                        new SqlCommand(updateSql, updateConnection);

                    updateCommand.Parameters.AddWithValue(
                        "@Hash", updatedHash);

                    updateCommand.Parameters.AddWithValue(
                        "@UserId", user.UserId);

                    await updateCommand.ExecuteNonQueryAsync();
                }

                var claims = new List<Claim>
                {
                    new Claim(
                        ClaimTypes.NameIdentifier,
                        user.UserId.ToString()),

                    new Claim(ClaimTypes.Name, user.FullName),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Role, user.Role)
                };

                var identity = new ClaimsIdentity(
                    claims,
                    CookieAuthenticationDefaults.AuthenticationScheme);

                var principal = new ClaimsPrincipal(identity);

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    principal,
                    new AuthenticationProperties
                    {
                        IsPersistent = true,
                        AllowRefresh = true,
                        ExpiresUtc = DateTimeOffset.UtcNow
                            .AddMinutes(30)
                    });

                if (!string.IsNullOrWhiteSpace(returnUrl) &&
                    Url.IsLocalUrl(returnUrl))
                {
                    return LocalRedirect(returnUrl);
                }

                if (user.Role == "Admin")
                    return RedirectToAction("Index", "Admin");

                if (user.Role == "Customer")
                    return RedirectToAction("Index", "User");

                await HttpContext.SignOutAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme);

                return RedirectToAction("Login");
            }
            catch (SqlException)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Unable to access the database. Please try again.");

                return View();
            }
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);

            return RedirectToAction("Index", "Home");
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
