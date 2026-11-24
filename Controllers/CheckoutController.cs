
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using OnlineFoodOrderingSystem.Models;
using System.Data;
using System.Security.Claims;
using System.Text.Json;

namespace OnlineFoodOrderingSystem.Controllers
{
    [Authorize(Roles = "Customer")]
    public class CheckoutController : Controller
    {
        private const string CartSessionKey = "ShoppingCart";

        private readonly string _connectionString;

        public CheckoutController(IConfiguration configuration)
        {
            _connectionString =
                configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "DefaultConnection was not found.");
        }

        [HttpGet]
        public IActionResult Index()
        {
            List<CartItem> cart = GetCart();

            if (cart.Count == 0)
            {
                TempData["CartMessage"] = "Your cart is empty.";
                return RedirectToAction("Index", "Cart");
            }

            ViewBag.CartTotal = cart.Sum(item => item.SubTotal);

            return View(cart);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult PlaceOrder(string deliveryAddress)
        {
            if (string.IsNullOrWhiteSpace(deliveryAddress) ||
                deliveryAddress.Trim().Length > 500)
            {
                ModelState.AddModelError(
                    "deliveryAddress",
                    "Enter a valid delivery address (maximum 500 characters).");

                return ShowCheckout();
            }

            List<CartItem> cart = GetCart();

            if (cart.Count == 0)
            {
                TempData["CartMessage"] = "Your cart is empty.";
                return RedirectToAction("Index", "Cart");
            }

            string? userIdValue =
                User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(userIdValue, out int userId))
            {
                return Challenge();
            }

            using var connection = new SqlConnection(_connectionString);
            connection.Open();

            using var transaction =
                connection.BeginTransaction(IsolationLevel.Serializable);

            try
            {
                decimal total = 0m;

                // Recheck availability and price in the database.
                foreach (CartItem item in cart)
                {
                    if (item.FoodId <= 0 ||
                        item.Quantity < 1 ||
                        item.Quantity > 99)
                    {
                        throw new InvalidOperationException(
                            "The cart contains an invalid item.");
                    }

                    const string foodSql = @"
                        SELECT Price
                        FROM Foods WITH (UPDLOCK, HOLDLOCK)
                        WHERE FoodId = @FoodId
                          AND IsAvailable = 1;";

                    using var foodCommand =
                        new SqlCommand(foodSql, connection, transaction);

                    foodCommand.Parameters.Add(
                        "@FoodId", SqlDbType.Int).Value = item.FoodId;

                    object? result = foodCommand.ExecuteScalar();

                    if (result == null || result == DBNull.Value)
                    {
                        throw new InvalidOperationException(
                            $"{item.FoodName} is no longer available.");
                    }

                    decimal currentPrice = Convert.ToDecimal(result);

                    item.UnitPrice = currentPrice;
                    total += currentPrice * item.Quantity;
                }

                const string orderSql = @"
                    INSERT INTO Orders
                        (UserId, OrderDate, TotalAmount, Status, DeliveryAddress)
                    OUTPUT INSERTED.OrderId
                    VALUES
                        (@UserId, SYSDATETIME(), @TotalAmount,
                         @Status, @DeliveryAddress);";

                int orderId;

                using (var orderCommand =
                    new SqlCommand(orderSql, connection, transaction))
                {
                    orderCommand.Parameters.Add(
                        "@UserId", SqlDbType.Int).Value = userId;

                    var totalParameter = orderCommand.Parameters.Add(
                        "@TotalAmount", SqlDbType.Decimal);
                    totalParameter.Precision = 10;
                    totalParameter.Scale = 2;
                    totalParameter.Value = total;

                    orderCommand.Parameters.Add(
                        "@Status", SqlDbType.NVarChar, 30).Value = "Pending";

                    orderCommand.Parameters.Add(
                        "@DeliveryAddress", SqlDbType.NVarChar, 500).Value =
                        deliveryAddress.Trim();

                    orderId = Convert.ToInt32(
                        orderCommand.ExecuteScalar());
                }

                const string detailSql = @"
                    INSERT INTO OrderDetails
                        (OrderId, FoodId, Quantity, UnitPrice, SubTotal)
                    VALUES
                        (@OrderId, @FoodId, @Quantity, @UnitPrice, @SubTotal);";

                foreach (CartItem item in cart)
                {
                    using var detailCommand =
                        new SqlCommand(detailSql, connection, transaction);

                    detailCommand.Parameters.Add(
                        "@OrderId", SqlDbType.Int).Value = orderId;

                    detailCommand.Parameters.Add(
                        "@FoodId", SqlDbType.Int).Value = item.FoodId;

                    detailCommand.Parameters.Add(
                        "@Quantity", SqlDbType.Int).Value = item.Quantity;

                    var unitPriceParameter = detailCommand.Parameters.Add(
                        "@UnitPrice", SqlDbType.Decimal);
                    unitPriceParameter.Precision = 10;
                    unitPriceParameter.Scale = 2;
                    unitPriceParameter.Value = item.UnitPrice;

                    var subtotalParameter = detailCommand.Parameters.Add(
                        "@SubTotal", SqlDbType.Decimal);
                    subtotalParameter.Precision = 10;
                    subtotalParameter.Scale = 2;
                    subtotalParameter.Value = item.SubTotal;

                    detailCommand.ExecuteNonQuery();
                }

                transaction.Commit();

                // Clear the cart only after the database transaction succeeds.
                HttpContext.Session.Remove(CartSessionKey);

                return RedirectToAction(
                    nameof(Success), new { id = orderId });
            }
            catch (Exception ex) when (
                ex is SqlException ||
                ex is InvalidOperationException ||
                ex is OverflowException)
            {
                try
                {
                    transaction.Rollback();
                }
                catch (SqlException)
                {
                    // Preserve the original checkout error.
                }

                ModelState.AddModelError(
                    string.Empty,
                    ex is InvalidOperationException
                        ? ex.Message
                        : "We could not save your order. Please try again.");

                return ShowCheckout();
            }
        }

        [HttpGet]
        public IActionResult Success(int id)
        {
            if (id <= 0)
            {
                return BadRequest();
            }

            ViewBag.OrderId = id;
            return View();
        }

        private IActionResult ShowCheckout()
        {
            List<CartItem> cart = GetCart();

            if (cart.Count == 0)
            {
                TempData["CartMessage"] = "Your cart is empty.";
                return RedirectToAction("Index", "Cart");
            }

            ViewBag.CartTotal = cart.Sum(item => item.SubTotal);
            return View("Index", cart);
        }

        private List<CartItem> GetCart()
        {
            string? json =
                HttpContext.Session.GetString(CartSessionKey);

            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<CartItem>();
            }

            try
            {
                return JsonSerializer.Deserialize<List<CartItem>>(json)
                    ?? new List<CartItem>();
            }
            catch (JsonException)
            {
                return new List<CartItem>();
            }
        }
    }
}
