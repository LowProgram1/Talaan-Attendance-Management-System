'use client';

import { Suspense, useState } from 'react';
import Image from 'next/image';
import { Eye, EyeOff } from 'lucide-react';
import { useRouter, useSearchParams } from 'next/navigation';
import { post, request, Session } from '@/lib/api';
import { passwordValid } from '@/lib/password';
import { PasswordHint } from '../forgot/page';

function Activation() {
  const router = useRouter();
  const params = useSearchParams();
  const email = params.get('email') || '';
  const token = params.get('token') || '';
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const passwordsMatch = password === confirmPassword;

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    if (!passwordValid(password) || !confirmPassword || !passwordsMatch || busy) return;
    setError('');
    setBusy(true);
    try {
      await post('/auth/activate', { email, token, password });
      await request<Session>('/auth/me');
      router.push('/app/dashboard');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Activation failed.');
    } finally {
      setBusy(false);
    }
  }

  return <main style={{ minHeight: '100vh', display: 'grid', placeItems: 'center', padding: 20 }}>
    <form onSubmit={submit} className="card" style={{ width: 'min(100%,460px)', padding: 32 }}>
      <div className="auth-brand"><Image src="/talaan-mark.svg" alt="Talaan logo" width={38} height={38}/><strong>Talaan</strong></div>
      <h1 className="heading">Activate your account</h1>
      <p className="muted" style={{ margin: '8px 0 26px' }}>Set a strong password to access your school workspace.</p>

      <label className="label" htmlFor="activation-email">Email</label>
      <input id="activation-email" className="field" value={email} readOnly/>

      <label className="label" htmlFor="activation-password" style={{ marginTop: 20 }}>Create password</label>
      <div className="settings-password">
        <input id="activation-password" className="field" type={showPassword ? 'text' : 'password'} autoComplete="new-password" required value={password} onChange={event => setPassword(event.target.value)}/>
        <button type="button" aria-label={showPassword ? 'Hide password' : 'Show password'} aria-controls="activation-password" onClick={() => setShowPassword(!showPassword)}>{showPassword ? <EyeOff size={18}/> : <Eye size={18}/>}</button>
      </div>
      <PasswordHint value={password}/>

      <label className="label" htmlFor="activation-confirm-password" style={{ marginTop: 20 }}>Confirm password</label>
      <div className="settings-password">
        <input id="activation-confirm-password" className="field" type={showConfirmPassword ? 'text' : 'password'} autoComplete="new-password" required value={confirmPassword} aria-invalid={confirmPassword.length > 0 && !passwordsMatch} aria-describedby={confirmPassword.length > 0 ? 'activation-password-match' : undefined} onChange={event => setConfirmPassword(event.target.value)}/>
        <button type="button" aria-label={showConfirmPassword ? 'Hide confirm password' : 'Show confirm password'} aria-controls="activation-confirm-password" onClick={() => setShowConfirmPassword(!showConfirmPassword)}>{showConfirmPassword ? <EyeOff size={18}/> : <Eye size={18}/>}</button>
      </div>
      {confirmPassword && <p id="activation-password-match" className={passwordsMatch ? 'muted' : 'error'} role={passwordsMatch ? 'status' : 'alert'} style={{ marginTop: 8 }}>{passwordsMatch ? 'Passwords match.' : 'Passwords do not match.'}</p>}
      {error && <p className="error" style={{ marginTop: 18 }}>{error}</p>}
      <button className="btn btn-primary" style={{ width: '100%', marginTop: 24 }} disabled={!passwordValid(password) || !confirmPassword || !passwordsMatch || busy}>Activate account</button>
      {busy && <div className="skeleton-table auth-progress" role="status" aria-label="Activating account"><div className="skeleton-line"/><div className="skeleton-line"/></div>}
    </form>
  </main>;
}

export default function Page() {
  return <Suspense><Activation/></Suspense>;
}
