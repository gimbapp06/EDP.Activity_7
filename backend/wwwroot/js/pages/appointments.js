// appointments.js
let services = [];

async function onPageLoad() {
  await loadServices();
  loadData();
}

async function loadServices() {
  try {
    const res  = await fetch(`${API}/appointments/services`);
    const data = await res.json();
    if (!data.success) return;
    services = data.data.services;
    const sel = document.getElementById('aService');
    sel.innerHTML = '<option value="">— Select Service —</option>';
    services.forEach(s => {
      sel.innerHTML += `<option value="${s.id}">${esc(s.name)} (${peso(s.price)} / ${s.duration_minutes} min)</option>`;
    });
  } catch (_) {}
}

async function loadData() {
  const search = document.getElementById('searchInput')?.value.trim() ?? '';
  const status = document.getElementById('statusFilter')?.value ?? '';
  const tbody  = document.getElementById('dataTable');
  tbody.innerHTML = '<tr><td colspan="9" class="no-data">Loading…</td></tr>';
  try {
    const res  = await fetch(`${API}/appointments/list?search=${encodeURIComponent(search)}&status=${status}`);
    const data = await res.json();
    if (!data.success || !data.data.appointments.length) {
      tbody.innerHTML = '<tr><td colspan="9" class="no-data">No appointments found.</td></tr>'; return;
    }
    tbody.innerHTML = data.data.appointments.map((a, i) => `
      <tr>
        <td style="color:var(--gray-mid);font-size:.8rem">${i + 1}</td>
        <td><strong>${esc(a.client_name)}</strong><br><small style="color:var(--gray-mid)">${esc(a.client_email)}</small></td>
        <td>${esc(a.service_name)}</td>
        <td>${peso(a.price)}</td>
        <td>${a.preferred_date}</td>
        <td>${a.preferred_time}</td>
        <td>${esc(a.therapist_name)}</td>
        <td><span class="badge badge-${a.status}">${a.status}</span></td>
        <td>
          <div class="actions-cell">
            ${a.status === 'pending'    ? `<button class="btn btn-primary btn-sm" onclick="updateStatus(${a.id},'confirmed')">Confirm</button>` : ''}
            ${a.status === 'confirmed'  ? `<button class="btn btn-primary btn-sm" onclick="updateStatus(${a.id},'completed')">Complete</button>` : ''}
            ${['pending','confirmed'].includes(a.status) ? `<button class="btn btn-danger btn-sm" onclick="updateStatus(${a.id},'cancelled')">Cancel</button>` : ''}
          </div>
        </td>
      </tr>`).join('');
  } catch (_) {
    tbody.innerHTML = '<tr><td colspan="9" class="no-data">Error loading data.</td></tr>';
  }
}

function openAddModal() {
  hideAlert('addAlert');
  ['aClientName','aClientPhone','aClientEmail','aNote'].forEach(id => document.getElementById(id).value = '');
  document.getElementById('aService').value = '';
  document.getElementById('aDate').value = new Date().toISOString().split('T')[0];
  document.getElementById('aTime').value = '09:00';
  openModal('addModal');
}

async function bookAppointment() {
  hideAlert('addAlert');
  const payload = {
    clientName:    document.getElementById('aClientName').value.trim(),
    clientEmail:   document.getElementById('aClientEmail').value.trim(),
    clientPhone:   document.getElementById('aClientPhone').value.trim(),
    serviceId:     parseInt(document.getElementById('aService').value),
    preferredDate: document.getElementById('aDate').value,
    preferredTime: document.getElementById('aTime').value,
    note:          document.getElementById('aNote').value.trim(),
  };
  if (!payload.clientName)    { showAlert('addAlert', 'Client name is required.');   return; }
  if (!payload.serviceId)     { showAlert('addAlert', 'Please select a service.');   return; }
  if (!payload.preferredDate) { showAlert('addAlert', 'Please pick a date.');        return; }

  setLoading('addBtn', 'addSpin', 'addBtnTxt', true, 'Book Now');
  try {
    const res  = await fetch(`${API}/appointments/add`, {
      method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload)
    });
    const data = await res.json();
    if (data.success) { closeModal('addModal'); toast(data.message); loadData(); }
    else showAlert('addAlert', data.message);
  } catch (_) { showAlert('addAlert', 'Network error.'); }
  finally { setLoading('addBtn', 'addSpin', 'addBtnTxt', false, 'Book Now'); }
}

async function updateStatus(id, status) {
  if (!confirm(`Set appointment #${id} to "${status}"?`)) return;
  try {
    const res  = await fetch(`${API}/appointments/update-status`, {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ id, status })
    });
    const data = await res.json();
    toast(data.message, !data.success);
    if (data.success) loadData();
  } catch (_) { toast('Network error.', true); }
}
