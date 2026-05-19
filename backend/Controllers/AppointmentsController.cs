// =====================================================
// Controllers/AppointmentsController.cs
// Transaction 1: Book / Manage Appointments
// =====================================================
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using ThbmsApi.Models;
using ThbmsApi.Services;

namespace ThbmsApi.Controllers
{
    [ApiController]
    [Route("api/appointments")]
    public class AppointmentsController : ControllerBase
    {
        private readonly Database _db;
        public AppointmentsController(Database db) => _db = db;

        private bool IsAuth() =>
            !string.IsNullOrEmpty(HttpContext.Session.GetString("user_id"));

        // ── GET /api/appointments/list ────────────────────────────────────────
        [HttpGet("list")]
        public IActionResult List([FromQuery] string search = "", [FromQuery] string status = "")
        {
            if (!IsAuth()) return Ok(ApiResponse.Fail("Unauthorized."));
            try
            {
                using var conn = _db.GetConnection();
                var sql = @"SELECT a.id, a.client_name, a.client_email, a.client_phone,
                                   s.name AS service_name, s.price,
                                   a.preferred_date, a.preferred_time,
                                   a.status, a.note, a.created_at,
                                   t.full_name AS therapist_name
                            FROM appointments a
                            JOIN services s ON s.id = a.service_id
                            LEFT JOIN assignments asgn ON asgn.appointment_id = a.id
                            LEFT JOIN therapists t ON t.id = asgn.therapist_id
                            WHERE 1=1";
                if (!string.IsNullOrWhiteSpace(search))
                    sql += " AND (a.client_name LIKE @s OR a.client_email LIKE @s OR s.name LIKE @s)";
                if (!string.IsNullOrWhiteSpace(status))
                    sql += " AND a.status = @status";
                sql += " ORDER BY a.preferred_date DESC, a.preferred_time DESC";

                using var cmd = new MySqlCommand(sql, conn);
                if (!string.IsNullOrWhiteSpace(search))
                    cmd.Parameters.AddWithValue("@s", $"%{search}%");
                if (!string.IsNullOrWhiteSpace(status))
                    cmd.Parameters.AddWithValue("@status", status);

                using var r = cmd.ExecuteReader();
                var list = new List<object>();
                while (r.Read())
                    list.Add(new {
                        id             = r.GetInt32("id"),
                        client_name    = r.GetString("client_name"),
                        client_email   = r.IsDBNull(r.GetOrdinal("client_email"))  ? "" : r.GetString("client_email"),
                        client_phone   = r.IsDBNull(r.GetOrdinal("client_phone"))  ? "" : r.GetString("client_phone"),
                        service_name   = r.GetString("service_name"),
                        price          = r.GetDecimal("price"),
                        preferred_date = r.GetDateTime("preferred_date").ToString("yyyy-MM-dd"),
                        preferred_time = r.GetTimeSpan("preferred_time").ToString(@"hh\:mm"),
                        status         = r.GetString("status"),
                        note           = r.IsDBNull(r.GetOrdinal("note")) ? "" : r.GetString("note"),
                        therapist_name = r.IsDBNull(r.GetOrdinal("therapist_name")) ? "Unassigned" : r.GetString("therapist_name"),
                        created_at     = r.GetDateTime("created_at").ToString("yyyy-MM-dd HH:mm"),
                    });
                return Ok(ApiResponse.Ok("OK", new { appointments = list }));
            }
            catch (Exception ex) { return Ok(ApiResponse.Fail(ex.Message)); }
        }

