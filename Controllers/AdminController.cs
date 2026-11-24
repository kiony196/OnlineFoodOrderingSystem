
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace OnlineFoodOrderingSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            IConfiguration configuration,
            ILogger<AdminController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public IActionResult Index()
        {
            string? connectionString =
                _configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                _logger.LogError(
                    "DefaultConnection is missing from appsettings.json.");

                TempData["Error"] =
                    "Database connection is not configured.";

                return View();
            }

            try
            {
                using var connection =
                    new SqlConnection(connectionString);

                connection.Open();

                using var command = new SqlCommand(@"
                    SELECT
                        (SELECT COUNT(*) FROM Users),
                        (SELECT COUNT(*) FROM Foods),
                        (SELECT COUNT(*) FROM Categories),
                        (SELECT COUNT(*) FROM Orders),
                        (SELECT COALESCE(
                            SUM(TotalAmount), 0)
                         FROM Orders
                         WHERE Status <> 'Cancelled');
                ", connection);

                using var reader = command.ExecuteReader();

                if (reader.Read())
                {
                    ViewBag.TotalUsers = reader.GetInt32(0);
                    ViewBag.TotalFoods = reader.GetInt32(1);
                    ViewBag.TotalCategories = reader.GetInt32(2);
                    ViewBag.TotalOrders = reader.GetInt32(3);
                    ViewBag.TotalSales = reader.GetDecimal(4);
                }
            }
            catch (SqlException ex)
            {
                _logger.LogError(
                    ex, "Failed to load admin dashboard.");

                TempData["Error"] =
                    "Could not load dashboard statistics. " +
                    "Check the database connection.";
            }

            return View();
        }
    }
}
