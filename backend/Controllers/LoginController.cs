// =========================================
// Controllers/LoginController.cs
// =========================================
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using ThbmsApi.Models;
using ThbmsApi.Services;

namespace ThbmsApi.Controllers
{
    [ApiController]
    [Route("api/login")]
    public class LoginController : ControllerBase
    {
        private readonly Database _db;
        public LoginController(Database db) => _db = db;

        // ── POST /api/login ───────────────────────────────────────────────────
        [HttpPost]
        public IActionResult Login([FromBody] LoginRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
                return Ok(ApiResponse.Fail("Email and password are required."));

            try
            {
                using var conn = _db.GetConnection();
                using var cmd  = new MySqlCommand(
                    "SELECT id, username, email, full_name, role, status, password " +
                    "FROM users WHERE email = @email LIMIT 1", conn);
                cmd.Parameters.AddWithValue("@email", req.Email.Trim());

                using var reader = cmd.ExecuteReader();
                if (!reader.Read())
                    return Ok(ApiResponse.Fail("No account found with that email."));

                var status   = reader.GetString("status");
                var hashInDb = reader.GetString("password");

                if (status == "inactive")
                    return Ok(ApiResponse.Fail("Your account is inactive. Contact the administrator."));

                if (!BCrypt.Net.BCrypt.Verify(req.Password, hashInDb))
                    return Ok(ApiResponse.Fail("Incorrect password."));

                var userId   = reader.GetInt32("id").ToString();
                var userName = reader.GetString("username");
                var fullName = reader.GetString("full_name");
                var role     = reader.GetString("role");
                var email    = reader.GetString("email");
                reader.Close();

                // ✅ Save to server-side session
                HttpContext.Session.SetString("user_id",        userId);
                HttpContext.Session.SetString("user_username",  userName);
                HttpContext.Session.SetString("user_email",     email);
                HttpContext.Session.SetString("user_full_name", fullName);
                HttpContext.Session.SetString("user_role",      role);

                return Ok(ApiResponse.Ok("Login successful.", new
                {
                    user = new { id = userId, username = userName, email, full_name = fullName, role }
                }));
            }
            catch (Exception ex)
            {
                return Ok(ApiResponse.Fail("Server error: " + ex.Message));
            }
        }

        // ── GET /api/login/check ──────────────────────────────────────────────
        [HttpGet("check")]
        public IActionResult Check()
        {
            var userId = HttpContext.Session.GetString("user_id");
            if (string.IsNullOrEmpty(userId))
                return Ok(ApiResponse.Fail("Not authenticated."));

            return Ok(ApiResponse.Ok("Authenticated.", new
            {
                user = new
                {
                    id        = userId,
                    username  = HttpContext.Session.GetString("user_username"),
                    email     = HttpContext.Session.GetString("user_email"),
                    full_name = HttpContext.Session.GetString("user_full_name"),
                    role      = HttpContext.Session.GetString("user_role"),
                }
            }));
        }

        // ── GET /api/login/logout ─────────────────────────────────────────────
        [HttpGet("logout")]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return Ok(ApiResponse.Ok("Logged out successfully."));
        }
    }

    public class LoginRequest
    {
        public string Email    { get; set; } = "";
        public string Password { get; set; } = "";
    }
}
