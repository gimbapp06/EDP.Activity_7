// =========================================
// recover.js — Password Recovery (C# API)
// =========================================

const API = '/api';

function showMsg(elId, msg, isError = true) {
  const el = document.getElementById(elId);
  el.textContent = msg;
  el.className = 'alert show ' + (isError ? 'alert-error' : 'alert-success');
}
function hideMsg(elId) { document.getElementById(elId).classList.remove('show'); }
function setLoading(btnId, spinId, textId, on, label = '') {
  document.getElementById(btnId).disabled = on;
  document.getElementById(spinId).classList.toggle('show', on);
  document.getElementById(textId).textContent = on ? 'Please wait…' : label;
}

async function requestToken() {
  hideMsg('s1Error'); hideMsg('s1Success');
  const email = document.getElementById('recoverEmail').value.trim();
  if (!email) { showMsg('s1Error', 'Please enter your email address.'); return; }

  setLoading('requestBtn', 's1Spin', 's1BtnText', true, 'Send Recovery Token');
  try {
    const res  = await fetch(`${API}/recover/request`, {
      method:      'POST',
      headers:     { 'Content-Type': 'application/json' },
      credentials: 'include',
      body:        JSON.stringify({ email }),
    });
    const data = await res.json();
    if (data.success) {
      document.getElementById('step1').classList.remove('active');
      document.getElementById('step2').classList.add('active');
      document.getElementById('tokenDisplay').textContent = data.data.token;
      document.getElementById('tokenInput').value         = data.data.token;
    } else {
      showMsg('s1Error', data.message || 'Request failed.');
    }
  } catch (err) {
    showMsg('s1Error', 'Network error. Is the C# server running on port 5000?');
  } finally {
    setLoading('requestBtn', 's1Spin', 's1BtnText', false, 'Send Recovery Token');
  }
}

async function resetPassword() {
  hideMsg('s2Error'); hideMsg('s2Success');
  const token   = document.getElementById('tokenInput').value.trim();
  const pass    = document.getElementById('newPass').value;
  const confirm = document.getElementById('confirmPass').value;

  if (!token)           { showMsg('s2Error', 'Recovery token is required.');      return; }
  if (!pass)            { showMsg('s2Error', 'Please enter a new password.');     return; }
  if (pass !== confirm) { showMsg('s2Error', 'Passwords do not match.');          return; }
  if (pass.length < 6)  { showMsg('s2Error', 'Password must be 6+ characters.'); return; }

  setLoading('resetBtn', 's2Spin', 's2BtnText', true, 'Reset Password');
  try {
    const res  = await fetch(`${API}/recover/reset`, {
      method:      'POST',
      headers:     { 'Content-Type': 'application/json' },
      credentials: 'include',
      body:        JSON.stringify({ token, password: pass, confirm }),
    });
    const data = await res.json();
    if (data.success) {
      showMsg('s2Success', data.message + ' Redirecting to login…', false);
      setTimeout(() => window.location.href = 'index.html', 2500);
    } else {
      showMsg('s2Error', data.message || 'Reset failed.');
    }
  } catch (err) {
    showMsg('s2Error', 'Network error. Is the C# server running on port 5000?');
  } finally {
    setLoading('resetBtn', 's2Spin', 's2BtnText', false, 'Reset Password');
  }
}
