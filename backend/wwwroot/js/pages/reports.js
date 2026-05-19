// reports.js
let currentReport = 'appointments';

function onPageLoad() {
  loadReport();
}

function switchReport(type, btn) {
  currentReport = type;
  document.querySelectorAll('.tab-btn').forEach(b => b.classList.remove('active'));
  btn.classList.add('active');
  document.getElementById('dateFilterCard').style.display = type === 'therapists' ? 'none' : 'block';
  loadReport();
}

function clearFilter() {
  document.getElementById('filterFrom').value = '';
  document.getElementById('filterTo').value   = '';
  loadReport();
}

async function loadReport() {
  const from = document.getElementById('filterFrom')?.value ?? '';
  const to   = document.getElementById('filterTo')?.value   ?? '';
  const head = document.getElementById('reportHead');
  const body = document.getElementById('reportBody');
  const foot = document.getElementById('reportFooter');
  const sumC = document.getElementById('summaryCards');

  body.innerHTML = '<tr><td colspan="9" class="no-data">Loading…</td></tr>';
  head.innerHTML = '';
  foot.innerHTML = '';
  sumC.innerHTML = '';

  try {
    let url = '';
    if (currentReport === 'appointments') {
      url = `/api/reports/appointments?from=${from}&to=${to}`;
      document.getElementById('reportTitle').textContent = 'Appointment Report';
    } else if (currentReport === 'payments') {
      url = `/api/reports/payments?from=${from}&to=${to}`;
      document.getElementById('reportTitle').textContent = 'Payment Records Report';
    } else {
      url = `/api/reports/therapists`;
      document.getElementById('reportTitle').textContent = 'Therapist Performance Report';
    }

    const res  = await fetch(url);
    const data = await res.json();
    if (!data.success) { body.innerHTML = `<tr><td colspan="9" class="no-data">${data.message}</td></tr>`; return; }

    const rows = data.data.rows;
    if (!rows.length) { body.innerHTML = '<tr><td colspan="9" class="no-data">No records found for the selected period.</td></tr>'; return; }

    if (currentReport === 'appointments') renderAppointments(rows, head, body, foot, sumC);
    else if (currentReport === 'payments') renderPayments(rows, head, body, foot, sumC);
    else renderTherapists(rows, head, body, foot, sumC);

  } catch (_) {
    body.innerHTML = '<tr><td colspan="9" class="no-data">Error loading report.</td></tr>';
  }
}

// ── Render: Appointments ────────────────────────────────────────────────────
function renderAppointments(rows, head, body, foot, sumC) {
  head.innerHTML = `<tr>
    <th>#</th><th>Client</th><th>Service</th><th>Price</th>
    <th>Date</th><th>Status</th><th>Therapist</th>
  </tr>`;

  body.innerHTML = rows.map((r, i) => `
    <tr>
      <td style="color:var(--gray-mid);font-size:.8rem">${i+1}</td>
      <td><strong>${esc(r.client_name)}</strong></td>
      <td>${esc(r.service_name)}</td>
      <td>${peso(r.price)}</td>
      <td>${r.preferred_date}</td>
      <td><span class="badge badge-${r.status}">${r.status}</span></td>
      <td>${esc(r.therapist_name)}</td>
    </tr>`).join('');

  const total     = rows.reduce((s, r) => s + parseFloat(r.price), 0);
  const completed = rows.filter(r => r.status === 'completed').length;
  const pending   = rows.filter(r => r.status === 'pending').length;

  foot.innerHTML = `Total records: <strong>${rows.length}</strong> &nbsp;|&nbsp; Completed: <strong>${completed}</strong> &nbsp;|&nbsp; Pending: <strong>${pending}</strong> &nbsp;|&nbsp; Total Value: <strong>${peso(total)}</strong>`;

  sumC.innerHTML = `
    <div class="summary-card"><div class="s-label">Total Bookings</div><div class="s-value">${rows.length}</div></div>
    <div class="summary-card"><div class="s-label">Completed</div><div class="s-value">${completed}</div></div>
    <div class="summary-card"><div class="s-label">Pending</div><div class="s-value">${pending}</div></div>
    <div class="summary-card"><div class="s-label">Total Value</div><div class="s-value">${peso(total)}</div></div>`;
}

