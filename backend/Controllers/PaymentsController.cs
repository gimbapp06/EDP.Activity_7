// =====================================================
// Controllers/PaymentsController.cs
// Transaction 3: Process Payments
// =====================================================
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using ThbmsApi.Models;
using ThbmsApi.Services;

namespace ThbmsApi.Controllers
{
    [ApiController]
    [Route("api/payments")]
    public class PaymentsController : ControllerBase
    {
        private readonly Database _db;
        public PaymentsController(Database db) => _db = db;

        private bool IsAuth(out int userId)
        {
            var s = HttpContext.Session.GetString("user_id");
            userId = int.TryParse(s, out var id) ? id : 0;
            return !string.IsNullOrEmpty(s);
        }

        // ── GET /api/payments/list ────────────────────────────────────────────
        [HttpGet("list")]
        public IActionResult List([FromQuery] string search = "")
        {
            if (!IsAuth(out _)) return Ok(ApiResponse.Fail("Unauthorized."));
            try
            {
                using var conn = _db.GetConnection();
                var sql = @"SELECT p.id, a.id AS appointment_id, a.client_name,
                                   s.name AS service_name, p.amount, p.method,
                                   p.status, p.remarks, u.full_name AS processed_by_name,
                                   p.processed_at
                            FROM payments p
                            JOIN appointments a ON a.id = p.appointment_id
                            JOIN services s     ON s.id = a.service_id
                            JOIN users u        ON u.id = p.processed_by
                            WHERE 1=1";
                if (!string.IsNullOrWhiteSpace(search))
                    sql += " AND (a.client_name LIKE @s OR s.name LIKE @s OR p.method LIKE @s)";
                sql += " ORDER BY p.processed_at DESC";

                using var cmd = new MySqlCommand(sql, conn);
                if (!string.IsNullOrWhiteSpace(search))
                    cmd.Parameters.AddWithValue("@s", $"%{search}%");

                using var r = cmd.ExecuteReader();
                var list = new List<object>();
                while (r.Read())
                    list.Add(new {
                        id                = r.GetInt32("id"),
                        appointment_id    = r.GetInt32("appointment_id"),
                        client_name       = r.GetString("client_name"),
                        service_name      = r.GetString("service_name"),
                        amount            = r.GetDecimal("amount"),
                        method            = r.GetString("method"),
                        status            = r.GetString("status"),
                        remarks           = r.IsDBNull(r.GetOrdinal("remarks")) ? "" : r.GetString("remarks"),
                        processed_by_name = r.GetString("processed_by_name"),
                        processed_at      = r.GetDateTime("processed_at").ToString("yyyy-MM-dd HH:mm"),
                    });
                return Ok(ApiResponse.Ok("OK", new { payments = list }));
            }
            catch (Exception ex) { return Ok(ApiResponse.Fail(ex.Message)); }
        }

        // ── GET /api/payments/unpaid ───────────────────────────────────────────
        [HttpGet("unpaid")]
        public IActionResult Unpaid()
        {
            if (!IsAuth(out _)) return Ok(ApiResponse.Fail("Unauthorized."));
            try
            {
                using var conn = _db.GetConnection();
                using var cmd  = new MySqlCommand(@"
                    SELECT a.id, a.client_name, s.name AS service_name, s.price, a.preferred_date
                    FROM appointments a
                    JOIN services s ON s.id = a.service_id
                    LEFT JOIN payments p ON p.appointment_id = a.id AND p.status = 'completed'
                    WHERE p.id IS NULL AND a.status IN ('confirmed','completed')
                    ORDER BY a.preferred_date", conn);
                using var r = cmd.ExecuteReader();
                var list = new List<object>();
                while (r.Read())
                    list.Add(new {
                        id             = r.GetInt32("id"),
                        client_name    = r.GetString("client_name"),
                        service_name   = r.GetString("service_name"),
                        price          = r.GetDecimal("price"),
                        preferred_date = r.GetDateTime("preferred_date").ToString("yyyy-MM-dd"),
                    });
                return Ok(ApiResponse.Ok("OK", new { appointments = list }));
            }
            catch (Exception ex) { return Ok(ApiResponse.Fail(ex.Message)); }
        }

        // ── POST /api/payments/process ────────────────────────────────────────
        [HttpPost("process")]
        public IActionResult Process([FromBody] PaymentRequest req)
        {
            if (!IsAuth(out int userId)) return Ok(ApiResponse.Fail("Unauthorized."));
            if (req.AppointmentId <= 0) return Ok(ApiResponse.Fail("Appointment is required."));
            if (req.Amount <= 0)        return Ok(ApiResponse.Fail("Amount must be greater than zero."));

            var validMethods = new[] { "cash","gcash","credit_card","paymaya" };
            if (!validMethods.Contains(req.Method))
                return Ok(ApiResponse.Fail("Invalid payment method."));
            try
            {
                using var conn = _db.GetConnection();

                // Check for existing completed payment
                using var chk = new MySqlCommand(
                    "SELECT id FROM payments WHERE appointment_id=@aid AND status='completed' LIMIT 1", conn);
                chk.Parameters.AddWithValue("@aid", req.AppointmentId);
                if (chk.ExecuteScalar() != null)
                    return Ok(ApiResponse.Fail("This appointment has already been paid."));

                using var cmd = new MySqlCommand(@"
                    INSERT INTO payments (appointment_id, amount, method, status, processed_by, remarks)
                    VALUES (@aid, @amt, @mth, 'completed', @uid, @rem)", conn);
                cmd.Parameters.AddWithValue("@aid", req.AppointmentId);
                cmd.Parameters.AddWithValue("@amt", req.Amount);
                cmd.Parameters.AddWithValue("@mth", req.Method);
                cmd.Parameters.AddWithValue("@uid", userId);
                cmd.Parameters.AddWithValue("@rem", req.Remarks?.Trim() ?? "");
                cmd.ExecuteNonQuery();

                // Mark appointment as completed
                using var upd = new MySqlCommand(
                    "UPDATE appointments SET status='completed' WHERE id=@id", conn);
                upd.Parameters.AddWithValue("@id", req.AppointmentId);
                upd.ExecuteNonQuery();

                return Ok(ApiResponse.Ok("Payment processed successfully.", new { id = cmd.LastInsertedId }));
            }
            catch (Exception ex) { return Ok(ApiResponse.Fail(ex.Message)); }
        }

        // ── POST /api/payments/refund ──────────────────────────────────────────
        [HttpPost("refund")]
        public IActionResult Refund([FromBody] RefundRequest req)
        {
            if (!IsAuth(out _)) return Ok(ApiResponse.Fail("Unauthorized."));
            try
            {
                using var conn = _db.GetConnection();
                using var cmd  = new MySqlCommand(
                    "UPDATE payments SET status='refunded' WHERE id=@id AND status='completed'", conn);
                cmd.Parameters.AddWithValue("@id", req.Id);
                var rows = cmd.ExecuteNonQuery();
                if (rows == 0) return Ok(ApiResponse.Fail("Payment not found or already refunded."));
                return Ok(ApiResponse.Ok("Payment refunded."));
            }
            catch (Exception ex) { return Ok(ApiResponse.Fail(ex.Message)); }
        }
    }

    public class PaymentRequest
    {
        public int     AppointmentId { get; set; }
        public decimal Amount        { get; set; }
        public string  Method        { get; set; } = "cash";
        public string? Remarks       { get; set; }
    }
    public class RefundRequest { public int Id { get; set; } }
}
