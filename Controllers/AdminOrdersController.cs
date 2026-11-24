
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Mvc.Rendering;
using OnlineFoodOrderingSystem.Models;
using System.Data;

namespace OnlineFoodOrderingSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminOrdersController : Controller
    {
        private readonly string _connectionString;

        private static readonly string[] AllowedStatuses =
        {
            "Pending",
            "Confirmed",
            "Preparing",
            "Out for Delivery",
            "Delivered",
            "Cancelled"
        };

        public AdminOrdersController(IConfiguration configuration)
        {
            _connectionString =
                configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "DefaultConnection was not found.");
        }

        // GET: /AdminOrders
        [HttpGet]
        public IActionResult Index()
        {
            var orders = new List<OrderViewModel>();

            const string sql = @"
                SELECT
                    o.OrderId,
                    o.OrderDate,
                    o.TotalAmount,
                    o.Status,
                    o.DeliveryAddress,
                    u.FullName,
                    u.Email
                FROM Orders AS o
                INNER JOIN Users AS u ON o.UserId = u.UserId
                ORDER BY o.OrderDate DESC, o.OrderId DESC;";

            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand(sql, connection);

            connection.Open();

            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                orders.Add(new OrderViewModel
                {
                    OrderId = reader.GetInt32(0),
                    OrderDate = reader.GetDateTime(1),
                    TotalAmount = reader.GetDecimal(2),
                    Status = reader.GetString(3),
                    DeliveryAddress = reader.GetString(4),
                    CustomerName = reader.GetString(5),
                    CustomerEmail = reader.GetString(6)
                });
            }

            ViewBag.AllStatuses = AllowedStatuses;

            return View(orders);
        }

        // POST: /AdminOrders/UpdateStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateStatus(int orderId, string status)
        {
            if (orderId <= 0 ||
                string.IsNullOrWhiteSpace(status) ||
                !AllowedStatuses.Contains(status))
            {
                TempData["AdminOrderMessage"] =
                    "Invalid order or status.";

                return RedirectToAction(nameof(Index));
            }

            const string sql = @"
                UPDATE Orders
                SET Status = @Status
                WHERE OrderId = @OrderId;";

            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand(sql, connection);

            command.Parameters.Add("@Status", SqlDbType.NVarChar, 30)
                .Value = status;

            command.Parameters.Add("@OrderId", SqlDbType.Int)
                .Value = orderId;

            connection.Open();

            int rowsAffected = command.ExecuteNonQuery();

            TempData["AdminOrderMessage"] = rowsAffected > 0
                ? $"Order #{orderId} status updated to {status}."
                : "Order not found.";

            return RedirectToAction(nameof(Index));
        }
    }
}
