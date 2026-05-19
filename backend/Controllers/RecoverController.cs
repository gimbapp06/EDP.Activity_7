// =========================================
// Controllers/RecoverController.cs
// Replaces: api/recover.php
// =========================================
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using ThbmsApi.Models;
using ThbmsApi.Services;

namespace ThbmsApi.Controllers
{
    [ApiController]
    [Route("api/recover")]
    public class RecoverController : ControllerBase
    {
        private readonly Database _db;
        public RecoverController(Database db) => _db = db;

        // ── POST /api/recover/request ─────────────────────────────────────────
        [HttpPost("request")]
        public IActionResult RequestToken([FromBody] RecoverRequestBody req)
        {
            if (string.IsNullOrWhiteSpace(req.Email))
                return Ok(ApiResponse.Fail("Email is required."));

            try
            {
                using var conn = _db.GetConnection();

                // Find active user by email
                using var findCmd = new MySqlCommand(
                    "SELECT id FROM users WHERE email = @email AND status = 'active' LIMIT 1", conn);
                findCmd.Parameters.AddWithValue("@email", req.Email.Trim());

                using var reader = findCmd.ExecuteReader();
                if (!reader.Read())
                    return Ok(ApiResponse.Fail("No active account found with that email."));

                var userId = reader.GetInt32("id");
                reader.Close();

                // Generate token + expiry as Unix timestamp
                var token  = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)).ToLower();
                var expiry = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();

                using var updCmd = new MySqlCommand(
                    "UPDATE users SET recovery_token = @token, token_expiry = @expiry WHERE id = @id", conn);
                updCmd.Parameters.AddWithValue("@token",  token);
                updCmd.Parameters.AddWithValue("@expiry", expiry);
                updCmd.Parameters.AddWithValue("@id",     userId);
                updCmd.ExecuteNonQuery();

                // Verify it was saved
                using var checkCmd = new MySqlCommand(
                    "SELECT recovery_token FROM users WHERE id = @id", conn);
                checkCmd.Parameters.AddWithValue("@id", userId);
                var saved = checkCmd.ExecuteScalar()?.ToString();

                if (saved != token)
                    return Ok(ApiResponse.Fail("Failed to save recovery token. Check DB column types."));

                // In production: send token by email. Demo: return it directly.
                return Ok(ApiResponse.Ok("Recovery token generated. (Demo: token returned directly)", new
                {
                    token,
                    note = "In a real system this would be emailed to the user."
                }));
            }
            catch (Exception ex)
            {
                return Ok(ApiResponse.Fail("Server error: " + ex.Message));
            }
        }

        // ── POST /api/recover/reset ───────────────────────────────────────────
        [HttpPost("reset")]
        public IActionResult Reset([FromBody] ResetRequestBody req)
        {
            if (string.IsNullOrWhiteSpace(req.Token) || string.IsNullOrWhiteSpace(req.Password))
                return Ok(ApiResponse.Fail("Token and new password are required."));

            if (req.Password != req.Confirm)
                return Ok(ApiResponse.Fail("Passwords do not match."));

            if (req.Password.Length < 6)
                return Ok(ApiResponse.Fail("Password must be at least 6 characters."));

            try
            {
                using var conn = _db.GetConnection();

                // Look up by token only — check expiry in C# to avoid DB timezone issues
                using var cmd = new MySqlCommand(
                    "SELECT id, token_expiry FROM users WHERE recovery_token = @token LIMIT 1", conn);
                cmd.Parameters.AddWithValue("@token", req.Token.Trim());

                using var reader = cmd.ExecuteReader();
                if (!reader.Read())
                    return Ok(ApiResponse.Fail("Invalid token. Please request a new one."));

                var userId = reader.GetInt32("id");
                var expiry = reader.GetInt64("token_expiry");
                reader.Close();

                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (now > expiry)
                    return Ok(ApiResponse.Fail("Token has expired. Please request a new one."));

                var hashed = BCrypt.Net.BCrypt.HashPassword(req.Password);

                using var updCmd = new MySqlCommand(
                    "UPDATE users SET password = @pw, recovery_token = NULL, token_expiry = NULL WHERE id = @id", conn);
                updCmd.Parameters.AddWithValue("@pw", hashed);
                updCmd.Parameters.AddWithValue("@id", userId);
                updCmd.ExecuteNonQuery();

                return Ok(ApiResponse.Ok("Password reset successfully. You may now log in."));
            }
            catch (Exception ex)
            {
                return Ok(ApiResponse.Fail("Server error: " + ex.Message));
            }
        }
    }

    public class RecoverRequestBody { public string Email   { get; set; } = ""; }
    public class ResetRequestBody
    {
        public string Token    { get; set; } = "";
        public string Password { get; set; } = "";
        public string Confirm  { get; set; } = "";
    }
}