// ── Render: Payments ────────────────────────────────────────────────────────
function renderPayments(rows, head, body, foot, sumC) {
  head.innerHTML = `<tr>
    <th>#</th><th>Client</th><th>Service</th><th>Amount</th>
    <th>Method</th><th>Status</th><th>Processed At</th><th>Processed By</th>
  </tr>`;

  body.innerHTML = rows.map((r, i) => `
    <tr>
      <td style="color:var(--gray-mid);font-size:.8rem">${i+1}</td>
      <td><strong>${esc(r.client_name)}</strong></td>
      <td>${esc(r.service_name)}</td>
      <td><strong>${peso(r.amount)}</strong></td>
      <td><span class="badge badge-${r.method}">${r.method.replace('_',' ')}</span></td>
      <td><span class="badge badge-${r.status}">${r.status}</span></td>
      <td style="font-size:.82rem">${r.processed_at}</td>
      <td>${esc(r.processed_by)}</td>
    </tr>`).join('');

  const totalRev  = rows.filter(r => r.status === 'completed').reduce((s, r) => s + parseFloat(r.amount), 0);
  const refunded  = rows.filter(r => r.status === 'refunded').reduce((s, r) => s + parseFloat(r.amount), 0);

  foot.innerHTML = `Total records: <strong>${rows.length}</strong> &nbsp;|&nbsp; Total Revenue: <strong>${peso(totalRev)}</strong> &nbsp;|&nbsp; Refunded: <strong>${peso(refunded)}</strong>`;

  sumC.innerHTML = `
    <div class="summary-card"><div class="s-label">Total Transactions</div><div class="s-value">${rows.length}</div></div>
    <div class="summary-card"><div class="s-label">Total Revenue</div><div class="s-value">${peso(totalRev)}</div></div>
    <div class="summary-card"><div class="s-label">Refunded</div><div class="s-value">${peso(refunded)}</div></div>
    <div class="summary-card"><div class="s-label">Net Revenue</div><div class="s-value">${peso(totalRev - refunded)}</div></div>`;
}

// ── Render: Therapists ──────────────────────────────────────────────────────
function renderTherapists(rows, head, body, foot, sumC) {
  head.innerHTML = `<tr>
    <th>#</th><th>Therapist</th><th>Specialty</th>
    <th>Assignments</th><th>Completed</th><th>Rate</th><th>Revenue</th>
  </tr>`;

  body.innerHTML = rows.map((r, i) => {
    const rate = r.total_assignments > 0
      ? ((r.completed / r.total_assignments) * 100).toFixed(1) + '%'
      : '—';
    return `<tr>
      <td style="color:var(--gray-mid);font-size:.8rem">${i+1}</td>
      <td><strong>${esc(r.full_name)}</strong></td>
      <td><span class="badge badge-staff">${esc(r.specialty)}</span></td>
      <td style="text-align:center">${r.total_assignments}</td>
      <td style="text-align:center">${r.completed}</td>
      <td style="text-align:center"><strong>${rate}</strong></td>
      <td><strong>${peso(r.total_revenue)}</strong></td>
    </tr>`;
  }).join('');

  const totalRev   = rows.reduce((s, r) => s + parseFloat(r.total_revenue), 0);
  const totalAsgn  = rows.reduce((s, r) => s + r.total_assignments, 0);
  const totalComp  = rows.reduce((s, r) => s + r.completed, 0);
  const topEarner  = rows[0]?.full_name ?? '—';

  foot.innerHTML = `Therapists: <strong>${rows.length}</strong> &nbsp;|&nbsp; Total Assignments: <strong>${totalAsgn}</strong> &nbsp;|&nbsp; Total Revenue: <strong>${peso(totalRev)}</strong>`;

  sumC.innerHTML = `
    <div class="summary-card"><div class="s-label">Active Therapists</div><div class="s-value">${rows.length}</div></div>
    <div class="summary-card"><div class="s-label">Total Assignments</div><div class="s-value">${totalAsgn}</div></div>
    <div class="summary-card"><div class="s-label">Completed Sessions</div><div class="s-value">${totalComp}</div></div>
    <div class="summary-card"><div class="s-label">Total Revenue</div><div class="s-value">${peso(totalRev)}</div></div>
    <div class="summary-card"><div class="s-label">Top Earner</div><div class="s-value" style="font-size:1rem">${esc(topEarner)}</div></div>`;
}

// ── Export to Excel ─────────────────────────────────────────────────────────
function exportExcel() {
  const from = document.getElementById('filterFrom')?.value ?? '';
  const to   = document.getElementById('filterTo')?.value   ?? '';

  let url = '';
  if (currentReport === 'appointments') {
    url = `/api/reports/export/appointments?from=${from}&to=${to}`;
  } else if (currentReport === 'payments') {
    url = `/api/reports/export/payments?from=${from}&to=${to}`;
  } else {
    url = `/api/reports/export/therapists`;
  }

  // Trigger download
  const link = document.createElement('a');
  link.href  = url;
  link.click();
  toast('Generating Excel report…');
}
