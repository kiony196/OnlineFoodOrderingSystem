
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using OnlineFoodOrderingSystem.Models;
using System.Data;
using System.Text.Json;

namespace OnlineFoodOrderingSystem.Controllers
{
    public class CartController : Controller
    {
        private const string CartSessionKey = "ShoppingCart";

        private readonly string _connectionString;

        public CartController(IConfiguration configuration)
        {
            _connectionString =
                configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "DefaultConnection was not found.");
        }

        // Read the cart from the current browser session.
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

        // Save the cart in the current browser session.
        private void SaveCart(List<CartItem> cart)
        {
            HttpContext.Session.SetString(
                CartSessionKey,
                JsonSerializer.Serialize(cart));
        }

        // GET: /Cart
        public IActionResult Index()
        {
            List<CartItem> cart = GetCart();

            ViewBag.CartTotal = cart.Sum(item => item.SubTotal);
            ViewBag.CartCount = cart.Sum(item => item.Quantity);

            return View(cart);
        }

        // POST: /Cart/Add
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Add(int foodId, int quantity = 1)
        {
            if (foodId <= 0 || quantity < 1 || quantity > 99)
            {
                TempData["CartMessage"] =
                    "Invalid food item or quantity.";

                return RedirectToAction("Index", "Foods");
            }

            CartItem food;

            // Always obtain the food details and price from the database.
            using (var connection =
                   new SqlConnection(_connectionString))
            {
                connection.Open();

                const string sql = @"
                    SELECT FoodId, FoodName, Price, ImageUrl
                    FROM Foods
                    WHERE FoodId = @FoodId
                      AND IsAvailable = 1;";

                using var command = new SqlCommand(sql, connection);

                command.Parameters.Add("@FoodId", SqlDbType.Int).Value =
                    foodId;

                using var reader = command.ExecuteReader();

                if (!reader.Read())
                {
                    TempData["CartMessage"] =
                        "This food is unavailable.";

                    return RedirectToAction("Index", "Foods");
                }

                food = new CartItem
                {
                    FoodId = reader.GetInt32(0),
                    FoodName = reader.GetString(1),
                    UnitPrice = reader.GetDecimal(2),
                    ImageUrl = reader.IsDBNull(3)
                        ? null
                        : reader.GetString(3),
                    Quantity = quantity
                };
            }

            List<CartItem> cart = GetCart();

            CartItem? existingItem =
                cart.FirstOrDefault(item => item.FoodId == foodId);

            if (existingItem != null)
            {
                if (existingItem.Quantity + quantity > 99)
                {
                    TempData["CartMessage"] =
                        "The maximum quantity per dish is 99.";

                    return RedirectToAction(nameof(Index));
                }

                existingItem.Quantity += quantity;

                // Refresh product information from the database.
                existingItem.FoodName = food.FoodName;
                existingItem.UnitPrice = food.UnitPrice;
                existingItem.ImageUrl = food.ImageUrl;
            }
            else
            {
                cart.Add(food);
            }

            SaveCart(cart);

            TempData["CartMessage"] =
                $"{food.FoodName} added to your cart.";

            return RedirectToAction(nameof(Index));
        }

        // POST: /Cart/Update
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Update(int foodId, int quantity)
        {
            List<CartItem> cart = GetCart();

            CartItem? item =
                cart.FirstOrDefault(x => x.FoodId == foodId);

            if (item == null)
            {
                return RedirectToAction(nameof(Index));
            }

            if (quantity <= 0)
            {
                cart.Remove(item);
            }
            else if (quantity <= 99)
            {
                item.Quantity = quantity;
            }
            else
            {
                TempData["CartMessage"] =
                    "The maximum quantity per dish is 99.";
            }

            SaveCart(cart);

            return RedirectToAction(nameof(Index));
        }

        // POST: /Cart/Remove
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Remove(int foodId)
        {
            List<CartItem> cart = GetCart();

            cart.RemoveAll(item => item.FoodId == foodId);

            SaveCart(cart);

            TempData["CartMessage"] =
                "Item removed from your cart.";

            return RedirectToAction(nameof(Index));
        }

        // POST: /Cart/Clear
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Clear()
        {
            SaveCart(new List<CartItem>());

            TempData["CartMessage"] =
                "Your cart has been cleared.";

            return RedirectToAction(nameof(Index));
        }
    }
}
