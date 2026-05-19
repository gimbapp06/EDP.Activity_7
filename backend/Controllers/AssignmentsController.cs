// =====================================================
// Controllers/AssignmentsController.cs
// Transaction 2: Assign Therapist to Appointment
// =====================================================
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using ThbmsApi.Models;
using ThbmsApi.Services;

namespace ThbmsApi.Controllers
{
    [ApiController]
    [Route("api/assignments")]
    public class AssignmentsController : ControllerBase
    {
        private readonly Database _db;
        public AssignmentsController(Database db) => _db = db;

        private bool IsAuth(out int userId)
        {
            var s = HttpContext.Session.GetString("user_id");
            userId = int.TryParse(s, out var id) ? id : 0;
            return !string.IsNullOrEmpty(s);
        }

        // ── GET /api/assignments/therapists ───────────────────────────────────
        [HttpGet("therapists")]
        public IActionResult Therapists()
        {
            if (!IsAuth(out _)) return Ok(ApiResponse.Fail("Unauthorized."));
            try
            {
                using var conn = _db.GetConnection();
                using var cmd  = new MySqlCommand(
                    "SELECT id, full_name, specialty FROM therapists WHERE status='active' ORDER BY full_name", conn);
                using var r = cmd.ExecuteReader();
                var list = new List<object>();
                while (r.Read())
                    list.Add(new {
                        id        = r.GetInt32("id"),
                        full_name = r.GetString("full_name"),
                        specialty = r.IsDBNull(r.GetOrdinal("specialty")) ? "" : r.GetString("specialty"),
                    });
                return Ok(ApiResponse.Ok("OK", new { therapists = list }));
            }
            catch (Exception ex) { return Ok(ApiResponse.Fail(ex.Message)); }
        }

