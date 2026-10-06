'use client';
import { useState } from 'react';
import { CheckCircle2, Eye, EyeOff, ShieldCheck, UserRound } from 'lucide-react';
import { post, put, Session } from '@/lib/api';
import { passwordChecks, passwordStrength, passwordValid } from '@/lib/password';

export default function Settings({ session, onSession }: { session: Session; onSession: (next: Session) => void }) {
  const [tab, setTab] = useState<'profile' | 'password'>('profile');
  const [name, setName] = useState(session.fullName);
  const [newPassword, setNewPassword] = useState('');
  const [otp, setOtp] = useState('');
  const [codeSent, setCodeSent] = useState(false);
  const [showPassword, setShowPassword] = useState(false);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState('');
  const [error, setError] = useState('');

  async function run(work: () => Promise<void>) {
    setBusy(true); setMessage(''); setError('');
    try { await work(); } catch (err) { setError(err instanceof Error ? err.message : 'Unable to save changes.'); }
    finally { setBusy(false); }
  }

  return <div className="settings-layout">
    <div className="tab-row" role="tablist" aria-label="Settings">
      <button className={`tab ${tab === 'profile' ? 'active' : ''}`} role="tab" aria-selected={tab === 'profile'} onClick={() => setTab('profile')}>Profile</button>
      <button className={`tab ${tab === 'password' ? 'active' : ''}`} role="tab" aria-selected={tab === 'password'} onClick={() => setTab('password')}>Password</button>
    </div>
    {tab === 'profile' ? <section className="card settings-card">
      <div className="settings-card-heading"><UserRound size={20}/><div><h2>Profile</h2><p>Keep your display name current.</p></div></div>
      <form onSubmit={event => { event.preventDefault(); run(async () => { const next = await put<Session>('/auth/profile', { fullName: name }); onSession(next); setMessage('Profile saved.'); }); }}>
        <label className="label" htmlFor="profile-name">Full name</label>
        <input id="profile-name" className="field" value={name} onChange={event => setName(event.target.value)} minLength={2} maxLength={100} required/>
        <label className="label settings-field-label" htmlFor="profile-email">Email address</label>
        <input id="profile-email" className="field" type="email" value={session.email} readOnly aria-readonly="true"/>
        <p className="muted settings-hint">Your login email is managed by the school administrator.</p>
        <button className="btn btn-primary" disabled={busy || name.trim() === session.fullName} aria-busy={busy} style={{marginTop:16}}>Save profile</button>
      </form>
    </section> : <section className="card settings-card">
      <div className="settings-card-heading"><ShieldCheck size={20}/><div><h2>Change password</h2><p>We’ll send a verification code to your registered email.</p></div></div>
      <label className="label" htmlFor="password-email">Registered email</label>
      <input id="password-email" className="field" type="email" value={session.email} readOnly aria-readonly="true"/>
      <button type="button" className="btn btn-outline" disabled={busy} aria-busy={busy} style={{marginTop:14}} onClick={() => run(async () => { await post('/auth/change-password/request', {}); setCodeSent(true); setMessage('A six-digit code was sent to your registered email.'); })}>Send email code</button>
      {codeSent && <form className="settings-confirm" onSubmit={event => { event.preventDefault(); run(async () => { await post('/auth/change-password/confirm', { otp, newPassword }); setOtp(''); setNewPassword(''); setCodeSent(false); setMessage('Password updated.'); }); }}>
        <label className="label" htmlFor="password-code">Six-digit email code</label>
        <input id="password-code" className="field" inputMode="numeric" pattern="[0-9]{6}" maxLength={6} value={otp} onChange={event => setOtp(event.target.value.replace(/\D/g, ''))} required/>
        <label className="label settings-field-label" htmlFor="new-password">New password</label>
        <div className="settings-password"><input id="new-password" className="field" type={showPassword ? 'text' : 'password'} autoComplete="new-password" value={newPassword} onChange={event => setNewPassword(event.target.value)} required/><button type="button" aria-label={showPassword ? 'Hide password' : 'Show password'} onClick={() => setShowPassword(!showPassword)}>{showPassword ? <EyeOff size={18}/> : <Eye size={18}/>}</button></div>
        {newPassword && <><p className="settings-strength">Strength: <strong>{passwordStrength(newPassword)}</strong></p><div className="settings-checks">{['At least 12 characters','Uppercase letter','Lowercase letter','Number','Special character','Not a common password'].map((label, index) => <span key={label} className={passwordChecks(newPassword)[index] ? 'met' : ''}><CheckCircle2 size={14}/>{label}</span>)}</div></>}
        <button className="btn btn-primary" disabled={busy || otp.length !== 6 || !passwordValid(newPassword)} aria-busy={busy} style={{marginTop:17}}>Update password</button>
      </form>}
    </section>}
    {busy&&<div className="skeleton-table auth-progress" role="status" aria-label="Processing settings"><div className="skeleton-line"/><div className="skeleton-line"/></div>}
    {(message || error) && <p className={error ? 'error settings-message' : 'success settings-message'} role="status">{error || message}</p>}
  </div>;
}
