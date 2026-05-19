// =========================================
// users.js — User Management (C# API)
// =========================================

const API = '/api';
let currentUser = null;
let editMode    = false;

(async () => {
  try {
    const res  = await fetch(`${API}/login/check`, { credentials: 'include' });
    const data = await res.json();
    if (!data.success) { window.location.href = 'index.html'; return; }
    currentUser = data.data.user;
    document.getElementById('topbarName').textContent = data.data.user.full_name;
    loadUsers();
  } catch (_) { window.location.href = 'index.html'; }
})();

async function doLogout() {
  await fetch(`${API}/login/logout`, { credentials: 'include' });
  window.location.href = 'index.html';
}

function toast(msg, isError = false) {
  const el = document.getElementById('toast');
  el.textContent = msg;
  el.className = 'show' + (isError ? ' error' : '');
  setTimeout(() => el.className = '', 3000);
}

async function loadUsers() {
  const search = document.getElementById('searchInput').value.trim();
  const tbody  = document.getElementById('userTable');
  tbody.innerHTML = '<tr><td colspan="7" class="no-data">Loading…</td></tr>';

  try {
    const res  = await fetch(`${API}/users/list?search=${encodeURIComponent(search)}`,
      { credentials: 'include' });
    const data = await res.json();

    if (!data.success) {
      tbody.innerHTML = `<tr><td colspan="7" class="no-data">${data.message}</td></tr>`;
      return;
    }
    if (!data.data.users.length) {
      tbody.innerHTML = '<tr><td colspan="7" class="no-data">No accounts found.</td></tr>';
      return;
    }
    tbody.innerHTML = data.data.users.map((u, i) => `
      <tr>
        <td style="color:var(--gray-mid);font-size:.82rem">${i + 1}</td>
        <td><strong>${esc(u.full_name)}</strong></td>
        <td style="color:var(--gray-mid)">${esc(u.username)}</td>
        <td>${esc(u.email)}</td>
        <td><span class="badge badge-${u.role}">${u.role}</span></td>
        <td><span class="badge badge-${u.status}">${u.status}</span></td>
        <td>
          <div class="actions-cell">
            <button class="btn btn-outline btn-sm" onclick="openEditModal(${u.id})">Edit</button>
            ${u.status === 'active'
              ? `<button class="btn btn-danger btn-sm" onclick="toggleStatus(${u.id},'inactive')">Deactivate</button>`
              : `<button class="btn btn-primary btn-sm" onclick="toggleStatus(${u.id},'active')">Activate</button>`
            }
          </div>
        </td>
      </tr>`).join('');
  } catch (_) {
    tbody.innerHTML = '<tr><td colspan="7" class="no-data">Error loading. Is the C# server running?</td></tr>';
  }
}

function esc(str) {
  const d = document.createElement('div');
  d.textContent = str; return d.innerHTML;
}

function openModal()  { document.getElementById('userModal').classList.add('open'); document.getElementById('modalAlert').classList.remove('show'); }
function closeModal() { document.getElementById('userModal').classList.remove('open'); clearForm(); }
function clearForm()  {
  ['mFullName','mUsername','mEmail','mPassword','editId'].forEach(id => document.getElementById(id).value = '');
  document.getElementById('mRole').value = 'staff';
}
function setModalLoading(on) {
  document.getElementById('saveBtn').disabled = on;
  document.getElementById('saveSpin').classList.toggle('show', on);
  document.getElementById('saveBtnText').textContent = on ? 'Saving…' : 'Save Account';
}
function modalAlert(msg) {
  const el = document.getElementById('modalAlert');
  el.textContent = msg; el.classList.add('show');
}

function openAddModal() {
  editMode = false; clearForm();
  document.getElementById('modalTitle').textContent = 'Add Account';
  document.getElementById('mUsername').disabled     = false;
  document.getElementById('passLabel').textContent  = 'Password';
  document.getElementById('passHint').style.display = 'none';
  document.getElementById('mPassword').placeholder  = 'Min. 6 characters';
  openModal();
}

async function openEditModal(id) {
  editMode = true; clearForm();
  document.getElementById('modalTitle').textContent = 'Edit Account';
  document.getElementById('mUsername').disabled     = true;
  document.getElementById('passLabel').textContent  = 'New Password (optional)';
  document.getElementById('passHint').style.display = 'block';
  document.getElementById('mPassword').placeholder  = 'Leave blank to keep current';
  openModal();

  try {
    const res  = await fetch(`${API}/users/get?id=${id}`, { credentials: 'include' });
    const data = await res.json();
    if (!data.success) { modalAlert(data.message); return; }
    const u = data.data.user;
    document.getElementById('editId').value    = u.id;
    document.getElementById('mFullName').value = u.full_name;
    document.getElementById('mUsername').value = u.username;
    document.getElementById('mEmail').value    = u.email;
    document.getElementById('mRole').value     = u.role;
  } catch (_) { modalAlert('Failed to load user data.'); }
}

async function saveUser() {
  document.getElementById('modalAlert').classList.remove('show');
  const id        = document.getElementById('editId').value;
  const full_name = document.getElementById('mFullName').value.trim();
  const username  = document.getElementById('mUsername').value.trim();
  const email     = document.getElementById('mEmail').value.trim();
  const role      = document.getElementById('mRole').value;
  const password  = document.getElementById('mPassword').value;

  if (!full_name || !email || (!editMode && !username) || (!editMode && !password)) {
    modalAlert('Please fill in all required fields.'); return;
  }

  setModalLoading(true);
  try {
    const action  = editMode ? 'update' : 'add';
    const payload = editMode
      ? { id: parseInt(id), fullName: full_name, email, role, password }
      : { username, fullName: full_name, email, role, password };

    const res  = await fetch(`${API}/users/${action}`, {
      method:      'POST',
      headers:     { 'Content-Type': 'application/json' },
      credentials: 'include',
      body:        JSON.stringify(payload),
    });
    const data = await res.json();
    if (data.success) { closeModal(); toast(data.message); loadUsers(); }
    else modalAlert(data.message || 'Save failed.');
  } catch (_) { modalAlert('Network error.'); }
  finally { setModalLoading(false); }
}

async function toggleStatus(id, status) {
  if (!confirm(`Are you sure you want to ${status === 'active' ? 'activate' : 'deactivate'} this account?`)) return;
  try {
    const res  = await fetch(`${API}/users/toggle`, {
      method:      'POST',
      headers:     { 'Content-Type': 'application/json' },
      credentials: 'include',
      body:        JSON.stringify({ id, status }),
    });
    const data = await res.json();
    toast(data.message, !data.success);
    if (data.success) loadUsers();
  } catch (_) { toast('Network error.', true); }
}

document.getElementById('userModal').addEventListener('click', function(e) {
  if (e.target === this) closeModal();
});
