
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using OnlineFoodOrderingSystem.Models;
using System.Data;

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

        private string? ConnectionString =>
            _configuration.GetConnectionString("DefaultConnection");

        // ADMIN DASHBOARD
        public IActionResult Index()
        {
            if (string.IsNullOrWhiteSpace(ConnectionString))
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
                    new SqlConnection(ConnectionString);

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

        // CUSTOMER ACCOUNTS - LIST AND SEARCH
        public async Task<IActionResult> Users(string? searchTerm)
        {
            var users = new List<User>();

            if (string.IsNullOrWhiteSpace(ConnectionString))
            {
                _logger.LogError(
                    "DefaultConnection is missing from appsettings.json.");

                TempData["Error"] =
                    "Database connection is not configured.";

                return View(users);
            }

            try
            {
                await using var connection =
                    new SqlConnection(ConnectionString);

                await connection.OpenAsync();

                const string sql = @"
                    SELECT
                        UserId,
                        FullName,
                        Email,
                        Phone,
                        Address,
                        Role,
                        CreatedAt
                    FROM Users
                    WHERE Role = 'Customer'
                    AND (
                        @SearchTerm = ''
                        OR FullName LIKE @SearchPattern
                        OR Email LIKE @SearchPattern
                        OR Phone LIKE @SearchPattern
                    )
                    ORDER BY CreatedAt DESC";

                await using var command =
                    new SqlCommand(sql, connection);

                string term = searchTerm?.Trim() ?? "";

                command.Parameters.AddWithValue(
                    "@SearchTerm", term);

                command.Parameters.AddWithValue(
                    "@SearchPattern", "%" + term + "%");

                await using var reader =
                    await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    users.Add(new User
                    {
                        UserId = reader.GetInt32(0),
                        FullName = reader.GetString(1),
                        Email = reader.GetString(2),
                        Phone = reader.IsDBNull(3)
                            ? null
                            : reader.GetString(3),
                        Address = reader.IsDBNull(4)
                            ? null
                            : reader.GetString(4),
                        Role = reader.GetString(5),
                        CreatedAt = reader.GetDateTime(6)
                    });
                }

                ViewBag.SearchTerm = term;
            }
            catch (SqlException ex)
            {
                _logger.LogError(
                    ex, "Failed to load customer accounts.");

                TempData["Error"] =
                    "Could not load customer accounts. " +
                    "Check the database connection.";
            }

            return View(users);
        }

        // CUSTOMER DETAILS AND ORDER HISTORY
        public async Task<IActionResult> Details(int id)
        {
            if (string.IsNullOrWhiteSpace(ConnectionString))
            {
                _logger.LogError(
                    "DefaultConnection is missing from appsettings.json.");

                TempData["Error"] =
                    "Database connection is not configured.";

                return RedirectToAction(nameof(Users));
            }

            var model = new CustomerDetailsViewModel();

            try
            {
                await using var connection =
                    new SqlConnection(ConnectionString);

                await connection.OpenAsync();

                // 1. GET CUSTOMER INFORMATION
                const string customerSql = @"
                    SELECT
                        UserId,
                        FullName,
                        Email,
                        Phone,
                        Address,
                        Role,
                        CreatedAt
                    FROM Users
                    WHERE UserId = @UserId
                    AND Role = 'Customer'";

                await using (var customerCommand =
                    new SqlCommand(customerSql, connection))
                {
                    customerCommand.Parameters.Add(
                        "@UserId", SqlDbType.Int).Value = id;

                    await using var reader =
                        await customerCommand.ExecuteReaderAsync();

                    if (!await reader.ReadAsync())
                    {
                        TempData["Error"] =
                            "Customer account was not found.";

                        return RedirectToAction(nameof(Users));
                    }

                    model.Customer = new User
                    {
                        UserId = reader.GetInt32(0),
                        FullName = reader.GetString(1),
                        Email = reader.GetString(2),
                        Phone = reader.IsDBNull(3)
                            ? null
                            : reader.GetString(3),
                        Address = reader.IsDBNull(4)
                            ? null
                            : reader.GetString(4),
                        Role = reader.GetString(5),
                        CreatedAt = reader.GetDateTime(6)
                    };
                }

                // 2. GET CUSTOMER ORDERS
                const string ordersSql = @"
                    SELECT
                        OrderId,
                        OrderDate,
                        TotalAmount,
                        Status,
                        DeliveryAddress
                    FROM Orders
                    WHERE UserId = @UserId
                    ORDER BY OrderDate DESC";

                var orders = new List<OrderViewModel>();

                await using (var ordersCommand =
                    new SqlCommand(ordersSql, connection))
                {
                    ordersCommand.Parameters.Add(
                        "@UserId", SqlDbType.Int).Value = id;

                    await using var reader =
                        await ordersCommand.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        orders.Add(new OrderViewModel
                        {
                            OrderId = reader.GetInt32(0),
                            OrderDate = reader.GetDateTime(1),
                            TotalAmount = reader.GetDecimal(2),
                            Status = reader.GetString(3),
                            DeliveryAddress = reader.GetString(4),
                            CustomerName = model.Customer.FullName,
                            CustomerEmail = model.Customer.Email
                        });
                    }
                }

                // 3. GET PRODUCTS FOR EACH ORDER
                const string itemsSql = @"
                    SELECT
                        od.OrderId,
                        od.FoodId,
                        f.FoodName,
                        od.Quantity,
                        od.UnitPrice,
                        od.SubTotal
                    FROM OrderDetails od
                    INNER JOIN Foods f
                        ON od.FoodId = f.FoodId
                    INNER JOIN Orders o
                        ON od.OrderId = o.OrderId
                    WHERE o.UserId = @UserId
                    ORDER BY od.OrderId";

                await using (var itemsCommand =
                    new SqlCommand(itemsSql, connection))
                {
                    itemsCommand.Parameters.Add(
                        "@UserId", SqlDbType.Int).Value = id;

                    await using var reader =
                        await itemsCommand.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        int orderId = reader.GetInt32(0);

                        var order = orders.FirstOrDefault(
                            o => o.OrderId == orderId);

                        if (order != null)
                        {
                            order.Items.Add(new OrderItemViewModel
                            {
                                FoodId = reader.GetInt32(1),
                                FoodName = reader.GetString(2),
                                Quantity = reader.GetInt32(3),
                                UnitPrice = reader.GetDecimal(4),
                                SubTotal = reader.GetDecimal(5)
                            });
                        }
                    }
                }

                model.Orders = orders;
                model.TotalOrders = orders.Count;

                model.TotalSpent = orders
                    .Where(o => o.Status != "Cancelled")
                    .Sum(o => o.TotalAmount);
            }
            catch (SqlException ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to load details for customer {UserId}.",
                    id);

                TempData["Error"] =
                    "Could not load customer details. " +
                    "Check the database connection.";

                return RedirectToAction(nameof(Users));
            }

            return View("UserDetails", model);
        }
    }
}
