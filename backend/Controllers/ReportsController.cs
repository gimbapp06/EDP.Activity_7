// =====================================================
// Controllers/ReportsController.cs
// Report Generation + Excel Export (EPPlus)
// with Company Header, Logo, Signature, Chart
// =====================================================
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using OfficeOpenXml.Drawing.Chart;
using System.Drawing;
using ThbmsApi.Models;
using ThbmsApi.Services;

namespace ThbmsApi.Controllers
{
    [ApiController]
    [Route("api/reports")]
    public class ReportsController : ControllerBase
    {
        private readonly Database _db;
        public ReportsController(Database db)
        {
            _db = db;
            // EPPlus 7 requires license context for non-commercial use
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        private bool IsAuth(out string userName)
        {
            userName = HttpContext.Session.GetString("user_full_name") ?? "Administrator";
            return !string.IsNullOrEmpty(HttpContext.Session.GetString("user_id"));
        }

        // ─────────────────────────────────────────────────────────────────────
        // HELPER: Build a styled header on any worksheet
        // ─────────────────────────────────────────────────────────────────────
        private static void BuildHeader(ExcelWorksheet ws, string title, string subtitle = "")
        {
            // Row 1 — Company Name
            ws.Cells["A1:H1"].Merge = true;
            ws.Cells["A1"].Value = "TRANQUILITY HAVEN BOOKING MANAGEMENT SYSTEM";
            ws.Cells["A1"].Style.Font.Size = 16;
            ws.Cells["A1"].Style.Font.Bold = true;
            ws.Cells["A1"].Style.Font.Color.SetColor(Color.White);
            ws.Cells["A1"].Style.Fill.PatternType = ExcelFillStyle.Solid;
            ws.Cells["A1"].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(26, 61, 43));
            ws.Cells["A1"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            ws.Cells["A1"].Style.VerticalAlignment   = ExcelVerticalAlignment.Center;
            ws.Row(1).Height = 36;

            // Row 2 — Address
            ws.Cells["A2:H2"].Merge = true;
            ws.Cells["A2"].Value = "Legazpi City, Albay, Philippines | Tel: (052) 123-4567 | thbms@tranquilityhaven.ph";
            ws.Cells["A2"].Style.Font.Size = 9;
            ws.Cells["A2"].Style.Font.Italic = true;
            ws.Cells["A2"].Style.Font.Color.SetColor(Color.White);
            ws.Cells["A2"].Style.Fill.PatternType = ExcelFillStyle.Solid;
            ws.Cells["A2"].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(45, 106, 79));
            ws.Cells["A2"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            ws.Row(2).Height = 18;

            // Row 3 — Report Title
            ws.Cells["A3:H3"].Merge = true;
            ws.Cells["A3"].Value = title;
            ws.Cells["A3"].Style.Font.Size = 13;
            ws.Cells["A3"].Style.Font.Bold = true;
            ws.Cells["A3"].Style.Font.Color.SetColor(Color.FromArgb(26, 61, 43));
            ws.Cells["A3"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            ws.Row(3).Height = 24;

            // Row 4 — Subtitle / Date
            ws.Cells["A4:H4"].Merge = true;
            ws.Cells["A4"].Value = string.IsNullOrEmpty(subtitle)
                ? $"Generated: {DateTime.Now:MMMM dd, yyyy  hh:mm tt}"
                : subtitle;
            ws.Cells["A4"].Style.Font.Size = 9;
            ws.Cells["A4"].Style.Font.Color.SetColor(Color.Gray);
            ws.Cells["A4"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            ws.Row(4).Height = 16;

            // Spacer row 5
            ws.Row(5).Height = 8;
        }

        // ─────────────────────────────────────────────────────────────────────
        // HELPER: Build signature section at the bottom
        // ─────────────────────────────────────────────────────────────────────
        private static void BuildSignature(ExcelWorksheet ws, int startRow, string signerName)
        {
            startRow += 2; // blank gap

            ws.Cells[startRow, 1].Value = "Prepared by:";
            ws.Cells[startRow, 1].Style.Font.Bold = true;

            startRow++;
            // Signature line
            ws.Cells[startRow, 1, startRow, 3].Merge = true;
            ws.Cells[startRow, 1].Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            ws.Row(startRow).Height = 28;

            startRow++;
            ws.Cells[startRow, 1, startRow, 3].Merge = true;
            ws.Cells[startRow, 1].Value = signerName;
            ws.Cells[startRow, 1].Style.Font.Bold = true;
            ws.Cells[startRow, 1].Style.Font.Size = 10;

            startRow++;
            ws.Cells[startRow, 1, startRow, 3].Merge = true;
            ws.Cells[startRow, 1].Value = "Authorized Signatory — TranquilityHaven BMSF";
            ws.Cells[startRow, 1].Style.Font.Italic = true;
            ws.Cells[startRow, 1].Style.Font.Size = 9;
            ws.Cells[startRow, 1].Style.Font.Color.SetColor(Color.Gray);

            startRow += 2;
            ws.Cells[startRow, 1, startRow, 3].Merge = true;
            ws.Cells[startRow, 1].Value = $"Date: {DateTime.Now:MMMM dd, yyyy}";
            ws.Cells[startRow, 1].Style.Font.Size = 9;
        }

        // ─────────────────────────────────────────────────────────────────────
        // HELPER: Style column headers (row 6 by convention)
        // ─────────────────────────────────────────────────────────────────────
        private static void StyleColumnHeaders(ExcelWorksheet ws, int headerRow, int colCount)
        {
            for (int c = 1; c <= colCount; c++)
            {
                var cell = ws.Cells[headerRow, c];
                cell.Style.Font.Bold = true;
                cell.Style.Font.Color.SetColor(Color.White);
                cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(64, 145, 108));
                cell.Style.Border.BorderAround(ExcelBorderStyle.Thin, Color.FromArgb(26, 61, 43));
                cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // HELPER: Zebra-stripe data rows
        // ─────────────────────────────────────────────────────────────────────
        private static void StyleDataRows(ExcelWorksheet ws, int startRow, int endRow, int colCount)
        {
            for (int r = startRow; r <= endRow; r++)
            {
                bool even = (r - startRow) % 2 == 1;
                for (int c = 1; c <= colCount; c++)
                {
                    var cell = ws.Cells[r, c];
                    if (even)
                    {
                        cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                        cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(216, 243, 220));
                    }
                    cell.Style.Border.BorderAround(ExcelBorderStyle.Hair, Color.LightGray);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET /api/reports/appointments — data for datagrid
        // ─────────────────────────────────────────────────────────────────────
        [HttpGet("appointments")]
        public IActionResult AppointmentsReport([FromQuery] string from = "", [FromQuery] string to = "")
        {
            if (!IsAuth(out _)) return Ok(ApiResponse.Fail("Unauthorized."));
            try
            {
                using var conn = _db.GetConnection();
                var sql = @"SELECT a.id, a.client_name, s.name AS service_name,
                                   s.price, a.preferred_date, a.status,
                                   COALESCE(t.full_name,'Unassigned') AS therapist_name
                            FROM appointments a
                            JOIN services s ON s.id = a.service_id
                            LEFT JOIN assignments asgn ON asgn.appointment_id = a.id
                            LEFT JOIN therapists t ON t.id = asgn.therapist_id
                            WHERE 1=1";
                if (!string.IsNullOrWhiteSpace(from)) sql += " AND a.preferred_date >= @from";
                if (!string.IsNullOrWhiteSpace(to))   sql += " AND a.preferred_date <= @to";
                sql += " ORDER BY a.preferred_date";

                using var cmd = new MySqlCommand(sql, conn);
                if (!string.IsNullOrWhiteSpace(from)) cmd.Parameters.AddWithValue("@from", from);
                if (!string.IsNullOrWhiteSpace(to))   cmd.Parameters.AddWithValue("@to", to);

                using var r = cmd.ExecuteReader();
                var list = new List<object>();
                while (r.Read())
                    list.Add(new {
                        id             = r.GetInt32("id"),
                        client_name    = r.GetString("client_name"),
                        service_name   = r.GetString("service_name"),
                        price          = r.GetDecimal("price"),
                        preferred_date = r.GetDateTime("preferred_date").ToString("yyyy-MM-dd"),
                        status         = r.GetString("status"),
                        therapist_name = r.GetString("therapist_name"),
                    });
                return Ok(ApiResponse.Ok("OK", new { rows = list }));
            }
            catch (Exception ex) { return Ok(ApiResponse.Fail(ex.Message)); }
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET /api/reports/payments — data for datagrid
        // ─────────────────────────────────────────────────────────────────────
        [HttpGet("payments")]
        public IActionResult PaymentsReport([FromQuery] string from = "", [FromQuery] string to = "")
        {
            if (!IsAuth(out _)) return Ok(ApiResponse.Fail("Unauthorized."));
            try
            {
                using var conn = _db.GetConnection();
                var sql = @"SELECT p.id, a.client_name, s.name AS service_name,
                                   p.amount, p.method, p.status,
                                   p.processed_at, u.full_name AS processed_by
                            FROM payments p
                            JOIN appointments a ON a.id = p.appointment_id
                            JOIN services s     ON s.id = a.service_id
                            JOIN users u        ON u.id = p.processed_by
                            WHERE 1=1";
                if (!string.IsNullOrWhiteSpace(from)) sql += " AND DATE(p.processed_at) >= @from";
                if (!string.IsNullOrWhiteSpace(to))   sql += " AND DATE(p.processed_at) <= @to";
                sql += " ORDER BY p.processed_at DESC";

                using var cmd = new MySqlCommand(sql, conn);
                if (!string.IsNullOrWhiteSpace(from)) cmd.Parameters.AddWithValue("@from", from);
                if (!string.IsNullOrWhiteSpace(to))   cmd.Parameters.AddWithValue("@to", to);

                using var r = cmd.ExecuteReader();
                var list = new List<object>();
                while (r.Read())
                    list.Add(new {
                        id           = r.GetInt32("id"),
                        client_name  = r.GetString("client_name"),
                        service_name = r.GetString("service_name"),
                        amount       = r.GetDecimal("amount"),
                        method       = r.GetString("method"),
                        status       = r.GetString("status"),
                        processed_at = r.GetDateTime("processed_at").ToString("yyyy-MM-dd HH:mm"),
                        processed_by = r.GetString("processed_by"),
                    });
                return Ok(ApiResponse.Ok("OK", new { rows = list }));
            }
            catch (Exception ex) { return Ok(ApiResponse.Fail(ex.Message)); }
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET /api/reports/therapists — data for datagrid
        // ─────────────────────────────────────────────────────────────────────
        [HttpGet("therapists")]
        public IActionResult TherapistsReport()
        {
            if (!IsAuth(out _)) return Ok(ApiResponse.Fail("Unauthorized."));
            try
            {
                using var conn = _db.GetConnection();
                using var cmd  = new MySqlCommand(@"
                    SELECT t.full_name, t.specialty,
                           COUNT(asgn.id) AS total_assignments,
                           SUM(CASE WHEN a.status='completed' THEN 1 ELSE 0 END) AS completed,
                           COALESCE(SUM(CASE WHEN p.status='completed' THEN p.amount ELSE 0 END),0) AS total_revenue
                    FROM therapists t
                    LEFT JOIN assignments asgn ON asgn.therapist_id = t.id
                    LEFT JOIN appointments a   ON a.id = asgn.appointment_id
                    LEFT JOIN payments p       ON p.appointment_id = a.id
                    WHERE t.status = 'active'
                    GROUP BY t.id, t.full_name, t.specialty
                    ORDER BY total_revenue DESC", conn);
                using var r = cmd.ExecuteReader();
                var list = new List<object>();
                while (r.Read())
                    list.Add(new {
                        full_name         = r.GetString("full_name"),
                        specialty         = r.IsDBNull(r.GetOrdinal("specialty")) ? "" : r.GetString("specialty"),
                        total_assignments = r.GetInt32("total_assignments"),
                        completed         = r.IsDBNull(r.GetOrdinal("completed")) ? 0 : r.GetInt32("completed"),
                        total_revenue     = r.GetDecimal("total_revenue"),
                    });
                return Ok(ApiResponse.Ok("OK", new { rows = list }));
            }
            catch (Exception ex) { return Ok(ApiResponse.Fail(ex.Message)); }
        }

        // ═════════════════════════════════════════════════════════════════════
        // EXCEL EXPORTS
        // ═════════════════════════════════════════════════════════════════════

        // ── GET /api/reports/export/appointments ──────────────────────────────
        [HttpGet("export/appointments")]
        public IActionResult ExportAppointments([FromQuery] string from = "", [FromQuery] string to = "")
        {
            if (!IsAuth(out string signerName)) return Unauthorized();
            try
            {
                using var conn = _db.GetConnection();
                var sql = @"SELECT a.id, a.client_name, a.client_phone, s.name AS service_name,
                                   s.price, a.preferred_date, a.preferred_time, a.status,
                                   COALESCE(t.full_name,'Unassigned') AS therapist_name
                            FROM appointments a
                            JOIN services s ON s.id = a.service_id
                            LEFT JOIN assignments asgn ON asgn.appointment_id = a.id
                            LEFT JOIN therapists t ON t.id = asgn.therapist_id
                            WHERE 1=1";
                if (!string.IsNullOrWhiteSpace(from)) sql += " AND a.preferred_date >= @from";
                if (!string.IsNullOrWhiteSpace(to))   sql += " AND a.preferred_date <= @to";
                sql += " ORDER BY a.preferred_date";

                using var cmd = new MySqlCommand(sql, conn);
                if (!string.IsNullOrWhiteSpace(from)) cmd.Parameters.AddWithValue("@from", from);
                if (!string.IsNullOrWhiteSpace(to))   cmd.Parameters.AddWithValue("@to", to);

                using var r = cmd.ExecuteReader();
                var rows = new List<(int id, string client, string phone, string service,
                                     decimal price, string date, string time, string status, string therapist)>();
                while (r.Read())
                    rows.Add((r.GetInt32("id"), r.GetString("client_name"),
                              r.IsDBNull(r.GetOrdinal("client_phone")) ? "" : r.GetString("client_phone"),
                              r.GetString("service_name"), r.GetDecimal("price"),
                              r.GetDateTime("preferred_date").ToString("yyyy-MM-dd"),
                              r.GetTimeSpan("preferred_time").ToString(@"hh\:mm"),
                              r.GetString("status"), r.GetString("therapist_name")));
                r.Close();

                using var pkg = new ExcelPackage();

                // ── Sheet 1: Data ──────────────────────────────────────────────
                var ws = pkg.Workbook.Worksheets.Add("Appointments");
                BuildHeader(ws, "APPOINTMENT REPORT",
                    $"Period: {(string.IsNullOrEmpty(from) ? "All Time" : from)} to {(string.IsNullOrEmpty(to) ? "Present" : to)}");

                // Column headers at row 6
                int hRow = 6;
                string[] headers = { "#", "Client Name", "Phone", "Service", "Price (₱)", "Date", "Time", "Status", "Therapist" };
                for (int i = 0; i < headers.Length; i++)
                    ws.Cells[hRow, i + 1].Value = headers[i];
                StyleColumnHeaders(ws, hRow, headers.Length);

                // Data rows
                int dataStart = hRow + 1;
                for (int i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    int r2 = dataStart + i;
                    ws.Cells[r2, 1].Value = row.id;
                    ws.Cells[r2, 2].Value = row.client;
                    ws.Cells[r2, 3].Value = row.phone;
                    ws.Cells[r2, 4].Value = row.service;
                    ws.Cells[r2, 5].Value = (double)row.price;
                    ws.Cells[r2, 5].Style.Numberformat.Format = "#,##0.00";
                    ws.Cells[r2, 6].Value = row.date;
                    ws.Cells[r2, 7].Value = row.time;
                    ws.Cells[r2, 8].Value = row.status;
                    ws.Cells[r2, 9].Value = row.therapist;
                }
                int dataEnd = dataStart + rows.Count - 1;
                if (rows.Count > 0) StyleDataRows(ws, dataStart, dataEnd, headers.Length);

                // Totals row
                int totRow = dataEnd + 1;
                ws.Cells[totRow, 1, totRow, 4].Merge = true;
                ws.Cells[totRow, 1].Value = "TOTAL";
                ws.Cells[totRow, 1].Style.Font.Bold = true;
                ws.Cells[totRow, 5].Formula = $"SUM(E{dataStart}:E{dataEnd})";
                ws.Cells[totRow, 5].Style.Numberformat.Format = "#,##0.00";
                ws.Cells[totRow, 5].Style.Font.Bold = true;

                ws.Cells[ws.Dimension.Address].AutoFitColumns();
                BuildSignature(ws, totRow, signerName);

                // ── Sheet 2: Chart ─────────────────────────────────────────────
                var wsChart = pkg.Workbook.Worksheets.Add("Chart");
                BuildHeader(wsChart, "APPOINTMENT STATUS CHART");

                // Count by status for chart data
                var statusGroups = rows.GroupBy(x => x.status)
                                       .Select(g => (status: g.Key, count: g.Count()))
                                       .ToList();
                wsChart.Cells["A6"].Value = "Status";
                wsChart.Cells["B6"].Value = "Count";
                wsChart.Cells["A6"].Style.Font.Bold = true;
                wsChart.Cells["B6"].Style.Font.Bold = true;
                for (int i = 0; i < statusGroups.Count; i++)
                {
                    wsChart.Cells[7 + i, 1].Value = statusGroups[i].status;
                    wsChart.Cells[7 + i, 2].Value = statusGroups[i].count;
                }

                var chart = wsChart.Drawings.AddChart("AppointmentStatusChart", eChartType.BarClustered);
                chart.Title.Text = "Appointments by Status";
                chart.SetPosition(10, 0, 1, 0);
                chart.SetSize(520, 320);
                var series = chart.Series.Add(
                    wsChart.Cells[7, 2, 7 + statusGroups.Count - 1, 2],
                    wsChart.Cells[7, 1, 7 + statusGroups.Count - 1, 1]);
                series.Header = "Count";

                var bytes = pkg.GetAsByteArray();
                return File(bytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"Appointments_Report_{DateTime.Now:yyyyMMdd}.xlsx");
            }
            catch (Exception ex) { return Ok(ApiResponse.Fail(ex.Message)); }
        }

        // ── GET /api/reports/export/payments ──────────────────────────────────
        [HttpGet("export/payments")]
        public IActionResult ExportPayments([FromQuery] string from = "", [FromQuery] string to = "")
        {
            if (!IsAuth(out string signerName)) return Unauthorized();
            try
            {
                using var conn = _db.GetConnection();
                var sql = @"SELECT p.id, a.client_name, s.name AS service_name,
                                   p.amount, p.method, p.status, p.processed_at, u.full_name AS processed_by
                            FROM payments p
                            JOIN appointments a ON a.id = p.appointment_id
                            JOIN services s ON s.id = a.service_id
                            JOIN users u ON u.id = p.processed_by
                            WHERE 1=1";
                if (!string.IsNullOrWhiteSpace(from)) sql += " AND DATE(p.processed_at) >= @from";
                if (!string.IsNullOrWhiteSpace(to))   sql += " AND DATE(p.processed_at) <= @to";
                sql += " ORDER BY p.processed_at DESC";

                using var cmd = new MySqlCommand(sql, conn);
                if (!string.IsNullOrWhiteSpace(from)) cmd.Parameters.AddWithValue("@from", from);
                if (!string.IsNullOrWhiteSpace(to))   cmd.Parameters.AddWithValue("@to", to);

                using var r = cmd.ExecuteReader();
                var rows = new List<(int id, string client, string service, decimal amount, string method, string status, string processedAt, string processedBy)>();
                while (r.Read())
                    rows.Add((r.GetInt32("id"), r.GetString("client_name"), r.GetString("service_name"),
                              r.GetDecimal("amount"), r.GetString("method"), r.GetString("status"),
                              r.GetDateTime("processed_at").ToString("yyyy-MM-dd HH:mm"), r.GetString("processed_by")));
                r.Close();

                using var pkg = new ExcelPackage();

                // Sheet 1
                var ws = pkg.Workbook.Worksheets.Add("Payments");
                BuildHeader(ws, "PAYMENT RECORDS REPORT",
                    $"Period: {(string.IsNullOrEmpty(from) ? "All Time" : from)} to {(string.IsNullOrEmpty(to) ? "Present" : to)}");

                int hRow = 6;
                string[] headers = { "#", "Client Name", "Service", "Amount (₱)", "Method", "Status", "Processed At", "Processed By" };
                for (int i = 0; i < headers.Length; i++)
                    ws.Cells[hRow, i + 1].Value = headers[i];
                StyleColumnHeaders(ws, hRow, headers.Length);

                int dataStart = hRow + 1;
                for (int i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    int r2 = dataStart + i;
                    ws.Cells[r2, 1].Value = row.id;
                    ws.Cells[r2, 2].Value = row.client;
                    ws.Cells[r2, 3].Value = row.service;
                    ws.Cells[r2, 4].Value = (double)row.amount;
                    ws.Cells[r2, 4].Style.Numberformat.Format = "#,##0.00";
                    ws.Cells[r2, 5].Value = row.method;
                    ws.Cells[r2, 6].Value = row.status;
                    ws.Cells[r2, 7].Value = row.processedAt;
                    ws.Cells[r2, 8].Value = row.processedBy;
                }
                int dataEnd = dataStart + rows.Count - 1;
                if (rows.Count > 0) StyleDataRows(ws, dataStart, dataEnd, headers.Length);

                int totRow = dataEnd + 1;
                ws.Cells[totRow, 1, totRow, 3].Merge = true;
                ws.Cells[totRow, 1].Value = "TOTAL REVENUE";
                ws.Cells[totRow, 1].Style.Font.Bold = true;
                ws.Cells[totRow, 4].Formula = $"SUM(D{dataStart}:D{dataEnd})";
                ws.Cells[totRow, 4].Style.Numberformat.Format = "#,##0.00";
                ws.Cells[totRow, 4].Style.Font.Bold = true;

                ws.Cells[ws.Dimension.Address].AutoFitColumns();
                BuildSignature(ws, totRow, signerName);

                // Sheet 2: Chart — Revenue by Payment Method
                var wsChart = pkg.Workbook.Worksheets.Add("Chart");
                BuildHeader(wsChart, "REVENUE BY PAYMENT METHOD");

                var methodGroups = rows.GroupBy(x => x.method)
                                        .Select(g => (method: g.Key, total: g.Sum(x => x.amount)))
                                        .ToList();
                wsChart.Cells["A6"].Value = "Method"; wsChart.Cells["A6"].Style.Font.Bold = true;
                wsChart.Cells["B6"].Value = "Total (₱)"; wsChart.Cells["B6"].Style.Font.Bold = true;
                for (int i = 0; i < methodGroups.Count; i++)
                {
                    wsChart.Cells[7 + i, 1].Value = methodGroups[i].method;
                    wsChart.Cells[7 + i, 2].Value = (double)methodGroups[i].total;
                    wsChart.Cells[7 + i, 2].Style.Numberformat.Format = "#,##0.00";
                }

                var chart = wsChart.Drawings.AddChart("RevenueByMethodChart", eChartType.Pie3D);
                chart.Title.Text = "Revenue by Payment Method";
                chart.SetPosition(10, 0, 1, 0);
                chart.SetSize(500, 320);
                var series = chart.Series.Add(
                    wsChart.Cells[7, 2, 7 + methodGroups.Count - 1, 2],
                    wsChart.Cells[7, 1, 7 + methodGroups.Count - 1, 1]);
                series.Header = "Revenue";

                var bytes = pkg.GetAsByteArray();
                return File(bytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"Payments_Report_{DateTime.Now:yyyyMMdd}.xlsx");
            }
            catch (Exception ex) { return Ok(ApiResponse.Fail(ex.Message)); }
        }

        // ── GET /api/reports/export/therapists ────────────────────────────────
        [HttpGet("export/therapists")]
        public IActionResult ExportTherapists()
        {
            if (!IsAuth(out string signerName)) return Unauthorized();
            try
            {
                using var conn = _db.GetConnection();
                using var cmd  = new MySqlCommand(@"
                    SELECT t.full_name, t.specialty,
                           COUNT(asgn.id) AS total_assignments,
                           SUM(CASE WHEN a.status='completed' THEN 1 ELSE 0 END) AS completed,
                           COALESCE(SUM(CASE WHEN p.status='completed' THEN p.amount ELSE 0 END),0) AS total_revenue
                    FROM therapists t
                    LEFT JOIN assignments asgn ON asgn.therapist_id = t.id
                    LEFT JOIN appointments a   ON a.id = asgn.appointment_id
                    LEFT JOIN payments p       ON p.appointment_id = a.id
                    WHERE t.status='active'
                    GROUP BY t.id, t.full_name, t.specialty
                    ORDER BY total_revenue DESC", conn);
                using var r = cmd.ExecuteReader();
                var rows = new List<(string name, string specialty, int total, int completed, decimal revenue)>();
                while (r.Read())
                    rows.Add((r.GetString("full_name"),
                              r.IsDBNull(r.GetOrdinal("specialty")) ? "" : r.GetString("specialty"),
                              r.GetInt32("total_assignments"),
                              r.IsDBNull(r.GetOrdinal("completed")) ? 0 : r.GetInt32("completed"),
                              r.GetDecimal("total_revenue")));
                r.Close();

                using var pkg = new ExcelPackage();

                // Sheet 1
                var ws = pkg.Workbook.Worksheets.Add("Therapist Performance");
                BuildHeader(ws, "THERAPIST PERFORMANCE REPORT",
                    $"Generated: {DateTime.Now:MMMM dd, yyyy}");

                int hRow = 6;
                string[] headers = { "Therapist Name", "Specialty", "Total Assignments", "Completed", "Completion Rate", "Total Revenue (₱)" };
                for (int i = 0; i < headers.Length; i++)
                    ws.Cells[hRow, i + 1].Value = headers[i];
                StyleColumnHeaders(ws, hRow, headers.Length);

                int dataStart = hRow + 1;
                for (int i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    int r2 = dataStart + i;
                    ws.Cells[r2, 1].Value = row.name;
                    ws.Cells[r2, 2].Value = row.specialty;
                    ws.Cells[r2, 3].Value = row.total;
                    ws.Cells[r2, 4].Value = row.completed;
                    double rate = row.total > 0 ? (double)row.completed / row.total : 0;
                    ws.Cells[r2, 5].Value = rate;
                    ws.Cells[r2, 5].Style.Numberformat.Format = "0.0%";
                    ws.Cells[r2, 6].Value = (double)row.revenue;
                    ws.Cells[r2, 6].Style.Numberformat.Format = "#,##0.00";
                }
                int dataEnd = dataStart + rows.Count - 1;
                if (rows.Count > 0) StyleDataRows(ws, dataStart, dataEnd, headers.Length);

                int totRow = dataEnd + 1;
                ws.Cells[totRow, 1, totRow, 5].Merge = true;
                ws.Cells[totRow, 1].Value = "TOTAL REVENUE";
                ws.Cells[totRow, 1].Style.Font.Bold = true;
                ws.Cells[totRow, 6].Formula = $"SUM(F{dataStart}:F{dataEnd})";
                ws.Cells[totRow, 6].Style.Numberformat.Format = "#,##0.00";
                ws.Cells[totRow, 6].Style.Font.Bold = true;

                ws.Cells[ws.Dimension.Address].AutoFitColumns();
                BuildSignature(ws, totRow, signerName);

                // Sheet 2: Bar chart — Revenue per Therapist
                var wsChart = pkg.Workbook.Worksheets.Add("Chart");
                BuildHeader(wsChart, "REVENUE PER THERAPIST CHART");

                wsChart.Cells["A6"].Value = "Therapist"; wsChart.Cells["A6"].Style.Font.Bold = true;
                wsChart.Cells["B6"].Value = "Revenue (₱)"; wsChart.Cells["B6"].Style.Font.Bold = true;
                for (int i = 0; i < rows.Count; i++)
                {
                    wsChart.Cells[7 + i, 1].Value = rows[i].name;
                    wsChart.Cells[7 + i, 2].Value = (double)rows[i].revenue;
                    wsChart.Cells[7 + i, 2].Style.Numberformat.Format = "#,##0.00";
                }

                var chart = wsChart.Drawings.AddChart("TherapistRevenueChart", eChartType.ColumnClustered);
                chart.Title.Text = "Revenue per Therapist";
                chart.SetPosition(10, 0, 1, 0);
                chart.SetSize(560, 340);
                var series = chart.Series.Add(
                    wsChart.Cells[7, 2, 7 + rows.Count - 1, 2],
                    wsChart.Cells[7, 1, 7 + rows.Count - 1, 1]);
                series.Header = "Revenue (₱)";

                var bytes = pkg.GetAsByteArray();
                return File(bytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"Therapists_Report_{DateTime.Now:yyyyMMdd}.xlsx");
            }
            catch (Exception ex) { return Ok(ApiResponse.Fail(ex.Message)); }
        }
    }
}
