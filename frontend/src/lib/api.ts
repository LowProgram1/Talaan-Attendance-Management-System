export const API = process.env.NEXT_PUBLIC_API_URL || '/api';
export type Session = { id: string; fullName: string; email: string; roles: string[] };
export async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const response = await fetch(`${API}${path}`, { ...options, credentials: 'include', headers: { ...(options.body ? { 'Content-Type': 'application/json' } : {}), ...options.headers }, cache: 'no-store' });
  if (!response.ok) { const error = await response.json().catch(() => ({})); throw new Error(error.message || error.detail || `Request failed (${response.status})`); }
  return response.status === 204 ? undefined as T : response.json();
}
export const post = <T>(path: string, body: unknown) => request<T>(path, { method: 'POST', body: JSON.stringify(body) });
export const put = <T>(path: string, body: unknown) => request<T>(path, { method: 'PUT', body: JSON.stringify(body) });
