
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using OnlineFoodOrderingSystem.Models;

namespace OnlineFoodOrderingSystem.Controllers
{
    public class CategoriesController : Controller
    {
        private readonly IConfiguration _configuration;

        public CategoriesController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<IActionResult> Index()
        {
            var categories = new List<Category>();

            string connectionString =
                _configuration.GetConnectionString("DefaultConnection")!;

            await using var connection =
                new SqlConnection(connectionString);

            await connection.OpenAsync();

            const string sql =
                "SELECT CategoryId, CategoryName, Description " +
                "FROM Categories ORDER BY CategoryName";

            await using var command = new SqlCommand(sql, connection);

            await using var reader = await command.ExecuteReaderAsync();

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
    }
}
