'use client';

import { Check, Circle } from 'lucide-react';
import { passwordChecks, passwordValid } from '@/lib/password';

const requirements=['At least 12 characters','An uppercase letter','A lowercase letter','A number','A special character','No common passwords'];

export function PasswordHint({value}:{value:string}) {
  const checks=passwordChecks(value);
  const passed=value?checks.filter(Boolean).length:0;
  const score=!value?0:!checks[5]?1:passwordValid(value)?4:passed>=5?3:passed>=3?2:1;
  const label=['Not set','Weak','Fair','Good','Strong'][score];
  return <div className={`password-meter strength-${score}`}>
    <div className="password-meter-heading"><span>Password strength</span><strong aria-live="polite">{label}</strong></div>
    <div className="password-meter-bars" role="meter" aria-label="Password strength" aria-valuemin={0} aria-valuemax={4} aria-valuenow={score} aria-valuetext={label}>{[1,2,3,4].map(i=><span key={i} className={i<=score?'filled':''}/>)}</div>
    <ul className="password-meter-rules">{requirements.map((label,i)=><li key={label} className={value&&checks[i]?'met':''}>{value&&checks[i]?<Check size={14}/>:<Circle size={12}/>}<span>{label}</span></li>)}</ul>
  </div>;
}
