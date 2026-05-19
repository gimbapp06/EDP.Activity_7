// =========================================
// Controllers/UsersController.cs
// Replaces: api/users.php
// =========================================
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using ThbmsApi.Models;
using ThbmsApi.Services;

namespace ThbmsApi.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class UsersController : ControllerBase
    {
        private readonly Database _db;
        public UsersController(Database db) => _db = db;

        // Helper — check auth via X-User-Id header sent by frontend
        private bool IsAuthenticated(out string currentUserId)
        {
            currentUserId = HttpContext.Request.Headers["X-User-Id"].FirstOrDefault() ?? "";
            return !string.IsNullOrEmpty(currentUserId);
        }

        // ── GET /api/users/list?search= ───────────────────────────────────────
        [HttpGet("list")]
        public IActionResult List([FromQuery] string search = "")
        {
            if (!IsAuthenticated(out _))
                return Ok(ApiResponse.Fail("Unauthorized. Please log in."));

            try
            {
                using var conn = _db.GetConnection();

                var sql = "SELECT id, username, email, full_name, role, status, created_at " +
                          "FROM users WHERE 1=1";

                if (!string.IsNullOrWhiteSpace(search))
                    sql += " AND (full_name LIKE @s OR email LIKE @s OR username LIKE @s)";

                sql += " ORDER BY created_at DESC";

                using var cmd = new MySqlCommand(sql, conn);
                if (!string.IsNullOrWhiteSpace(search))
                    cmd.Parameters.AddWithValue("@s", $"%{search}%");

                using var reader = cmd.ExecuteReader();
                var users = new List<object>();
                while (reader.Read())
                {
                    users.Add(new
                    {
                        id         = reader.GetInt32("id"),
                        username   = reader.GetString("username"),
                        email      = reader.GetString("email"),
                        full_name  = reader.GetString("full_name"),
                        role       = reader.GetString("role"),
                        status     = reader.GetString("status"),
                        created_at = reader.GetDateTime("created_at").ToString("yyyy-MM-dd HH:mm:ss"),
                    });
                }
                return Ok(ApiResponse.Ok("OK", new { users }));
            }
            catch (Exception ex)
            {
                return Ok(ApiResponse.Fail("Server error: " + ex.Message));
            }
        }

        // ── GET /api/users/get?id= ────────────────────────────────────────────
        [HttpGet("get")]
        public IActionResult Get([FromQuery] int id)
        {
            if (!IsAuthenticated(out _))
                return Ok(ApiResponse.Fail("Unauthorized. Please log in."));

            try
            {
                using var conn = _db.GetConnection();
                using var cmd  = new MySqlCommand(
                    "SELECT id, username, email, full_name, role, status FROM users WHERE id = @id", conn);
                cmd.Parameters.AddWithValue("@id", id);

                using var reader = cmd.ExecuteReader();
                if (!reader.Read())
                    return Ok(ApiResponse.Fail("User not found."));

                return Ok(ApiResponse.Ok("OK", new
                {
                    user = new
                    {
                        id        = reader.GetInt32("id"),
                        username  = reader.GetString("username"),
                        email     = reader.GetString("email"),
                        full_name = reader.GetString("full_name"),
                        role      = reader.GetString("role"),
                        status    = reader.GetString("status"),
                    }
                }));
            }
            catch (Exception ex)
            {
                return Ok(ApiResponse.Fail("Server error: " + ex.Message));
            }
        }

        // ── POST /api/users/add ───────────────────────────────────────────────
        [HttpPost("add")]
        public IActionResult Add([FromBody] AddUserRequest req)
        {
            if (!IsAuthenticated(out _))
                return Ok(ApiResponse.Fail("Unauthorized. Please log in."));

            if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Email) ||
                string.IsNullOrWhiteSpace(req.FullName) || string.IsNullOrWhiteSpace(req.Password))
                return Ok(ApiResponse.Fail("All fields are required."));

            if (!IsValidEmail(req.Email))
                return Ok(ApiResponse.Fail("Invalid email format."));

            if (req.Password.Length < 6)
                return Ok(ApiResponse.Fail("Password must be at least 6 characters."));

            var role = req.Role == "admin" ? "admin" : "staff";

            try
            {
                using var conn = _db.GetConnection();

                // Check duplicates
                using var chkCmd = new MySqlCommand(
                    "SELECT id FROM users WHERE email = @email OR username = @username LIMIT 1", conn);
                chkCmd.Parameters.AddWithValue("@email",    req.Email.Trim());
                chkCmd.Parameters.AddWithValue("@username", req.Username.Trim());
                var existing = chkCmd.ExecuteScalar();
                if (existing != null)
                    return Ok(ApiResponse.Fail("Email or username already exists."));

                var hashed = BCrypt.Net.BCrypt.HashPassword(req.Password);

                using var insCmd = new MySqlCommand(
                    "INSERT INTO users (username, email, password, full_name, role, status) " +
                    "VALUES (@u, @e, @p, @f, @r, 'active')", conn);
                insCmd.Parameters.AddWithValue("@u", req.Username.Trim());
                insCmd.Parameters.AddWithValue("@e", req.Email.Trim());
                insCmd.Parameters.AddWithValue("@p", hashed);
                insCmd.Parameters.AddWithValue("@f", req.FullName.Trim());
                insCmd.Parameters.AddWithValue("@r", role);
                insCmd.ExecuteNonQuery();

                var newId = insCmd.LastInsertedId;
                return Ok(ApiResponse.Ok("Account created successfully.", new { id = newId }));
            }
            catch (Exception ex)
            {
                return Ok(ApiResponse.Fail("Server error: " + ex.Message));
            }
        }

        // ── POST /api/users/update ────────────────────────────────────────────
        [HttpPost("update")]
        public IActionResult Update([FromBody] UpdateUserRequest req)
        {
            if (!IsAuthenticated(out _))
                return Ok(ApiResponse.Fail("Unauthorized. Please log in."));

            if (string.IsNullOrWhiteSpace(req.FullName) || string.IsNullOrWhiteSpace(req.Email))
                return Ok(ApiResponse.Fail("Full name and email are required."));

            if (!IsValidEmail(req.Email))
                return Ok(ApiResponse.Fail("Invalid email format."));

            var role = req.Role == "admin" ? "admin" : "staff";

            try
            {
                using var conn = _db.GetConnection();

                // Check email conflict with OTHER users
                using var chkCmd = new MySqlCommand(
                    "SELECT id FROM users WHERE email = @email AND id != @id LIMIT 1", conn);
                chkCmd.Parameters.AddWithValue("@email", req.Email.Trim());
                chkCmd.Parameters.AddWithValue("@id",    req.Id);
                if (chkCmd.ExecuteScalar() != null)
                    return Ok(ApiResponse.Fail("Email already used by another account."));

                string sql;
                MySqlCommand updCmd;

                if (!string.IsNullOrWhiteSpace(req.Password))
                {
                    if (req.Password.Length < 6)
                        return Ok(ApiResponse.Fail("Password must be at least 6 characters."));

                    var hashed = BCrypt.Net.BCrypt.HashPassword(req.Password);
                    sql = "UPDATE users SET full_name=@f, email=@e, role=@r, password=@p WHERE id=@id";
                    updCmd = new MySqlCommand(sql, conn);
                    updCmd.Parameters.AddWithValue("@p", hashed);
                }
                else
                {
                    sql = "UPDATE users SET full_name=@f, email=@e, role=@r WHERE id=@id";
                    updCmd = new MySqlCommand(sql, conn);
                }

                updCmd.Parameters.AddWithValue("@f",  req.FullName.Trim());
                updCmd.Parameters.AddWithValue("@e",  req.Email.Trim());
                updCmd.Parameters.AddWithValue("@r",  role);
                updCmd.Parameters.AddWithValue("@id", req.Id);
                updCmd.ExecuteNonQuery();

                return Ok(ApiResponse.Ok("Account updated successfully."));
            }
            catch (Exception ex)
            {
                return Ok(ApiResponse.Fail("Server error: " + ex.Message));
            }
        }

        // ── POST /api/users/toggle ────────────────────────────────────────────
        [HttpPost("toggle")]
        public IActionResult Toggle([FromBody] ToggleRequest req)
        {
            if (!IsAuthenticated(out string currentUserId))
                return Ok(ApiResponse.Fail("Unauthorized. Please log in."));

            var status = req.Status == "active" ? "active" : "inactive";

            if (req.Id.ToString() == currentUserId && status == "inactive")
                return Ok(ApiResponse.Fail("You cannot deactivate your own account."));

            try
            {
                using var conn   = _db.GetConnection();
                using var updCmd = new MySqlCommand(
                    "UPDATE users SET status = @s WHERE id = @id", conn);
                updCmd.Parameters.AddWithValue("@s",  status);
                updCmd.Parameters.AddWithValue("@id", req.Id);
                updCmd.ExecuteNonQuery();

                return Ok(ApiResponse.Ok($"Account set to {status}."));
            }
            catch (Exception ex)
            {
                return Ok(ApiResponse.Fail("Server error: " + ex.Message));
            }
        }

        // ── Helper ────────────────────────────────────────────────────────────
        private static bool IsValidEmail(string email)
        {
            try { var addr = new System.Net.Mail.MailAddress(email); return addr.Address == email.Trim(); }
            catch { return false; }
        }
    }

    // ── Request models ────────────────────────────────────────────────────────
    public class AddUserRequest
    {
        public string Username { get; set; } = "";
        public string Email    { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Password { get; set; } = "";
        public string Role     { get; set; } = "staff";
    }
    public class UpdateUserRequest
    {
        public int    Id       { get; set; }
        public string FullName { get; set; } = "";
        public string Email    { get; set; } = "";
        public string Role     { get; set; } = "staff";
        public string Password { get; set; } = "";
    }
    public class ToggleRequest
    {
        public int    Id     { get; set; }
        public string Status { get; set; } = "";
    }
}
