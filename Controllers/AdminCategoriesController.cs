
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using OnlineFoodOrderingSystem.Models;

namespace OnlineFoodOrderingSystem.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminCategoriesController : Controller
    {
        private readonly IConfiguration _configuration;

        public AdminCategoriesController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private string ConnectionString =>
            _configuration.GetConnectionString("DefaultConnection")!;

        // LIST CATEGORIES
        public async Task<IActionResult> Index()
        {
            var categories = new List<Category>();

            await using var connection =
                new SqlConnection(ConnectionString);

            await connection.OpenAsync();

            const string sql = @"
                SELECT CategoryId, CategoryName, Description
                FROM Categories
                ORDER BY CategoryName";

            await using var command =
                new SqlCommand(sql, connection);

            await using var reader =
                await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                categories.Add(new Category
                {
                    CategoryId = reader.GetInt32(0),
                    CategoryName = reader.GetString(1),
                    Description = reader.IsDBNull(2)
                        ? null
                        : reader.GetString(2)
                });
            }

            return View(categories);
        }

        // CREATE - GET
        [HttpGet]
        public IActionResult Create()
        {
            return View(new Category());
        }

        // CREATE - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Category category)
        {
            category.CategoryName = category.CategoryName?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(category.CategoryName))
            {
                ModelState.AddModelError(
                    nameof(category.CategoryName),
                    "Category name is required.");
            }

            if (!ModelState.IsValid)
                return View(category);

            await using var connection =
                new SqlConnection(ConnectionString);

            await connection.OpenAsync();

            const string checkSql = @"
                SELECT COUNT(*)
                FROM Categories
                WHERE CategoryName = @CategoryName";

            await using (var checkCommand =
                new SqlCommand(checkSql, connection))
            {
                checkCommand.Parameters.AddWithValue(
                    "@CategoryName", category.CategoryName);

                int exists = Convert.ToInt32(
                    await checkCommand.ExecuteScalarAsync());

                if (exists > 0)
                {
                    ModelState.AddModelError(
                        nameof(category.CategoryName),
                        "This category already exists.");

                    return View(category);
                }
            }

            const string insertSql = @"
                INSERT INTO Categories (CategoryName, Description)
                VALUES (@CategoryName, @Description)";

            await using var command =
                new SqlCommand(insertSql, connection);

            command.Parameters.AddWithValue(
                "@CategoryName", category.CategoryName);

            command.Parameters.AddWithValue(
                "@Description",
                (object?)category.Description ?? DBNull.Value);

            await command.ExecuteNonQueryAsync();

            TempData["SuccessMessage"] =
                "Category created successfully.";

            return RedirectToAction(nameof(Index));
        }

        // DETAILS
        public async Task<IActionResult> Details(int id)
        {
            var category = await GetCategory(id);

            if (category == null)
                return NotFound();

            return View(category);
        }

        // EDIT - GET
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var category = await GetCategory(id);

            if (category == null)
                return NotFound();

            return View(category);
        }

        // EDIT - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id, Category category)
        {
            if (id != category.CategoryId)
                return NotFound();

            category.CategoryName =
                category.CategoryName?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(category.CategoryName))
            {
                ModelState.AddModelError(
                    nameof(category.CategoryName),
                    "Category name is required.");
            }

            if (!ModelState.IsValid)
                return View(category);

            await using var connection =
                new SqlConnection(ConnectionString);

            await connection.OpenAsync();

            const string checkSql = @"
                SELECT COUNT(*)
                FROM Categories
                WHERE CategoryName = @CategoryName
                AND CategoryId <> @CategoryId";

            await using (var checkCommand =
                new SqlCommand(checkSql, connection))
            {
                checkCommand.Parameters.AddWithValue(
                    "@CategoryName", category.CategoryName);

                checkCommand.Parameters.AddWithValue(
                    "@CategoryId", category.CategoryId);

                int exists = Convert.ToInt32(
                    await checkCommand.ExecuteScalarAsync());

                if (exists > 0)
                {
                    ModelState.AddModelError(
                        nameof(category.CategoryName),
                        "Another category already uses this name.");

                    return View(category);
                }
            }

            const string updateSql = @"
                UPDATE Categories
                SET CategoryName = @CategoryName,
                    Description = @Description
                WHERE CategoryId = @CategoryId";

            await using var command =
                new SqlCommand(updateSql, connection);

            command.Parameters.AddWithValue(
                "@CategoryName", category.CategoryName);

            command.Parameters.AddWithValue(
                "@Description",
                (object?)category.Description ?? DBNull.Value);

            command.Parameters.AddWithValue(
                "@CategoryId", category.CategoryId);

            await command.ExecuteNonQueryAsync();

            TempData["SuccessMessage"] =
                "Category updated successfully.";

            return RedirectToAction(nameof(Index));
        }

        // DELETE - GET
        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var category = await GetCategory(id);

            if (category == null)
                return NotFound();

            return View(category);
        }

        // DELETE - POST
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await using var connection =
                new SqlConnection(ConnectionString);

            await connection.OpenAsync();

            // Check whether foods use this category
            const string checkSql = @"
                SELECT COUNT(*)
                FROM Foods
                WHERE CategoryId = @CategoryId";

            await using (var checkCommand =
                new SqlCommand(checkSql, connection))
            {
                checkCommand.Parameters.AddWithValue(
                    "@CategoryId", id);

                int foodCount = Convert.ToInt32(
                    await checkCommand.ExecuteScalarAsync());

                if (foodCount > 0)
                {
                    TempData["ErrorMessage"] =
                        "This category cannot be deleted because it contains food items.";

                    return RedirectToAction(nameof(Index));
                }
            }

            const string deleteSql = @"
                DELETE FROM Categories
                WHERE CategoryId = @CategoryId";

            await using var command =
                new SqlCommand(deleteSql, connection);

            command.Parameters.AddWithValue("@CategoryId", id);

            int rowsAffected = await command.ExecuteNonQueryAsync();

            TempData["SuccessMessage"] = rowsAffected > 0
                ? "Category deleted successfully."
                : "Category not found.";

            return RedirectToAction(nameof(Index));
        }

        // GET CATEGORY BY ID
        private async Task<Category?> GetCategory(int id)
        {
            await using var connection =
                new SqlConnection(ConnectionString);

            await connection.OpenAsync();

            const string sql = @"
                SELECT CategoryId, CategoryName, Description
                FROM Categories
                WHERE CategoryId = @CategoryId";

            await using var command =
                new SqlCommand(sql, connection);

            command.Parameters.AddWithValue("@CategoryId", id);

            await using var reader =
                await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new Category
                {
                    CategoryId = reader.GetInt32(0),
                    CategoryName = reader.GetString(1),
                    Description = reader.IsDBNull(2)
                        ? null
                        : reader.GetString(2)
                };
            }

            return null;
        }
    }
}
