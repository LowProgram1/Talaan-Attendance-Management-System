'use client';

import { useState } from 'react';
import Image from 'next/image';
import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import { ArrowLeft, CheckCircle2, Eye, EyeOff, LockKeyhole } from 'lucide-react';
import { post } from '@/lib/api';
import { passwordValid } from '@/lib/password';
import { PasswordHint } from '@/components/password-hint';

export default function ResetForm(){
  const params=useSearchParams();
  const email=params.get('email')||'';
  const token=params.get('token')||'';
  const linkPresent=!!email&&/^[a-f0-9]{64}$/i.test(token);
  const [password,setPassword]=useState('');
  const [confirmPassword,setConfirmPassword]=useState('');
  const [showPassword,setShowPassword]=useState(false);
  const [showConfirm,setShowConfirm]=useState(false);
  const [busy,setBusy]=useState(false);
  const [done,setDone]=useState(false);
  const [error,setError]=useState('');
  const matches=!!confirmPassword&&password===confirmPassword;
  async function submit(event:React.FormEvent){
    event.preventDefault();
    if(busy||!linkPresent||!passwordValid(password)||!matches)return;
    setBusy(true);setError('');
    try{
      await post('/auth/reset',{email,token,password,confirmPassword});
      setPassword('');setConfirmPassword('');setDone(true);
      window.history.replaceState(null,'','/reset');
    }catch(e){setError(e instanceof Error?e.message:'Unable to reset your password. Request a new link and try again.');}
    finally{setBusy(false);}
  }
  return <main className="recovery-page"><section className="recovery-card">
    <div className="auth-brand"><Image src="/student-attendance-mark.svg" alt="Student Attendance Management Information System logo" width={36} height={36}/><strong>Student Attendance Management Information System</strong></div>
    <div className="recovery-icon">{done?<CheckCircle2 size={25}/>:<LockKeyhole size={25}/>}</div>
    <h1>{done?'Password updated':!linkPresent?'Request a new link':'Create a new password'}</h1>
    {done?<div role="status"><p className="recovery-intro">Your new password is ready. Sign in to continue to your school workspace.</p><Link className="btn btn-primary recovery-submit" href="/">Continue to sign in</Link></div>:!linkPresent?<><p className="recovery-intro">This password reset link is incomplete or invalid. Request a new link to continue.</p><Link className="btn btn-primary recovery-submit" href="/forgot">Request reset link</Link></>:<>
      <p className="recovery-intro">Choose a strong password you haven’t used elsewhere.</p>
      <form onSubmit={submit}>
        <label htmlFor="reset-password" className="label">New password</label>
        <div className="recovery-password"><input id="reset-password" className="field" type={showPassword?'text':'password'} autoComplete="new-password" required minLength={12} disabled={busy} value={password} onChange={e=>setPassword(e.target.value)}/><button type="button" aria-label={showPassword?'Hide new password':'Show new password'} aria-pressed={showPassword} aria-controls="reset-password" disabled={busy} onClick={()=>setShowPassword(v=>!v)}>{showPassword?<EyeOff size={18}/>:<Eye size={18}/>}</button></div>
        <PasswordHint value={password}/>
        <label htmlFor="reset-confirm-password" className="label recovery-confirm-label">Confirm password</label>
        <div className="recovery-password"><input id="reset-confirm-password" className="field" type={showConfirm?'text':'password'} autoComplete="new-password" required disabled={busy} value={confirmPassword} aria-invalid={!!confirmPassword&&!matches} aria-describedby={confirmPassword?'reset-match':undefined} onChange={e=>setConfirmPassword(e.target.value)}/><button type="button" aria-label={showConfirm?'Hide confirm password':'Show confirm password'} aria-pressed={showConfirm} aria-controls="reset-confirm-password" disabled={busy} onClick={()=>setShowConfirm(v=>!v)}>{showConfirm?<EyeOff size={18}/>:<Eye size={18}/>}</button></div>
        {!!confirmPassword&&<p id="reset-match" className={`recovery-match ${matches?'success':'error'}`} role="status">{matches?'Passwords match.':'Passwords do not match.'}</p>}
        {error&&<div className="recovery-error" role="alert"><p>{error}</p><Link href="/forgot">Request a new reset link</Link></div>}
        <button className="btn btn-primary recovery-submit" disabled={busy||!passwordValid(password)||!matches} aria-busy={busy}>{busy?'Updating password…':'Reset password'}</button>
      </form>
    </>}
    {!done&&<Link className="recovery-back" href="/"><ArrowLeft size={15}/>Back to sign in</Link>}
  </section></main>;
}
