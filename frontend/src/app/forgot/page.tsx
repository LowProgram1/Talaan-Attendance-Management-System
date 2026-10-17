'use client';
import { useState } from 'react';
import Image from 'next/image';
import Link from 'next/link';
import { ArrowLeft, ArrowRight, Mail, MailCheck } from 'lucide-react';
import { post } from '@/lib/api';

export default function Forgot() {
  const [email,setEmail]=useState('');
  const [sent,setSent]=useState(false);
  const [busy,setBusy]=useState(false);
  const [error,setError]=useState('');
  async function submit(event:React.FormEvent){
    event.preventDefault();if(busy)return;
    setBusy(true);setError('');
    try{await post('/auth/forgot',{email:email.trim()});setSent(true);}
    catch(e){setError(e instanceof Error?e.message:'Unable to request a reset link. Try again.');}
    finally{setBusy(false);}
  }
  return <main className="recovery-page"><section className="recovery-card">
    <div className="auth-brand"><Image src="/student-attendance-mark.svg" alt="Student Attendance Management Information System logo" width={36} height={36}/><strong>Student Attendance Management Information System</strong></div>
    <div className="recovery-icon">{sent?<MailCheck size={25}/>:<Mail size={25}/>}</div>
    <h1>{sent?'Check your inbox':'Forgot your password?'}</h1>
    {sent?<div role="status"><p className="recovery-intro">If an eligible account exists for <strong>{email}</strong>, we will send a link to create a new password.</p><div className="recovery-note">The link expires in 30 minutes. Check your spam folder if you do not see the email.</div><button className="btn btn-outline recovery-submit" onClick={()=>{setSent(false);setError('')}}>Try another email or request again</button></div>:<>
      <p className="recovery-intro">Enter your registered email and we will send you a password reset link.</p>
      <form onSubmit={submit}><label htmlFor="recovery-email" className="label">Email address</label><input id="recovery-email" className="field" type="email" autoComplete="email" placeholder="you@example.com" required maxLength={254} disabled={busy} value={email} onChange={e=>setEmail(e.target.value)}/>
        {error&&<p className="error" role="alert">{error}</p>}
        <button className="btn btn-primary recovery-submit" disabled={busy} aria-busy={busy}>{busy?'Sending link...':'Send reset link'}{!busy&&<ArrowRight size={17}/>}</button>
      </form>
    </>}
    <Link className="recovery-back" href="/"><ArrowLeft size={15}/>Back to sign in</Link>
  </section></main>;
}
