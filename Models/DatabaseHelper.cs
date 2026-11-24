
using Microsoft.Data.SqlClient;

namespace OnlineFoodOrderingSystem.Models
{
    public class DatabaseHelper
    {
        private readonly string _connectionString;

        public DatabaseHelper(IConfiguration configuration)
        {
            _connectionString =
                configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "A connection string 'DefaultConnection' não foi encontrada.");
        }

        public async Task<bool> TestConnectionAsync()
        {
            await using var connection =
                new SqlConnection(_connectionString);

            await connection.OpenAsync();

            await using var command = new SqlCommand(
                "SELECT DB_NAME()", connection);

            var result = await command.ExecuteScalarAsync();

            return result?.ToString() == "OnlineFoodDB";
        }
    }
}
