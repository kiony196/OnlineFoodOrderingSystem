
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using OnlineFoodOrderingSystem.Models;
using System.Data;
using System.Security.Claims;

namespace OnlineFoodOrderingSystem.Controllers
{
    [Authorize(Roles = "Customer")]
    public class OrdersController : Controller
    {
        private readonly string _connectionString;

        public OrdersController(IConfiguration configuration)
        {
            _connectionString =
                configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "DefaultConnection was not found.");
        }

        [HttpGet]
        public IActionResult Index()
        {
            if (!int.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out int userId))
            {
                return Challenge();
            }

            var orders = new List<OrderViewModel>();

            const string sql = @"
                SELECT OrderId, OrderDate, TotalAmount,
                       Status, DeliveryAddress
                FROM Orders
                WHERE UserId = @UserId
                ORDER BY OrderDate DESC, OrderId DESC;";

            using var connection = new SqlConnection(_connectionString);
            using var command = new SqlCommand(sql, connection);

            command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;

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
                    DeliveryAddress = reader.GetString(4)
                });
            }

            return View(orders);
        }

        [HttpGet]
        public IActionResult Details(int id)
        {
            if (id <= 0)
            {
                return BadRequest();
            }

            if (!int.TryParse(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                out int userId))
            {
                return Challenge();
            }

            OrderViewModel? order = null;

            const string orderSql = @"
                SELECT OrderId, OrderDate, TotalAmount,
                       Status, DeliveryAddress
                FROM Orders
                WHERE OrderId = @OrderId
                  AND UserId = @UserId;";

            using var connection = new SqlConnection(_connectionString);

            using (var command = new SqlCommand(orderSql, connection))
            {
                command.Parameters.Add("@OrderId", SqlDbType.Int).Value = id;
                command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;

                connection.Open();

                using var reader = command.ExecuteReader();

                if (reader.Read())
                {
                    order = new OrderViewModel
                    {
                        OrderId = reader.GetInt32(0),
                        OrderDate = reader.GetDateTime(1),
                        TotalAmount = reader.GetDecimal(2),
                        Status = reader.GetString(3),
                        DeliveryAddress = reader.GetString(4)
                    };
                }
            }

            // Do not reveal orders belonging to other customers.
            if (order == null)
            {
                return NotFound();
            }

            const string itemsSql = @"
                SELECT od.FoodId, f.FoodName, od.Quantity,
                       od.UnitPrice, od.SubTotal
                FROM OrderDetails od
                INNER JOIN Foods f ON od.FoodId = f.FoodId
                WHERE od.OrderId = @OrderId
                ORDER BY f.FoodName;";

            using (var command = new SqlCommand(itemsSql, connection))
            {
                command.Parameters.Add("@OrderId", SqlDbType.Int).Value = id;

                using var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    order.Items.Add(new OrderItemViewModel
                    {
                        FoodId = reader.GetInt32(0),
                        FoodName = reader.GetString(1),
                        Quantity = reader.GetInt32(2),
                        UnitPrice = reader.GetDecimal(3),
                        SubTotal = reader.GetDecimal(4)
                    });
                }
            }

            return View(order);
        }
    }
}
