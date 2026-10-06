'use client';

import { useEffect, useState } from 'react';
import { Pencil, Plus, Trash2 } from 'lucide-react';
import { post, put, request } from '@/lib/api';
import Pagination, { PAGE_SIZE } from './pagination';

type UserRow = {
  id: string;
  fullName: string;
  email: string;
  role: string;
  emailConfirmed: boolean;
  children: number;
};

type FormState = { fullName: string; email: string; role: string };
const emptyForm: FormState = { fullName: '', email: '', role: 'Teacher' };

export default function UsersView({ currentUserId }: { currentUserId: string }) {
  const [users, setUsers] = useState<UserRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [page, setPage] = useState(1);
  const [editing, setEditing] = useState<UserRow | 'new' | null>(null);
  const [form, setForm] = useState<FormState>(emptyForm);
  const [busy, setBusy] = useState(false);

  async function reload() {
    setLoading(true);
    try {
      setUsers(await request<UserRow[]>('/users'));
      setError('');
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'Unable to load users.');
    } finally {
      setLoading(false);
    }
  }
  useEffect(() => {
    let active = true;
    request<UserRow[]>('/users')
      .then(rows => { if (active) { setUsers(rows); setError(''); } })
      .catch(cause => { if (active) setError(cause instanceof Error ? cause.message : 'Unable to load users.'); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, []);

  function openNew() {
    setForm(emptyForm);
    setError('');
    setEditing('new');
  }
  function openEdit(user: UserRow) {
    setForm({ fullName: user.fullName, email: user.email, role: user.role });
    setError('');
    setEditing(user);
  }
  async function save(event: React.FormEvent) {
    event.preventDefault();
    if (!editing) return;
    setBusy(true);
    setError('');
    try {
      if (editing === 'new') await post('/auth/users', form);
      else await put(`/auth/users/${editing.id}`, {
        fullName: form.fullName,
        email: editing.id === currentUserId ? editing.email : form.email,
        role: editing.id === currentUserId ? editing.role : form.role
      });
      setEditing(null);
      await reload();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'Unable to save user.');
    } finally {
      setBusy(false);
    }
  }
  async function remove(user: UserRow) {
    if (!window.confirm(`Delete ${user.fullName} (${user.email})? This cannot be undone.`)) return;
    setBusy(true);
    setError('');
    try {
      await request<void>(`/auth/users/${user.id}`, { method: 'DELETE' });
      await reload();
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'Unable to delete user.');
    } finally {
      setBusy(false);
    }
  }

  const currentPage = Math.min(page, Math.max(1, Math.ceil(users.length / PAGE_SIZE)));
  const visible = users.slice((currentPage - 1) * PAGE_SIZE, currentPage * PAGE_SIZE);
  const self = editing !== null && editing !== 'new' && editing.id === currentUserId;
  const guardian = editing !== null && editing !== 'new' && editing.role === 'Guardian';

  return <>
    <div className="section-toolbar">
      <p className="muted">Guardian email contacts appear automatically when students are enrolled. They do not sign in.</p>
      <button className="btn btn-primary" onClick={openNew}><Plus size={18}/> Add staff</button>
    </div>
    {error && !editing && <p className="error" role="alert">{error}</p>}
    <div className="card table-wrap">
      {loading ? <div className="skeleton-table" aria-label="Loading users"><div className="skeleton-line"/><div className="skeleton-line"/><div className="skeleton-line"/></div> :
        users.length ? <table className="table"><thead><tr>{['Name', 'Email', 'Role', 'Children', 'Status', 'Actions'].map(label => <th key={label}>{label}</th>)}</tr></thead><tbody>
          {visible.map(user => <tr key={user.id}>
            <td><strong>{user.fullName}</strong></td><td>{user.email}</td><td>{user.role}</td>
            <td>{user.role === 'Guardian' ? `${user.children} ${user.children === 1 ? 'child' : 'children'}` : '—'}</td>
            <td style={{color:user.role === 'Guardian' ? 'var(--muted)' : user.emailConfirmed ? '#168456' : 'var(--orange)'}}>{user.role === 'Guardian' ? 'Email contact' : user.emailConfirmed ? 'Active' : 'Pending activation'}</td>
            <td><div className="row-actions">
              <button className="icon-button" title="Edit user" aria-label={`Edit ${user.fullName}`} onClick={() => openEdit(user)}><Pencil size={16}/></button>
              <button className="icon-button" title={user.id === currentUserId ? 'Cannot delete your own account' : 'Delete user'} aria-label={`Delete ${user.fullName}`} disabled={busy || user.id === currentUserId} onClick={() => void remove(user)}><Trash2 size={16}/></button>
            </div></td>
          </tr>)}
        </tbody></table> : <div className="empty">No users found.</div>}
    </div>
    <Pagination page={currentPage} total={users.length} onPageChange={setPage}/>
    {editing && <div className="modal-backdrop" onMouseDown={() => !busy && setEditing(null)}><div className="modal" role="dialog" aria-modal="true" aria-label={editing === 'new' ? 'Add staff' : 'Edit user'} onMouseDown={event => event.stopPropagation()}>
      <div className="modal-head"><h2 className="section-title">{editing === 'new' ? 'Add staff' : 'Edit user'}</h2><button className="icon-button" aria-label="Close" onClick={() => setEditing(null)}>×</button></div>
      <form onSubmit={save}><div className="form-grid">
        <div><label className="label" htmlFor="user-full-name">Full name</label><input id="user-full-name" className="field" required minLength={2} maxLength={100} value={form.fullName} onChange={event => setForm({...form, fullName:event.target.value})}/></div>
        {!self && <div><label className="label" htmlFor="user-email">Email address</label><input id="user-email" className="field" type="email" required value={form.email} onChange={event => setForm({...form, email:event.target.value})}/></div>}
        {!self && !guardian && <div><label className="label" htmlFor="user-role">Role</label><select id="user-role" className="field" value={form.role} onChange={event => setForm({...form, role:event.target.value})}><option value="Teacher">Teacher</option><option value="Administrator">Administrator</option></select></div>}
      </div>
      {error && <p className="error" role="alert" style={{marginTop:16}}>{error}</p>}
      <div className="form-actions"><button className="btn btn-outline" type="button" disabled={busy} onClick={() => setEditing(null)}>Cancel</button><button className="btn btn-primary" disabled={busy} aria-busy={busy}>Save</button></div>
      </form>
    </div></div>}
  </>;
}
