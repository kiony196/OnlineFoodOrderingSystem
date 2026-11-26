
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
                        CreatedAt,
                        IsActive
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
                        CreatedAt = reader.GetDateTime(6),
                        IsActive = reader.GetBoolean(7)
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
                        CreatedAt,
                        IsActive
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
                        CreatedAt = reader.GetDateTime(6),
                        IsActive = reader.GetBoolean(7)
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

        // EDIT CUSTOMER - GET
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            if (string.IsNullOrWhiteSpace(ConnectionString))
            {
                TempData["Error"] =
                    "Database connection is not configured.";

                return RedirectToAction(nameof(Users));
            }

            try
            {
                await using var connection =
                    new SqlConnection(ConnectionString);

                await connection.OpenAsync();

                const string sql = @"
                    SELECT UserId, FullName, Email, Phone, Address
                    FROM Users
                    WHERE UserId = @UserId
                    AND Role = 'Customer'";

                await using var command =
                    new SqlCommand(sql, connection);

                command.Parameters.Add(
                    "@UserId", SqlDbType.Int).Value = id;

                await using var reader =
                    await command.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    TempData["Error"] =
                        "Customer account was not found.";

                    return RedirectToAction(nameof(Users));
                }

                var model = new EditCustomerViewModel
                {
                    UserId = reader.GetInt32(0),
                    FullName = reader.GetString(1),
                    Email = reader.GetString(2),
                    Phone = reader.IsDBNull(3)
                        ? null
                        : reader.GetString(3),
                    Address = reader.IsDBNull(4)
                        ? null
                        : reader.GetString(4)
                };

                return View("EditCustomer", model);
            }
            catch (SqlException ex)
            {
                _logger.LogError(
                    ex, "Failed to load customer {UserId} for editing.", id);

                TempData["Error"] =
                    "Could not load customer information.";

                return RedirectToAction(nameof(Users));
            }
        }

        // EDIT CUSTOMER - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditCustomerViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("EditCustomer", model);
            }

            if (string.IsNullOrWhiteSpace(ConnectionString))
            {
                TempData["Error"] =
                    "Database connection is not configured.";

                return View("EditCustomer", model);
            }

            model.FullName = model.FullName.Trim();
            model.Email = model.Email.Trim();
            model.Phone = string.IsNullOrWhiteSpace(model.Phone)
                ? null
                : model.Phone.Trim();
            model.Address = string.IsNullOrWhiteSpace(model.Address)
                ? null
                : model.Address.Trim();

            try
            {
                await using var connection =
                    new SqlConnection(ConnectionString);

                await connection.OpenAsync();

                // CHECK THAT THE CUSTOMER EXISTS
                const string customerCheckSql = @"
                    SELECT COUNT(1)
                    FROM Users
                    WHERE UserId = @UserId
                    AND Role = 'Customer'";

                await using (var checkCommand =
                    new SqlCommand(customerCheckSql, connection))
                {
                    checkCommand.Parameters.Add(
                        "@UserId", SqlDbType.Int).Value = model.UserId;

                    int customerExists = Convert.ToInt32(
                        await checkCommand.ExecuteScalarAsync());

                    if (customerExists == 0)
                    {
                        TempData["Error"] =
                            "Customer account was not found.";

                        return RedirectToAction(nameof(Users));
                    }
                }

                // CHECK FOR DUPLICATE EMAIL
                const string emailCheckSql = @"
                    SELECT COUNT(1)
                    FROM Users
                    WHERE Email = @Email
                    AND UserId <> @UserId";

                await using (var emailCommand =
                    new SqlCommand(emailCheckSql, connection))
                {
                    emailCommand.Parameters.Add(
                        "@Email", SqlDbType.NVarChar, 150).Value =
                        model.Email;

                    emailCommand.Parameters.Add(
                        "@UserId", SqlDbType.Int).Value = model.UserId;

                    int emailExists = Convert.ToInt32(
                        await emailCommand.ExecuteScalarAsync());

                    if (emailExists > 0)
                    {
                        ModelState.AddModelError(
                            nameof(model.Email),
                            "This email address is already registered.");

                        return View("EditCustomer", model);
                    }
                }

                // UPDATE CUSTOMER INFORMATION
                // PasswordHash, Role, UserId and IsActive are not modified.
                const string updateSql = @"
                    UPDATE Users
                    SET
                        FullName = @FullName,
                        Email = @Email,
                        Phone = @Phone,
                        Address = @Address
                    WHERE UserId = @UserId
                    AND Role = 'Customer'";

                await using (var updateCommand =
                    new SqlCommand(updateSql, connection))
                {
                    updateCommand.Parameters.Add(
                        "@FullName", SqlDbType.NVarChar, 100).Value =
                        model.FullName;

                    updateCommand.Parameters.Add(
                        "@Email", SqlDbType.NVarChar, 150).Value =
                        model.Email;

                    updateCommand.Parameters.Add(
                        "@Phone", SqlDbType.NVarChar, 20).Value =
                        (object?)model.Phone ?? DBNull.Value;

                    updateCommand.Parameters.Add(
                        "@Address", SqlDbType.NVarChar, 255).Value =
                        (object?)model.Address ?? DBNull.Value;

                    updateCommand.Parameters.Add(
                        "@UserId", SqlDbType.Int).Value =
                        model.UserId;

                    int rowsAffected =
                        await updateCommand.ExecuteNonQueryAsync();

                    if (rowsAffected == 0)
                    {
                        TempData["Error"] =
                            "Customer information could not be updated.";

                        return RedirectToAction(nameof(Users));
                    }
                }

                TempData["Success"] =
                    "Customer information updated successfully.";

                return RedirectToAction(
                    nameof(Details),
                    new { id = model.UserId });
            }
            catch (SqlException ex) when (
                ex.Number == 2601 || ex.Number == 2627)
            {
                ModelState.AddModelError(
                    nameof(model.Email),
                    "This email address is already registered.");

                return View("EditCustomer", model);
            }
            catch (SqlException ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to update customer {UserId}.",
                    model.UserId);

                TempData["Error"] =
                    "An error occurred while updating customer information.";

                return View("EditCustomer", model);
            }
        }

        // ACTIVATE / DEACTIVATE CUSTOMER ACCOUNT
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleAccountStatus(int id)
        {
            if (string.IsNullOrWhiteSpace(ConnectionString))
            {
                TempData["Error"] =
                    "Database connection is not configured.";

                return RedirectToAction(nameof(Users));
            }

            try
            {
                await using var connection =
                    new SqlConnection(ConnectionString);

                await connection.OpenAsync();

                const string sql = @"
                    UPDATE Users
                    SET IsActive =
                        CASE
                            WHEN IsActive = 1 THEN 0
                            ELSE 1
                        END
                    OUTPUT INSERTED.IsActive
                    WHERE UserId = @UserId
                    AND Role = 'Customer';";

                await using var command =
                    new SqlCommand(sql, connection);

                command.Parameters.Add(
                    "@UserId", SqlDbType.Int).Value = id;

                object? result =
                    await command.ExecuteScalarAsync();

                if (result == null || result == DBNull.Value)
                {
                    TempData["Error"] =
                        "Customer account was not found.";

                    return RedirectToAction(nameof(Users));
                }

                bool isActive = Convert.ToBoolean(result);

                TempData["Success"] = isActive
                    ? "Customer account activated successfully."
                    : "Customer account deactivated successfully.";

                return RedirectToAction(nameof(Users));
            }
            catch (SqlException ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to change account status for customer {UserId}.",
                    id);

                TempData["Error"] =
                    "Could not change the customer account status.";

                return RedirectToAction(nameof(Users));
            }
        }
    }
}
