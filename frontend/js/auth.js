// =========================================
// auth.js — Login Page Logic (localStorage)
// =========================================

const API = '/api';

// Redirect to dashboard if already logged in
if (localStorage.getItem('thbms_user')) {
  window.location.href = 'dashboard.html';
}

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
  document.getElementById('btnText').textContent = on ? 'Logging in...' : '-> Login';
  document.getElementById('loginBtn').disabled = on;
}

async function doLogin() {
  hideAlert();
  const email    = document.getElementById('email').value.trim();
  const password = document.getElementById('password').value;

  if (!email || !password) { showAlert('Please fill in both fields.'); return; }

  setLoading(true);
  try {
    const res  = await fetch(API + '/login', {
      method:  'POST',
      headers: { 'Content-Type': 'application/json' },
      body:    JSON.stringify({ email, password }),
    });
    const data = await res.json();

    if (data.success) {
      localStorage.setItem('thbms_user', JSON.stringify(data.data.user));
      window.location.href = 'dashboard.html';
    } else {
      showAlert(data.message || 'Login failed.');
    }
  } catch (err) {
    showAlert('Network error. Is the C# server running on port 5000?');
  } finally {
    setLoading(false);
  }
}

document.addEventListener('keydown', function(e) { if (e.key === 'Enter') doLogin(); });
