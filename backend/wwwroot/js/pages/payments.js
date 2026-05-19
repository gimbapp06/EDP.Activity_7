// payments.js
let unpaidList = [];

async function onPageLoad() {
  loadData();
}

async function loadData() {
  const search = document.getElementById('searchInput')?.value.trim() ?? '';
  const tbody  = document.getElementById('dataTable');
  tbody.innerHTML = '<tr><td colspan="9" class="no-data">Loading…</td></tr>';
  try {
    const res  = await fetch(`${API}/payments/list?search=${encodeURIComponent(search)}`);
    const data = await res.json();
    if (!data.success || !data.data.payments.length) {
      tbody.innerHTML = '<tr><td colspan="9" class="no-data">No payment records found.</td></tr>'; return;
    }
    tbody.innerHTML = data.data.payments.map((p, i) => `
      <tr>
        <td style="color:var(--gray-mid);font-size:.8rem">${i + 1}</td>
        <td><strong>${esc(p.client_name)}</strong></td>
        <td>${esc(p.service_name)}</td>
        <td><strong>${peso(p.amount)}</strong></td>
        <td><span class="badge badge-${p.method}">${p.method.replace('_',' ')}</span></td>
        <td><span class="badge badge-${p.status}">${p.status}</span></td>
        <td style="font-size:.82rem">${p.processed_at}</td>
        <td>${esc(p.processed_by_name)}</td>
        <td>
          <div class="actions-cell">
            ${p.status === 'completed'
              ? `<button class="btn btn-danger btn-sm" onclick="doRefund(${p.id})">Refund</button>`
              : `<span style="color:var(--gray-mid);font-size:.8rem">${p.status}</span>`}
          </div>
        </td>
      </tr>`).join('');
  } catch (_) {
    tbody.innerHTML = '<tr><td colspan="9" class="no-data">Error loading data.</td></tr>';
  }
}

async function openPayModal() {
  hideAlert('payAlert');
  document.getElementById('pAmount').value  = '';
  document.getElementById('pRemarks').value = '';
  document.getElementById('pMethod').value  = 'cash';

  // Load unpaid appointments
  try {
    const res  = await fetch(`${API}/payments/unpaid`);
    const data = await res.json();
    const sel  = document.getElementById('pAppt');
    unpaidList = data.success ? data.data.appointments : [];
    sel.innerHTML = '<option value="">— Select Appointment —</option>';
    if (unpaidList.length) {
      unpaidList.forEach(a => {
        sel.innerHTML += `<option value="${a.id}" data-price="${a.price}">[APT-${a.id}] ${esc(a.client_name)} — ${esc(a.service_name)} (${peso(a.price)})</option>`;
      });
    } else {
      sel.innerHTML = '<option value="">No unpaid appointments</option>';
    }
  } catch (_) {}
  openModal('payModal');
}

function fillAmount() {
  const sel     = document.getElementById('pAppt');
  const opt     = sel.options[sel.selectedIndex];
  const price   = opt?.dataset?.price;
  if (price) document.getElementById('pAmount').value = price;
}

async function doPayment() {
  hideAlert('payAlert');
  const payload = {
    appointmentId: parseInt(document.getElementById('pAppt').value),
    amount:        parseFloat(document.getElementById('pAmount').value),
    method:        document.getElementById('pMethod').value,
    remarks:       document.getElementById('pRemarks').value.trim(),
  };
  if (!payload.appointmentId)    { showAlert('payAlert', 'Please select an appointment.'); return; }
  if (!payload.amount || payload.amount <= 0) { showAlert('payAlert', 'Enter a valid amount.'); return; }

  setLoading('payBtn', 'paySpin', 'payBtnTxt', true, 'Process Payment');
  try {
    const res  = await fetch(`${API}/payments/process`, {
      method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload)
    });
    const data = await res.json();
    if (data.success) { closeModal('payModal'); toast(data.message); loadData(); }
    else showAlert('payAlert', data.message);
  } catch (_) { showAlert('payAlert', 'Network error.'); }
  finally { setLoading('payBtn', 'paySpin', 'payBtnTxt', false, 'Process Payment'); }
}

async function doRefund(id) {
  if (!confirm('Refund this payment? This cannot be undone.')) return;
  try {
    const res  = await fetch(`${API}/payments/refund`, {
      method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ id })
    });
    const data = await res.json();
    toast(data.message, !data.success);
    if (data.success) loadData();
  } catch (_) { toast('Network error.', true); }
}
