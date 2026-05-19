// =========================================
// auth.js — Login Page (session-based)
// =========================================
const API = '/api';

// If already logged in via server session, go straight to dashboard
(async () => {
  try {
    const res  = await fetch(`${API}/login/check`);
    const data = await res.json();
    if (data.success) window.location.href = '/dashboard.html';
  } catch (_) {}
})();

function showAlert(msg) {
  const el = document.getElementById('alert');
  el.textContent = msg;
  el.classList.add('show');
}
function hideAlert() {
  document.getElementById('alert').classList.remove('show');
}
function setLoading(on) {
  document.getElementById('spinner').classList.toggle('show', on);
  document.getElementById('btnText').textContent = on ? 'Logging in…' : '→ Login';
  document.getElementById('loginBtn').disabled = on;
}

async function doLogin() {
  hideAlert();
  const email    = document.getElementById('email').value.trim();
  const password = document.getElementById('password').value;

  if (!email || !password) { showAlert('Please fill in both fields.'); return; }

  setLoading(true);
  try {
    const res  = await fetch(`${API}/login`, {
      method:  'POST',
      headers: { 'Content-Type': 'application/json' },
      body:    JSON.stringify({ email, password }),
    });
    const data = await res.json();
    if (data.success) {
      window.location.href = '/dashboard.html';
    } else {
      showAlert(data.message || 'Login failed.');
    }
  } catch (err) {
    showAlert('Network error. Is the C# server running on port 5000?');
  } finally {
    setLoading(false);
  }
}

document.addEventListener('keydown', (e) => { if (e.key === 'Enter') doLogin(); });
