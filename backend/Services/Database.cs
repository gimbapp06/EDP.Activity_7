// =========================================
// Services/Database.cs — Public DB Class
// =========================================
using MySql.Data.MySqlClient;

namespace ThbmsApi.Services
{
    /// <summary>
    /// Public singleton class for MySQL database connections.
    /// Replaces the PHP Database class in db.php.
    /// </summary>
    public class Database
    {
        // Public connection properties (matches the original PHP class)
        public string Host     { get; } = "localhost";
        public string DbName   { get; } = "thbms_demo";
        public string Username { get; } = "root";
        public string Password { get; } = "";
        public int    Port     { get; } = 3306;

        private readonly string _connectionString;

        public Database()
        {
            _connectionString =
                $"Server={Host};Port={Port};Database={DbName};" +
                $"Uid={Username};Pwd={Password};CharSet=utf8mb4;" +
                $"Convert Zero Datetime=True;";
        }

        /// <summary>
        /// Returns a new open MySqlConnection.
        /// Always use inside a `using` block so it is properly closed.
        /// </summary>
        public MySqlConnection GetConnection()
        {
            var conn = new MySqlConnection(_connectionString);
            try
            {
                conn.Open();
            }
            catch (MySqlException ex)
            {
                throw new Exception($"Database connection failed: {ex.Message}");
            }
            return conn;
        }
    }
}
