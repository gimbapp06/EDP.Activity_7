// common.js — Shared helpers for transaction/dashboard pages
// NOTE: Do NOT include this on index.html (login page)
const API = '/api';

// Pages that do NOT need auth guard (login page handles itself)
const PUBLIC_PAGES = ['/index.html', '/'];

(async () => {
  const path = window.location.pathname;

  // Skip auth check if we're on the login page
  if (PUBLIC_PAGES.includes(path)) return;

  try {
    const res  = await fetch(`${API}/login/check`);
    const data = await res.json();

    if (!data.success) {
      // Not logged in — go to login
      window.location.replace('/index.html');
      return;
    }

    // Logged in — set name in topbar and fire page init
    const nameEl = document.getElementById('topbarName');
    if (nameEl) nameEl.textContent = data.data.user.full_name;

    if (typeof onPageLoad === 'function') onPageLoad(data.data.user);

  } catch (_) {
    window.location.replace('/index.html');
  }
})();

// ── Logout ────────────────────────────────────────────────────────────────────
async function doLogout() {
  try { await fetch(`${API}/login/logout`); } catch (_) {}
  window.location.replace('/index.html');
}

// ── Toast notification ────────────────────────────────────────────────────────
function toast(msg, isError = false) {
  const el = document.getElementById('toast');
  if (!el) return;
  el.textContent = msg;
  el.className   = 'show' + (isError ? ' error' : '');
  setTimeout(() => { el.className = ''; }, 3200);
}

// ── Modal open / close ────────────────────────────────────────────────────────
function openModal(id)  { document.getElementById(id)?.classList.add('open');    }
function closeModal(id) { document.getElementById(id)?.classList.remove('open'); }

// ── Alert inside modal ────────────────────────────────────────────────────────
function showAlert(elId, msg) {
  const el = document.getElementById(elId);
  if (!el) return;
  el.textContent = msg;
  el.classList.add('show');
}
function hideAlert(elId) {
  document.getElementById(elId)?.classList.remove('show');
}

// ── Button spinner ────────────────────────────────────────────────────────────
function setLoading(btnId, spinId, txtId, on, label = '') {
  const btn = document.getElementById(btnId);
  if (btn) btn.disabled = on;
  document.getElementById(spinId)?.classList.toggle('show', on);
  const t = document.getElementById(txtId);
  if (t) t.textContent = on ? 'Please wait…' : label;
}

// ── Escape HTML ───────────────────────────────────────────────────────────────
function esc(str) {
  const d = document.createElement('div');
  d.textContent = String(str ?? '');
  return d.innerHTML;
}

// ── Format Philippine Peso ────────────────────────────────────────────────────
function peso(n) {
  return '₱' + parseFloat(n || 0).toLocaleString('en-PH', { minimumFractionDigits: 2 });
}

// ── Close modal when clicking backdrop ───────────────────────────────────────
document.querySelectorAll('.modal-overlay').forEach(el => {
  el.addEventListener('click', function (e) {
    if (e.target === this) this.classList.remove('open');
  });
});
