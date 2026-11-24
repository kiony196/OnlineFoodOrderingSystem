
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using OnlineFoodOrderingSystem.Models;
using System.Data;

namespace OnlineFoodOrderingSystem.Controllers
{
    public class FoodsController : Controller
    {
        private readonly string _connectionString;

        public FoodsController(IConfiguration configuration)
        {
            _connectionString =
                configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "DefaultConnection was not found.");
        }

        public IActionResult Index(int? categoryId)
        {
            var foods = new List<Food>();
            var categories = new List<SelectListItem>();

            using (SqlConnection connection =
                   new SqlConnection(_connectionString))
            {
                connection.Open();

                // Load categories for the filter.
                string categoriesSql = @"
                    SELECT CategoryId, CategoryName
                    FROM Categories
                    ORDER BY CategoryName;";

                using (SqlCommand categoriesCommand =
                       new SqlCommand(categoriesSql, connection))
                using (SqlDataReader categoriesReader =
                       categoriesCommand.ExecuteReader())
                {
                    while (categoriesReader.Read())
                    {
                        categories.Add(new SelectListItem
                        {
                            Value = categoriesReader
                                .GetInt32(0).ToString(),

                            Text = categoriesReader.GetString(1),

                            Selected = categoryId.HasValue &&
                                categoryId.Value ==
                                categoriesReader.GetInt32(0)
                        });
                    }
                }

                ViewBag.Categories = categories;
                ViewBag.SelectedCategoryId = categoryId;

                // Load available foods.
                string foodsSql = @"
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
                    WHERE f.IsAvailable = 1
                      AND (
                          @CategoryId IS NULL
                          OR f.CategoryId = @CategoryId
                      )
                    ORDER BY f.FoodName;";

                using (SqlCommand command =
                       new SqlCommand(foodsSql, connection))
                {
                    command.Parameters.Add(
                        "@CategoryId",
                        SqlDbType.Int).Value =
                        (object?)categoryId ?? DBNull.Value;

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
                                    ? null
                                    : reader.GetString(3),

                                Price = reader.GetDecimal(4),

                                ImageUrl = reader.IsDBNull(5)
                                    ? null
                                    : reader.GetString(5),

                                IsAvailable = reader.GetBoolean(6),

                                CategoryName = reader.GetString(7)
                            });
                        }
                    }
                }
            }

            return View(foods);
        }
    }
}