
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using OnlineFoodOrderingSystem.Models;
using System.Data;

namespace OnlineFoodOrderingSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminFoodsController : Controller
    {
        private readonly string _connectionString;

        public AdminFoodsController(IConfiguration configuration)
        {
            _connectionString =
                configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "DefaultConnection was not found.");
        }

        // GET: AdminFoods
        public IActionResult Index()
        {
            var foods = new List<Food>();

            using (SqlConnection connection =
                   new SqlConnection(_connectionString))
            {
                connection.Open();

                string sql = @"
                    SELECT
                        f.FoodId,
                        f.CategoryId,
                        f.FoodName,
                        f.Description,
                        f.Price,
                        f.ImageUrl,
                        f.IsAvailable,
                        c.CategoryName
                    FROM Foods f
                    INNER JOIN Categories c
                        ON f.CategoryId = c.CategoryId
                    ORDER BY f.FoodId DESC;";

                using (SqlCommand command =
                       new SqlCommand(sql, connection))
                using (SqlDataReader reader =
                       command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        foods.Add(new Food
                        {
                            FoodId = reader.GetInt32(0),
                            CategoryId = reader.GetInt32(1),
                            FoodName = reader.GetString(2),
                            Description = reader.IsDBNull(3)
                                ? null : reader.GetString(3),
                            Price = reader.GetDecimal(4),
                            ImageUrl = reader.IsDBNull(5)
                                ? null : reader.GetString(5),
                            IsAvailable = reader.GetBoolean(6),
                            CategoryName = reader.GetString(7)
                        });
                    }
                }
            }

            return View(foods);
        }

        // GET: AdminFoods/Create
        [HttpGet]
        public IActionResult Create()
        {
            LoadCategories();

            return View(new Food
            {
                IsAvailable = true
            });
        }

        // POST: AdminFoods/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Food food)
        {
            if (!ModelState.IsValid)
            {
                LoadCategories();
                return View(food);
            }

            using (SqlConnection connection =
                   new SqlConnection(_connectionString))
            {
                connection.Open();

                string sql = @"
                    INSERT INTO Foods
                    (
                        CategoryId,
                        FoodName,
                        Description,
                        Price,
                        ImageUrl,
                        IsAvailable
                    )
                    VALUES
                    (
                        @CategoryId,
                        @FoodName,
                        @Description,
                        @Price,
                        @ImageUrl,
                        @IsAvailable
                    );";

                using (SqlCommand command =
                       new SqlCommand(sql, connection))
                {
                    command.Parameters.Add(
                        "@CategoryId", SqlDbType.Int)
                        .Value = food.CategoryId;

                    command.Parameters.Add(
                        "@FoodName", SqlDbType.NVarChar, 150)
                        .Value = food.FoodName;

                    command.Parameters.Add(
                        "@Description", SqlDbType.NVarChar, 500)
                        .Value = (object?)food.Description
                            ?? DBNull.Value;

                    var priceParameter = command.Parameters.Add(
                        "@Price", SqlDbType.Decimal);

                    priceParameter.Precision = 10;
                    priceParameter.Scale = 2;
                    priceParameter.Value = food.Price;

                    command.Parameters.Add(
                        "@ImageUrl", SqlDbType.NVarChar, 255)
                        .Value = (object?)food.ImageUrl
                            ?? DBNull.Value;

                    command.Parameters.Add(
                        "@IsAvailable", SqlDbType.Bit)
                        .Value = food.IsAvailable;

                    command.ExecuteNonQuery();
                }
            }

            TempData["SuccessMessage"] =
                "Food item added successfully!";

            return RedirectToAction(nameof(Index));
        }

        // GET: AdminFoods/Edit/5
        [HttpGet]
        public IActionResult Edit(int id)
        {
            Food? food = GetFoodById(id);

            if (food == null)
                return NotFound();

            LoadCategories();

            return View(food);
        }

        // POST: AdminFoods/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(Food food)
        {
            if (!ModelState.IsValid)
            {
                LoadCategories();
                return View(food);
            }

            using (SqlConnection connection =
                   new SqlConnection(_connectionString))
            {
                connection.Open();

                string sql = @"
                    UPDATE Foods
                    SET
                        CategoryId = @CategoryId,
                        FoodName = @FoodName,
                        Description = @Description,
                        Price = @Price,
                        ImageUrl = @ImageUrl,
                        IsAvailable = @IsAvailable
                    WHERE FoodId = @FoodId;";

                using (SqlCommand command =
                       new SqlCommand(sql, connection))
                {
                    command.Parameters.Add(
                        "@FoodId", SqlDbType.Int).Value =
                        food.FoodId;

                    command.Parameters.Add(
                        "@CategoryId", SqlDbType.Int).Value =
                        food.CategoryId;

                    command.Parameters.Add(
                        "@FoodName", SqlDbType.NVarChar, 150).Value =
                        food.FoodName;

                    command.Parameters.Add(
                        "@Description", SqlDbType.NVarChar, 500).Value =
                        (object?)food.Description ?? DBNull.Value;

                    var priceParameter = command.Parameters.Add(
                        "@Price", SqlDbType.Decimal);

                    priceParameter.Precision = 10;
                    priceParameter.Scale = 2;
                    priceParameter.Value = food.Price;

                    command.Parameters.Add(
                        "@ImageUrl", SqlDbType.NVarChar, 255).Value =
                        (object?)food.ImageUrl ?? DBNull.Value;

                    command.Parameters.Add(
                        "@IsAvailable", SqlDbType.Bit).Value =
                        food.IsAvailable;

                    int rowsAffected = command.ExecuteNonQuery();

                    if (rowsAffected == 0)
                        return NotFound();
                }
            }

            TempData["SuccessMessage"] =
                "Food item updated successfully!";

            return RedirectToAction(nameof(Index));
        }

        // GET: AdminFoods/Details/5
        [HttpGet]
        public IActionResult Details(int id)
        {
            Food? food = GetFoodById(id);

            if (food == null)
                return NotFound();

            return View(food);
        }

        // GET: AdminFoods/Delete/5
        [HttpGet]
        public IActionResult Delete(int id)
        {
            Food? food = GetFoodById(id);

            if (food == null)
                return NotFound();

            return View(food);
        }

        // POST: AdminFoods/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            using (SqlConnection connection =
                   new SqlConnection(_connectionString))
            {
                connection.Open();

                // Check whether this food is used in an order.
                string checkSql = @"
                    SELECT COUNT(*)
                    FROM OrderDetails
                    WHERE FoodId = @FoodId;";

                using (SqlCommand checkCommand =
                       new SqlCommand(checkSql, connection))
                {
                    checkCommand.Parameters.Add(
                        "@FoodId", SqlDbType.Int).Value = id;

                    int orderCount = (int)checkCommand.ExecuteScalar();

                    if (orderCount > 0)
                    {
                        TempData["ErrorMessage"] =
                            "This food item cannot be deleted because it is associated with existing orders.";

                        return RedirectToAction(nameof(Index));
                    }
                }

                // Delete only if the food is not used in orders.
                string deleteSql = @"
                    DELETE FROM Foods
                    WHERE FoodId = @FoodId;";

                using (SqlCommand deleteCommand =
                       new SqlCommand(deleteSql, connection))
                {
                    deleteCommand.Parameters.Add(
                        "@FoodId", SqlDbType.Int).Value = id;

                    int rowsAffected = deleteCommand.ExecuteNonQuery();

                    if (rowsAffected == 0)
                        return NotFound();
                }
            }

            TempData["SuccessMessage"] =
                "Food item deleted successfully!";

            return RedirectToAction(nameof(Index));
        }

        // Retrieve one food item.
        private Food? GetFoodById(int id)
        {
            Food? food = null;

            using (SqlConnection connection =
                   new SqlConnection(_connectionString))
            {
                connection.Open();

                string sql = @"
                    SELECT
                        f.FoodId,
                        f.CategoryId,
                        f.FoodName,
                        f.Description,
                        f.Price,
                        f.ImageUrl,
                        f.IsAvailable,
                        c.CategoryName
                    FROM Foods f
                    INNER JOIN Categories c
                        ON f.CategoryId = c.CategoryId
                    WHERE f.FoodId = @FoodId;";

                using (SqlCommand command =
                       new SqlCommand(sql, connection))
                {
                    command.Parameters.Add(
                        "@FoodId", SqlDbType.Int).Value = id;

                    using (SqlDataReader reader =
                           command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            food = new Food
                            {
                                FoodId = reader.GetInt32(0),
                                CategoryId = reader.GetInt32(1),
                                FoodName = reader.GetString(2),
                                Description = reader.IsDBNull(3)
                                    ? null : reader.GetString(3),
                                Price = reader.GetDecimal(4),
                                ImageUrl = reader.IsDBNull(5)
                                    ? null : reader.GetString(5),
                                IsAvailable = reader.GetBoolean(6),
                                CategoryName = reader.GetString(7)
                            };
                        }
                    }
                }
            }

            return food;
        }

        // Load categories into dropdown.
        private void LoadCategories()
        {
            var categories = new List<SelectListItem>();

            using (SqlConnection connection =
                   new SqlConnection(_connectionString))
            {
                connection.Open();

                string sql = @"
                    SELECT CategoryId, CategoryName
                    FROM Categories
                    ORDER BY CategoryName;";

                using (SqlCommand command =
                       new SqlCommand(sql, connection))
                using (SqlDataReader reader =
                       command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        categories.Add(new SelectListItem
                        {
                            Value = reader.GetInt32(0).ToString(),
                            Text = reader.GetString(1)
                        });
                    }
                }
            }

            ViewBag.Categories = categories;
        }
    }
}