        // ── GET /api/appointments/services ────────────────────────────────────
        [HttpGet("services")]
        public IActionResult Services()
        {
            if (!IsAuth()) return Ok(ApiResponse.Fail("Unauthorized."));
            try
            {
                using var conn = _db.GetConnection();
                using var cmd  = new MySqlCommand(
                    "SELECT id, name, duration_minutes, price FROM services WHERE status='active' ORDER BY name", conn);
                using var r = cmd.ExecuteReader();
                var list = new List<object>();
                while (r.Read())
                    list.Add(new {
                        id               = r.GetInt32("id"),
                        name             = r.GetString("name"),
                        duration_minutes = r.GetInt32("duration_minutes"),
                        price            = r.GetDecimal("price"),
                    });
                return Ok(ApiResponse.Ok("OK", new { services = list }));
            }
            catch (Exception ex) { return Ok(ApiResponse.Fail(ex.Message)); }
        }

        // ── POST /api/appointments/add ────────────────────────────────────────
        [HttpPost("add")]
        public IActionResult Add([FromBody] AppointmentRequest req)
        {
            if (!IsAuth()) return Ok(ApiResponse.Fail("Unauthorized."));
            if (string.IsNullOrWhiteSpace(req.ClientName))
                return Ok(ApiResponse.Fail("Client name is required."));
            if (req.ServiceId <= 0)
                return Ok(ApiResponse.Fail("Please select a service."));
            if (string.IsNullOrWhiteSpace(req.PreferredDate))
                return Ok(ApiResponse.Fail("Preferred date is required."));
            if (string.IsNullOrWhiteSpace(req.PreferredTime))
                return Ok(ApiResponse.Fail("Preferred time is required."));
            try
            {
                using var conn = _db.GetConnection();
                using var cmd  = new MySqlCommand(@"
                    INSERT INTO appointments
                        (client_name, client_email, client_phone, service_id, preferred_date, preferred_time, note, status)
                    VALUES (@cn, @ce, @cp, @sid, @pd, @pt, @note, 'pending')", conn);
                cmd.Parameters.AddWithValue("@cn",   req.ClientName.Trim());
                cmd.Parameters.AddWithValue("@ce",   req.ClientEmail?.Trim() ?? "");
                cmd.Parameters.AddWithValue("@cp",   req.ClientPhone?.Trim() ?? "");
                cmd.Parameters.AddWithValue("@sid",  req.ServiceId);
                cmd.Parameters.AddWithValue("@pd",   req.PreferredDate);
                cmd.Parameters.AddWithValue("@pt",   req.PreferredTime);
                cmd.Parameters.AddWithValue("@note", req.Note?.Trim() ?? "");
                cmd.ExecuteNonQuery();
                return Ok(ApiResponse.Ok("Appointment booked successfully.", new { id = cmd.LastInsertedId }));
            }
            catch (Exception ex) { return Ok(ApiResponse.Fail(ex.Message)); }
        }

        // ── POST /api/appointments/update-status ──────────────────────────────
        [HttpPost("update-status")]
        public IActionResult UpdateStatus([FromBody] StatusRequest req)
        {
            if (!IsAuth()) return Ok(ApiResponse.Fail("Unauthorized."));
            var valid = new[] { "pending","confirmed","completed","cancelled" };
            if (!valid.Contains(req.Status)) return Ok(ApiResponse.Fail("Invalid status."));
            try
            {
                using var conn = _db.GetConnection();
                using var cmd  = new MySqlCommand(
                    "UPDATE appointments SET status=@s WHERE id=@id", conn);
                cmd.Parameters.AddWithValue("@s",  req.Status);
                cmd.Parameters.AddWithValue("@id", req.Id);
                cmd.ExecuteNonQuery();
                return Ok(ApiResponse.Ok($"Appointment set to {req.Status}."));
            }
            catch (Exception ex) { return Ok(ApiResponse.Fail(ex.Message)); }
        }
    }

    public class AppointmentRequest
    {
        public string ClientName  { get; set; } = "";
        public string? ClientEmail { get; set; }
        public string? ClientPhone { get; set; }
        public int    ServiceId   { get; set; }
        public string PreferredDate { get; set; } = "";
        public string PreferredTime { get; set; } = "";
        public string? Note        { get; set; }
    }
    public class StatusRequest { public int Id { get; set; } public string Status { get; set; } = ""; }
}
