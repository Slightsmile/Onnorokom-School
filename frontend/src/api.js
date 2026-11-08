const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:8080';

async function request(path, { method = 'GET', studentId, body } = {}) {
  const headers = { 'Content-Type': 'application/json' };
  if (studentId) headers['X-Student-Id'] = studentId;

  const res = await fetch(`${API_URL}${path}`, {
    method,
    headers,
    body: body ? JSON.stringify(body) : undefined,
  });

  if (res.status === 204) return null;

  let data = null;
  try {
    data = await res.json();
  } catch {
    // no body
  }

  if (!res.ok) {
    const message = data?.message || `Request failed with status ${res.status}`;
    const err = new Error(message);
    err.status = res.status;
    err.data = data;
    throw err;
  }

  return data;
}

export const api = {
  createClass: (name, totalSeats) => request('/classes', { method: 'POST', body: { name, totalSeats } }),
  listClasses: () => request('/classes'),
  getClass: (classId) => request(`/classes/${classId}`),
  createHold: (classId, studentId) => request(`/classes/${classId}/holds`, { method: 'POST', studentId }),
  getHold: (holdId) => request(`/holds/${holdId}`),
  confirmHold: (holdId, studentId) => request(`/holds/${holdId}/confirm`, { method: 'POST', studentId }),
  cancelHold: (holdId, studentId) => request(`/holds/${holdId}`, { method: 'DELETE', studentId }),
  listMyHolds: (studentId) => request('/me/holds', { studentId }),
};
