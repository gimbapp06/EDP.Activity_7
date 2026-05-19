// assignments.js
let therapistList = [];

async function onPageLoad() {
  await loadTherapists();
  loadData();
}

async function loadTherapists() {
  try {
    const res  = await fetch(`${API}/assignments/therapists`);
    const data = await res.json();
    if (!data.success) return;
    therapistList = data.data.therapists;
    ['aTherapist', 'rTherapist'].forEach(selId => {
      const sel = document.getElementById(selId);
      if (!sel) return;
      sel.innerHTML = '<option value="">— Select Therapist —</option>';
      therapistList.forEach(t => {
        sel.innerHTML += `<option value="${t.id}">${esc(t.full_name)} — ${esc(t.specialty)}</option>`;
      });
    });
  } catch (_) {}
}

async function loadData() {
  const tbody = document.getElementById('dataTable');
  tbody.innerHTML = '<tr><td colspan="9" class="no-data">Loading…</td></tr>';
  try {
    const res  = await fetch(`${API}/assignments/list`);
    const data = await res.json();
    if (!data.success || !data.data.assignments.length) {
      tbody.innerHTML = '<tr><td colspan="9" class="no-data">No assignments found.</td></tr>'; return;
    }
    tbody.innerHTML = data.data.assignments.map((a, i) => `
      <tr>
        <td style="color:var(--gray-mid);font-size:.8rem">${i + 1}</td>
        <td><strong>${esc(a.client_name)}</strong></td>
        <td>${esc(a.service_name)}</td>
        <td>${a.preferred_date}</td>
        <td>${a.preferred_time}</td>
        <td><strong>${esc(a.therapist_name)}</strong></td>
        <td><span class="badge badge-staff">${esc(a.specialty)}</span></td>
        <td>${esc(a.assigned_by_name)}</td>
        <td>
          <div class="actions-cell">
            <button class="btn btn-outline btn-sm" onclick="openReassign(${a.id})">Reassign</button>
          </div>
        </td>
      </tr>`).join('');
  } catch (_) {
    tbody.innerHTML = '<tr><td colspan="9" class="no-data">Error loading data.</td></tr>';
  }
}

async function openAssignModal() {
  hideAlert('assignAlert');
  document.getElementById('aNotes').value = '';
  document.getElementById('aTherapist').value = '';

  // Load unassigned appointments
  try {
    const res  = await fetch(`${API}/assignments/unassigned`);
    const data = await res.json();
    const sel  = document.getElementById('aAppt');
    sel.innerHTML = '<option value="">— Select Appointment —</option>';
    if (data.success && data.data.appointments.length) {
      data.data.appointments.forEach(a => {
        sel.innerHTML += `<option value="${a.id}">[APT-${a.id}] ${esc(a.client_name)} — ${esc(a.service_name)} (${a.preferred_date})</option>`;
      });
    } else {
      sel.innerHTML = '<option value="">No unassigned appointments</option>';
    }
  } catch (_) {}
  openModal('assignModal');
}

async function doAssign() {
  hideAlert('assignAlert');
  const payload = {
    appointmentId: parseInt(document.getElementById('aAppt').value),
    therapistId:   parseInt(document.getElementById('aTherapist').value),
    notes:         document.getElementById('aNotes').value.trim(),
  };
  if (!payload.appointmentId) { showAlert('assignAlert', 'Please select an appointment.'); return; }
  if (!payload.therapistId)   { showAlert('assignAlert', 'Please select a therapist.');   return; }

  setLoading('assignBtn', 'assignSpin', 'assignBtnTxt', true, 'Assign');
  try {
    const res  = await fetch(`${API}/assignments/assign`, {
      method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload)
    });
    const data = await res.json();
    if (data.success) { closeModal('assignModal'); toast(data.message); loadData(); }
    else showAlert('assignAlert', data.message);
  } catch (_) { showAlert('assignAlert', 'Network error.'); }
  finally { setLoading('assignBtn', 'assignSpin', 'assignBtnTxt', false, 'Assign'); }
}

function openReassign(assignId) {
  hideAlert('reassignAlert');
  document.getElementById('rAssignId').value = assignId;
  document.getElementById('rTherapist').value = '';
  document.getElementById('rNotes').value = '';
  openModal('reassignModal');
}

async function doReassign() {
  hideAlert('reassignAlert');
  const payload = {
    id:          parseInt(document.getElementById('rAssignId').value),
    therapistId: parseInt(document.getElementById('rTherapist').value),
    notes:       document.getElementById('rNotes').value.trim(),
  };
  if (!payload.therapistId) { showAlert('reassignAlert', 'Please select a therapist.'); return; }
  try {
    const res  = await fetch(`${API}/assignments/reassign`, {
      method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload)
    });
    const data = await res.json();
    if (data.success) { closeModal('reassignModal'); toast(data.message); loadData(); }
    else showAlert('reassignAlert', data.message);
  } catch (_) { showAlert('reassignAlert', 'Network error.'); }
}