        // ── GET /api/assignments/list ─────────────────────────────────────────
        [HttpGet("list")]
        public IActionResult List()
        {
            if (!IsAuth(out _)) return Ok(ApiResponse.Fail("Unauthorized."));
            try
            {
                using var conn = _db.GetConnection();
                using var cmd  = new MySqlCommand(@"
                    SELECT asgn.id, a.id AS appointment_id, a.client_name,
                           s.name AS service_name, a.preferred_date, a.preferred_time, a.status,
                           t.full_name AS therapist_name, t.specialty,
                           u.full_name AS assigned_by_name,
                           asgn.assigned_at, asgn.notes
                    FROM assignments asgn
                    JOIN appointments a  ON a.id  = asgn.appointment_id
                    JOIN services s      ON s.id  = a.service_id
                    JOIN therapists t    ON t.id  = asgn.therapist_id
                    JOIN users u         ON u.id  = asgn.assigned_by
                    ORDER BY asgn.assigned_at DESC", conn);
                using var r = cmd.ExecuteReader();
                var list = new List<object>();
                while (r.Read())
                    list.Add(new {
                        id               = r.GetInt32("id"),
                        appointment_id   = r.GetInt32("appointment_id"),
                        client_name      = r.GetString("client_name"),
                        service_name     = r.GetString("service_name"),
                        preferred_date   = r.GetDateTime("preferred_date").ToString("yyyy-MM-dd"),
                        preferred_time   = r.GetTimeSpan("preferred_time").ToString(@"hh\:mm"),
                        appt_status      = r.GetString("status"),
                        therapist_name   = r.GetString("therapist_name"),
                        specialty        = r.IsDBNull(r.GetOrdinal("specialty")) ? "" : r.GetString("specialty"),
                        assigned_by_name = r.GetString("assigned_by_name"),
                        assigned_at      = r.GetDateTime("assigned_at").ToString("yyyy-MM-dd HH:mm"),
                        notes            = r.IsDBNull(r.GetOrdinal("notes")) ? "" : r.GetString("notes"),
                    });
                return Ok(ApiResponse.Ok("OK", new { assignments = list }));
            }
            catch (Exception ex) { return Ok(ApiResponse.Fail(ex.Message)); }
        }

        // ── GET /api/assignments/unassigned ───────────────────────────────────
        [HttpGet("unassigned")]
        public IActionResult Unassigned()
        {
            if (!IsAuth(out _)) return Ok(ApiResponse.Fail("Unauthorized."));
            try
            {
                using var conn = _db.GetConnection();
                using var cmd  = new MySqlCommand(@"
                    SELECT a.id, a.client_name, s.name AS service_name,
                           a.preferred_date, a.preferred_time, a.status
                    FROM appointments a
                    JOIN services s ON s.id = a.service_id
                    LEFT JOIN assignments asgn ON asgn.appointment_id = a.id
                    WHERE asgn.id IS NULL AND a.status IN ('pending','confirmed')
                    ORDER BY a.preferred_date, a.preferred_time", conn);
                using var r = cmd.ExecuteReader();
                var list = new List<object>();
                while (r.Read())
                    list.Add(new {
                        id             = r.GetInt32("id"),
                        client_name    = r.GetString("client_name"),
                        service_name   = r.GetString("service_name"),
                        preferred_date = r.GetDateTime("preferred_date").ToString("yyyy-MM-dd"),
                        preferred_time = r.GetTimeSpan("preferred_time").ToString(@"hh\:mm"),
                        status         = r.GetString("status"),
                    });
                return Ok(ApiResponse.Ok("OK", new { appointments = list }));
            }
            catch (Exception ex) { return Ok(ApiResponse.Fail(ex.Message)); }
        }

        // ── POST /api/assignments/assign ──────────────────────────────────────
        [HttpPost("assign")]
        public IActionResult Assign([FromBody] AssignRequest req)
        {
            if (!IsAuth(out int userId)) return Ok(ApiResponse.Fail("Unauthorized."));
            if (req.AppointmentId <= 0) return Ok(ApiResponse.Fail("Appointment is required."));
            if (req.TherapistId   <= 0) return Ok(ApiResponse.Fail("Therapist is required."));
            try
            {
                using var conn = _db.GetConnection();

                // Check if already assigned
                using var chk = new MySqlCommand(
                    "SELECT id FROM assignments WHERE appointment_id=@aid LIMIT 1", conn);
                chk.Parameters.AddWithValue("@aid", req.AppointmentId);
                if (chk.ExecuteScalar() != null)
                    return Ok(ApiResponse.Fail("This appointment already has a therapist assigned."));

                using var cmd = new MySqlCommand(@"
                    INSERT INTO assignments (appointment_id, therapist_id, assigned_by, notes)
                    VALUES (@aid, @tid, @uid, @notes)", conn);
                cmd.Parameters.AddWithValue("@aid",   req.AppointmentId);
                cmd.Parameters.AddWithValue("@tid",   req.TherapistId);
                cmd.Parameters.AddWithValue("@uid",   userId);
                cmd.Parameters.AddWithValue("@notes", req.Notes?.Trim() ?? "");
                cmd.ExecuteNonQuery();

                // Auto-confirm appointment
                using var upd = new MySqlCommand(
                    "UPDATE appointments SET status='confirmed' WHERE id=@id AND status='pending'", conn);
                upd.Parameters.AddWithValue("@id", req.AppointmentId);
                upd.ExecuteNonQuery();

                return Ok(ApiResponse.Ok("Therapist assigned successfully."));
            }
            catch (Exception ex) { return Ok(ApiResponse.Fail(ex.Message)); }
        }

        // ── POST /api/assignments/reassign ────────────────────────────────────
        [HttpPost("reassign")]
        public IActionResult Reassign([FromBody] ReassignRequest req)
        {
            if (!IsAuth(out int userId)) return Ok(ApiResponse.Fail("Unauthorized."));
            try
            {
                using var conn = _db.GetConnection();
                using var cmd  = new MySqlCommand(@"
                    UPDATE assignments SET therapist_id=@tid, assigned_by=@uid, notes=@notes, assigned_at=NOW()
                    WHERE id=@id", conn);
                cmd.Parameters.AddWithValue("@tid",   req.TherapistId);
                cmd.Parameters.AddWithValue("@uid",   userId);
                cmd.Parameters.AddWithValue("@notes", req.Notes?.Trim() ?? "");
                cmd.Parameters.AddWithValue("@id",    req.Id);
                cmd.ExecuteNonQuery();
                return Ok(ApiResponse.Ok("Assignment updated."));
            }
            catch (Exception ex) { return Ok(ApiResponse.Fail(ex.Message)); }
        }
    }

    public class AssignRequest
    {
        public int     AppointmentId { get; set; }
        public int     TherapistId   { get; set; }
        public string? Notes         { get; set; }
    }
    public class ReassignRequest
    {
        public int     Id          { get; set; }
        public int     TherapistId { get; set; }
        public string? Notes       { get; set; }
    }
}
