'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import Image from 'next/image';
import { Eye, EyeOff } from 'lucide-react';
import { post, Session } from '@/lib/api';

export default function Login() {
  const router = useRouter();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [rememberMe, setRememberMe] = useState(false);
  const [show, setShow] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError('');
    try {
      await post<Session>('/auth/login', { email, password, rememberMe });
      router.push('/app/dashboard');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Unable to sign in.');
    } finally {
      setBusy(false);
    }
  }

  return (
    <main className="login login-simple">
      <section className="login-left">
        <div className="login-form">
          <Link className="login-form-brand" href="/" aria-label="Talaan home">
            <Image src="/talaan-mark.svg" alt="" width={40} height={40} />
            <span>Talaan</span>
          </Link>
          <p className="login-eyebrow">SCHOOL PORTAL</p>
          <h1 className="heading">Sign in to your school</h1>
          <p className="login-intro">Your school day, organized in one place.</p>

          <form onSubmit={submit}>
            <label className="label" htmlFor="email">Email address</label>
            <input className="field" id="email" type="email" autoComplete="username" value={email} onChange={e => setEmail(e.target.value)} required placeholder="you@school.edu" />
            <div className="login-field-gap" />
            <label className="label" htmlFor="password">Password</label>
            <div className="login-password-wrap">
              <input className="field" id="password" type={show ? 'text' : 'password'} autoComplete="current-password" value={password} onChange={e => setPassword(e.target.value)} required placeholder="Enter your password" />
              <button className="icon-button login-password-toggle" type="button" aria-label={show ? 'Hide password' : 'Show password'} onClick={() => setShow(!show)}>{show ? <EyeOff size={19} /> : <Eye size={19} />}</button>
            </div>
            <div className="login-form-options">
              <label className="login-remember"><input type="checkbox" checked={rememberMe} onChange={e => setRememberMe(e.target.checked)} /> Remember me</label>
              <Link href="/forgot">Forgot password?</Link>
            </div>
            {error && <p className="error" role="alert">{error}</p>}
            <button className="btn btn-primary login-submit" disabled={busy}>{busy ? 'Signing in…' : 'Sign in'}</button>
            {busy && <div className="skeleton-table login-progress" role="status" aria-label="Signing in"><div className="skeleton-line" /><div className="skeleton-line" /></div>}
          </form>
          <p className="login-admin-note">Your account is created by your school administrator.</p>
        </div>
      </section>

    </main>
  );
}
